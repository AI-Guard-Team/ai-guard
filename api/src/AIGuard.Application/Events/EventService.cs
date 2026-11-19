using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Events;

/// <summary>
/// Olayları kaydedip oturumun risk skorunu ve işaretlerini güncelliyor (A2).
/// Tarayıcı olayları <see cref="RecordBrowserEventAsync"/>, sunucuda üretilen olaylar
/// <see cref="RecordSystemEventAsync"/> üzerinden geliyor.
/// </summary>
public sealed class EventService(IAppDbContext db, RiskEngine riskEngine, TimeProvider clock)
{
    public const double MaxDurationSec = 3600;
    public const int MaxPayloadLength = 4096;
    public static readonly TimeSpan BrowserRateLimit = TimeSpan.FromSeconds(1);

    // Sunucunun kendi ürettiği olaylarda saatin ileriye kaymasına izin verilen pay.
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromSeconds(5);

    private static readonly SessionState[] ClosedStates =
        [SessionState.Completed, SessionState.Cancelled, SessionState.Decided];

    public async Task<EventRecordResult> RecordBrowserEventAsync(
        Guid sessionId, BrowserEventRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Type) || !EventCatalog.IsBrowserReportable(request.Type))
        {
            return EventRecordResult.Fail(EventRecordStatus.NotAllowedFromBrowser,
                "Bu olay türü tarayıcıdan gönderilemez.");
        }

        if (ValidateCommon(request.DurationSec, null, null) is { } error)
        {
            return EventRecordResult.Fail(EventRecordStatus.Invalid, error);
        }

        var now = clock.GetUtcNow();
        var started = now.AddSeconds(-request.DurationSec);
        return await RecordAsync(sessionId, request.Type, started, request.DurationSec, null, null,
            now, rateLimited: true, ct);
    }

    public async Task<EventRecordResult> RecordSystemEventAsync(
        Guid sessionId, SystemEvent input, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(input.Type))
        {
            return EventRecordResult.Fail(EventRecordStatus.Invalid, "Bilinmeyen olay türü.");
        }

        if (ValidateCommon(input.DurationSec, input.Confidence, input.Payload) is { } error)
        {
            return EventRecordResult.Fail(EventRecordStatus.Invalid, error);
        }

        var now = clock.GetUtcNow();
        if (input.StartedAt > now + ClockSkewTolerance)
        {
            return EventRecordResult.Fail(EventRecordStatus.Invalid, "Olay zamanı gelecekte olamaz.");
        }

        // Küçük saat farkıyla birkaç saniye ileri tarihlenmiş olay "şimdi" sayılıyor;
        // yoksa hesaplayıcı onu gelecekte görüp skora katmazdı.
        var started = input.StartedAt > now ? now : input.StartedAt;

        return await RecordAsync(sessionId, input.Type, started, input.DurationSec, input.Confidence,
            input.Payload, now, rateLimited: false, ct);
    }

    private async Task<EventRecordResult> RecordAsync(
        Guid sessionId, EventType type, DateTimeOffset startedAt, double durationSec, double? confidence,
        string? payload, DateTimeOffset now, bool rateLimited, CancellationToken ct)
    {
        // Aynı oturuma aynı anda birden fazla olay gelebiliyor (kamera, ses, tarayıcı).
        // Oturum satırını kilitleyerek bu yazmaları sıraya sokuyoruz; böylece skor her zaman
        // tüm olaylarla hesaplanıyor ve hız sınırı kontrolü yarışa girmiyor (K-18).
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        if (!await db.ExamSessions.AnyAsync(s => s.Id == sessionId, ct))
        {
            return EventRecordResult.Fail(EventRecordStatus.SessionNotFound);
        }

        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);

        if (ClosedStates.Contains(session.State))
        {
            return EventRecordResult.Fail(EventRecordStatus.SessionClosed);
        }

        // İstemci tarafındaki sınırlamaya güvenmiyoruz; aynı türden saniyede en fazla bir olay.
        if (rateLimited)
        {
            var since = now - BrowserRateLimit;
            if (await db.Events.AnyAsync(e => e.ExamSessionId == sessionId && e.Type == type && e.ReceivedAt > since, ct))
            {
                return EventRecordResult.Fail(EventRecordStatus.RateLimited);
            }
        }

        var newEvent = new Event
        {
            ExamSessionId = sessionId,
            Type = type,
            Source = EventCatalog.SourceOf(type),
            StartedAt = startedAt,
            DurationSec = durationSec,
            Confidence = confidence,
            Payload = payload,
            ReceivedAt = now,
            Reason = ReasonFormatter.Format(type, startedAt, durationSec, session.StartedAt),
        };

        await ApplyRiskAsync(session, newEvent, now, ct);
        db.Events.Add(newEvent);
        await db.SaveChangesAsync(ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return new EventRecordResult(EventRecordStatus.Recorded, newEvent);
    }

    private async Task ApplyRiskAsync(ExamSession session, Event newEvent, DateTimeOffset now, CancellationToken ct)
    {
        var stored = await db.Events.AsNoTracking()
            .Where(e => e.ExamSessionId == session.Id)
            .Select(e => new { e.Id, e.Type, e.StartedAt, e.DurationSec, e.ReceivedAt, e.IsFalseAlarm })
            .ToListAsync(ct);

        var inputs = stored
            .Select(e => new RiskEventInput(e.Id, e.Type, e.StartedAt, e.DurationSec, e.ReceivedAt, e.IsFalseAlarm))
            .Append(new RiskEventInput(newEvent.Id, newEvent.Type, newEvent.StartedAt, newEvent.DurationSec, newEvent.ReceivedAt));

        // Sınav bittiyse skor bitiş anında sabitleniyor; sonrasında azalma uygulanmıyor.
        var at = session.EndedAt is { } ended && ended < now ? ended : now;
        var result = riskEngine.Calculate(inputs, at);

        session.RiskScore = result.Score;

        // Katkı, skorla aynı hesaptan alınıyor (tekrar ve tür üst sınırı dahil); değerlendiricinin
        // gördüğü katkı ile skora giren değer farklı olmasın.
        newEvent.ScoreContribution = Math.Round(
            result.Contributions.Single(c => c.EventId == newEvent.Id).Contribution, 4);

        // Risky bir kez konunca kalkmıyor: skor zamanla düşse de değerlendirici incelemeli.
        if (result.IsRisky)
        {
            session.Flags |= SessionFlags.Risky;
        }

        if (newEvent.Type == EventType.MONITORING_GAP)
        {
            session.Flags |= SessionFlags.MonitoringGap;
        }
    }

    private static string? ValidateCommon(double durationSec, double? confidence, string? payload)
    {
        if (double.IsNaN(durationSec) || durationSec < 0 || durationSec > MaxDurationSec)
        {
            return $"Süre 0 ile {MaxDurationSec} sn arasında olmalı.";
        }

        if (confidence is { } c && (double.IsNaN(c) || c < 0 || c > 1))
        {
            return "Güven skoru 0 ile 1 arasında olmalı.";
        }

        if (payload is null)
        {
            return null;
        }

        if (payload.Length > MaxPayloadLength)
        {
            return $"Ek bilgi en fazla {MaxPayloadLength} karakter olabilir.";
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? null
                : "Ek bilgi bir JSON nesnesi veya dizisi olmalı.";
        }
        catch (JsonException)
        {
            return "Ek bilgi geçerli bir JSON değil.";
        }
    }
}
