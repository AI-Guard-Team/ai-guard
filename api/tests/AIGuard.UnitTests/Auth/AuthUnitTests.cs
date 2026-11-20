using AIGuard.Application.Auth;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AIGuard.UnitTests.Auth;

/// <summary>E2'nin veritabanı gerektirmeyen parçaları: şifre kuralı, belirteç üretimi, JWT.</summary>
public class AuthUnitTests
{
    private const string Secret = "unit-tests-only-jwt-secret-0123456789abcdef";

    private static JwtSettings Settings(string? secret = Secret) => JwtSettings.FromConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_SECRET"] = secret }).Build());

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1234567", false)]   // 7 karakter
    [InlineData("        ", false)]  // yalnızca boşluk
    [InlineData("12345678", true)]
    [InlineData("çok güzel şifre", true)] // Türkçe karakter ve boşluk serbest
    public void Password_rule_requires_8_to_128_characters(string? password, bool valid)
    {
        Assert.Equal(valid, AuthPolicy.ValidatePassword(password) is null);
        Assert.NotNull(AuthPolicy.ValidatePassword(new string('a', AuthPolicy.MaxPasswordLength + 1)));
    }

    [Fact]
    public void Secure_tokens_are_random_url_safe_and_hashed()
    {
        var a = SecureToken.Generate();
        var b = SecureToken.Generate();

        Assert.NotEqual(a, b);
        Assert.True(SecureToken.IsWellFormed(a));
        Assert.False(SecureToken.IsWellFormed(a + "x"));
        Assert.False(SecureToken.IsWellFormed(null));
        Assert.Equal(64, SecureToken.Hash(a).Length); // SHA-256, hex
        Assert.NotEqual(a, SecureToken.Hash(a));      // veritabanına belirtecin kendisi yazılmıyor
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kisa-anahtar")] // 32 bayttan kısa
    public void Missing_or_short_jwt_secret_fails_fast(string? secret)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Settings(secret));
        Assert.Contains("JWT_SECRET", ex.Message);
    }

    [Fact]
    public async Task Access_token_is_valid_for_15_minutes_and_carries_user_claims()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var settings = Settings();
        var user = new User
        {
            FullName = "Şeyma Örnek", Email = "a@test.local", NormalizedEmail = "a@test.local", Role = UserRole.Reviewer,
        };

        var token = new JwtTokenService(settings, clock).CreateAccessToken(user);
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token.Token, settings.ValidationParameters());
        var jwt = (JsonWebToken)result.SecurityToken;

        Assert.True(result.IsValid);
        Assert.Equal(clock.GetUtcNow() + TimeSpan.FromMinutes(15), token.ExpiresAt);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("Reviewer", jwt.GetClaim("role").Value);
        Assert.Equal("Şeyma Örnek", jwt.GetClaim("name").Value);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var user = new User { FullName = "X", Email = "x@test.local", NormalizedEmail = "x@test.local", Role = UserRole.Admin };
        var foreign = new JwtTokenService(Settings("another-secret-that-is-long-enough-0123456789"), TimeProvider.System)
            .CreateAccessToken(user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(foreign.Token, Settings().ValidationParameters());

        Assert.False(result.IsValid);
    }
}
