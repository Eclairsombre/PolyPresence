using System.Security.Claims;
using System.Text.Json;
using backend.Controllers;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class SessionControllerTests
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

    private static ClaimsPrincipal PrincipalWithIdAndRole(int id, string role = "User", string isDelegate = "false")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim("role", role),
            new Claim("isDelegate", isDelegate),
            new Claim("studentNumber", "S" + id)
        }, "test"));
    }

    private static string Serialize(object obj) => JsonSerializer.Serialize(obj);

    [Fact]
    public async Task GetSessions_ShouldHideValidationCode_ForAnonymousUser()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 10,
            Name = "Algo",
            Year = "3A",
            Room = "B12",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "ABCD",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetSessions();

        result.Value.Should().NotBeNull();
        var payload = Serialize(result.Value!.First());
        payload.Should().NotContain("ValidationCode");
    }

    [Fact]
    public async Task GetSessions_ShouldIncludeValidationCode_ForAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "Admin", Firstname = "A", Email = "a@a.a", Year = "ADMIN", IsAdmin = true });
        db.Sessions.Add(new Session
        {
            Id = 11,
            Name = "Math",
            Year = "3A",
            Room = "C14",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "ZZ99",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithIdAndRole(1));

        var result = await controller.GetSessions();

        result.Value.Should().NotBeNull();
        var payload = Serialize(result.Value!.First());
        payload.Should().Contain("ValidationCode");
    }

    [Fact]
    public async Task GetSession_ShouldReturnNotFound_WhenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetSession(404);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSession_ShouldHideValidationCode_ForNonAdminNonDelegate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 12,
            Name = "Reseau",
            Year = "3A",
            Room = "D20",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "HIDE",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithIdAndRole(2, role: "User", isDelegate: "false"));

        var result = await controller.GetSession(12);

        result.Value.Should().NotBeNull();
        Serialize(result.Value!).Should().NotContain("ValidationCode");
    }

    [Fact]
    public async Task GetSession_ShouldIncludeValidationCode_ForDelegate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 13,
            Name = "BDD",
            Year = "3A",
            Room = "A01",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "SHOW",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, PrincipalWithIdAndRole(3, role: "User", isDelegate: "true"));

        var result = await controller.GetSession(13);

        result.Value.Should().NotBeNull();
        Serialize(result.Value!).Should().Contain("ValidationCode");
    }

    [Fact]
    public async Task GetSessionsByYear_ShouldReturnNotFound_WhenEmpty()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetSessionsByYear("5A");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCurrentSession_ShouldReturnNotFound_WhenNoCurrentSession()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetCurrentSession("3A");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCurrentSession_ShouldHideValidationCode_ForAnonymous()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 14,
            Name = "POO",
            Year = "3A",
            Room = "E11",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddMinutes(-10),
            EndTime = DateTime.Now.AddMinutes(50),
            ValidationCode = "CURR",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetCurrentSession("3A");

        result.Value.Should().NotBeNull();
        Serialize(result.Value!).Should().NotContain("ValidationCode");
    }

    [Fact]
    public async Task AddStudentsToSession_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.AddStudentsToSession(777, "S1");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddStudentsToSession_ShouldReturnNotFound_WhenUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 20,
            Name = "Algo",
            Year = "3A",
            Room = "B12",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(2),
            ValidationCode = "1234",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.AddStudentsToSession(20, "UNKNOWN");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddStudentsToSession_ShouldReturnConflict_WhenAlreadyExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 21,
            Name = "Algo",
            Year = "3A",
            Room = "B12",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(2),
            ValidationCode = "1234",
            SpecializationId = 1
        });
        db.Attendances.Add(new Attendance { SessionId = 21, StudentId = 1, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.AddStudentsToSession(21, "S1");

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AddStudentsToSession_ShouldReturnOk_WhenCreated()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 2, StudentNumber = "S2", Name = "C", Firstname = "D", Email = "c@d.e", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 22,
            Name = "Algo",
            Year = "3A",
            Room = "B12",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(2),
            ValidationCode = "1234",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.AddStudentsToSession(22, "S2");

        result.Should().BeOfType<OkObjectResult>();
        db.Attendances.Should().ContainSingle(a => a.SessionId == 22 && a.StudentId == 2);
    }

    [Fact]
    public async Task ValidateSession_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, PrincipalWithIdAndRole(1, role: "User", isDelegate: "false"));

        var result = await controller.ValidateSession(404, "S1", new SessionController.ValidateSessionModel { ValidationCode = "1234" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateSession_ShouldReturnBadRequest_WhenCodeMismatch()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 30,
            Name = "Cours",
            Year = "3A",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddMinutes(-30),
            EndTime = DateTime.Now.AddMinutes(30),
            ValidationCode = "RIGHT",
            SpecializationId = 1
        });
        db.Attendances.Add(new Attendance { SessionId = 30, StudentId = 1, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("studentNumber", "S1"),
            new Claim("role", "User"),
            new Claim("isDelegate", "false")
        }, "test"));
        var controller = BuildController(db, principal);

        var result = await controller.ValidateSession(30, "S1", new SessionController.ValidateSessionModel { ValidationCode = "WRONG" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ValidateSession_ShouldReturnConflict_WhenAlreadyPresent()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 31,
            Name = "Cours",
            Year = "3A",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddMinutes(-30),
            EndTime = DateTime.Now.AddMinutes(30),
            ValidationCode = "OK",
            SpecializationId = 1
        });
        db.Attendances.Add(new Attendance { SessionId = 31, StudentId = 1, Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("studentNumber", "S1"),
            new Claim("role", "User"),
            new Claim("isDelegate", "false")
        }, "test"));
        var controller = BuildController(db, principal);

        var result = await controller.ValidateSession(31, "S1", new SessionController.ValidateSessionModel { ValidationCode = "OK" });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ValidateSession_ShouldReturnOk_WhenValid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A" });
        db.Sessions.Add(new Session
        {
            Id = 32,
            Name = "Cours",
            Year = "3A",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now.AddMinutes(-30),
            EndTime = DateTime.Now.AddMinutes(30),
            ValidationCode = "OK",
            SpecializationId = 1
        });
        db.Attendances.Add(new Attendance { SessionId = 32, StudentId = 1, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("studentNumber", "S1"),
            new Claim("role", "User"),
            new Claim("isDelegate", "false")
        }, "test"));
        var controller = BuildController(db, principal);

        var result = await controller.ValidateSession(32, "S1", new SessionController.ValidateSessionModel { ValidationCode = "OK" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Attendances.FindAsync(1))!.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task SetSessionProfessor_ShouldReturnBadRequest_WhenSlotInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, PrincipalWithIdAndRole(1));

        var result = await controller.SetSessionProfessor(1, 3, new SessionController.SetSessionProfessorModel { ProfessorId = null });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResendProfMail_ShouldReturnNotFound_WhenMissingData()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 40,
            Name = "Cours",
            Year = "3A",
            Room = "A1",
            Date = DateTime.Today,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            ValidationCode = "1111",
            SpecializationId = 1,
            ProfId = null
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ResendProfMail(40);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetAutoImportStatus_ShouldReturnCurrentFlag()
    {
        TimerService.EnableAutoImport(NullLogger.Instance);
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = controller.GetAutoImportStatus();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void SetAutoImportStatus_ShouldToggleFlag()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var disable = controller.SetAutoImportStatus(new SessionController.AutoImportStatusModel { Enabled = false });
        disable.Should().BeOfType<OkObjectResult>();
        TimerService.IsAutoImportEnabled.Should().BeFalse();

        var enable = controller.SetAutoImportStatus(new SessionController.AutoImportStatusModel { Enabled = true });
        enable.Should().BeOfType<OkObjectResult>();
        TimerService.IsAutoImportEnabled.Should().BeTrue();
    }

    [Fact]
    public void GetTimers_ShouldReturnOkPayload()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = controller.GetTimers();

        result.Should().BeOfType<OkObjectResult>();
    }
}
