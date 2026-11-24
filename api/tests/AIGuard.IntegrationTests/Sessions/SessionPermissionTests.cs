using AIGuard.Application.Sessions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Sessions;

/// <summary>
/// Kamera/mikrofon izni (S4, FR-17, K-30): izin kalkınca PermissionPending, gelince InProgress. İzinsiz süreyi
/// istemci değil sunucu hesaplıyor; sınav izin beklerken biterse de süre yazılıyor.
/// </summary>
public class SessionPermissionTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private readonly FakeTimeProvider _clock = Clock(); // EventTestHelpers.Now

    private SessionService Service(AppDbContext db) => new(db, Events.EventTestHelpers.Service(db, _clock), _clock);

    private async Task<Guid> SessionAsync(SessionState state = SessionState.InProgress)
    {
        await using var db = fx.CreateContext();
        return (await InProgressSessionAsync(db, state)).Id;
    }

    private async Task<ExamSession> ReloadAsync(Guid id)
    {
        await using var db = fx.CreateContext();
        return await db.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    private async Task<List<Event>> EventsAsync(Guid id)
    {
        await using var db = fx.CreateContext();
        return await db.Events.AsNoTracking().Where(e => e.ExamSessionId == id).ToListAsync();
    }

    private async Task<SessionOperationResult> RevokeAsync(Guid id)
    {
        await using var db = fx.CreateContext();
        return await Service(db).RevokePermissionAsync(id);
    }

    private async Task<SessionOperationResult> RestoreAsync(Guid id)
    {
        await using var db = fx.CreateContext();
        return await Service(db).RestorePermissionAsync(id);
    }

    [SkippableFact]
    public async Task Revoking_moves_to_permission_pending_and_hides_questions()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(SessionOperationStatus.Success, (await RevokeAsync(id)).Status);

        var session = await ReloadAsync(id);
        Assert.Equal(SessionState.PermissionPending, session.State);
        Assert.Equal(Now.AddMinutes(2), session.PermissionRevokedAt);
        // Olay izin geri gelince süresiyle yazılıyor; şimdilik yok.
        Assert.Empty(await EventsAsync(id));
        await using var db = fx.CreateContext();
        Assert.Empty((await Service(db).GetAsync(id))!.Questions);
    }

    [SkippableFact]
    public async Task Restoring_records_the_server_measured_gap_and_flags_the_session()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        _clock.Advance(TimeSpan.FromMinutes(2));
        await RevokeAsync(id);
        _clock.Advance(TimeSpan.FromSeconds(45));

        Assert.Equal(SessionOperationStatus.Success, (await RestoreAsync(id)).Status);

        var session = await ReloadAsync(id);
        Assert.Equal(SessionState.InProgress, session.State);
        Assert.Null(session.PermissionRevokedAt);
        // Süre işlemeye devam etti: bitiş değişmedi (FR-17).
        Assert.Equal(Now.AddMinutes(30), session.EndsAt);
        Assert.True(session.Flags.HasFlag(SessionFlags.PermissionGap));

        var gap = Assert.Single(await EventsAsync(id));
        Assert.Equal(EventType.PERMISSION_REVOKED, gap.Type);
        Assert.Equal(EventSource.Browser, gap.Source);
        Assert.Equal(Now.AddMinutes(2), gap.StartedAt);
        Assert.Equal(45, gap.DurationSec, precision: 3);
    }

    [SkippableFact]
    public async Task Repeated_revoke_keeps_the_first_moment()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        await RevokeAsync(id);
        _clock.Advance(TimeSpan.FromSeconds(20));

        // Tarayıcı hem kamera hem mikrofon için bildirebilir; ikinci bildirim süreyi kısaltmamalı.
        Assert.Equal(SessionOperationStatus.Duplicate, (await RevokeAsync(id)).Status);

        Assert.Equal(Now, (await ReloadAsync(id)).PermissionRevokedAt);
    }

    [SkippableFact]
    public async Task Restore_without_revoke_writes_nothing()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();

        Assert.Equal(SessionOperationStatus.Duplicate, (await RestoreAsync(id)).Status);

        Assert.Empty(await EventsAsync(id));
        Assert.Equal(SessionState.InProgress, (await ReloadAsync(id)).State);
    }

    [SkippableTheory]
    [InlineData(SessionState.IdentityVerification)]
    [InlineData(SessionState.Disconnected)]
    [InlineData(SessionState.Completed)]
    public async Task Revoke_outside_the_exam_is_a_conflict(SessionState state)
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync(state);

        Assert.Equal(SessionOperationStatus.Conflict, (await RevokeAsync(id)).Status);

        Assert.Equal(state, (await ReloadAsync(id)).State);
    }

    [SkippableFact]
    public async Task Unknown_session_is_not_found()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

        Assert.Equal(SessionOperationStatus.NotFound, (await RevokeAsync(Guid.NewGuid())).Status);
        Assert.Equal(SessionOperationStatus.NotFound, (await RestoreAsync(Guid.NewGuid())).Status);
    }

    [SkippableFact]
    public async Task Submitting_while_waiting_for_permission_records_the_gap()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        await RevokeAsync(id);
        _clock.Advance(TimeSpan.FromSeconds(90));

        await using (var db = fx.CreateContext())
        {
            Assert.Equal(SessionOperationStatus.Success, (await Service(db).SubmitAsync(id)).Status);
        }

        var session = await ReloadAsync(id);
        Assert.Equal(SessionState.Completed, session.State);
        Assert.True(session.Flags.HasFlag(SessionFlags.PermissionGap));
        var gap = Assert.Single(await EventsAsync(id));
        Assert.Equal(90, gap.DurationSec, precision: 3);
    }

    [SkippableFact]
    public async Task Time_running_out_while_waiting_for_permission_records_the_gap_until_the_end()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync(); // 30 dk sınav, Now'da başladı
        _clock.Advance(TimeSpan.FromMinutes(29));
        await RevokeAsync(id);
        // Aday hiç dönmedi; otomatik teslim süre bittikten biraz sonra çalıştı.
        _clock.Advance(TimeSpan.FromMinutes(3));

        await using (var db = fx.CreateContext())
        {
            // Ortak test veritabanında başka oturumlar da işlenebilir; sayı değil bu oturumun durumu kontrol ediliyor.
            await new SessionTimekeeper(db, Events.EventTestHelpers.Service(db, _clock), _clock).RunOnceAsync();
        }

        var session = await ReloadAsync(id);
        Assert.Equal(SessionState.Completed, session.State);
        Assert.True(session.Flags.HasFlag(SessionFlags.PermissionGap));
        var gap = Assert.Single(await EventsAsync(id));
        Assert.Equal(Now.AddMinutes(29), gap.StartedAt);
        // Süre sonunu geçmiyor: izinsiz süre 1 dk.
        Assert.Equal(60, gap.DurationSec, precision: 3);
    }

    [SkippableFact]
    public async Task Restore_after_time_is_up_is_a_conflict()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        await RevokeAsync(id);
        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(SessionOperationStatus.Conflict, (await RestoreAsync(id)).Status);

        Assert.Equal(SessionState.PermissionPending, (await ReloadAsync(id)).State);
    }

    [SkippableFact]
    public async Task Waiting_for_permission_does_not_count_as_disconnected()
    {
        Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");
        var id = await SessionAsync();
        await RevokeAsync(id);
        // Son istek 2 dk önce: InProgress olsaydı kesik sayılırdı (30 sn).
        _clock.Advance(TimeSpan.FromMinutes(2));

        await using (var db = fx.CreateContext())
        {
            await new SessionTimekeeper(db, Events.EventTestHelpers.Service(db, _clock), _clock).RunOnceAsync();
        }

        Assert.Equal(SessionState.PermissionPending, (await ReloadAsync(id)).State);
    }
}
