using System.Security.Claims;
using backend.Controllers;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace backend.Tests.Controllers;

/// <summary>
/// Affectation d'un étudiant à ses trois emplacements de groupe (sous-groupe, LV1, LV2)
/// via l'API, et conséquence sur ses émargements à venir.
/// </summary>
public class UserGroupAssignmentTests
{
    private const int AdminId = 900;
    private const int Alice = 1;

    private const int GroupInfo1A = 10;
    private const int GroupInfo1F = 11;
    private const int GroupAnglaisA = 20;
    private const int GroupEspagnolA = 30;

    private static (UserController Controller, AdminTokenService Tokens) Build(
        backend.Data.ApplicationDbContext db, int currentUserId, bool withAdminToken = true)
    {
        var tokens = new AdminTokenService();
        var services = new ServiceCollection();
        services.AddSingleton(tokens);

        var controller = new UserController(
            db,
            NullLogger<UserController>.Instance,
            Mock.Of<IJwtService>(),
            new PasswordService(NullLogger<PasswordService>.Instance),
            Mock.Of<IRateLimitService>(),
            Mock.Of<ICookieEncryptionService>());

        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()) }, "test"))
        };
        if (withAdminToken)
            http.Request.Headers["Admin-Token"] = tokens.GenerateToken(AdminId);

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, tokens);
    }

    /// <summary>
    /// Alice est en INFO 1-A. Deux TP à venir visent l'un le groupe 1-A, l'autre le 1-F ;
    /// un CM vise la promo entière. Un TP déjà passé sert à vérifier qu'on ne réécrit pas
    /// l'historique.
    /// </summary>
    private static async Task<backend.Data.ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();
        var today = DateTime.Now.Date;

        db.Specializations.AddRange(
            new Specialization { Id = 1, Name = "Informatique", Code = "INFO", IsActive = true },
            new Specialization { Id = 2, Name = "Langues", Code = "LANGUES", IsActive = true });

        db.Groups.AddRange(
            new Group { Id = GroupInfo1A, Label = "INFO 1-A", DisplayName = "INFO 1-A", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = GroupInfo1F, Label = "INFO 1-F", DisplayName = "INFO 1-F", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = GroupAnglaisA, Label = "A-PL9003TR-BE91", DisplayName = "Anglais A", Type = GroupType.Lv1, SpecializationId = 2, Year = "3A" },
            new Group { Id = GroupEspagnolA, Label = "A-PL9004TR-BE91", DisplayName = "Espagnol A", Type = GroupType.Lv2, SpecializationId = 2, Year = "3A" });

        db.Users.AddRange(
            new User { Id = AdminId, StudentNumber = "ADM", Name = "Ad", Firstname = "Min", Email = "adm@x.fr", Year = "ADMIN", IsAdmin = true },
            new User
            {
                Id = Alice, StudentNumber = "p1", Name = "Alice", Firstname = "A", Email = "a@x.fr",
                Year = "3A", SpecializationId = 1, SubGroupId = GroupInfo1A
            });

        Session S(int id, string name, int daysFromNow, int spec, GroupType kind, int groupId)
        {
            var day = today.AddDays(daysFromNow);
            return new Session
            {
                Id = id, Date = day, StartTime = day.AddHours(8), EndTime = day.AddHours(10),
                Year = "3A", Name = name, Room = "R" + id, ValidationCode = "1234",
                SpecializationId = spec, IcsKind = kind,
                SessionGroups = new List<SessionGroup> { new() { SessionId = id, GroupId = groupId } }
            };
        }

        db.Sessions.AddRange(
            S(100, "TP groupe A", 3, 1, GroupType.Sub, GroupInfo1A),
            S(101, "TP groupe F", 4, 1, GroupType.Sub, GroupInfo1F),
            S(102, "Anglais", 5, 2, GroupType.Lv1, GroupAnglaisA),
            S(103, "TP groupe A passé", -3, 1, GroupType.Sub, GroupInfo1A));

        await db.SaveChangesAsync();

        // Émargements cohérents avec l'affectation initiale.
        db.Attendances.AddRange(
            new Attendance { SessionId = 100, StudentId = Alice, Status = AttendanceStatus.Absent },
            new Attendance { SessionId = 103, StudentId = Alice, Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();
        return db;
    }

    private static User Payload(backend.Data.ApplicationDbContext db, Action<User> tweak)
    {
        var user = new User
        {
            StudentNumber = "p1", Name = "Alice", Firstname = "A", Email = "a@x.fr",
            Year = "3A", SpecializationId = 1, SubGroupId = GroupInfo1A
        };
        tweak(user);
        return user;
    }

    // ------------------------------------------------------------------ PutUser

    [Fact]
    public async Task PutUser_PersistsTheThreeGroupSlots()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        var result = await controller.PutUser("p1", Payload(db, u =>
        {
            u.SubGroupId = GroupInfo1F;
            u.Lv1GroupId = GroupAnglaisA;
            u.Lv2GroupId = GroupEspagnolA;
        }));

        result.Should().BeOfType<NoContentResult>();

        var alice = await db.Users.SingleAsync(u => u.Id == Alice);
        alice.SubGroupId.Should().Be(GroupInfo1F);
        alice.Lv1GroupId.Should().Be(GroupAnglaisA);
        alice.Lv2GroupId.Should().Be(GroupEspagnolA);
    }

    [Fact]
    public async Task PutUser_WhenSubGroupChanges_FutureAttendanceFollows()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        await controller.PutUser("p1", Payload(db, u => u.SubGroupId = GroupInfo1F));

        // Elle quitte le TP du groupe A et rejoint celui du groupe F.
        (await db.Attendances.AnyAsync(a => a.StudentId == Alice && a.SessionId == 100)).Should().BeFalse();
        (await db.Attendances.AnyAsync(a => a.StudentId == Alice && a.SessionId == 101)).Should().BeTrue();
    }

    [Fact]
    public async Task PutUser_NeverRewritesPastAttendance()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        await controller.PutUser("p1", Payload(db, u => u.SubGroupId = GroupInfo1F));

        // La séance passée où elle était présente reste intacte, même si son groupe
        // ne correspond plus : on ne réécrit jamais l'historique d'émargement.
        var past = await db.Attendances.SingleAsync(a => a.StudentId == Alice && a.SessionId == 103);
        past.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task PutUser_AssigningALanguage_EnrolsInItsSessions()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        (await db.Attendances.AnyAsync(a => a.StudentId == Alice && a.SessionId == 102)).Should().BeFalse();

        await controller.PutUser("p1", Payload(db, u => u.Lv1GroupId = GroupAnglaisA));

        (await db.Attendances.AnyAsync(a => a.StudentId == Alice && a.SessionId == 102)).Should().BeTrue();
    }

    [Fact]
    public async Task PutUser_RejectsAGroupOfTheWrongType()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        // Un code de LV2 placé dans la case sous-groupe : sans contrôle, l'étudiante
        // serait inscrite à des séances qui ne la concernent pas, sans erreur visible.
        var result = await controller.PutUser("p1", Payload(db, u => u.SubGroupId = GroupEspagnolA));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Users.SingleAsync(u => u.Id == Alice)).SubGroupId.Should().Be(GroupInfo1A);
    }

    [Fact]
    public async Task PutUser_RejectsAnUnknownGroup()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        var result = await controller.PutUser("p1", Payload(db, u => u.SubGroupId = 12345));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PutUser_AStudentCannotChangeTheirOwnGroup()
    {
        await using var db = await SeedAsync();
        // Alice modifie son propre profil, sans jeton admin.
        var (controller, _) = Build(db, Alice, withAdminToken: false);

        var result = await controller.PutUser("p1", Payload(db, u =>
        {
            u.Firstname = "Alicia";
            u.SubGroupId = GroupInfo1F;
        }));

        result.Should().BeOfType<NoContentResult>();

        var alice = await db.Users.SingleAsync(u => u.Id == Alice);
        alice.Firstname.Should().Be("Alicia");
        // Sinon un étudiant choisirait les feuilles d'émargement sur lesquelles il apparaît.
        alice.SubGroupId.Should().Be(GroupInfo1A);
    }

    // ------------------------------------------------------------------ PostUser

    [Fact]
    public async Task PostUser_CreatesWithGroups_AndEnrolsInTheRightFutureSessions()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        var result = await controller.PostUser(new User
        {
            StudentNumber = "p9", Name = "Zoe", Firstname = "Z", Email = "z@x.fr",
            Year = "3A", SpecializationId = 1,
            SubGroupId = GroupInfo1F, Lv1GroupId = GroupAnglaisA
        });

        result.Should().BeOfType<CreatedAtActionResult>();

        var zoe = await db.Users.SingleAsync(u => u.StudentNumber == "p9");
        zoe.SubGroupId.Should().Be(GroupInfo1F);

        var sessionIds = await db.Attendances
            .Where(a => a.StudentId == zoe.Id)
            .Select(a => a.SessionId)
            .ToListAsync();

        // Le TP du groupe F et l'anglais, à venir. Pas le TP du groupe A, ni le passé.
        sessionIds.Should().BeEquivalentTo(new[] { 101, 102 });
    }

    [Fact]
    public async Task PostUser_RejectsAGroupOfTheWrongType()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        var result = await controller.PostUser(new User
        {
            StudentNumber = "p9", Name = "Zoe", Firstname = "Z", Email = "z@x.fr",
            Year = "3A", SpecializationId = 1, Lv1GroupId = GroupInfo1A
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Users.AnyAsync(u => u.StudentNumber == "p9")).Should().BeFalse();
    }

    [Fact]
    public async Task PostUser_AnAdminHasNoGroup()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        await controller.PostUser(new User
        {
            StudentNumber = "adm2", Name = "Ad", Firstname = "Min2", Email = "adm2@x.fr",
            Year = "ADMIN", SubGroupId = GroupInfo1A, Lv1GroupId = GroupAnglaisA
        });

        var created = await db.Users.SingleAsync(u => u.StudentNumber == "adm2");
        created.IsAdmin.Should().BeTrue();
        created.SubGroupId.Should().BeNull();
        created.Lv1GroupId.Should().BeNull();
    }

    // ------------------------------------------------------------------ liste

    [Fact]
    public async Task GetUserByYear_ExposesGroupIdsAndLabels()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, AdminId);

        var alice = await db.Users.SingleAsync(u => u.Id == Alice);
        alice.Lv1GroupId = GroupAnglaisA;
        await db.SaveChangesAsync();

        var result = await controller.GetUserByYear("3A", specializationId: 1);
        var items = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<UserController.StudentListItemDto>>().Subject.ToList();

        var dto = items.Single(i => i.StudentNumber == "p1");
        dto.SubGroupId.Should().Be(GroupInfo1A);
        dto.SubGroupLabel.Should().Be("INFO 1-A");
        dto.Lv1GroupLabel.Should().Be("Anglais A");
        dto.Lv2GroupId.Should().BeNull();
    }
}
