using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class PasswordServiceTests
{
    private readonly PasswordService _service = new(NullLogger<PasswordService>.Instance);

    [Fact]
    public void ValidatePasswordStrength_ShouldAcceptStrongPassword()
    {
        var result = _service.ValidatePasswordStrength("SecureP@ss123");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePasswordStrength_ShouldRejectTooShort()
    {
        var result = _service.ValidatePasswordStrength("Ab1@");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("8 caractères"));
    }

    [Fact]
    public void ValidatePasswordStrength_ShouldRejectWithoutUppercase()
    {
        var result = _service.ValidatePasswordStrength("securep@ss123");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("majuscule"));
    }

    [Fact]
    public void ValidatePasswordStrength_ShouldRejectWithoutSpecialChar()
    {
        var result = _service.ValidatePasswordStrength("SecurePass123");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("caractère spécial"));
    }

    [Fact]
    public void ValidatePasswordStrength_ShouldRejectEmptyPassword()
    {
        var result = _service.ValidatePasswordStrength(string.Empty);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("requis"));
    }

    [Fact]
    public void HashPassword_Twice_ShouldCreateDifferentHashes()
    {
        var password = "SecureP@ss123";

        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrueForValidPassword()
    {
        var password = "SecureP@ss123";
        var hash = _service.HashPassword(password);

        var isValid = _service.VerifyPassword(password, hash);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalseForCorruptedHash()
    {
        var isValid = _service.VerifyPassword("SecureP@ss123", "not-a-valid-hash");

        isValid.Should().BeFalse();
    }
}
