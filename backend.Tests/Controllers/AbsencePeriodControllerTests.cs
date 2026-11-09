using System.Security.Claims;
using System.Text.Json;
using backend.Controllers;
using backend.Data;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class AbsencePeriodControllerTests
{
    private const int AdminId = 900;
    private const int ProfId = 800;
    private const int Alice = 1;
    private const int Bob = 2;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ jeu de données

    /// <summary>
    /// Alice est absente à quatre séances : Algo 5/10 et 12/10 (signées), Algo 19/10 et
    /// Anglais 20/10 (à venir, pas encore signées). Bob est absent le 5/10.
    /// </summary>
    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();

        db.Users.AddRange(
            new User { Id = AdminId, StudentNumber = "ADM", Name = "Scola", Firstname = "Rité", Email = "scola@x.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = ProfId, Name = "TURING", Firstname = "Alan", Email = "turing@x.fr", Year = "PROF", IsProfessor = true },
            new User { Id = Alice, StudentNumber = "p1", Name = "Martin", Firstname = "Alice", Email = "alice@x.fr", Year = "3A", SpecializationId = 1 },
            new User { Id = Bob, StudentNumber = "p2", Name = "Durand", Firstname = "Bob", Email = "bob@x.fr", Year = "3A", SpecializationId = 1 });

        db.Sessions.AddRange(
            Session(100, "Algo", new DateTime(2026, 10, 5, 8, 0, 0), signed: true),
            Session(101, "Algo", new DateTime(2026, 10, 12, 8, 0, 0), signed: true),
            Session(102, "Algo", new DateTime(2026, 10, 19, 8, 0, 0), signed: false),
            Session(103, "Anglais", new DateTime(2026, 10, 20, 10, 0, 0), signed: false));

        db.Attendances.AddRange(
            Absent(1, 100, Alice),
            Absent(2, 101, Alice, comment: "Parti à la pause"),
            Absent(3, 102, Alice),
            Absent(4, 103, Alice),
            Absent(5, 100, Bob));

        db.AbsenceReasons.AddRange(
            new AbsenceReason { Id = 1, Label = "Hospitalisation", SortOrder = 1 },
            new AbsenceReason { Id = 2, Label = "Ancien motif", SortOrder = 2, IsActive = false });

        await db.SaveChangesAsync();
        return db;
    }

    private static Session Session(int id, string name, DateTime start, bool signed) => new()
    {
        Id = id,
        Name = name,
        Date = start.Date,
        StartTime = start,
        EndTime = start.AddHours(2),
        Year = "3A",
        SpecializationId = 1,
        Room = "R1",
        ValidationCode = "0000",
        ProfId = ProfId.ToString(),
        ProfSignatureToken = $"token-{id}",
        ProfSignature = signed ? "data:image/png;base64,AAAA" : null
    };

    private static Attendance Absent(int id, int sessionId, int studentId, string? comment = null)
        => new() { Id = id, SessionId = sessionId, StudentId = studentId, Status = AttendanceStatus.Absent, Comment = comment };

    private static AbsencePeriodController Controller(ApplicationDbContext db, int userId) =>
        new(db, NullLogger<AbsencePeriodController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };

    private static SessionController SessionController(ApplicationDbContext db) =>
        new(db, NullLogger<SessionController>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static AbsencePeriodController.AbsencePeriodRequest Request(
        int studentId = Alice, int fromDay = 15, int toDay = 25) => new()
    {
        StudentId = studentId,
        From = new DateTime(2026, 10, fromDay),
        To = new DateTime(2026, 10, toDay)
    };

    /// <summary>Dispense : un étudiant et un cours, sans dates.</summary>
    private static AbsencePeriodController.AbsencePeriodRequest Dispense(
        int studentId = Alice, string course = "Algo") => new()
    {
        StudentId = studentId,
        CourseName = course,
        IsDispense = true
    };

    private static JsonElement Body(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value, Web);

    private static async Task<Attendance> Get(ApplicationDbContext db, int id) =>
        await db.Attendances.AsNoTracking().SingleAsync(a => a.Id == id);

    // ------------------------------------------------------------------ création

    [Fact]
    public async Task Create_JustifiesUpcomingAbsencesInAdvance()
    {
        await using var db = await SeedAsync();
        var request = Request();
        request.ReasonId = 1;
        request.Comment = "Opération prévue";

        var body = Body(await Controller(db, AdminId).Create(request));

        body.GetProperty("updated").GetInt32().Should().Be(0, "aucune feuille signée dans la période");
        body.GetProperty("upcoming").GetInt32().Should().Be(2);
        foreach (var id in new[] { 3, 4 })
        {
            var a = await Get(db, id);
            a.IsJustified.Should().BeTrue();
            a.ReasonId.Should().Be(1);
            a.Comment.Should().Be("Opération prévue");
            a.AbsencePeriodId.Should().NotBeNull();
        }
        (await Get(db, 2)).IsJustified.Should().BeFalse("hors période");
    }

    [Fact]
    public async Task Create_AnticipatedAbsence_AlwaysCoversEveryCourse()
    {
        await using var db = await SeedAsync();
        var request = Request();
        request.CourseName = "Anglais";

        await Controller(db, AdminId).Create(request);

        (await db.AbsencePeriods.SingleAsync()).CourseName.Should().BeNull("le cours est ignoré");
        (await Get(db, 3)).IsJustified.Should().BeTrue("Algo 19/10 est couvert");
        (await Get(db, 4)).IsJustified.Should().BeTrue("Anglais 20/10 aussi");
    }

    [Fact]
    public async Task Create_AlsoCoversAbsencesAlreadyRecorded()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).Create(Request(fromDay: 1, toDay: 12)));

        body.GetProperty("updated").GetInt32().Should().Be(2);
        (await Get(db, 1)).IsJustified.Should().BeTrue();
        (await Get(db, 2)).Comment.Should().Be("Parti à la pause", "sans commentaire saisi, le commentaire reste");
        (await Get(db, 5)).IsJustified.Should().BeFalse("autre étudiant");
    }

    [Fact]
    public async Task Create_Dispense_CoversEverySessionOfItsCourse_WithoutDates()
    {
        await using var db = await SeedAsync();
        var request = Dispense();
        // Des dates envoyées par erreur sont ignorées : une dispense n'a pas de durée.
        request.From = new DateTime(2026, 10, 19);
        request.To = new DateTime(2026, 10, 19);

        var body = Body(await Controller(db, AdminId).Create(request));

        body.GetProperty("updated").GetInt32().Should().Be(2, "Algo 5/10 et 12/10, signées");
        body.GetProperty("upcoming").GetInt32().Should().Be(1, "Algo 19/10");
        foreach (var id in new[] { 1, 2, 3 })
        {
            var a = await Get(db, id);
            a.IsJustified.Should().BeTrue();
            a.Comment.Should().Be("Dispense");
        }
        (await Get(db, 4)).IsJustified.Should().BeFalse("seul l'algo est concerné");
        var period = await db.AbsencePeriods.SingleAsync();
        period.From.Should().BeNull();
        period.To.Should().BeNull();
    }

    [Fact]
    public async Task Create_DispenseWithAComment_KeepsDispenseFirst()
    {
        await using var db = await SeedAsync();
        var request = Dispense();
        request.Comment = "  sport, certificat annuel ";

        await Controller(db, AdminId).Create(request);

        (await Get(db, 3)).Comment.Should().Be("Dispense : sport, certificat annuel");
    }

    [Fact]
    public async Task Create_RejectsInvalidRequests()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        (await controller.Create(Request(fromDay: 20, toDay: 10))).Should().BeOfType<BadRequestObjectResult>();
        var withoutDates = Request();
        withoutDates.From = null;
        (await controller.Create(withoutDates)).Should().BeOfType<BadRequestObjectResult>("une absence anticipée a une durée");
        (await controller.Create(Dispense(course: " "))).Should().BeOfType<BadRequestObjectResult>("une dispense porte sur un cours");
        var inactive = Request();
        inactive.ReasonId = 2;
        (await controller.Create(inactive)).Should().BeOfType<BadRequestObjectResult>();
        var unknown = Request();
        unknown.StudentId = 404;
        (await controller.Create(unknown)).Should().BeOfType<NotFoundObjectResult>();
        var tooLong = Request();
        tooLong.Comment = new string('x', 501);
        (await controller.Create(tooLong)).Should().BeOfType<BadRequestObjectResult>();

        db.AbsencePeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyTheScolarite_ManagesPeriods()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, ProfId);

        ((ObjectResult)await controller.Create(Request())).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.GetAll(null, null)).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.Delete(1)).StatusCode.Should().Be(403);
    }

    // ------------------------------------------------------------------ modification, suppression

    [Fact]
    public async Task Update_UndoesTheOldEffect_AndAppliesTheNewOne()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);
        var id = Body(await controller.Create(Request())).GetProperty("id").GetInt32();

        var narrower = Request(fromDay: 20, toDay: 25);
        narrower.Comment = "Raccourcie";
        await controller.Update(id, narrower);

        var algo = await Get(db, 3);
        algo.IsJustified.Should().BeFalse("le 19/10 n'est plus couvert");
        algo.AbsencePeriodId.Should().BeNull();
        var anglais = await Get(db, 4);
        anglais.IsJustified.Should().BeTrue();
        anglais.Comment.Should().Be("Raccourcie");
    }

    [Fact]
    public async Task Delete_PutsTheAbsencesBackToUnjustified_AndClearsWhatThePeriodHadSet()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);
        var request = Request(fromDay: 1, toDay: 31);
        request.ReasonId = 1;
        request.Comment = "Opération prévue";
        var id = Body(await controller.Create(request)).GetProperty("id").GetInt32();

        var body = Body(await controller.Delete(id));

        body.GetProperty("reverted").GetInt32().Should().Be(4);
        var a = await Get(db, 1);
        a.IsJustified.Should().BeFalse();
        a.ReasonId.Should().BeNull();
        a.Comment.Should().BeNull();
        db.AbsencePeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_KeepsACommentEditedSince()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);
        var id = Body(await controller.Create(Dispense())).GetProperty("id").GetInt32();
        (await db.Attendances.FindAsync(3))!.Comment = "Revu avec l'étudiant";
        await db.SaveChangesAsync();

        await controller.Delete(id);

        (await Get(db, 3)).Comment.Should().Be("Revu avec l'étudiant");
    }

    [Fact]
    public async Task GetAll_ListsPeriodsWithTheirStudentAndCoveredAbsences()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);
        await controller.Create(Dispense(studentId: Bob));
        await controller.Create(Request());
        await controller.Create(Request(fromDay: 1, toDay: 3));

        var all = Body(await controller.GetAll(null, null)).EnumerateArray().ToList();
        var current = Body(await controller.GetAll(null, null, current: true)).EnumerateArray().ToList();
        var onlyDispenses = Body(await controller.GetAll(null, "durand", dispense: true)).EnumerateArray().ToList();

        all.Should().HaveCount(3);
        all[0].GetProperty("from").GetDateTime().Day.Should().Be(15, "absences anticipées, les plus récentes d'abord");
        all[0].GetProperty("absenceCount").GetInt32().Should().Be(2);
        all[2].GetProperty("isDispense").GetBoolean().Should().BeTrue("les dispenses viennent après");
        current.Should().Contain(p => p.GetProperty("isDispense").GetBoolean(), "une dispense n'est jamais terminée");
        onlyDispenses.Should().ContainSingle();
        onlyDispenses[0].GetProperty("from").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ------------------------------------------------------------------ page d'appel

    [Fact]
    public async Task SigningTheSheet_JustifiesAStudentAddedAfterThePeriod()
    {
        await using var db = await SeedAsync();
        await Controller(db, AdminId).Create(Dispense(studentId: Bob));
        // Bob est inscrit à la séance du 19/10 après la saisie de sa dispense.
        db.Attendances.Add(Absent(6, 102, Bob));
        await db.SaveChangesAsync();

        await SessionController(db).SaveProfSignature("token-102", new SessionController.SignatureModel { Signature = "sig" });

        var bob = await Get(db, 6);
        bob.IsJustified.Should().BeTrue();
        bob.Comment.Should().Be("Dispense");
    }
}
