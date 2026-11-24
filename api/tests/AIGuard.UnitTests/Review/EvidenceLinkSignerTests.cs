using System.Web;
using AIGuard.Application.Review;
using Microsoft.Extensions.Time.Testing;

namespace AIGuard.UnitTests.Review;

/// <summary>A4: kanıt karesinin kısa ömürlü imzalı bağlantısı (K-31).</summary>
public class EvidenceLinkSignerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero));

    private static (Guid Id, long Exp, string Sig) Parse(string url)
    {
        var parts = url.Split('?');
        var query = HttpUtility.ParseQueryString(parts[1]);
        return (Guid.Parse(parts[0].Split('/')[^1]), long.Parse(query["exp"]!), query["sig"]!);
    }

    [Fact]
    public void Link_is_valid_for_its_frame_until_it_expires()
    {
        var signer = new EvidenceLinkSigner(_clock);
        var frameId = Guid.NewGuid();
        var (id, exp, sig) = Parse(signer.CreateUrl(frameId));

        Assert.Equal(frameId, id);
        Assert.Matches("^[A-Za-z0-9_-]+$", sig); // sorgu dizesinde kaçış gerekmiyor
        Assert.True(signer.IsValid(frameId, exp, sig));

        _clock.Advance(EvidenceLinkSigner.Lifetime);
        Assert.True(signer.IsValid(frameId, exp, sig));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(signer.IsValid(frameId, exp, sig));
    }

    [Fact]
    public void Signature_cannot_be_moved_to_another_frame_or_a_later_expiry()
    {
        var signer = new EvidenceLinkSigner(_clock);
        var frameId = Guid.NewGuid();
        var (_, exp, sig) = Parse(signer.CreateUrl(frameId));

        Assert.False(signer.IsValid(Guid.NewGuid(), exp, sig));
        Assert.False(signer.IsValid(frameId, exp - 1, sig));
    }

    [Fact]
    public void Expiry_beyond_the_lifetime_is_rejected_even_if_signed()
    {
        var clock = new AdjustableClock { Now = _clock.GetUtcNow() };
        var signer = new EvidenceLinkSigner(clock);
        var frameId = Guid.NewGuid();
        var (_, exp, sig) = Parse(signer.CreateUrl(frameId));

        // Saat geri alınırsa (ör. sunucu saati düzeltildi) ömründen uzun görünen bağlantı da reddediliyor.
        clock.Now = clock.Now.AddMinutes(-10);

        Assert.False(signer.IsValid(frameId, exp, sig));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!!")]
    [InlineData("AAAA")]
    public void Missing_or_malformed_signature_is_rejected(string? sig)
    {
        var signer = new EvidenceLinkSigner(_clock);

        Assert.False(signer.IsValid(Guid.NewGuid(), _clock.GetUtcNow().AddMinutes(1).ToUnixTimeSeconds(), sig));
    }

    [Fact]
    public void Links_from_another_process_are_not_accepted()
    {
        var frameId = Guid.NewGuid();
        var (_, exp, sig) = Parse(new EvidenceLinkSigner(_clock).CreateUrl(frameId));

        // Anahtar her açılışta yeniden üretiliyor; yeniden başlayan API eski bağlantıyı tanımıyor.
        Assert.False(new EvidenceLinkSigner(_clock).IsValid(frameId, exp, sig));
    }
}

/// <summary>FakeTimeProvider saati geri almaya izin vermediği için.</summary>
internal sealed class AdjustableClock : TimeProvider
{
    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}
