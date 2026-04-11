using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using backend.Models;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class JwtServiceTests
{
    private static JwtService CreateService(string accessExpiryMinutes = "15", string refreshExpiryDays = "7")
    {
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "THIS_IS_A_VERY_LONG_TEST_SECRET_KEY_1234567890");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "PolytechPresence",
                ["Jwt:Audience"] = "PolytechPresenceAPI",
                ["Jwt:AccessTokenExpiryMinutes"] = accessExpiryMinutes,
                ["Jwt:RefreshTokenExpiryDays"] = refreshExpiryDays
            })
            .Build();

        return new JwtService(config, NullLogger<JwtService>.Instance);
    }

    private static User CreateUser() => new()
    {
        Id = 10,
        StudentNumber = "S1000",
        Name = "Test",
        Firstname = "User",
        Year = "4A",
        IsAdmin = false,
        IsDelegate = false
    };

    [Fact]
    public async Task GenerateTokensAsync_ShouldReturnAccessAndRefresh()
    {
        var service = CreateService();

        var token = await service.GenerateTokensAsync(CreateUser());

        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.RefreshToken.Should().NotBeNullOrWhiteSpace();
        token.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task GenerateAccessTokenOnlyAsync_ShouldReturnJwt()
    {
        var service = CreateService();

        var token = await service.GenerateAccessTokenOnlyAsync(CreateUser());
        var handler = new JwtSecurityTokenHandler();

        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnPrincipalForValidToken()
    {
        var service = CreateService();
        var access = await service.GenerateAccessTokenOnlyAsync(CreateUser());

        var principal = await service.ValidateTokenAsync(access);

        principal.Should().NotBeNull();
        principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("10");
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnNullForMalformedToken()
    {
        var service = CreateService();
        var principal = await service.ValidateTokenAsync("not-a-jwt-token");

        principal.Should().BeNull();
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldInvalidateToken()
    {
        var service = CreateService();
        var access = await service.GenerateAccessTokenOnlyAsync(CreateUser());

        await service.RevokeTokenAsync(access);
        var principal = await service.ValidateTokenAsync(access);

        principal.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNewRefreshToken()
    {
        var service = CreateService();
        var generated = await service.GenerateTokensAsync(CreateUser());

        var refreshed = await service.RefreshTokenAsync(generated.RefreshToken);

        refreshed.Should().NotBeNull();
        refreshed!.RefreshToken.Should().NotBe(generated.RefreshToken);
        refreshed.AccessToken.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNullForInvalidToken()
    {
        var service = CreateService();

        var refreshed = await service.RefreshTokenAsync("invalid-refresh-token");

        refreshed.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_ShouldInvalidateRefreshTokensForUser()
    {
        var service = CreateService();
        var user = CreateUser();
        var t1 = await service.GenerateTokensAsync(user);
        var t2 = await service.GenerateTokensAsync(user);

        await service.RevokeAllUserTokensAsync(user.Id);

        service.GetUserIdFromRefreshToken(t1.RefreshToken).Should().BeNull();
        service.GetUserIdFromRefreshToken(t2.RefreshToken).Should().BeNull();
    }
}
