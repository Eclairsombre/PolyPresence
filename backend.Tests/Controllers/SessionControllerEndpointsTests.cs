using System.Security.Claims;
using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class SessionControllerEndpointsTests
{
    private static SessionController BuildController(
        backend.Data.ApplicationDbContext db,
        ClaimsPrincipal? principal = null)
    {
        var controller = new SessionController(
            db,
            NullLogger<SessionController>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal ?? new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return controller;
    }

    private static ClaimsPrincipal PrincipalWithId(int id) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));

    [Fact]
    public async Task PostSession_ShouldCreateSession_WithSignatureToken()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.PostSession(new Session
        {
            Name = "Cours",
            Year = "3A",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Today.AddHours(8),
            EndTime = DateTime.Today.AddHours(10),
            ValidationCode = "ABCD",
            SpecializationId = 1
        });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        db.Sessions.Should().ContainSingle(s => !string.IsNullOrEmpty(s.ProfSignatureToken));
    }

    [Fact]
    public async Task GetSessionByProfSignatureToken_ShouldReturnNotFound_WhenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetSessionByProfSignatureToken("unknown");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SaveProfSignature_ShouldUpdateSignature_WhenTokenMatches()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session
        {
            Id = 1,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A",
            ProfSignatureToken = "token-1"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.SaveProfSignature("token-1", new SessionController.SignatureModel { Signature = "sig" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Sessions.FindAsync(1))!.ProfSignature.Should().Be("sig");
    }

    [Fact]
    public async Task PutSession_ShouldReturnBadRequest_WhenIdsMismatch()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, PrincipalWithId(1));

        var result = await controller.PutSession(1, new Session { Id = 2, Year = "3A", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now, ValidationCode = "X" });

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task PutSession_ShouldReturnUnauthorized_WhenClaimInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "bad") }, "test"));
        var controller = BuildController(db, principal);

        var result = await controller.PutSession(1, new Session { Id = 1, Year = "3A", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now, ValidationCode = "X" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task DeleteSession_ShouldReturnForbid_WhenUserNotAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "U", Firstname = "X", Email = "u@x.fr", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(1));

        var result = await controller.DeleteSession(123);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteSession_ShouldDeleteSessionAndAttendances_WhenAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true });
        db.Sessions.Add(new Session
        {
            Id = 10,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "AAA"
        });
        db.Attendances.Add(new Attendance { Id = 1, SessionId = 10, StudentId = 1, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(1));

        var result = await controller.DeleteSession(10);

        result.Should().BeOfType<NoContentResult>();
        db.Sessions.Should().BeEmpty();
        db.Attendances.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAttendance_ShouldReturnForbid_WhenNotAuthorized()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 1, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "A" });
        db.Attendances.Add(new Attendance { Id = 1, SessionId = 1, StudentId = 1, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetAttendance(1, "S1");

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SaveSignature_ShouldReturnOk_WhenOwnProfile()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 5, StudentNumber = "S5", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(5));

        var result = await controller.SaveSignature("S5", new SessionController.SignatureModel { Signature = "sig-data" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Users.FindAsync(5))!.Signature.Should().Be("sig-data");
    }

    [Fact]
    public async Task GetSignature_ShouldReturnNotFound_WhenStudentMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetSignature("UNKNOWN");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetNotSendSessions_ShouldReturnNotFound_WhenNone()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetNotSendSessions();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetProfEmail_ShouldUpdateProfessorEmail_WhenValid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.Add(new Professor { Id = 7, Name = "Prof", Firstname = "One", Email = "old@prof.fr" });
        db.Sessions.Add(new Session
        {
            Id = 1,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A",
            ProfId = "7"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.SetProfEmail(1, new SessionController.SetProfEmailModel { ProfEmail = "new@prof.fr" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Professors.FindAsync(7))!.Email.Should().Be("new@prof.fr");
    }

    [Fact]
    public async Task ResendProf2Mail_ShouldReturnNotFound_WhenProf2Missing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session
        {
            Id = 30,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ResendProf2Mail(30);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ChangeAttendanceStatus_ShouldReturnForbid_WhenNoAuthAndNoToken()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 9, StudentNumber = "S9", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 90,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A"
        });
        db.Attendances.Add(new Attendance { Id = 90, SessionId = 90, StudentId = 9, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ChangeAttendanceStatus(90, "S9", new SessionController.ChangeAttendanceStatusModel { Status = 0 });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateAttendanceComment_ShouldReturnOk_WhenOwnAttendance()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 11, StudentNumber = "S11", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 110,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A"
        });
        db.Attendances.Add(new Attendance { Id = 110, SessionId = 110, StudentId = 11, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(11));

        var result = await controller.UpdateAttendanceComment(110, "S11", new SessionController.CommentUpdateModel { Comment = "Retard métro" });

        result.Should().BeOfType<OkObjectResult>();
        db.Attendances.Single(a => a.Id == 110).Comment.Should().Be("Retard métro");
    }

    [Fact]
    public async Task GetSessionByProfSignatureToken_ShouldReturnSession_WhenFound()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session
        {
            Id = 201,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A",
            ProfSignatureToken = "tok-201"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetSessionByProfSignatureToken("tok-201");

        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(201);
    }

    [Fact]
    public async Task SaveProfSignature_ShouldReturnNotFound_WhenTokenUnknown()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.SaveProfSignature("nope", new SessionController.SignatureModel { Signature = "sig" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PutSession_ShouldReturnForbid_WhenUserNotAdminOrDelegate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 2, StudentNumber = "S2", Name = "U", Firstname = "Two", Email = "u@two.fr", Year = "3A", IsAdmin = false, IsDelegate = false });
        db.Sessions.Add(new Session { Id = 202, Year = "3A", Name = "Old", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(2));
        var result = await controller.PutSession(202, new Session { Id = 202, Year = "3A", Name = "New", Room = "A2", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSessionAttendances_ShouldReturnOk_WhenAttendancesExist()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 10, StudentNumber = "S10", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 203, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        db.Attendances.Add(new Attendance { Id = 203, SessionId = 203, StudentId = 10, Status = AttendanceStatus.Absent, Comment = "RAS" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetSessionAttendances(203);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetProf2Email_ShouldReturnNotFound_WhenProf2IdInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session
        {
            Id = 204,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A",
            ProfId2 = "not-int"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.SetProf2Email(204, new SessionController.SetProfEmailModel { ProfEmail = "p2@test.fr" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldAssignProfessor_WhenDelegate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 20, StudentNumber = "DEL", Name = "D", Firstname = "E", Email = "d@e.fr", Year = "3A", IsDelegate = true });
        db.Professors.Add(new Professor { Id = 21, Name = "Prof", Firstname = "One", Email = "p1@test.fr" });
        db.Sessions.Add(new Session { Id = 205, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "A" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(20));
        var result = await controller.SetSessionProfessor(205, 1, new SessionController.SetSessionProfessorModel { ProfessorId = 21 });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Sessions.FindAsync(205))!.ProfId.Should().Be("21");
    }

    [Fact]
    public async Task ChangeAttendanceStatus_ShouldReturnOk_WhenAdminAuthenticated()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 30, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = 31, StudentNumber = "S31", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 206, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "A" });
        db.Attendances.Add(new Attendance { Id = 206, SessionId = 206, StudentId = 31, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(30));
        var result = await controller.ChangeAttendanceStatus(206, "S31", new SessionController.ChangeAttendanceStatusModel { Status = (int)AttendanceStatus.Present });

        result.Should().BeOfType<OkObjectResult>();
        db.Attendances.Single(a => a.Id == 206).Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task GetSessionsByYear_ShouldIncludeValidationCode_ForAdminUser()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 50, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true });
        db.Sessions.Add(new Session
        {
            Id = 500,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "VCODE",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(50));
        var result = await controller.GetSessionsByYear("3A");

        result.Value.Should().NotBeNull();
        var first = result.Value!.First();
        first.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAttendance_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetAttendance(999, "S1");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetAttendance_ShouldAllowAccess_WithProfessorTokenHeader()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 61, StudentNumber = "S61", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 610,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "A",
            ProfSignatureToken = "ptok"
        });
        db.Attendances.Add(new Attendance { Id = 610, SessionId = 610, StudentId = 61, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        controller.ControllerContext.HttpContext.Request.Headers["Prof-Signature-Token"] = "ptok";

        var result = await controller.GetAttendance(610, "S61");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SaveSignature_ShouldReturnForbid_WhenUserIsNotOwnerAndNotAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 70, StudentNumber = "S70", Name = "A", Firstname = "A", Email = "a@a.fr", Year = "3A", IsAdmin = false },
            new User { Id = 71, StudentNumber = "S71", Name = "B", Firstname = "B", Email = "b@b.fr", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(70));
        var result = await controller.SaveSignature("S71", new SessionController.SignatureModel { Signature = "sig" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SaveSignature_ShouldReturnNotFound_WhenStudentMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 72, StudentNumber = "S72", Name = "A", Firstname = "A", Email = "a@a.fr", Year = "3A", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(72));
        var result = await controller.SaveSignature("UNKNOWN", new SessionController.SignatureModel { Signature = "sig" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetProfEmail_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.SetProfEmail(999, new SessionController.SetProfEmailModel { ProfEmail = "prof@test.fr" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetProfEmail_ShouldReturnNotFound_WhenProfIdInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session
        {
            Id = 730,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "X",
            ProfId = "invalid"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.SetProfEmail(730, new SessionController.SetProfEmailModel { ProfEmail = "prof@test.fr" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetProf2Email_ShouldReturnNotFound_WhenProfessorMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session
        {
            Id = 740,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "X",
            ProfId2 = "74"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.SetProf2Email(740, new SessionController.SetProfEmailModel { ProfEmail = "p2@test.fr" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSessionAttendances_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetSessionAttendances(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSessionAttendances_ShouldReturnNotFound_WhenNoAttendances()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Sessions.Add(new Session { Id = 760, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetSessionAttendances(760);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ChangeAttendanceStatus_ShouldReturnBadRequest_WhenModelIsNull()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 80, StudentNumber = "ADM80", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = 81, StudentNumber = "S81", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 801, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        db.Attendances.Add(new Attendance { Id = 801, SessionId = 801, StudentId = 81, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(80));
        var result = await controller.ChangeAttendanceStatus(801, "S81", null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAttendanceComment_ShouldReturnBadRequest_WhenModelIsNull()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 90, StudentNumber = "S90", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 900, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        db.Attendances.Add(new Attendance { Id = 900, SessionId = 900, StudentId = 90, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(90));
        var result = await controller.UpdateAttendanceComment(900, "S90", null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CheckAndSendSessionMails_ShouldMarkBothFlags_WhenSessionsAreDue()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.AddRange(
            new Professor { Id = 101, Name = "P1", Firstname = "One", Email = "" },
            new Professor { Id = 102, Name = "P2", Firstname = "Two", Email = "" });
        db.Sessions.Add(new Session
        {
            Id = 1000,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Now.Date,
            StartTime = DateTime.Now.AddMinutes(-30),
            EndTime = DateTime.Now.AddMinutes(30),
            ValidationCode = "X",
            ProfId = "101",
            ProfId2 = "102",
            ProfSignatureToken = "tok-1",
            ProfSignatureToken2 = "tok-2",
            IsMailSent = false,
            IsMailSent2 = false
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        await controller.CheckAndSendSessionMails();

        var updated = await db.Sessions.SingleAsync(s => s.Id == 1000);
        updated.IsMailSent.Should().BeTrue();
        updated.IsMailSent2.Should().BeTrue();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldReturnUnauthorized_WhenClaimInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "bad") }, "test"));
        var controller = BuildController(db, principal);

        var result = await controller.SetSessionProfessor(1, 1, new SessionController.SetSessionProfessorModel { ProfessorId = 1 });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldReturnNotFound_WhenCurrentUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, PrincipalWithId(999));

        var result = await controller.SetSessionProfessor(1, 1, new SessionController.SetSessionProfessorModel { ProfessorId = 1 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 300, StudentNumber = "ADM300", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(300));
        var result = await controller.SetSessionProfessor(999, 1, new SessionController.SetSessionProfessorModel { ProfessorId = 1 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldReturnNotFound_WhenProfessorMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 301, StudentNumber = "DEL301", Name = "D", Firstname = "E", Email = "d@e.fr", Year = "3A", IsDelegate = true });
        db.Sessions.Add(new Session { Id = 3010, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(301));
        var result = await controller.SetSessionProfessor(3010, 1, new SessionController.SetSessionProfessorModel { ProfessorId = 999 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldReturnConflict_WhenProfessorAlreadyOnOtherSlot()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 302, StudentNumber = "DEL302", Name = "D", Firstname = "E", Email = "d@e.fr", Year = "3A", IsDelegate = true });
        db.Professors.Add(new Professor { Id = 3021, Name = "P", Firstname = "One", Email = "p@x.fr" });
        db.Sessions.Add(new Session
        {
            Id = 3020,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "X",
            ProfId2 = "3021"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(302));
        var result = await controller.SetSessionProfessor(3020, 1, new SessionController.SetSessionProfessorModel { ProfessorId = 3021 });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldRemoveProfessor_WhenProfessorIdNull()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 303, StudentNumber = "ADM303", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true });
        db.Sessions.Add(new Session { Id = 3030, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X", ProfId = "44" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithId(303));
        var result = await controller.SetSessionProfessor(3030, 1, new SessionController.SetSessionProfessorModel { ProfessorId = null });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Sessions.FindAsync(3030))!.ProfId.Should().BeNull();
    }

    [Fact]
    public async Task ResendProfMail_ShouldReturnOk_WhenProfessorIdSet()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.Add(new Professor { Id = 401, Name = "P", Firstname = "One", Email = "" });
        db.Sessions.Add(new Session
        {
            Id = 4010,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "X",
            ProfId = "401",
            ProfSignatureToken = "tok-401"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.ResendProfMail(4010);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResendProf2Mail_ShouldReturnOk_WhenProfessor2IdSet()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.Add(new Professor { Id = 402, Name = "P", Firstname = "Two", Email = "" });
        db.Sessions.Add(new Session
        {
            Id = 4020,
            Year = "3A",
            Name = "Cours",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "X",
            ProfId2 = "402",
            ProfSignatureToken2 = "tok-402"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.ResendProf2Mail(4020);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ChangeAttendanceStatus_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ChangeAttendanceStatus(999, "S1", new SessionController.ChangeAttendanceStatusModel { Status = 0 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ChangeAttendanceStatus_ShouldAllow_WithProfTokenInQuery()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 501, StudentNumber = "S501", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 5010, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X", ProfSignatureToken = "qtok" });
        db.Attendances.Add(new Attendance { Id = 5010, SessionId = 5010, StudentId = 501, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?token=qtok");

        var result = await controller.ChangeAttendanceStatus(5010, "S501", new SessionController.ChangeAttendanceStatusModel { Status = (int)AttendanceStatus.Present });

        result.Should().BeOfType<OkObjectResult>();
        db.Attendances.Single(a => a.Id == 5010).Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task ChangeAttendanceStatus_ShouldAllow_WithProfTokenInBody()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 502, StudentNumber = "S502", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 5020, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X", ProfSignatureToken2 = "btok" });
        db.Attendances.Add(new Attendance { Id = 5020, SessionId = 5020, StudentId = 502, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.ChangeAttendanceStatus(5020, "S502", new SessionController.ChangeAttendanceStatusModel { Status = (int)AttendanceStatus.Present, ProfSignatureToken = "btok" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateAttendanceComment_ShouldReturnForbid_WhenUnauthorized()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 601, StudentNumber = "S601", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 6010, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        db.Attendances.Add(new Attendance { Id = 6010, SessionId = 6010, StudentId = 601, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.UpdateAttendanceComment(6010, "S601", new SessionController.CommentUpdateModel { Comment = "x" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateAttendanceComment_ShouldAllow_WithProfTokenInHeader()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 602, StudentNumber = "S602", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });
        db.Sessions.Add(new Session { Id = 6020, Year = "3A", Name = "Cours", Room = "A1", Date = DateTime.Today, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X", ProfSignatureToken = "htok" });
        db.Attendances.Add(new Attendance { Id = 6020, SessionId = 6020, StudentId = 602, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        controller.ControllerContext.HttpContext.Request.Headers["Prof-Signature-Token"] = "htok";

        var result = await controller.UpdateAttendanceComment(6020, "S602", new SessionController.CommentUpdateModel { Comment = "ok" });

        result.Should().BeOfType<OkObjectResult>();
        db.Attendances.Single(a => a.Id == 6020).Comment.Should().Be("ok");
    }
}
