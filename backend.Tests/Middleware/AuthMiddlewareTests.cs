using System.Security.Claims;
using System.Text;
using backend.Data;
using backend.Middleware;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Middleware;

public class AuthMiddlewareTests
{
    private class FakeJwtService : IJwtService
    {
        public ClaimsPrincipal? PrincipalToReturn { get; set; }

        public Task<TokenResponse> GenerateTokensAsync(User user) => throw new NotImplementedException();
        public Task<string> GenerateAccessTokenOnlyAsync(User user) => throw new NotImplementedException();
        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token) => Task.FromResult(PrincipalToReturn);
        public Task<TokenResponse?> RefreshTokenAsync(string refreshToken) => throw new NotImplementedException();
        public Task RevokeTokenAsync(string token) => Task.CompletedTask;
        public Task RevokeAllUserTokensAsync(int userId) => Task.CompletedTask;
        public bool IsTokenRevoked(string token) => false;
        public int? GetUserIdFromRefreshToken(string refreshToken) => null;
    }

    [Fact]
    public async Task InvokeAsync_PublicPath_ShouldPassThrough()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var fakeJwt = new FakeJwtService();
        var rate = new RateLimitService(NullLogger<RateLimitService>.Instance);
        var nextCalled = false;

        var middleware = new AuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<AuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/Status";

        await middleware.InvokeAsync(context, db, fakeJwt, rate);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingToken_ShouldReturn401()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var fakeJwt = new FakeJwtService();
        var rate = new RateLimitService(NullLogger<RateLimitService>.Instance);
        var middleware = new AuthMiddleware(_ => Task.CompletedTask, NullLogger<AuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/User";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, fakeJwt, rate);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_ShouldReturn401()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var fakeJwt = new FakeJwtService { PrincipalToReturn = null };
        var rate = new RateLimitService(NullLogger<RateLimitService>.Instance);
        var middleware = new AuthMiddleware(_ => Task.CompletedTask, NullLogger<AuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/User";
        context.Request.Headers.Authorization = "Bearer invalid-token";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, fakeJwt, rate);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_ValidTokenAndUser_ShouldSetContextAndCallNext()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 5, StudentNumber = "S5", Name = "A", Firstname = "B", Year = "3A", Email = "a@b.c" });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "5")
        }, "Bearer"));

        var fakeJwt = new FakeJwtService { PrincipalToReturn = principal };
        var rate = new RateLimitService(NullLogger<RateLimitService>.Instance);

        var nextCalled = false;
        var middleware = new AuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<AuthMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/User";
        context.Request.Headers.Authorization = "Bearer good-token";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, fakeJwt, rate);

        nextCalled.Should().BeTrue();
        context.Items["UserId"].Should().Be(5);
    }

    [Fact]
    public async Task InvokeAsync_DeletedUser_ShouldReturn401()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 9,
            StudentNumber = "S9",
            Name = "Del",
            Firstname = "User",
            Year = "5A",
            Email = "d@u.c",
            IsDeleted = true
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "9")
        }, "Bearer"));

        var middleware = new AuthMiddleware(_ => Task.CompletedTask, NullLogger<AuthMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/User";
        context.Request.Headers.Authorization = "Bearer good-token";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, new FakeJwtService { PrincipalToReturn = principal }, new RateLimitService(NullLogger<RateLimitService>.Instance));

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_RateLimitExceeded_ShouldReturn429()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var fakeJwt = new FakeJwtService();

        var rateMock = new Moq.Mock<IRateLimitService>();
        rateMock.Setup(r => r.IsApiCallAllowed("1")).Returns(false);

        var middleware = new AuthMiddleware(_ => Task.CompletedTask, NullLogger<AuthMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/User";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }));

        await middleware.InvokeAsync(context, db, fakeJwt, rateMock.Object);

        context.Response.StatusCode.Should().Be(429);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().Contain("Rate limit exceeded");
    }
}
