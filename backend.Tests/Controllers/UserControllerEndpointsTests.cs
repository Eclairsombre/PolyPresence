using System.Security.Claims;
using backend.Controllers;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace backend.Tests.Controllers;

public class UserControllerEndpointsTests
{
    private static UserController BuildController(
        backend.Data.ApplicationDbContext db,
        Mock<IJwtService>? jwtMock = null,
        Mock<IPasswordService>? passwordMock = null,
        Mock<IRateLimitService>? rateLimitMock = null,
        Mock<ICookieEncryptionService>? cookieMock = null,
        ClaimsPrincipal? principal = null,
        AdminTokenService? adminTokenService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(adminTokenService ?? new AdminTokenService());

        var controller = new UserController(
            db,
            NullLogger<UserController>.Instance,
            (jwtMock ?? new Mock<IJwtService>()).Object,
            (passwordMock ?? new Mock<IPasswordService>()).Object,
            (rateLimitMock ?? new Mock<IRateLimitService>()).Object,
            (cookieMock ?? new Mock<ICookieEncryptionService>()).Object);

        var httpContext = new DefaultHttpContext
        {
            User = principal ?? new ClaimsPrincipal(new ClaimsIdentity()),
            RequestServices = services.BuildServiceProvider()
        };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static ClaimsPrincipal PrincipalWithId(int id) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));

    [Fact]
    public async Task PutUser_ShouldReturnBadRequest_WhenStudentNumberMismatch()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, principal: PrincipalWithId(1));

        var result = await controller.PutUser("S1", new User { StudentNumber = "S2", Name = "N", Firstname = "F", Email = "n@f.fr", Year = "3A" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PutUser_ShouldReturnNoContent_WhenOwnProfile()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "Old", Firstname = "Name", Email = "old@test.fr", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithId(1));

        var result = await controller.PutUser("S1", new User
        {
            StudentNumber = "S1",
            Name = "New",
            Firstname = "Name",
            Email = "new@test.fr",
            Year = "4A",
            IsAdmin = false,
            IsDelegate = false
        });

        result.Should().BeOfType<NoContentResult>();
        var updated = db.Users.Single(u => u.StudentNumber == "S1");
        updated.Name.Should().Be("New");
        updated.Year.Should().Be("4A");
    }

    [Fact]
    public async Task DeleteUser_ShouldReturnUnauthorized_WhenClaimMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.DeleteUser("S1");

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task DeleteUser_ShouldReturnNoContent_WhenAdminWithValidToken()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var admin = new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true };
        var target = new User { Id = 2, StudentNumber = "S2", Name = "U", Firstname = "T", Email = "u@t.fr", Year = "3A", IsAdmin = false, IsDeleted = false };
        db.Users.AddRange(admin, target);
        db.Sessions.Add(new Session { Id = 10, Year = "3A", Name = "Future", Room = "A1", Date = DateTime.Today.AddDays(1), StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X" });
        db.Attendances.Add(new Attendance { Id = 1, SessionId = 10, StudentId = 2, Status = AttendanceStatus.Absent });
        await db.SaveChangesAsync();

        var tokenService = new AdminTokenService();
        var controller = BuildController(db, principal: PrincipalWithId(1), adminTokenService: tokenService);
        controller.Request.Headers["Admin-Token"] = tokenService.GenerateToken(1);

        var result = await controller.DeleteUser("S2");

        result.Should().BeOfType<NoContentResult>();
        db.Users.Single(u => u.StudentNumber == "S2").IsDeleted.Should().BeTrue();
        db.Attendances.Should().BeEmpty();
    }

    [Fact]
    public async Task SendRegisterLink_ShouldReturnNotFound_WhenStudentMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.SendRegisterLink(new UserController.RegisterLinkRequest { StudentNumber = "UNKNOWN" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SendRegisterLink_ShouldReturnBadRequest_WhenPasswordAlreadyExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            StudentNumber = "S1",
            Name = "U",
            Firstname = "One",
            Email = "u@one.fr",
            Year = "3A",
            PasswordHash = "hashed"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.SendRegisterLink(new UserController.RegisterLinkRequest { StudentNumber = "S1" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetPassword_ShouldReturnOk_WhenTokenValidAndPasswordStrongEnough()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            StudentNumber = "S1",
            Name = "U",
            Firstname = "One",
            Email = "u@one.fr",
            Year = "3A",
            RegisterToken = "valid-token",
            RegisterTokenExpiration = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.SetPassword(new UserController.SetPasswordRequest { Token = "valid-token", Password = "abcdef" });

        result.Should().BeOfType<OkObjectResult>();
        db.Users.Single(u => u.StudentNumber == "S1").PasswordHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturn429_WhenRateLimitExceeded()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsPasswordResetAllowed("S1")).Returns(false);
        var controller = BuildController(db, rateLimitMock: rateMock);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { StudentNumber = "S1" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnOk_WhenUserMissing_ToPreventEnumeration()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsPasswordResetAllowed("S404")).Returns(true);
        var controller = BuildController(db, rateLimitMock: rateMock);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { StudentNumber = "S404" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var pwdMock = new Mock<IPasswordService>();
        var controller = BuildController(db, passwordMock: pwdMock);

        var result = await controller.ResetPassword(new PasswordResetRequest { Token = "invalid-token", NewPassword = "NewPass@123" });

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnOk_WhenValidData()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            StudentNumber = "S1",
            Name = "U",
            Firstname = "One",
            Email = "u@one.fr",
            Year = "3A",
            RegisterToken = "reset-token",
            RegisterTokenExpiration = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.ValidatePasswordStrength("NewPass@123")).Returns((true, new List<string>()));
        pwdMock.Setup(p => p.HashPassword("NewPass@123")).Returns("new-hash");
        var jwtMock = new Mock<IJwtService>();

        var controller = BuildController(db, passwordMock: pwdMock, jwtMock: jwtMock);

        var result = await controller.ResetPassword(new PasswordResetRequest { Token = "reset-token", NewPassword = "NewPass@123" });

        result.Should().BeOfType<OkObjectResult>();
        db.Users.Single(u => u.StudentNumber == "S1").PasswordHash.Should().Be("new-hash");
        jwtMock.Verify(j => j.RevokeAllUserTokensAsync(1), Times.Once);
    }

    [Fact]
    public async Task PutUser_ShouldReturnForbid_WhenNonAdminTriesToChangeRights()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 8, StudentNumber = "S8", Name = "User", Firstname = "Eight", Email = "u8@test.fr", Year = "3A", IsAdmin = false, IsDelegate = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithId(8));
        var result = await controller.PutUser("S8", new User
        {
            StudentNumber = "S8",
            Name = "User",
            Firstname = "Eight",
            Email = "u8@test.fr",
            Year = "3A",
            IsAdmin = true,
            IsDelegate = true
        });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteUser_ShouldReturnNotFound_WhenTargetMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var tokenService = new AdminTokenService();
        var controller = BuildController(db, principal: PrincipalWithId(1), adminTokenService: tokenService);
        controller.Request.Headers["Admin-Token"] = tokenService.GenerateToken(1);

        var result = await controller.DeleteUser("UNKNOWN");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SendRegisterLink_ShouldReturnBadRequest_WhenEmailMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 12,
            StudentNumber = "S12",
            Name = "NoMail",
            Firstname = "User",
            Email = "",
            Year = "3A"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.SendRegisterLink(new UserController.RegisterLinkRequest { StudentNumber = "S12" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetPassword_ShouldReturnBadRequest_WhenPasswordTooShort()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 13,
            StudentNumber = "S13",
            Name = "Short",
            Firstname = "Pwd",
            Email = "s13@test.fr",
            Year = "3A",
            RegisterToken = "tok-short",
            RegisterTokenExpiration = DateTime.UtcNow.AddMinutes(20)
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.SetPassword(new UserController.SetPasswordRequest { Token = "tok-short", Password = "123" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnOk_WhenExistingValidTokenAlreadySent()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 14,
            StudentNumber = "S14",
            Name = "Already",
            Firstname = "Sent",
            Email = "s14@test.fr",
            Year = "3A",
            RegisterToken = "tok-existing",
            RegisterTokenExpiration = DateTime.UtcNow.AddMinutes(40),
            RegisterMailSent = true
        });
        await db.SaveChangesAsync();

        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsPasswordResetAllowed("S14")).Returns(true);
        var controller = BuildController(db, rateLimitMock: rateMock);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { StudentNumber = "S14" });
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenPasswordStrengthInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 15,
            StudentNumber = "S15",
            Name = "Weak",
            Firstname = "Pwd",
            Email = "s15@test.fr",
            Year = "3A",
            RegisterToken = "tok-weak",
            RegisterTokenExpiration = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.ValidatePasswordStrength("weak")).Returns((false, new List<string> { "weak" }));

        var controller = BuildController(db, passwordMock: pwdMock);
        var result = await controller.ResetPassword(new PasswordResetRequest { Token = "tok-weak", NewPassword = "weak" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
