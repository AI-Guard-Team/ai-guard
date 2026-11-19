using AIGuard.Infrastructure.Security;

namespace AIGuard.UnitTests;

public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void Hash_does_not_contain_the_password_and_verifies()
    {
        const string password = "Dogru-Sifre-123";

        var hash = _hasher.Hash(password);

        Assert.DoesNotContain(password, hash);
        Assert.True(_hasher.Verify(hash, password));
    }

    [Fact]
    public void Same_password_produces_different_hashes()
    {
        // Tuz sayesinde aynı şifre farklı özetler üretmeli.
        Assert.NotEqual(_hasher.Hash("Ayni-Sifre-123"), _hasher.Hash("Ayni-Sifre-123"));
    }

    [Theory]
    [InlineData("Yanlis-Sifre-123")]
    [InlineData("")]
    public void Wrong_or_empty_password_is_rejected(string attempt)
    {
        var hash = _hasher.Hash("Dogru-Sifre-123");

        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bozuk-ozet")]
    public void Missing_or_garbage_hash_is_rejected_without_throwing(string hash)
    {
        // Davet bekleyen (özeti olmayan) kullanıcı veya bozuk kayıt girişte istisna fırlatmamalı.
        Assert.False(_hasher.Verify(hash, "Dogru-Sifre-123"));
    }

    [Fact]
    public void Hashing_empty_password_is_refused()
    {
        Assert.ThrowsAny<ArgumentException>(() => _hasher.Hash(""));
    }
}
