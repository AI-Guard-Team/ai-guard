using AIGuard.Application.Abstractions;
using AIGuard.Application.Sessions;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Sessions;

/// <summary>
/// Sınav sırası kamera karesi ve ses parçası (S4, FR-14): biçim ve boyut kontrolü, yalnızca sınav sürerken kabul,
/// sunucu tarafı hız sınırı ve bağlantı canlılığı. Analiz D4/D5'te; burada analizciye gidip gitmediğine bakılıyor.
/// </summary>
public class SessionMediaServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private readonly FakeTimeProvider _clock = Clock();
    private readonly RecordingMediaAnalyzer _analyzer = new();
    private readonly SessionMediaRateLimiter _limiter = new();

    private SessionMediaService Service(AppDbContext db) => new(db, _analyzer, _limiter, _clock);

    internal static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    internal static readonly byte[] WebM = [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01];
    internal static readonly byte[] Wav = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8];

    private async Task<Guid> SessionAsync(SessionState state = SessionState.InProgress)
    {
        await using var db = fx.CreateContext();
        return (await InProgressSessionAsync(db, state)).Id;
    }

    private async Task<MediaReceiveStatus> FrameAsync(Guid id, byte[] data)
    {
        await using var db = fx.CreateContext();
        return await Service(db).ReceiveFrameAsync(id, data);
    }

    private async Task<MediaReceiveStatus> AudioAsync(Guid id, byte[] data)
    {
        await using var db = fx.CreateContext();
        return await Service(db).ReceiveAudioAsync(id, data);
    }

    [SkippableFact]
    public async Task Frame_is_passed_to_the_analyzer_and_marks_the_candidate_as_seen()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        _clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(MediaReceiveStatus.Accepted, await FrameAsync(id, Jpeg));

        var call = Assert.Single(_analyzer.Frames);
        Assert.Equal(id, call.SessionId);
        Assert.Equal(Jpeg, call.Data);
        Assert.Equal(Now.AddSeconds(5), call.ReceivedAt);
        await using var db = fx.CreateContext();
        Assert.Equal(Now.AddSeconds(5), (await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == id)).LastSeenAt);
    }

    [SkippableTheory]
    [InlineData("webm")]
    [InlineData("wav")]
    public async Task Audio_format_is_read_from_the_bytes(string kind)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();

        Assert.Equal(MediaReceiveStatus.Accepted, await AudioAsync(id, kind == "webm" ? WebM : Wav));

        var call = Assert.Single(_analyzer.Audio);
        Assert.Equal(kind == "webm" ? AudioFormat.WebM : AudioFormat.Wav, call.Format);
    }

    [SkippableFact]
    public async Task Invalid_or_oversized_content_is_rejected_before_anything_else()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        var bigJpeg = new byte[SessionMediaService.MaxFrameBytes + 1];
        Jpeg.CopyTo(bigJpeg, 0);
        var bigWebM = new byte[SessionMediaService.MaxAudioBytes + 1];
        WebM.CopyTo(bigWebM, 0);

        Assert.Equal(MediaReceiveStatus.Invalid, await FrameAsync(id, "GIF89a"u8.ToArray()));
        Assert.Equal(MediaReceiveStatus.Invalid, await FrameAsync(id, []));
        Assert.Equal(MediaReceiveStatus.Invalid, await FrameAsync(id, bigJpeg));
        Assert.Equal(MediaReceiveStatus.Invalid, await AudioAsync(id, Jpeg));
        Assert.Equal(MediaReceiveStatus.Invalid, await AudioAsync(id, bigWebM));

        Assert.Empty(_analyzer.Frames);
        Assert.Empty(_analyzer.Audio);
    }

    [SkippableTheory]
    [InlineData(SessionState.PermissionPending)]
    [InlineData(SessionState.Disconnected)]
    [InlineData(SessionState.IdentityVerification)]
    [InlineData(SessionState.Completed)]
    public async Task Media_is_accepted_only_while_the_exam_is_in_progress(SessionState state)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync(state);

        Assert.Equal(MediaReceiveStatus.Conflict, await FrameAsync(id, Jpeg));
        Assert.Equal(MediaReceiveStatus.Conflict, await AudioAsync(id, WebM));

        Assert.Empty(_analyzer.Frames);
    }

    [SkippableFact]
    public async Task Media_after_time_is_up_is_a_conflict()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(MediaReceiveStatus.Conflict, await FrameAsync(id, Jpeg));
    }

    [SkippableFact]
    public async Task Unknown_session_is_not_found()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        Assert.Equal(MediaReceiveStatus.NotFound, await FrameAsync(Guid.NewGuid(), Jpeg));
    }

    [SkippableFact]
    public async Task Server_limits_each_kind_to_about_one_per_second()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();

        Assert.Equal(MediaReceiveStatus.Accepted, await FrameAsync(id, Jpeg));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        // İstemci sınırına güvenilmiyor: arka arkaya gelen kare reddediliyor, ses ayrı sayılıyor.
        Assert.Equal(MediaReceiveStatus.RateLimited, await FrameAsync(id, Jpeg));
        Assert.Equal(MediaReceiveStatus.Accepted, await AudioAsync(id, WebM));
        _clock.Advance(SessionMediaRateLimiter.MinInterval);
        Assert.Equal(MediaReceiveStatus.Accepted, await FrameAsync(id, Jpeg));

        Assert.Equal(2, _analyzer.Frames.Count);
    }

    [SkippableFact]
    public async Task Limit_is_per_session()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var first = await SessionAsync();
        var second = await SessionAsync();

        Assert.Equal(MediaReceiveStatus.Accepted, await FrameAsync(first, Jpeg));
        Assert.Equal(MediaReceiveStatus.Accepted, await FrameAsync(second, Jpeg));
    }
}

/// <summary>Analizciye giden çağrıları biriktirir; D4/D5 gelene kadar gerçek analizci yok.</summary>
internal sealed class RecordingMediaAnalyzer : IMediaAnalyzer
{
    public List<(Guid SessionId, byte[] Data, DateTimeOffset ReceivedAt)> Frames { get; } = [];

    public List<(Guid SessionId, byte[] Data, AudioFormat Format, DateTimeOffset ReceivedAt)> Audio { get; } = [];

    public Task AnalyzeFrameAsync(Guid sessionId, byte[] image, DateTimeOffset receivedAt, CancellationToken ct = default)
    {
        lock (Frames)
        {
            Frames.Add((sessionId, image, receivedAt));
        }

        return Task.CompletedTask;
    }

    public Task AnalyzeAudioAsync(Guid sessionId, byte[] chunk, AudioFormat format, DateTimeOffset receivedAt, CancellationToken ct = default)
    {
        lock (Audio)
        {
            Audio.Add((sessionId, chunk, format, receivedAt));
        }

        return Task.CompletedTask;
    }
}
