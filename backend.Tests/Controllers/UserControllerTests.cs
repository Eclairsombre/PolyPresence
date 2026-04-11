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

public class UserControllerTests
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

        var httpContext = new DefaultHttpContext();
        httpContext.User = principal ?? new ClaimsPrincipal(new ClaimsIdentity());
        httpContext.RequestServices = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static ClaimsPrincipal PrincipalWithUserId(int userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

    [Fact]
    public async Task GetUsers_ShouldReturnUnauthorized_WhenClaimMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetUsers();

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetUsers_ShouldReturnNotFound_WhenCurrentUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, principal: PrincipalWithUserId(123));

        var result = await controller.GetUsers();

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetUsers_ShouldReturnForbid_WhenCurrentUserNotAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(1));

        var result = await controller.GetUsers();

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetUsers_ShouldReturnNonDeletedUsers_WhenAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "ADM", Name = "Admin", Firstname = "A", Email = "a@a.a", Year = "ADMIN", IsAdmin = true },
            new User { Id = 2, StudentNumber = "S2", Name = "U", Firstname = "One", Email = "u1@x.z", Year = "3A", IsDeleted = false },
            new User { Id = 3, StudentNumber = "S3", Name = "U", Firstname = "Two", Email = "u2@x.z", Year = "3A", IsDeleted = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(1));

        var result = await controller.GetUsers();

        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUser_ShouldReturnForbid_WhenNonAdminAccessesOtherProfile()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A" },
            new User { Id = 2, StudentNumber = "S2", Name = "C", Firstname = "D", Email = "c@d.e", Year = "3A" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(1));

        var result = await controller.GetUser(2);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetUser_ShouldReturnUser_WhenOwnProfile()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 2, StudentNumber = "S2", Name = "C", Firstname = "D", Email = "c@d.e", Year = "3A" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(2));

        var result = await controller.GetUser(2);

        result.Value.Should().NotBeNull();
        result.Value!.StudentNumber.Should().Be("S2");
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturnUnauthorized_WhenClaimInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "bad") }, "test"));
        var controller = BuildController(db, principal: principal);

        var result = await controller.GetCurrentUser();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SearchUserByNumber_ShouldReturnNotFound_WhenRequestedUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(1));

        var result = await controller.SearchUserByNumber("S404");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void IsUserAdmin_ShouldReturnTrueForActiveAdmin()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.SaveChanges();

        var controller = BuildController(db);

        var result = controller.IsUserAdmin("ADM");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserByYear_ShouldReturnNotFound_WhenNoUsers()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetUserByYear("3A", null);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetUserByYear_ShouldFilterBySpecialization_WhenProvidedAndNotAdminYear()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A", SpecializationId = 1 },
            new User { StudentNumber = "S2", Name = "C", Firstname = "D", Email = "c@d.e", Year = "3A", SpecializationId = 2 });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetUserByYear("3A", 1);

        result.Value.Should().HaveCount(1);
        result.Value!.Single().StudentNumber.Should().Be("S1");
    }

    [Fact]
    public async Task HavePassword_ShouldReturnNotFound_WhenMissingUser()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.HavePassword("UNK");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task HavePassword_ShouldReturnOk_WhenUserExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.c", Year = "3A", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.HavePassword("S1");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenCredentialsMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.Login(new UserController.LoginRequest { StudentNumber = "", Password = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_ShouldReturn429_WhenRateLimitExceeded()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsLoginAttemptAllowed("S1")).Returns(false);

        var controller = BuildController(db, rateLimitMock: rateMock);

        var result = await controller.Login(new UserController.LoginRequest { StudentNumber = "S1", Password = "x" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenPasswordInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            StudentNumber = "S1",
            Name = "A",
            Firstname = "B",
            Email = "a@b.c",
            Year = "3A",
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsLoginAttemptAllowed("S1")).Returns(true);

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.VerifyPassword("bad", "hash")).Returns(false);

        var controller = BuildController(db, passwordMock: pwdMock, rateLimitMock: rateMock);

        var result = await controller.Login(new UserController.LoginRequest { StudentNumber = "S1", Password = "bad" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ShouldReturnOk_AndSetCookie_WhenSuccess()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var user = new User
        {
            Id = 1,
            StudentNumber = "S1",
            Name = "Doe",
            Firstname = "John",
            Email = "john@doe.fr",
            Year = "3A",
            PasswordHash = "hash"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsLoginAttemptAllowed("S1")).Returns(true);

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.VerifyPassword("good", "hash")).Returns(true);

        var jwtMock = new Mock<IJwtService>();
        jwtMock.Setup(j => j.GenerateTokensAsync(It.IsAny<User>())).ReturnsAsync(new TokenResponse
        {
            AccessToken = "acc",
            RefreshToken = "ref",
            ExpiresIn = 900,
            TokenType = "Bearer"
        });

        var cookieMock = new Mock<ICookieEncryptionService>();
        cookieMock.Setup(c => c.Protect(It.IsAny<string>())).Returns("encrypted");

        var controller = BuildController(
            db,
            jwtMock: jwtMock,
            passwordMock: pwdMock,
            rateLimitMock: rateMock,
            cookieMock: cookieMock);

        var result = await controller.Login(new UserController.LoginRequest { StudentNumber = "S1", Password = "good" });

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("user_info=");
        rateMock.Verify(r => r.ResetLoginAttempts("S1"), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnBadRequest_WhenEmptyToken()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnUnauthorized_WhenServiceReturnsNull()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var jwtMock = new Mock<IJwtService>();
        jwtMock.Setup(j => j.RefreshTokenAsync("invalid")).ReturnsAsync((TokenResponse?)null);

        var controller = BuildController(db, jwtMock: jwtMock);

        var result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "invalid" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnOk_WhenFlowIsValid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var user = new User { Id = 1, StudentNumber = "S1", Name = "Doe", Firstname = "John", Email = "john@doe.fr", Year = "3A" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var jwtMock = new Mock<IJwtService>();
        jwtMock.Setup(j => j.RefreshTokenAsync("r1")).ReturnsAsync(new TokenResponse
        {
            RefreshToken = "r2",
            AccessToken = "",
            ExpiresIn = 900,
            TokenType = "Bearer"
        });
        jwtMock.Setup(j => j.GetUserIdFromRefreshToken("r2")).Returns(1);
        jwtMock.Setup(j => j.GenerateAccessTokenOnlyAsync(It.IsAny<User>())).ReturnsAsync("new-access");

        var cookieMock = new Mock<ICookieEncryptionService>();
        cookieMock.Setup(c => c.Protect(It.IsAny<string>())).Returns("encrypted");

        var controller = BuildController(db, jwtMock: jwtMock, cookieMock: cookieMock);

        var result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "r1" });

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("user_info=");
    }

    [Fact]
    public async Task Logout_ShouldReturnOk_AndCallRevokeMethods()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var jwtMock = new Mock<IJwtService>();
        var controller = BuildController(db, jwtMock: jwtMock, principal: PrincipalWithUserId(7));
        controller.Request.Headers.Authorization = "Bearer token-123";

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        jwtMock.Verify(j => j.RevokeTokenAsync("token-123"), Times.Once);
        jwtMock.Verify(j => j.RevokeAllUserTokensAsync(7), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnBadRequest_WhenPayloadInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, principal: PrincipalWithUserId(1));

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "", NewPassword = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnUnauthorized_WhenClaimMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "NewPass@123" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnUnauthorized_WhenCurrentPasswordInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.VerifyPassword("bad", "hash")).Returns(false);

        var controller = BuildController(db, passwordMock: pwdMock, principal: PrincipalWithUserId(1));

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "bad", NewPassword = "NewPass@123" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnBadRequest_WhenNewPasswordWeak()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.VerifyPassword("old", "hash")).Returns(true);
        pwdMock.Setup(p => p.ValidatePasswordStrength("weak")).Returns((false, new List<string> { "weak" }));

        var controller = BuildController(db, passwordMock: pwdMock, principal: PrincipalWithUserId(1));

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "weak" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnOk_WhenSuccessful()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var pwdMock = new Mock<IPasswordService>();
        pwdMock.Setup(p => p.VerifyPassword("old", "hash")).Returns(true);
        pwdMock.Setup(p => p.ValidatePasswordStrength("NewPass@123")).Returns((true, new List<string>()));
        pwdMock.Setup(p => p.HashPassword("NewPass@123")).Returns("new-hash");

        var jwtMock = new Mock<IJwtService>();

        var controller = BuildController(db, jwtMock: jwtMock, passwordMock: pwdMock, principal: PrincipalWithUserId(1));

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "NewPass@123" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Users.FindAsync(1))!.PasswordHash.Should().Be("new-hash");
        jwtMock.Verify(j => j.RevokeAllUserTokensAsync(1), Times.Once);
    }

    [Fact]
    public async Task MakeAdmin_ShouldReturnUnauthorized_WhenHeaderMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.MakeAdmin("S1");

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task MakeAdmin_ShouldReturnNotFound_WhenTargetMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            StudentNumber = "ADM",
            Name = "Admin",
            Firstname = "A",
            Email = "a@a.a",
            Year = "ADMIN",
            IsAdmin = true
        });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var token = adminTokenService.GenerateToken(1);
        var controller = BuildController(db, adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = token;

        var result = await controller.MakeAdmin("UNKNOWN");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task MakeAdmin_ShouldPromoteUser_WhenValid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User
            {
                Id = 1,
                StudentNumber = "ADM",
                Name = "Admin",
                Firstname = "A",
                Email = "a@a.a",
                Year = "ADMIN",
                IsAdmin = true
            },
            new User
            {
                Id = 2,
                StudentNumber = "S2",
                Name = "User",
                Firstname = "U",
                Email = "u@u.u",
                Year = "3A",
                IsAdmin = false,
                IsDelegate = true,
                SpecializationId = 1
            });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var token = adminTokenService.GenerateToken(1);
        var controller = BuildController(db, adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = token;

        var result = await controller.MakeAdmin("S2");

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Users.FirstAsync(u => u.StudentNumber == "S2");
        updated.IsAdmin.Should().BeTrue();
        updated.IsDelegate.Should().BeFalse();
        updated.SpecializationId.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAdminToken_ShouldReturnBadRequest_WhenClaimInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "bad") }, "test"));
        var controller = BuildController(db, principal: principal);

        var result = await controller.GenerateAdminToken();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GenerateAdminToken_ShouldReturnForbid_WhenUserNotAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 3, StudentNumber = "S3", Name = "U", Firstname = "V", Email = "u@v.c", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(3));

        var result = await controller.GenerateAdminToken();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GenerateAdminToken_ShouldReturnOk_WhenAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 4, StudentNumber = "ADM4", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(4));

        var result = await controller.GenerateAdminToken();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnUnauthorized_WhenClaimMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.PostUser(new User { StudentNumber = "S1", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnUnauthorized_WhenAdminTokenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(1));

        var result = await controller.PostUser(new User { StudentNumber = "S2", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", SpecializationId = 1 });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnBadRequest_WhenStudentWithoutSpecialization()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var token = adminTokenService.GenerateToken(1);
        var controller = BuildController(db, principal: PrincipalWithUserId(1), adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = token;

        var result = await controller.PostUser(new User { StudentNumber = "S2", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnCreated_ForAdminYearWithoutSpecialization()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var token = adminTokenService.GenerateToken(1);
        var controller = BuildController(db, principal: PrincipalWithUserId(1), adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = token;

        var result = await controller.PostUser(new User
        {
            StudentNumber = "ADM2",
            Name = "Admin2",
            Firstname = "A2",
            Email = "a2@a2.c",
            Year = "ADMIN"
        });

        result.Should().BeOfType<CreatedAtActionResult>();
        var created = await db.Users.FirstAsync(u => u.StudentNumber == "ADM2");
        created.IsAdmin.Should().BeTrue();
        created.SpecializationId.Should().BeNull();
    }

    [Fact]
    public async Task PostUser_ShouldReturnNotFound_WhenCurrentUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();

        var adminTokenService = new AdminTokenService();
        var controller = BuildController(db, principal: PrincipalWithUserId(999), adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = adminTokenService.GenerateToken(999);

        var result = await controller.PostUser(new User { StudentNumber = "S10", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", SpecializationId = 1 });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnForbid_WhenCurrentUserNotAdmin()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 20, StudentNumber = "S20", Name = "U", Firstname = "T", Email = "u@t.fr", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(20));
        var result = await controller.PostUser(new User { StudentNumber = "S21", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", SpecializationId = 1 });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnBadRequest_WhenSpecializationInactive()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Id = 2, Name = "GC", Code = "GC", IsActive = false });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var controller = BuildController(db, principal: PrincipalWithUserId(1), adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = adminTokenService.GenerateToken(1);

        var result = await controller.PostUser(new User { StudentNumber = "S22", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", SpecializationId = 2 });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReturnConflict_WhenActiveUserAlreadyExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true },
            new User { Id = 2, StudentNumber = "S23", Name = "Old", Firstname = "User", Email = "old@x.z", Year = "3A", IsDeleted = false, SpecializationId = 1 });
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO", IsActive = true });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var controller = BuildController(db, principal: PrincipalWithUserId(1), adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = adminTokenService.GenerateToken(1);

        var result = await controller.PostUser(new User { StudentNumber = "S23", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", SpecializationId = 1 });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task PostUser_ShouldReactivateDeletedUser_AndRecreateAttendances()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var today = DateTime.Now.Date;
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO", IsActive = true });
        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "ADM", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true },
            new User { Id = 30, StudentNumber = "S30", Name = "Deleted", Firstname = "User", Email = "d@u.fr", Year = "3A", IsDeleted = true, SpecializationId = 1 });
        db.Sessions.Add(new Session { Id = 301, Year = "3A", Name = "Cours", Room = "A1", Date = today.AddDays(1), StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1), ValidationCode = "X", SpecializationId = 1 });
        await db.SaveChangesAsync();

        var adminTokenService = new AdminTokenService();
        var controller = BuildController(db, principal: PrincipalWithUserId(1), adminTokenService: adminTokenService);
        controller.Request.Headers["Admin-Token"] = adminTokenService.GenerateToken(1);

        var result = await controller.PostUser(new User { StudentNumber = "S30", Name = "Re", Firstname = "Activated", Email = "re@ac.fr", Year = "3A", IsAdmin = false, IsDelegate = false, SpecializationId = 1 });

        result.Should().BeOfType<CreatedAtActionResult>();
        var reactivated = await db.Users.FirstAsync(u => u.StudentNumber == "S30");
        reactivated.IsDeleted.Should().BeFalse();
        db.Attendances.Should().Contain(a => a.SessionId == 301 && a.StudentId == 30);
    }

    [Fact]
    public async Task GenerateAdminToken_ShouldReturnNotFound_WhenUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, principal: PrincipalWithUserId(333));

        var result = await controller.GenerateAdminToken();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenUserDeleted()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 40,
            StudentNumber = "S40",
            Name = "A",
            Firstname = "B",
            Email = "a@b.c",
            Year = "3A",
            PasswordHash = "hash",
            IsDeleted = true
        });
        await db.SaveChangesAsync();

        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsLoginAttemptAllowed("S40")).Returns(true);
        var controller = BuildController(db, rateLimitMock: rateMock);

        var result = await controller.Login(new UserController.LoginRequest { StudentNumber = "S40", Password = "good" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.RefreshToken(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnUnauthorized_WhenUserNotFoundAfterRefresh()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var jwtMock = new Mock<IJwtService>();
        jwtMock.Setup(j => j.RefreshTokenAsync("r1")).ReturnsAsync(new TokenResponse
        {
            RefreshToken = "r2",
            AccessToken = "",
            ExpiresIn = 900,
            TokenType = "Bearer"
        });
        jwtMock.Setup(j => j.GetUserIdFromRefreshToken("r2")).Returns(404);

        var controller = BuildController(db, jwtMock: jwtMock);
        var result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "r1" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnNotFound_WhenUserHasNoPasswordHash()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 50, StudentNumber = "S50", Name = "N", Firstname = "F", Email = "x@y.z", Year = "3A", PasswordHash = "" });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(50));
        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "NewPass@123" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnBadRequest_WhenStudentNumberMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { StudentNumber = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturn500_WhenSendEmailFails()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 60,
            StudentNumber = "S60",
            Name = "U",
            Firstname = "Fail",
            Email = "u@fail.fr",
            Year = "3A"
        });
        await db.SaveChangesAsync();

        var rateMock = new Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsPasswordResetAllowed("S60")).Returns(true);
        var controller = BuildController(db, rateLimitMock: rateMock);

        var oldFrom = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL");
        try
        {
            Environment.SetEnvironmentVariable("SMTP_FROM_EMAIL", null);
            var result = await controller.ForgotPassword(new ForgotPasswordRequest { StudentNumber = "S60" });
            result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SMTP_FROM_EMAIL", oldFrom);
        }
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenPayloadMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ResetPassword(new PasswordResetRequest { Token = "", NewPassword = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturnNotFound_WhenUserMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db, principal: PrincipalWithUserId(999));

        var result = await controller.GetCurrentUser();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturnOk_WithUserDto()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 25,
            StudentNumber = "S25",
            Name = "Doe",
            Firstname = "Jane",
            Email = "jane@doe.fr",
            Year = "4A",
            IsAdmin = false,
            IsDelegate = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(25));
        var result = await controller.GetCurrentUser();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value!.GetType().GetProperty("StudentId")!.GetValue(ok.Value)!.Should().Be("S25");
        ok.Value.GetType().GetProperty("Lastname")!.GetValue(ok.Value)!.Should().Be("Doe");
        ok.Value.GetType().GetProperty("IsDelegate")!.GetValue(ok.Value)!.Should().Be(true);
    }

    [Fact]
    public async Task SearchUserByNumber_ShouldReturnForbid_WhenNonAdminSearchesAnotherUser()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 31, StudentNumber = "S31", Name = "A", Firstname = "A", Email = "a@a.fr", Year = "3A", IsAdmin = false },
            new User { Id = 32, StudentNumber = "S32", Name = "B", Firstname = "B", Email = "b@b.fr", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(31));
        var result = await controller.SearchUserByNumber("S32");

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SearchUserByNumber_ShouldReturnOk_WhenAdminSearchesAnotherUser()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 40, StudentNumber = "ADM40", Name = "Admin", Firstname = "Root", Email = "adm@x.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = 41, StudentNumber = "S41", Name = "Usr", Firstname = "One", Email = "u1@x.fr", Year = "3A", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db, principal: PrincipalWithUserId(40));
        var result = await controller.SearchUserByNumber("S41");

        result.Result.Should().BeNull();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void IsUserAdmin_ShouldReturnFalse_WhenUserMissingOrNotAdmin()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { StudentNumber = "S50", Name = "U", Firstname = "X", Email = "u@x.fr", Year = "3A", IsAdmin = false, IsDeleted = false });
        db.SaveChanges();

        var controller = BuildController(db);
        var result = controller.IsUserAdmin("S50") as OkObjectResult;

        result.Should().NotBeNull();
        result!.Value.Should().NotBeNull();
        result.Value!.GetType().GetProperty("IsAdmin")!.GetValue(result.Value)!.Should().Be(false);
    }
}
