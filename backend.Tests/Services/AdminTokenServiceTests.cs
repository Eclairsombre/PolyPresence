using backend.Services;
using FluentAssertions;

namespace backend.Tests.Services;

public class AdminTokenServiceTests
{
    [Fact]
    public void GenerateAndValidateToken_ShouldReturnUserId()
    {
        var service = new AdminTokenService();

        var token = service.GenerateToken(42);
        var userId = service.ValidateToken(token);

        userId.Should().Be(42);
    }

    [Fact]
    public void ValidateToken_ShouldReturnNullForUnknownToken()
    {
        var service = new AdminTokenService();

        service.ValidateToken("unknown").Should().BeNull();
    }

    [Fact]
    public void ValidateToken_ShouldReturnNullForExpiredToken()
    {
        var service = new AdminTokenService();

        var token = service.GenerateToken(12, -1);

        service.ValidateToken(token).Should().BeNull();
    }

    [Fact]
    public void RevokeToken_ShouldRemoveToken()
    {
        var service = new AdminTokenService();
        var token = service.GenerateToken(33);

        var revoked = service.RevokeToken(token);

        revoked.Should().BeTrue();
        service.ValidateToken(token).Should().BeNull();
    }

    [Fact]
    public void RevokeToken_ShouldReturnFalseForUnknownToken()
    {
        var service = new AdminTokenService();

        service.RevokeToken("missing").Should().BeFalse();
    }

    [Fact]
    public void RevokeAllUserTokens_ShouldRemoveOnlyMatchingUserTokens()
    {
        var service = new AdminTokenService();
        var tokenA = service.GenerateToken(1);
        var tokenB = service.GenerateToken(1);
        var tokenC = service.GenerateToken(2);

        service.RevokeAllUserTokens(1);

        service.ValidateToken(tokenA).Should().BeNull();
        service.ValidateToken(tokenB).Should().BeNull();
        service.ValidateToken(tokenC).Should().Be(2);
    }
}
