using AIGuard.Application.Agents;

namespace AIGuard.UnitTests.Agents;

public class PairingCodeTests
{
    [Fact]
    public void Generated_code_is_well_formed_and_43_url_safe_characters()
    {
        var code = PairingCode.Generate();

        Assert.Equal(PairingCode.Length, code.Length);
        Assert.True(PairingCode.IsWellFormed(code));
        Assert.DoesNotContain('=', code);
        Assert.DoesNotContain('+', code);
        Assert.DoesNotContain('/', code);
    }

    [Fact]
    public void Generated_codes_do_not_repeat()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => PairingCode.Generate()).ToHashSet();

        Assert.Equal(1000, codes.Count);
    }

    [Fact]
    public void Hash_is_deterministic_differs_from_code_and_fits_the_column()
    {
        var code = PairingCode.Generate();

        var hash = PairingCode.Hash(code);

        Assert.Equal(hash, PairingCode.Hash(code));
        Assert.NotEqual(code, hash);
        Assert.Equal(64, hash.Length); // agent_pairings.pairing_code_hash en fazla 128
        Assert.NotEqual(hash, PairingCode.Hash(PairingCode.Generate()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kisa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 44 karakter
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=")]  // dolgu karakteri
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]  // Base64Url dışı
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaı")]  // ASCII dışı harf
    public void Malformed_codes_are_rejected(string? code)
    {
        Assert.False(PairingCode.IsWellFormed(code));
    }
}
