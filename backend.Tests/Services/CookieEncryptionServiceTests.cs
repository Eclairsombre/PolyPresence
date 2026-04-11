using backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace backend.Tests.Services;

public class CookieEncryptionServiceTests
{
    [Fact]
    public void ProtectAndUnprotect_ShouldRoundTripValue()
    {
        var provider = DataProtectionProvider.Create("backend-tests");
        var service = new CookieEncryptionService(provider);

        var encrypted = service.Protect("user:42");
        var plain = service.Unprotect(encrypted);

        encrypted.Should().NotBe("user:42");
        plain.Should().Be("user:42");
    }

    [Fact]
    public void Unprotect_InvalidValue_ShouldReturnEmptyString()
    {
        var provider = DataProtectionProvider.Create("backend-tests");
        var service = new CookieEncryptionService(provider);

        service.Unprotect("invalid").Should().BeEmpty();
    }
}
