using AIGuard.Application.Events;
using AIGuard.Application.Risk;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static AIGuard.IntegrationTests.Events.EventTestHelpers;

namespace AIGuard.IntegrationTests.Events;

public class EventServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private void RequireDb() => Skip.If(fx.ConnectionString is null, "PostgreSQL yok.");

    [SkippableFact]
    public async Task Browser_event_gets_server_side_source_time_contribution_and_reason()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        clock.Advance(TimeSpan.FromSeconds(11 * 60 + 8));

        var result = await Service(db, clock).RecordBrowserEventAsync(session.Id, new BrowserEventRequest(EventType.TAB_BLUR, 8));

        var stored = await db.Events.AsNoTracking().SingleAsync(e => e.Id == result.Event!.Id);
        Assert.Equal(EventSource.Browser, stored.Source);
        Assert.Equal(Now.AddMinutes(11), stored.StartedAt); // alınma anı − süre
        Assert.Equal(1 + Math.Log(1.8), stored.ScoreContribution, 4);
        Assert.Equal("12. dakikada 8 sn sınav sekmesinden çıkıldı", stored.Reason);
    }

    [SkippableTheory]
    [InlineData(EventType.PHONE)]
    [InlineData(EventType.VM_DETECTED)]
    [InlineData(EventType.IP_CHANGE)]
    [InlineData((EventType)999)]
    public async Task Browser_cannot_report_server_side_or_unknown_events(EventType type)
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);

        var result = await Service(db, Clock()).RecordBrowserEventAsync(session.Id, new BrowserEventRequest(type));

        Assert.Equal(EventRecordStatus.NotAllowedFromBrowser, result.Status);
        Assert.False(await db.Events.AnyAsync(e => e.ExamSessionId == session.Id));
    }

    [SkippableFact]
    public async Task Browser_events_of_the_same_type_are_rate_limited()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        var service = Service(db, clock);

        var first = await service.RecordBrowserEventAsync(session.Id, new BrowserEventRequest(EventType.RIGHT_CLICK));
        clock.Advance(TimeSpan.FromMilliseconds(500));
        var tooSoon = await service.RecordBrowserEventAsync(session.Id, new BrowserEventRequest(EventType.RIGHT_CLICK));
        clock.Advance(TimeSpan.FromMilliseconds(600));
        var later = await service.RecordBrowserEventAsync(session.Id, new BrowserEventRequest(EventType.RIGHT_CLICK));

        Assert.Equal(EventRecordStatus.Recorded, first.Status);
        Assert.Equal(EventRecordStatus.RateLimited, tooSoon.Status);
        Assert.Equal(EventRecordStatus.Recorded, later.Status);
    }

    [SkippableFact]
    public async Task Invalid_input_and_closed_sessions_are_rejected()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var open = await InProgressSessionAsync(db);
        var closed = await InProgressSessionAsync(db, SessionState.Completed);
        var service = Service(db, Clock());

        var negative = await service.RecordBrowserEventAsync(open.Id, new BrowserEventRequest(EventType.TAB_BLUR, -1));
        var badJson = await service.RecordSystemEventAsync(open.Id, new SystemEvent(EventType.PHONE, Now, Payload: "\"metin\""));
        var future = await service.RecordSystemEventAsync(open.Id, new SystemEvent(EventType.PHONE, Now.AddMinutes(1)));
        var onClosed = await service.RecordSystemEventAsync(closed.Id, new SystemEvent(EventType.PHONE, Now));

        Assert.Equal(EventRecordStatus.Invalid, negative.Status);
        Assert.Equal(EventRecordStatus.Invalid, badJson.Status);
        Assert.Equal(EventRecordStatus.Invalid, future.Status);
        Assert.Equal(EventRecordStatus.SessionClosed, onClosed.Status);
    }

    [SkippableFact]
    public async Task Slightly_future_system_event_counts_immediately()
    {
        // Saat farkıyla birkaç saniye ileri tarihlenen olay skora hemen girmeli.
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);

        await Service(db, Clock()).RecordSystemEventAsync(session.Id, new SystemEvent(EventType.PHONE, Now.AddSeconds(3)));
        await db.Entry(session).ReloadAsync();

        Assert.Equal(5, session.RiskScore, 4);
    }

    [SkippableFact]
    public async Task Risky_flag_stays_after_decay_and_monitoring_gap_adds_no_risk()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var clock = Clock();
        var service = Service(db, clock);

        await service.RecordSystemEventAsync(session.Id, new SystemEvent(EventType.VM_DETECTED, Now));
        clock.Advance(TimeSpan.FromHours(5));
        await service.RecordSystemEventAsync(session.Id, new SystemEvent(EventType.MONITORING_GAP, clock.GetUtcNow(), 60));
        await db.Entry(session).ReloadAsync();

        Assert.Equal(SessionFlags.Risky | SessionFlags.MonitoringGap, session.Flags);
        Assert.Equal(10, session.RiskScore, 4); // VM azalmıyor, kesinti puan eklemiyor
    }

    [SkippableFact]
    public async Task Permission_revoked_marks_the_session_for_review()
    {
        // FR-17: izinsiz geçen süre olan oturum risk skorundan bağımsız incelemeye düşer.
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);

        await Service(db, Clock()).RecordBrowserEventAsync(session.Id, new BrowserEventRequest(EventType.PERMISSION_REVOKED, 40));
        await db.Entry(session).ReloadAsync();

        Assert.Equal(SessionFlags.PermissionGap, session.Flags);
    }

    [SkippableFact]
    public async Task False_alarms_are_left_out_of_the_score()
    {
        RequireDb();
        await using var db = fx.CreateContext();
        var session = await InProgressSessionAsync(db);
        var service = Service(db, Clock());

        var phone = await service.RecordSystemEventAsync(session.Id, new SystemEvent(EventType.PHONE, Now));
        var stored = await db.Events.SingleAsync(e => e.Id == phone.Event!.Id);
        stored.IsFalseAlarm = true;
        stored.FalseAlarmMarkedById = session.CandidateId;
        stored.FalseAlarmMarkedAt = Now;
        await db.SaveChangesAsync();

        await service.RecordSystemEventAsync(session.Id, new SystemEvent(EventType.GAZE_AWAY, Now));
        await db.Entry(session).ReloadAsync();

        Assert.Equal(1, session.RiskScore, 4);
    }

    [SkippableFact]
    public async Task Concurrent_events_on_one_session_are_all_recorded_with_a_consistent_score()
    {
        RequireDb();
        Guid sessionId;
        await using (var setup = fx.CreateContext())
        {
            sessionId = (await InProgressSessionAsync(setup)).Id;
        }

        EventType[] types = [EventType.PHONE, EventType.SPEECH, EventType.GAZE_AWAY, EventType.FACE_MISSING, EventType.MULTIPLE_FACES, EventType.CONNECTION_LOST];
        var results = await Task.WhenAll(types.Select(async type =>
        {
            await using var db = fx.CreateContext();
            return await Service(db, Clock()).RecordSystemEventAsync(sessionId, new SystemEvent(type, Now));
        }));

        await using var check = fx.CreateContext();
        var events = await check.Events.AsNoTracking().Where(e => e.ExamSessionId == sessionId).ToListAsync();
        var session = await check.ExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        var expected = new RiskEngine(Options())
            .Calculate(events.Select(e => new RiskEventInput(e.Id, e.Type, e.StartedAt, e.DurationSec, e.ReceivedAt)), Now);

        Assert.All(results, r => Assert.Equal(EventRecordStatus.Recorded, r.Status));
        Assert.Equal(types.Length, events.Count);
        Assert.Equal(expected.Score, session.RiskScore, 4);
    }
}
