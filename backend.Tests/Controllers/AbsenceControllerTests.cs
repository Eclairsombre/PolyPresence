using System.Security.Claims;
using System.Text.Json;
using backend.Controllers;
using backend.Data;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class AbsenceControllerTests
{
    private const int AdminId = 900;
    private const int ProfAlgoId = 800;
    private const int ProfLangId = 801;
    private const int StudentAccountId = 3;
    private const int AcademicYear = 2026;

    private const int Alice = 1;
    private const int Bob = 2;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ jeu de données

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();

        db.Specializations.AddRange(
            new Specialization { Id = 1, Name = "Informatique", Code = "INFO" },
            new Specialization { Id = 2, Name = "Langues", Code = "LANGUES" });

        db.Groups.AddRange(
            new Group { Id = 10, Label = "INFO 1-A", DisplayName = "INFO 1-A", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = 11, Label = "INFO 1-B", DisplayName = "INFO 1-B", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = 20, Label = "A-I3002TR-AR51", DisplayName = "Anglais A", Type = GroupType.Language, SpecializationId = 2, Year = "3A" },
            new Group { Id = 21, Label = "A-I3004TR-ES51", DisplayName = "Espagnol B", Type = GroupType.Language, SpecializationId = 2, Year = "3A" });

        db.Users.AddRange(
            new User { Id = AdminId, StudentNumber = "ADM", Name = "Scola", Firstname = "Rité", Email = "scola@x.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = ProfAlgoId, Name = "TURING", Firstname = "Alan", Email = "turing@x.fr", Year = "PROF", IsProfessor = true },
            new User { Id = ProfLangId, Name = "SHAKESPEARE", Firstname = "William", Email = "will@x.fr", Year = "PROF", IsProfessor = true },
            new User { Id = StudentAccountId, StudentNumber = "p3", Name = "Curieux", Firstname = "Carl", Email = "c@x.fr", Year = "3A", SpecializationId = 1 },
            new User { Id = Alice, StudentNumber = "p1", Name = "Martin", Firstname = "Alice", Email = "alice@x.fr", Year = "3A", SpecializationId = 1, SubGroupId = 10, Lv1GroupId = 20 },
            new User { Id = Bob, StudentNumber = "p2", Name = "Durand", Firstname = "Bob", Email = "bob@x.fr", Year = "3A", SpecializationId = 1, SubGroupId = 11, Lv1GroupId = 21 });

        db.Sessions.AddRange(
            Session(100, "Algo", new DateTime(2026, 10, 5, 8, 0, 0), 3, ProfAlgoId, signed: true),
            Session(101, "Algo", new DateTime(2026, 10, 12, 8, 0, 0), 3, ProfAlgoId, signed: true),
            Session(102, "Réseaux", new DateTime(2026, 10, 6, 14, 0, 0), 2, ProfLangId, signed: true),
            Session(103, "Algo", new DateTime(2026, 10, 19, 8, 0, 0), 3, ProfAlgoId, signed: false),
            Session(104, "Algo", new DateTime(2026, 6, 1, 8, 0, 0), 3, ProfAlgoId, signed: true),
            Session(200, "Anglais", new DateTime(2026, 10, 7, 10, 0, 0), 2, ProfLangId, signed: true, language: true, groups: new[] { 20, 21 }),
            Session(201, "Anglais", new DateTime(2026, 10, 14, 10, 0, 0), 2, ProfLangId, signed: true, language: true, groups: new[] { 20 }));

        db.Attendances.AddRange(
            Attendance(1, 100, Alice, AttendanceStatus.Absent),
            Attendance(2, 101, Alice, AttendanceStatus.Absent),
            Attendance(3, 102, Alice, AttendanceStatus.Present),
            Attendance(4, 103, Alice, AttendanceStatus.Absent),   // feuille non signée : pas une absence
            Attendance(5, 104, Alice, AttendanceStatus.Absent),   // année universitaire précédente
            Attendance(6, 100, Bob, AttendanceStatus.Present),
            Attendance(7, 101, Bob, AttendanceStatus.Present),
            Attendance(8, 102, Bob, AttendanceStatus.Absent, comment: "Parti à la pause"),
            Attendance(9, 200, Alice, AttendanceStatus.Absent),
            Attendance(10, 201, Alice, AttendanceStatus.Absent),
            Attendance(11, 200, Bob, AttendanceStatus.Absent));

        db.AbsenceReasons.AddRange(
            new AbsenceReason { Id = 1, Label = "Certificat médical", SortOrder = 1 },
            new AbsenceReason { Id = 2, Label = "Grève TCL", SortOrder = 2 },
            new AbsenceReason { Id = 3, Label = "Ancien motif", SortOrder = 3, IsActive = false });

        await db.SaveChangesAsync();
        return db;
    }

    private static Session Session(int id, string name, DateTime start, int hours, int profId,
        bool signed, bool language = false, int[]? groups = null)
    {
        return new Session
        {
            Id = id,
            Name = name,
            Date = start.Date,
            StartTime = start,
            EndTime = start.AddHours(hours),
            Year = "3A",
            SpecializationId = language ? 2 : 1,
            Room = "R1",
            ValidationCode = "0000",
            ProfId = profId.ToString(),
            ProfSignature = signed ? "data:image/png;base64,AAAA" : null,
            IcsKind = language ? GroupType.Language : GroupType.Sub,
            SessionGroups = (groups ?? Array.Empty<int>())
                .Select(g => new SessionGroup { SessionId = id, GroupId = g })
                .ToList()
        };
    }

    private static Attendance Attendance(int id, int sessionId, int studentId, AttendanceStatus status, string? comment = null)
        => new() { Id = id, SessionId = sessionId, StudentId = studentId, Status = status, Comment = comment };

    private static AbsenceController Controller(ApplicationDbContext db, int userId)
    {
        return new AbsenceController(db, new AbsenceService(db), NullLogger<AbsenceController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpAs(userId) }
        };
    }

    private static DefaultHttpContext HttpAs(int userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
    };

    private static JsonElement Body(IActionResult result)
    {
        result.Should().BeAssignableTo<ObjectResult>();
        return JsonSerializer.SerializeToElement(((ObjectResult)result).Value, Web);
    }

    private static int Status(IActionResult result) => ((ObjectResult)result).StatusCode ?? 200;

    private static List<int> AttendanceIds(JsonElement items)
        => items.EnumerateArray().Select(i => i.GetProperty("attendanceId").GetInt32()).OrderBy(i => i).ToList();

    // ------------------------------------------------------------------ droits

    [Fact]
    public async Task Student_IsRefused()
    {
        await using var db = await SeedAsync();

        var result = await Controller(db, StudentAccountId).GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear });

        Status(result).Should().Be(403);
    }

    [Fact]
    public async Task Professor_CanRead_ButNotJustify()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, ProfAlgoId);

        var read = await controller.GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear });
        var write = await controller.SetJustification(new AbsenceController.JustificationRequest { AttendanceIds = { 1 }, IsJustified = true });

        Status(read).Should().Be(200);
        Status(write).Should().Be(403);
        (await db.Attendances.FindAsync(1))!.IsJustified.Should().BeFalse();
    }

    // ------------------------------------------------------------------ consultation

    [Fact]
    public async Task GetAbsences_KeepsOnlyAbsencesOnSignedSessionsOfTheYear()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear }));

        AttendanceIds(body.GetProperty("items")).Should().Equal(1, 2, 8, 9, 10, 11);
        body.GetProperty("total").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task GetAbsences_ReturnsDurationTeachersAndComment()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).GetAbsences(new AbsenceController.AbsenceQuery
        {
            AcademicYear = AcademicYear,
            Course = "Réseaux"
        }));

        var row = body.GetProperty("items").EnumerateArray().Single();
        row.GetProperty("durationMinutes").GetInt32().Should().Be(120);
        row.GetProperty("teachers").GetString().Should().Be("William SHAKESPEARE");
        row.GetProperty("comment").GetString().Should().Be("Parti à la pause");
        row.GetProperty("isJustified").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetAbsences_SearchMatchesNameNumberOrEmail()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        async Task<List<int>> Search(string term) => AttendanceIds(Body(await controller.GetAbsences(
            new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Search = term })).GetProperty("items"));

        (await Search("durand")).Should().Equal(8, 11);
        (await Search("P1")).Should().Equal(1, 2, 9, 10);
        (await Search("bob@x")).Should().Equal(8, 11);
        (await Search("alice martin")).Should().Equal(1, 2, 9, 10);
    }

    [Fact]
    public async Task GetAbsences_SingleStudent_IsReportedForThePeriodShortcut()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).GetAbsences(
            new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Search = "p2" }));

        body.GetProperty("studentCount").GetInt32().Should().Be(1);
        body.GetProperty("singleStudent").GetProperty("studentNumber").GetString().Should().Be("p2");
    }

    [Fact]
    public async Task GetAbsences_GroupFilter_MatchesSubGroupOrLanguageGroup()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        var bySubGroup = Body(await controller.GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, GroupId = 11 }));
        var byLanguage = Body(await controller.GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, GroupId = 20 }));

        AttendanceIds(bySubGroup.GetProperty("items")).Should().Equal(8, 11);
        AttendanceIds(byLanguage.GetProperty("items")).Should().Equal(1, 2, 9, 10);
    }

    [Fact]
    public async Task GetAbsences_MineForAProfessor_KeepsTheCoursesHeTaught()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, ProfAlgoId).GetAbsences(
            new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Mine = true }));

        AttendanceIds(body.GetProperty("items")).Should().Equal(1, 2);
    }

    [Fact]
    public async Task GetAbsences_MineIsIgnoredForTheScolarite()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).GetAbsences(
            new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Mine = true }));

        body.GetProperty("total").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task GetAbsences_IsPaginated()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).GetAbsences(
            new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Page = 2, PageSize = 4 }));

        body.GetProperty("total").GetInt32().Should().Be(6);
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetStudent_ComputesTheRatePerCourse_ExcludingJustifiedAbsences()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        await controller.SetJustification(new AbsenceController.JustificationRequest { AttendanceIds = { 1 }, IsJustified = true });
        var body = Body(await controller.GetStudent(Alice, new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear }));

        var algo = body.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Algo");
        algo.GetProperty("sessionCount").GetInt32().Should().Be(2);
        algo.GetProperty("absenceCount").GetInt32().Should().Be(2);
        algo.GetProperty("justifiedCount").GetInt32().Should().Be(1);
        algo.GetProperty("rate").GetDouble().Should().BeApproximately(0.5, 1e-9);
        algo.GetProperty("hasAlert").GetBoolean().Should().BeTrue();

        var reseaux = body.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Réseaux");
        reseaux.GetProperty("rate").GetDouble().Should().Be(0);
        reseaux.GetProperty("hasAlert").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetStudent_GivesAGlobalSummary()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        await controller.SetJustification(new AbsenceController.JustificationRequest { AttendanceIds = { 1 }, IsJustified = true });
        var body = Body(await controller.GetStudent(Alice, new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear }));

        // Algo 2×3 h (2 absences dont 1 justifiée), Réseaux 2 h (présente), Anglais 2×2 h (2 absences).
        var summary = body.GetProperty("summary");
        summary.GetProperty("courseCount").GetInt32().Should().Be(3);
        summary.GetProperty("sessionCount").GetInt32().Should().Be(5);
        summary.GetProperty("totalMinutes").GetInt32().Should().Be(720);
        summary.GetProperty("absenceCount").GetInt32().Should().Be(4);
        summary.GetProperty("justifiedCount").GetInt32().Should().Be(1);
        summary.GetProperty("absenceRate").GetDouble().Should().BeApproximately(600.0 / 720, 1e-9);
        summary.GetProperty("unjustifiedRate").GetDouble().Should().BeApproximately(420.0 / 720, 1e-9);

        var algo = body.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Algo");
        algo.GetProperty("absenceRate").GetDouble().Should().Be(1);
    }

    [Theory]
    [InlineData("mart", new[] { Alice })]
    [InlineData("alice martin", new[] { Alice })]
    [InlineData("MARTIN ALICE", new[] { Alice })]
    [InlineData("p2", new[] { Bob })]
    [InlineData("@x.fr", new[] { StudentAccountId, Bob, Alice })]
    [InlineData("a", new int[0])]
    public async Task SearchStudents_MatchesNameNumberOrEmail_AndSkipsStaff(string q, int[] expected)
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).SearchStudents(q));

        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32())
            .Should().Equal(expected);
    }

    [Fact]
    public async Task SearchStudents_IsReservedToTheScolarite()
    {
        await using var db = await SeedAsync();

        Status(await Controller(db, ProfAlgoId).SearchStudents("martin")).Should().Be(403);
    }

    [Fact]
    public async Task GetStudents_ListsStudentsWithAbsences_AndFlagsTheThreshold()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).GetStudents(
            new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Course = "Algo" }));

        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle();
        items[0].GetProperty("studentId").GetInt32().Should().Be(Alice);
        items[0].GetProperty("absenceMinutes").GetInt32().Should().Be(360);
        items[0].GetProperty("hasAlert").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetFilters_GivesTheProfessorHisCourses()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, ProfAlgoId).GetFilters(AcademicYear));

        body.GetProperty("user").GetProperty("isProfessor").GetBoolean().Should().BeTrue();
        body.GetProperty("myCourses").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Equal("Algo");
        body.GetProperty("academicYears").EnumerateArray().Select(y => y.GetInt32())
            .Should().Contain(new[] { 2026, 2025 });
    }

    // ------------------------------------------------------------------ justification

    [Fact]
    public async Task SetJustification_UpdatesAbsencesOnly()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).SetJustification(
            new AbsenceController.JustificationRequest { AttendanceIds = { 1, 2, 3 }, IsJustified = true }));

        body.GetProperty("updated").GetInt32().Should().Be(2);
        (await db.Attendances.FindAsync(3))!.IsJustified.Should().BeFalse("une présence ne se justifie pas");
    }

    [Fact]
    public async Task SetJustification_StateFilterFollows()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        await controller.SetJustification(new AbsenceController.JustificationRequest { AttendanceIds = { 2, 8 }, IsJustified = true });
        var justified = Body(await controller.GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, State = "justified" }));
        var unjustified = Body(await controller.GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, State = "unjustified" }));

        AttendanceIds(justified.GetProperty("items")).Should().Equal(2, 8);
        AttendanceIds(unjustified.GetProperty("items")).Should().Equal(1, 9, 10, 11);
    }

    [Fact]
    public async Task SetJustification_EmptySelection_IsRejected()
    {
        await using var db = await SeedAsync();

        var result = await Controller(db, AdminId).SetJustification(new AbsenceController.JustificationRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetJustificationForPeriod_CoversSignedAbsencesBetweenTheDates()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, AdminId).SetJustificationForPeriod(new AbsenceController.PeriodJustificationRequest
        {
            StudentNumber = "p1",
            From = new DateTime(2026, 10, 1),
            To = new DateTime(2026, 10, 19),
            IsJustified = true
        }));

        // Feuilles signées : 100 (5/10), 101 (12/10), 200 (7/10), 201 (14/10).
        // La 103 du 19/10, pas encore signée, est justifiée d'avance (absence anticipée).
        body.GetProperty("updated").GetInt32().Should().Be(4);
        body.GetProperty("upcoming").GetInt32().Should().Be(1);
        (await db.Attendances.FindAsync(4))!.IsJustified.Should().BeTrue();
        (await db.Attendances.FindAsync(5))!.IsJustified.Should().BeFalse("hors période");
        db.AbsencePeriods.Should().ContainSingle(p => p.StudentId == Alice && !p.IsDispense);
    }

    [Fact]
    public async Task SetJustificationForPeriod_Unjustified_IsOneOff_AndCreatesNoPeriod()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);
        await controller.SetJustification(new AbsenceController.JustificationRequest { AttendanceIds = { 1, 2 }, IsJustified = true });

        var body = Body(await controller.SetJustificationForPeriod(new AbsenceController.PeriodJustificationRequest
        {
            StudentId = Alice, From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 31), IsJustified = false
        }));

        body.GetProperty("updated").GetInt32().Should().Be(4);
        (await db.Attendances.FindAsync(1))!.IsJustified.Should().BeFalse();
        db.AbsencePeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task SetJustificationForPeriod_WithAComment_CommentsEveryAbsenceOfThePeriod()
    {
        await using var db = await SeedAsync();

        await Controller(db, AdminId).SetJustificationForPeriod(new AbsenceController.PeriodJustificationRequest
        {
            StudentNumber = "p2",
            From = new DateTime(2026, 10, 1),
            To = new DateTime(2026, 10, 31),
            IsJustified = true,
            Comment = "  Hospitalisation, bulletin reçu  "
        });

        var comments = await db.Attendances.AsNoTracking()
            .Where(a => a.Id == 8 || a.Id == 11).Select(a => a.Comment).ToListAsync();
        comments.Should().AllBe("Hospitalisation, bulletin reçu");
    }

    [Fact]
    public async Task SetJustificationForPeriod_WithAReason_SetsItOnEveryAbsence_AndRejectsAnInactiveOne()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);
        AbsenceController.PeriodJustificationRequest Period(int reasonId) => new()
        {
            StudentNumber = "p2", From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 31),
            IsJustified = true, ReasonId = reasonId
        };

        (await controller.SetJustificationForPeriod(Period(3))).Should().BeOfType<BadRequestObjectResult>();
        await controller.SetJustificationForPeriod(Period(1));

        var reasons = await db.Attendances.AsNoTracking()
            .Where(a => a.Id == 8 || a.Id == 11).Select(a => a.ReasonId).ToListAsync();
        reasons.Should().Equal(1, 1);
        (await db.Attendances.FindAsync(8))!.Comment.Should().Be("Parti à la pause", "sans commentaire saisi, le commentaire reste");
    }

    [Fact]
    public async Task SetJustificationForPeriod_WithoutComment_KeepsExistingComments()
    {
        await using var db = await SeedAsync();

        await Controller(db, AdminId).SetJustificationForPeriod(new AbsenceController.PeriodJustificationRequest
        {
            StudentNumber = "p2", From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 31), IsJustified = true
        });

        (await db.Attendances.FindAsync(8))!.Comment.Should().Be("Parti à la pause");
    }

    [Fact]
    public async Task SetJustificationForPeriod_RejectsAnInvertedPeriod_AndAnUnknownStudent()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        var inverted = await controller.SetJustificationForPeriod(new AbsenceController.PeriodJustificationRequest
        {
            StudentId = Alice, From = new DateTime(2026, 10, 10), To = new DateTime(2026, 10, 1), IsJustified = true
        });
        var unknown = await controller.SetJustificationForPeriod(new AbsenceController.PeriodJustificationRequest
        {
            StudentNumber = "inconnu", From = new DateTime(2026, 10, 1), To = new DateTime(2026, 10, 10), IsJustified = true
        });

        inverted.Should().BeOfType<BadRequestObjectResult>();
        unknown.Should().BeOfType<NotFoundObjectResult>();
    }

    // ------------------------------------------------------------------ motifs et commentaires

    private static AbsenceController.AnnotationRequest Annotation(int attendanceId) =>
        new() { AttendanceIds = { attendanceId } };

    [Fact]
    public async Task SetAnnotation_ReasonOnly_KeepsTheExistingComment_AndDoesNotJustify()
    {
        await using var db = await SeedAsync();

        var request = Annotation(8);
        request.SetReason = true;
        request.ReasonId = 2;
        await Controller(db, AdminId).SetAnnotation(request);

        var attendance = await db.Attendances.AsNoTracking().SingleAsync(a => a.Id == 8);
        attendance.ReasonId.Should().Be(2);
        attendance.Comment.Should().Be("Parti à la pause");
        attendance.IsJustified.Should().BeFalse("un motif ne justifie jamais une absence");
    }

    [Fact]
    public async Task SetAnnotation_CommentOnly_EditsTheCommentAndKeepsTheReason()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        var reason = Annotation(8);
        reason.SetReason = true;
        reason.ReasonId = 2;
        await controller.SetAnnotation(reason);

        var comment = Annotation(8);
        comment.SetComment = true;
        comment.Comment = "  Grève confirmée par le service RH  ";
        await controller.SetAnnotation(comment);

        var attendance = await db.Attendances.AsNoTracking().SingleAsync(a => a.Id == 8);
        attendance.ReasonId.Should().Be(2);
        attendance.Comment.Should().Be("Grève confirmée par le service RH");
    }

    [Fact]
    public async Task SetAnnotation_ReasonAndComment_AtOnce_AndEmptyValuesClearThem()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        var both = Annotation(1);
        both.SetReason = true;
        both.ReasonId = 1;
        both.SetComment = true;
        both.Comment = "Certificat reçu par mail";
        await controller.SetAnnotation(both);
        var set = await db.Attendances.AsNoTracking().SingleAsync(a => a.Id == 1);

        var clear = Annotation(1);
        clear.SetReason = true;
        clear.SetComment = true;
        clear.Comment = "   ";
        await controller.SetAnnotation(clear);
        var cleared = await db.Attendances.AsNoTracking().SingleAsync(a => a.Id == 1);

        set.ReasonId.Should().Be(1);
        set.Comment.Should().Be("Certificat reçu par mail");
        cleared.ReasonId.Should().BeNull();
        cleared.Comment.Should().BeNull();
    }

    [Fact]
    public async Task SetAnnotation_AppliesToASelection()
    {
        await using var db = await SeedAsync();

        var request = new AbsenceController.AnnotationRequest
        {
            AttendanceIds = { 1, 2, 3 },
            SetComment = true,
            Comment = "Convocation au tribunal"
        };
        var body = Body(await Controller(db, AdminId).SetAnnotation(request));

        body.GetProperty("updated").GetInt32().Should().Be(2, "la présence n°3 n'est pas une absence");
        (await db.Attendances.FindAsync(3))!.Comment.Should().BeNull();
    }

    [Fact]
    public async Task SetAnnotation_NothingToChange_IsRejected()
    {
        await using var db = await SeedAsync();

        var result = await Controller(db, AdminId).SetAnnotation(Annotation(8));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetAnnotation_TooLongComment_IsRejected()
    {
        await using var db = await SeedAsync();

        var request = Annotation(8);
        request.SetComment = true;
        request.Comment = new string('x', 501);

        (await Controller(db, AdminId).SetAnnotation(request)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetAnnotation_InactiveReason_IsRejected_UnlessAlreadyThere()
    {
        await using var db = await SeedAsync();
        var keep = await db.Attendances.FindAsync(2);
        keep!.ReasonId = 3;
        await db.SaveChangesAsync();
        var controller = Controller(db, AdminId);

        var elsewhere = Annotation(8);
        elsewhere.SetReason = true;
        elsewhere.ReasonId = 3;
        var unchanged = Annotation(2);
        unchanged.SetReason = true;
        unchanged.ReasonId = 3;
        unchanged.SetComment = true;
        unchanged.Comment = "Ancien motif conservé";

        (await controller.SetAnnotation(elsewhere)).Should().BeOfType<BadRequestObjectResult>();
        Status(await controller.SetAnnotation(unchanged)).Should().Be(200);
    }

    [Fact]
    public async Task SetAnnotation_IsReservedToTheScolarite()
    {
        await using var db = await SeedAsync();

        var request = Annotation(8);
        request.SetComment = true;
        request.Comment = "Tentative";

        Status(await Controller(db, ProfAlgoId).SetAnnotation(request)).Should().Be(403);
        (await db.Attendances.FindAsync(8))!.Comment.Should().Be("Parti à la pause");
    }

    [Fact]
    public async Task GetAbsences_ShowsTheReasonLabel()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        var request = Annotation(1);
        request.SetReason = true;
        request.ReasonId = 1;
        await controller.SetAnnotation(request);
        var body = Body(await controller.GetAbsences(new AbsenceController.AbsenceQuery { AcademicYear = AcademicYear, Search = "p1", Course = "Algo" }));

        var row = body.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("attendanceId").GetInt32() == 1);
        row.GetProperty("reasonLabel").GetString().Should().Be("Certificat médical");
    }

    // ------------------------------------------------------------------ « Mes cours » du prof

    [Fact]
    public async Task GetMySessions_ListsTheProfessorsSessions_MostRecentFirst_WithTheirAbsentees()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, ProfAlgoId).GetMySessions(AcademicYear, null, null));

        var sessions = body.GetProperty("sessions").EnumerateArray().ToList();
        sessions.Select(s => s.GetProperty("sessionId").GetInt32()).Should().Equal(103, 101, 100);

        var unsigned = sessions[0];
        unsigned.GetProperty("isSigned").GetBoolean().Should().BeFalse();
        unsigned.GetProperty("absentees").GetArrayLength().Should().Be(0, "sans signature, aucune absence ne compte");

        var lastSigned = sessions[1];
        lastSigned.GetProperty("studentCount").GetInt32().Should().Be(2);
        lastSigned.GetProperty("absentees").EnumerateArray()
            .Select(a => a.GetProperty("lastname").GetString()).Should().Equal("Martin");
    }

    [Fact]
    public async Task GetMySessions_PeriodFilter()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, ProfAlgoId).GetMySessions(
            AcademicYear, new DateTime(2026, 10, 10), new DateTime(2026, 10, 15)));

        body.GetProperty("sessions").EnumerateArray().Select(s => s.GetProperty("sessionId").GetInt32())
            .Should().Equal(101);
    }

    [Fact]
    public async Task GetMySessions_FlagsStudentsAbsentTwiceInARowFromACourse()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, ProfAlgoId).GetMySessions(AcademicYear, null, null));

        var repeated = body.GetProperty("repeated").EnumerateArray().ToList();
        repeated.Should().ContainSingle();
        repeated[0].GetProperty("lastname").GetString().Should().Be("Martin");
        repeated[0].GetProperty("courseName").GetString().Should().Be("Algo");
        repeated[0].GetProperty("consecutiveCount").GetInt32().Should().Be(2);
        repeated[0].GetProperty("absenceCount").GetInt32().Should().Be(2);
        repeated[0].GetProperty("dates").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetMySessions_IgnoresTwoAbsencesSeparatedByAPresence()
    {
        await using var db = await SeedAsync();
        // Alice est présente à une séance d'algo entre ses deux absences du 5 et du 12/10.
        db.Sessions.Add(Session(105, "Algo", new DateTime(2026, 10, 8, 8, 0, 0), 3, ProfAlgoId, signed: true));
        db.Attendances.Add(Attendance(12, 105, Alice, AttendanceStatus.Present));
        await db.SaveChangesAsync();

        var body = Body(await Controller(db, ProfAlgoId).GetMySessions(AcademicYear, null, null));

        body.GetProperty("repeated").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GetMySessions_ShowsTheGroupsOfALanguageSession()
    {
        await using var db = await SeedAsync();

        var body = Body(await Controller(db, ProfLangId).GetMySessions(AcademicYear, null, null));

        var anglais = body.GetProperty("sessions").EnumerateArray().Single(s => s.GetProperty("sessionId").GetInt32() == 200);
        anglais.GetProperty("groups").EnumerateArray().Select(g => g.GetString())
            .Should().BeEquivalentTo(new[] { "Anglais A", "Espagnol B" });
        anglais.GetProperty("isLanguage").GetBoolean().Should().BeTrue();
        anglais.GetProperty("absentees").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetMySessions_IsReservedToProfessors()
    {
        await using var db = await SeedAsync();

        var result = await Controller(db, AdminId).GetMySessions(AcademicYear, null, null);

        Status(result).Should().Be(403);
    }
}
