using AIGuard.Application.Abstractions;
using AIGuard.Application.Biometrics;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Sessions;

public enum MediaReceiveStatus
{
    /// <summary>Analize gönderildi.</summary>
    Accepted,

    /// <summary>Boş, çok büyük ya da desteklenmeyen biçim.</summary>
    Invalid,

    NotFound,

    /// <summary>Sınav sürmüyor (izin bekliyor, bağlantı kesik, başlamadı ya da bitti).</summary>
    Conflict,

    /// <summary>Aynı türden önceki parçanın üzerinden <see cref="SessionMediaRateLimiter.MinInterval"/> geçmedi.</summary>
    RateLimited,
}

/// <summary>
/// Sınav sırasında tarayıcıdan gelen kamera karesi ve ses parçası (S4, FR-14). Burada yalnızca kontrol ediliyor ve
/// analizciye veriliyor; analiz ve olay kuralları D4/D5'te (<see cref="IMediaAnalyzer"/>). Hiçbir şey saklanmıyor.
/// </summary>
public sealed class SessionMediaService(
    IAppDbContext db, IMediaAnalyzer analyzer, SessionMediaRateLimiter limiter, TimeProvider clock)
{
    /// <summary>Kare sınırı api'deki diğer karelerle ve vision sözleşmesiyle aynı (docs/contracts.md 1. bölüm).</summary>
    public const int MaxFrameBytes = ImageFile.MaxFrameBytes;

    /// <summary>
    /// ~1 sn WebM/Opus birkaç KB; 16 kHz mono WAV ~32 KB. Uzun ya da yüksek örneklemeli parçaya da yer bırakan
    /// üst sınır (S-22).
    /// </summary>
    public const int MaxAudioBytes = 256 * 1024;

    // Son görülme anı her parçada değil, en geç bu aralıkla yazılıyor; kesinti eşiği (30 sn) bunun çok üstünde.
    private static readonly TimeSpan TouchInterval = TimeSpan.FromSeconds(5);

    private static readonly byte[] Ebml = [0x1A, 0x45, 0xDF, 0xA3];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Wave = "WAVE"u8.ToArray();

    public async Task<MediaReceiveStatus> ReceiveFrameAsync(Guid sessionId, byte[] image, CancellationToken ct = default)
    {
        if (!ImageFile.IsAcceptable(image, MaxFrameBytes))
        {
            return MediaReceiveStatus.Invalid;
        }

        var now = clock.GetUtcNow();
        var status = await AdmitAsync(sessionId, MediaKind.Frame, now, ct);
        if (status == MediaReceiveStatus.Accepted)
        {
            await analyzer.AnalyzeFrameAsync(sessionId, image, now, ct);
        }

        return status;
    }

    public async Task<MediaReceiveStatus> ReceiveAudioAsync(Guid sessionId, byte[] chunk, CancellationToken ct = default)
    {
        if (chunk.Length > MaxAudioBytes || DetectAudio(chunk) is not { } format)
        {
            return MediaReceiveStatus.Invalid;
        }

        var now = clock.GetUtcNow();
        var status = await AdmitAsync(sessionId, MediaKind.Audio, now, ct);
        if (status == MediaReceiveStatus.Accepted)
        {
            await analyzer.AnalyzeAudioAsync(sessionId, chunk, format, now, ct);
        }

        return status;
    }

    /// <summary>İstemcinin bildirdiği Content-Type'a güvenilmiyor; biçim ilk baytlardan.</summary>
    public static AudioFormat? DetectAudio(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(Ebml))
        {
            return AudioFormat.WebM;
        }

        // WAV: "RIFF" + 4 bayt uzunluk + "WAVE"
        if (data.Length >= 12 && data.StartsWith(Riff) && data.Slice(8, 4).SequenceEqual(Wave))
        {
            return AudioFormat.Wav;
        }

        return null;
    }

    private async Task<MediaReceiveStatus> AdmitAsync(Guid sessionId, MediaKind kind, DateTimeOffset now, CancellationToken ct)
    {
        var session = await db.ExamSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return MediaReceiveStatus.NotFound;
        }

        // İzin beklerken ya da bağlantı kesikken gelen parça işlenmiyor; önyüz bu durumlarda göndermiyor.
        if (session.State != SessionState.InProgress || (session.EndsAt is { } endsAt && now > endsAt))
        {
            return MediaReceiveStatus.Conflict;
        }

        if (!limiter.TryAcquire(sessionId, kind, now))
        {
            return MediaReceiveStatus.RateLimited;
        }

        // Kareler saniyede bir geldiği için ayrıca oturum yoklamasına gerek kalmadan bağlantı canlı sayılıyor (K-24).
        if (session.LastSeenAt is not { } seen || now - seen >= TouchInterval)
        {
            session.LastSeenAt = now;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Aynı anda oturum başka bir işlemle güncellendi; son görülme anı sonraki parçada yazılır.
            }
        }

        return MediaReceiveStatus.Accepted;
    }
}
