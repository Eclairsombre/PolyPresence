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

namespace backend.Tests.Controllers;

public class GroupControllerTests
{
    private const int AdminId = 900;

    private static (GroupController Controller, AdminTokenService Tokens) Build(
        backend.Data.ApplicationDbContext db, bool withAdminToken = true)
    {
        var tokens = new AdminTokenService();
        var services = new ServiceCollection();
        services.AddSingleton(tokens);

        var controller = new GroupController(db, NullLogger<GroupController>.Instance);
        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, AdminId.ToString()) }, "test"))
        };

        if (withAdminToken)
            http.Request.Headers["Admin-Token"] = tokens.GenerateToken(AdminId);

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, tokens);
    }

    private static async Task<backend.Data.ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();

        db.Specializations.AddRange(
            new Specialization { Id = 1, Name = "Informatique", Code = "INFO" },
            new Specialization { Id = 2, Name = "Langues", Code = "LANGUES" });

        db.Groups.AddRange(
            new Group { Id = 10, Label = "INFO 1-A", DisplayName = "INFO 1-A", Type = GroupType.Sub, SpecializationId = 1, Year = "3A", SeenCount = 61 },
            new Group { Id = 11, Label = "INFO 1-B", DisplayName = "INFO 1-B", Type = GroupType.Sub, SpecializationId = 1, Year = "3A", SeenCount = 61 },
            // Bruit d'un cours mutualisé : découvert via le calendrier INFO, sans aucun étudiant.
            new Group { Id = 12, Label = "MAT5 A", DisplayName = "MAT5 A", Type = GroupType.Sub, SpecializationId = 1, Year = "3A", SeenCount = 2 },
            new Group { Id = 20, Label = "A-PL9003TR-BE91", DisplayName = "Anglais A", Type = GroupType.Lv1, SpecializationId = 2, Year = "3A" },
            new Group { Id = 99, Label = "Ancien groupe", DisplayName = "Ancien groupe", Type = GroupType.Sub, SpecializationId = 1, Year = "2A", IsActive = false });

        db.Users.AddRange(
            new User { Id = AdminId, StudentNumber = "ADM", Name = "Ad", Firstname = "Min", Email = "adm@x.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = 1, StudentNumber = "p1", Name = "Alice", Firstname = "A", Email = "a@x.fr", Year = "3A", SpecializationId = 1, SubGroupId = 10, Lv1GroupId = 20 },
            new User { Id = 2, StudentNumber = "p2", Name = "Bob", Firstname = "B", Email = "b@x.fr", Year = "3A", SpecializationId = 1, SubGroupId = 10 });

        db.Sessions.Add(new Session
        {
            Id = 500, Date = DateTime.Today, StartTime = DateTime.Today.AddHours(8), EndTime = DateTime.Today.AddHours(10),
            Year = "3A", Name = "Algo", Room = "R1", ValidationCode = "1234", SpecializationId = 1,
            SessionGroups = new List<SessionGroup> { new() { SessionId = 500, GroupId = 10 } }
        });

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task GetAll_ReturnsActiveGroups_WithStudentAndSessionCounts()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var result = await controller.GetAll();
        var groups = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<GroupController.GroupListItemDto>>().Subject.ToList();

        groups.Should().HaveCount(4); // le groupe inactif est exclu par défaut

        var infoA = groups.Single(g => g.Label == "INFO 1-A");
        infoA.StudentCount.Should().Be(2);
        infoA.SessionCount.Should().Be(1);
        infoA.SpecializationCode.Should().Be("INFO");

        // Les compteurs sont là pour repérer le bruit : un libellé venu d'un cours
        // mutualisé d'une autre filière n'a ni étudiant ni séance de notre côté.
        var foreign = groups.Single(g => g.Label == "MAT5 A");
        foreign.StudentCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_FiltersByTypeAndSpecialization()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var lv1 = (await controller.GetAll(type: GroupType.Lv1)).Result
            .Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<GroupController.GroupListItemDto>>().Subject.ToList();
        lv1.Should().ContainSingle().Which.Label.Should().Be("A-PL9003TR-BE91");

        var info = (await controller.GetAll(specializationId: 1)).Result
            .Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<GroupController.GroupListItemDto>>().Subject.ToList();
        info.Should().OnlyContain(g => g.SpecializationId == 1);
    }

    [Fact]
    public async Task GetAll_IncludeInactive_ReturnsDeactivatedGroups()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var all = (await controller.GetAll(includeInactive: true)).Result
            .Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<GroupController.GroupListItemDto>>().Subject.ToList();

        all.Should().HaveCount(5);
    }

    [Fact]
    public async Task Create_RejectsDuplicateLabel_CaseInsensitively()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var result = await controller.Create(new GroupController.GroupCreateModel { Label = "info 1-a" });

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_AllowsPreDeclaringAGroupBeforeItsCalendarExists()
    {
        // Cas réel : les EDT de langues ne sont pas encore publiés, mais les étudiants
        // sont déjà répartis. On déclare le groupe pour pouvoir les affecter tout de suite.
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var result = await controller.Create(new GroupController.GroupCreateModel
        {
            Label = "B-PL9004TR-BE92", DisplayName = "Allemand B", Type = GroupType.Lv2, SpecializationId = 2, Year = "3A"
        });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var created = await db.Groups.SingleAsync(g => g.Label == "B-PL9004TR-BE92");
        created.Type.Should().Be(GroupType.Lv2);
        created.DisplayName.Should().Be("Allemand B");
    }

    [Fact]
    public async Task Create_WithoutAdminToken_IsUnauthorized()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db, withAdminToken: false);

        var result = await controller.Create(new GroupController.GroupCreateModel { Label = "X" });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Update_ChangesDisplayName_ButNeverTheAdeLabel()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var result = await controller.Update(10, new GroupController.GroupUpdateModel
        {
            DisplayName = "Groupe A (TD)", Type = GroupType.Sub
        });

        result.Should().BeOfType<NoContentResult>();
        var group = await db.Groups.FindAsync(10);
        group!.DisplayName.Should().Be("Groupe A (TD)");
        // Le libellé reste la clé de rapprochement avec ADE : rien ne permet de le modifier.
        group.Label.Should().Be("INFO 1-A");
    }

    [Fact]
    public async Task Deactivate_IsRefused_WhenStudentsAreStillAttached()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        var result = await controller.Deactivate(10);

        result.Should().BeOfType<ConflictObjectResult>();
        (await db.Groups.FindAsync(10))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_HidesTheGroup_WithoutDeletingIt()
    {
        await using var db = await SeedAsync();
        var (controller, _) = Build(db);

        // "MAT5 A" n'a aucun étudiant : c'est exactement le bruit qu'on veut masquer.
        var result = await controller.Deactivate(12);

        result.Should().BeOfType<NoContentResult>();
        var group = await db.Groups.FindAsync(12);
        // Jamais de suppression physique : des séances passées le référencent.
        group.Should().NotBeNull();
        group!.IsActive.Should().BeFalse();
    }
}
