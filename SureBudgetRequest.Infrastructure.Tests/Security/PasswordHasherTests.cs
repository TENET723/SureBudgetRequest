using FluentAssertions;
using SureBudgetRequest.Infrastructure.Security;
using Xunit;

namespace SureBudgetRequest.Infrastructure.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_ShouldThrowArgumentException_WhenPasswordIsNullOrEmpty(string? password)
    {
        var act = () => _hasher.Hash(password!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Password is required*");
    }

    [Fact]
    public void Hash_ShouldReturnFormattedString_WithFourParts()
    {
        var hash = _hasher.Hash("Secret123!");

        hash.Should().NotBeNullOrWhiteSpace();
        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("v1");
        parts[1].Should().Be("210000");

        var saltBytes = Convert.FromBase64String(parts[2]);
        saltBytes.Should().HaveCount(16);

        var hashBytes = Convert.FromBase64String(parts[3]);
        hashBytes.Should().HaveCount(32);
    }

    [Fact]
    public void Hash_ShouldGenerateUniqueHashes_ForSamePasswordDueToSalting()
    {
        var hash1 = _hasher.Hash("SamePassword123!");
        var hash2 = _hasher.Hash("SamePassword123!");

        hash1.Should().NotBe(hash2);
    }

    [Theory]
    [InlineData("P@ssw0rd123")]
    [InlineData("Short1!")]
    [InlineData("A very long password with multiple words and spaces and symbols #$!%^&*()")]
    [InlineData("မြန်မာစကားဝှက်၁၂၃!")] // Unicode / Myanmar script
    public void Verify_ShouldReturnTrue_WhenPasswordMatchesHash(string password)
    {
        var hash = _hasher.Hash(password);

        var result = _hasher.Verify(hash, password);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        var hash = _hasher.Hash("CorrectPassword");

        var result = _hasher.Verify(hash, "WrongPassword");

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ShouldBeCaseSensitive()
    {
        var hash = _hasher.Hash("Password123");

        var result = _hasher.Verify(hash, "password123");

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "Password123")]
    [InlineData("", "Password123")]
    [InlineData("v1$210000$salt$hash", null)]
    [InlineData("v1$210000$salt$hash", "")]
    public void Verify_ShouldReturnFalse_WhenStoredHashOrPasswordIsNullOrEmpty(string? storedHash, string? password)
    {
        var result = _hasher.Verify(storedHash!, password!);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("invalid-hash")]
    [InlineData("v1$210000$salt")]                         // 3 parts instead of 4
    [InlineData("v2$210000$c2FsdA==$aGFzaA==")]             // unknown version v2
    [InlineData("v1$notanumber$c2FsdA==$aGFzaA==")]         // invalid iterations
    [InlineData("v1$0$c2FsdA==$aGFzaA==")]                  // iterations < 1
    [InlineData("v1$-100$c2FsdA==$aGFzaA==")]               // iterations negative
    [InlineData("v1$210000$notbase64!$aGFzaA==")]           // invalid base64 salt
    [InlineData("v1$210000$c2FsdA==$notbase64!")]           // invalid base64 hash
    public void Verify_ShouldReturnFalse_WhenStoredHashFormatIsMalformed(string malformedHash)
    {
        var result = _hasher.Verify(malformedHash, "Password123");

        result.Should().BeFalse();
    }
}
