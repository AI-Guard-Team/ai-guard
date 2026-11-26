using System.Text.Json;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Review;

/// <summary>
/// Değerlendirici paneli (A4, FR-27–FR-33): oturum listesi, oturum detayı, karar, not ve yanlış alarm.
/// Erişim kuralı K-31: atanmış değerlendirici okur ve yazar, Admin yalnızca okur; erişimi olmayana
/// oturumun varlığı da söylenmiyor (404).
/// </summary>
public sealed class ReviewService(IAppDbContext db, ExamRiskService risks, EvidenceLinkSigner links, TimeProvider clock)
{
    // Skor grafiği en fazla bu kadar noktadan oluşuyor; uzun bir oturumda örnekleme aralığı büyüyor.
    private const int MaxScorePoints = 300;

    public async Task<ReviewSessionList?> ListAsync(Guid examId, ReviewActor actor, ReviewListQuery query, CancellationToken ct = default)
    {
        if (!await ReviewRules.CanReadExamAsync(db, examId, actor, ct))
        {
            return null;
        }

        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == examId, ct);
        var now = clock.GetUtcNow();

        var sessions = await db.ExamSessions.AsNoTracking()
            .Where(s => s.ExamId == examId && ReviewRules.Listed.Contains(s.State))
            .Select(s => new
            {
                s.Id,
                s.CandidateId,
                CandidateName = s.Candidate.FullName,
                StartedAt = s.StartedAt ?? s.CreatedAt,
                s.EndedAt,
                s.RiskScore,
                s.Flags,
                s.State,
                Decision = s.ReviewerDecision == null ? (ReviewDecisionType?)null : s.ReviewerDecision.Decision,

                // Satırdaki kısa gerekçe: kaydedildiği andaki katkısı en yüksek olay (yanlış alarm hariç).
                TopReason = s.Events.Where(e => !e.IsFalseAlarm && e.ScoreContribution > 0)
                    .OrderByDescending(e => e.ScoreContribution).Select(e => e.Reason).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var rows = sessions
            .Select(s => (Row: s, Status: ReviewRules.Status(s.State, s.Flags, exam, now)))
            .Where(x => x.Status is not null)
            .Select(x => new ReviewSessionRow(
                x.Row.Id, x.Row.CandidateId, x.Row.CandidateName, x.Row.StartedAt, x.Row.EndedAt, x.Row.RiskScore,
                SessionFlagList.Of(x.Row.Flags), x.Status!.Value, x.Row.Decision, ReasonFormatter.Localize(x.Row.TopReason)))
            .ToList();

        var pending = rows.Count(r => r.Status == ReviewStatus.AwaitingReview);

        IEnumerable<ReviewSessionRow> filtered = rows;
        if (query.Risky is { } risky)
        {
            filtered = filtered.Where(r => r.Flags.Contains(SessionFlags.Risky) == risky);
        }

        if (query.Flags is { Count: > 0 } wanted)
        {
            filtered = filtered.Where(r => r.Flags.Any(wanted.Contains));
        }

        return new ReviewSessionList(pending, [.. Sort(filtered, query.Sort)]);
    }

    public async Task<ReviewSessionDetail?> GetAsync(Guid sessionId, ReviewActor actor, CancellationToken ct = default)
    {
        var session = await db.ExamSessions.AsNoTracking()
            .Include(s => s.Exam)
            .Include(s => s.Candidate)
            .Include(s => s.ReviewerDecision!).ThenInclude(d => d.Reviewer)
            .SingleOrDefaultAsync(s => s.Id == sessionId, ct);

        var now = clock.GetUtcNow();
        var status = session is null ? null : ReviewRules.Status(session.State, session.Flags, session.Exam, now);
        if (session is null || status is null || !await ReviewRules.CanReadExamAsync(db, session.ExamId, actor, ct))
        {
            return null;
        }

        // Koleksiyonlar ayrı sorgularla: tek sorguda olay × not çarpımı satır sayısını katlıyordu.
        await LoadReviewDataAsync(session, ct);

        var evaluatedAt = ReviewRules.EvaluatedAt(session, now);
        var risk = await risks.EngineForExamAsync(session.ExamId, ct);
        var contributions = risk.Calculate(ReviewRules.RiskInputs(session.Events), evaluatedAt).Contributions
            .ToDictionary(c => c.EventId, c => c.Contribution);

        var events = session.Events
            .OrderBy(e => e.StartedAt).ThenBy(e => e.ReceivedAt)
            .Select(e => new ReviewEventView(
                e.Id, e.Type, e.Source, e.StartedAt, e.DurationSec, e.Confidence, ReasonFormatter.Localize(e.Reason),
                Math.Round(contributions.GetValueOrDefault(e.Id), 4),
                e.IsFalseAlarm, e.FalseAlarmMarkedBy?.FullName, e.FalseAlarmMarkedAt,
                e.EvidenceFrame is { } frame ? links.CreateUrl(frame.Id) : null))
            .ToList();

        // Kanıt karelerine ve olaylara kimin baktığı izlenebilsin (GAB 3.3.2, K-32).
        await ReviewRules.AuditViewAsync(db, "review.view", session.Id, actor, now,
            new { evidenceLinks = events.Count(e => e.EvidenceUrl is not null) }, ct);

        // Saklama süresi dolup olaylar silindiyse grafik çizilemiyor; sıfırdan oluşan bir eğri kayıtlı skorla çelişirdi.
        var history = session.MonitoringDataDeletedAt is null ? ScoreHistory(risk, session, evaluatedAt) : [];

        return new ReviewSessionDetail(
            session.Id, session.ExamId, session.Exam.Name, session.CandidateId, session.Candidate.FullName,
            session.State, status.Value, session.CreatedAt, session.StartedAt, session.EndedAt, session.RiskScore,
            SessionFlagList.Of(session.Flags), ReviewRules.Decision(session.ReviewerDecision),
            ReviewRules.Notes(session.ReviewerNotes), events, history, session.MonitoringDataDeletedAt);
    }

    /// <summary>
    /// Temiz / Şüpheli / İhlal kararı (FR-30). Yalnızca riskli veya işaretli tamamlanmış oturumda ve bir kez;
    /// oturum Decided'a geçiyor (K-24) ve karar denetim kaydına yazılıyor.
    /// </summary>
    public async Task<ReviewOperationResult> DecideAsync(Guid sessionId, ReviewActor actor, DecisionRequest request, CancellationToken ct = default)
    {
        var note = ReviewRules.Clean(request.Note);
        if (request.Decision is not { } decision || !Enum.IsDefined(decision))
        {
            return ReviewOperationResult.Invalid("Karar Clean, Suspicious ya da Violation olmalı.");
        }

        if (note?.Length > ReviewerNote.MaxLength)
        {
            return ReviewOperationResult.Invalid($"Not en fazla {ReviewerNote.MaxLength} karakter olabilir.");
        }

        return await WithSessionLockAsync(sessionId, actor, (session, now) =>
        {
            if (session.State == SessionState.Decided)
            {
                return Task.FromResult(ReviewOperationResult.Conflict("Bu oturum için zaten karar verildi."));
            }

            // Kart: karar yalnızca riskli veya işaretli oturumlar için.
            if (SessionReadyForReview.For(session) is null)
            {
                return Task.FromResult(ReviewOperationResult.Conflict("Karar yalnızca riskli veya işaretli olarak tamamlanmış oturumlarda verilebilir."));
            }

            session.TransitionTo(SessionState.Decided);
            db.ReviewerDecisions.Add(new ReviewerDecision
            {
                ExamSessionId = session.Id,
                ReviewerId = actor.UserId,
                Decision = decision,
                Note = note,
                DecidedAt = now,
            });
            Audit(actor, "review.decision", nameof(ExamSession), session.Id, now,
                newValue: new { decision = decision.ToString(), hasNote = note is not null });
            return Task.FromResult(ReviewOperationResult.Ok);
        }, ct);
    }

    /// <summary>Oturuma not ekler (S-1). Karar verilmeden önce de, sonra da yazılabiliyor; notlar düzenlenmiyor.</summary>
    public async Task<(ReviewOperationResult Result, ReviewNoteView? Note)> AddNoteAsync(
        Guid sessionId, ReviewActor actor, NoteRequest request, CancellationToken ct = default)
    {
        var text = ReviewRules.Clean(request.Text);
        if (text is null || text.Length > ReviewerNote.MaxLength)
        {
            return (ReviewOperationResult.Invalid($"Not boş olamaz ve en fazla {ReviewerNote.MaxLength} karakter olabilir."), null);
        }

        var session = await db.ExamSessions.AsNoTracking().Include(s => s.Exam).SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        var now = clock.GetUtcNow();
        if (session is null
            || ReviewRules.Status(session.State, session.Flags, session.Exam, now) is null
            || !await ReviewRules.CanWriteExamAsync(db, session.ExamId, actor, ct))
        {
            return (ReviewOperationResult.NotFound, null);
        }

        var note = new ReviewerNote { ExamSessionId = sessionId, AuthorId = actor.UserId, Text = text, CreatedAt = now };
        db.ReviewerNotes.Add(note);

        // Not metni denetim kaydına kopyalanmıyor; yalnızca hangi notun kimin tarafından eklendiği.
        Audit(actor, "review.note", nameof(ReviewerNote), note.Id, now, newValue: new { sessionId });
        await db.SaveChangesAsync(ct);

        var author = await db.Users.AsNoTracking().Where(u => u.Id == actor.UserId).Select(u => u.FullName).SingleAsync(ct);
        return (ReviewOperationResult.Ok, new ReviewNoteView(note.Id, note.Text, author, note.CreatedAt));
    }

    /// <summary>
    /// Olayı yanlış alarm işaretler (FR-31). Olay silinmiyor; işaretleyen ve zaman kaydediliyor, olay skora
    /// artık girmiyor ve oturumun skoru bitiş anına göre yeniden hesaplanıyor. "Riskli" işareti kalıyor
    /// (K-17): değerlendirici oturumun neden incelemeye düştüğünü görmeye devam etmeli.
    /// </summary>
    public async Task<ReviewOperationResult> MarkFalseAlarmAsync(Guid eventId, ReviewActor actor, CancellationToken ct = default)
    {
        var sessionId = await db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => (Guid?)e.ExamSessionId).SingleOrDefaultAsync(ct);
        if (sessionId is null)
        {
            return ReviewOperationResult.NotFound;
        }

        return await WithSessionLockAsync(sessionId.Value, actor, async (session, now) =>
        {
            // Karar verilmiş oturumun kanıtı değişmesin; süren oturumda henüz inceleme yok.
            if (session.State != SessionState.Completed)
            {
                return ReviewOperationResult.Conflict(session.State == SessionState.Decided
                    ? "Karar verilmiş oturumda yanlış alarm işaretlenemez."
                    : "Yanlış alarm yalnızca tamamlanmış oturumda işaretlenebilir.");
            }

            var target = await db.Events.SingleAsync(e => e.Id == eventId, ct);
            if (target.IsFalseAlarm)
            {
                return ReviewOperationResult.Conflict("Olay zaten yanlış alarm olarak işaretli.");
            }

            target.IsFalseAlarm = true;
            target.FalseAlarmMarkedById = actor.UserId;
            target.FalseAlarmMarkedAt = now;

            var events = await db.Events.AsNoTracking().Where(e => e.ExamSessionId == session.Id).ToListAsync(ct);
            var inputs = ReviewRules.RiskInputs(events).Select(e => e.EventId == eventId ? e with { IsFalseAlarm = true } : e);
            var oldScore = session.RiskScore;
            var risk = await risks.EngineForExamAsync(session.ExamId, ct);
            session.RiskScore = risk.Calculate(inputs, ReviewRules.EvaluatedAt(session, now)).Score;

            Audit(actor, "event.false_alarm", nameof(Event), eventId, now,
                oldValue: new { riskScore = oldScore }, newValue: new { riskScore = session.RiskScore, sessionId = session.Id });
            return ReviewOperationResult.Ok;
        }, ct);
    }

    /// <summary>
    /// Oturumu kilitleyip (K-18) yazma yetkisini kontrol eder ve değişikliği tek işlemde kaydeder.
    /// Aynı oturuma iki karar ya da kararla eşzamanlı yanlış alarm sırayla işleniyor.
    /// </summary>
    private async Task<ReviewOperationResult> WithSessionLockAsync(
        Guid sessionId, ReviewActor actor, Func<ExamSession, DateTimeOffset, Task<ReviewOperationResult>> change, CancellationToken ct)
    {
        var examId = await db.ExamSessions.AsNoTracking().Where(s => s.Id == sessionId).Select(s => (Guid?)s.ExamId).SingleOrDefaultAsync(ct);
        if (examId is null || !await ReviewRules.CanWriteExamAsync(db, examId.Value, actor, ct))
        {
            return ReviewOperationResult.NotFound;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.SingleAsync(s => s.Id == sessionId, ct);

        var result = await change(session, clock.GetUtcNow());
        if (result.Status != ReviewOperationStatus.Success)
        {
            return result;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
        {
            // Kilit bunu zaten önlüyor; yine de oturum başına tek karar indeksi son savunma (K-11).
            return ReviewOperationResult.Conflict("Bu oturum için zaten karar verildi.");
        }

        await transaction.CommitAsync(ct);
        return result;
    }

    /// <summary>Olaylar (kanıt karesi ve yanlış alarmı işaretleyenle) ve notlar; ikisi de AsNoTracking.</summary>
    private async Task LoadReviewDataAsync(ExamSession session, CancellationToken ct)
    {
        session.Events = await db.Events.AsNoTracking()
            .Where(e => e.ExamSessionId == session.Id)
            .Include(e => e.EvidenceFrame)
            .Include(e => e.FalseAlarmMarkedBy)
            .ToListAsync(ct);
        session.ReviewerNotes = await db.ReviewerNotes.AsNoTracking()
            .Where(n => n.ExamSessionId == session.Id)
            .Include(n => n.Author)
            .ToListAsync(ct);
    }

    private void Audit(ReviewActor actor, string action, string targetType, Guid targetId, DateTimeOffset now,
        object? oldValue = null, object? newValue = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor.UserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
            OccurredAt = now,
        });

    private static IEnumerable<ReviewSessionRow> Sort(IEnumerable<ReviewSessionRow> rows, ReviewSort sort) => sort switch
    {
        ReviewSort.Score => rows.OrderByDescending(r => r.RiskScore).ThenBy(r => r.StartedAt),
        ReviewSort.StartedAt => rows.OrderBy(r => r.StartedAt),

        // FR-27: işaretli oturumlar skordan bağımsız önde; karar bekleyenler en üstte.
        _ => rows.OrderByDescending(r => r.Status == ReviewStatus.AwaitingReview)
            .ThenByDescending(r => r.Flags.Count > 0)
            .ThenByDescending(r => r.RiskScore)
            .ThenBy(r => r.StartedAt),
    };

    /// <summary>
    /// Skor geçmişi (grafik verisi): olayların başlangıç ve bitişleri ile düzenli aralıklı noktalar. Skor her
    /// noktada risk motoruyla yeniden hesaplanıyor; azalma eğrisi de böylece grafikte görünüyor.
    /// </summary>
    private static IReadOnlyList<ScorePoint> ScoreHistory(RiskEngine risk, ExamSession session, DateTimeOffset end)
    {
        var start = session.StartedAt ?? session.CreatedAt;
        if (session.Events.Count > 0)
        {
            var firstEvent = session.Events.Min(e => e.StartedAt);
            start = firstEvent < start ? firstEvent : start;
        }

        if (end <= start)
        {
            return [new ScorePoint(start, 0)];
        }

        var step = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMinutes(1).Ticks, (end - start).Ticks / MaxScorePoints));
        var moments = new SortedSet<DateTimeOffset> { start, end };
        for (var t = start + step; t < end; t += step)
        {
            moments.Add(t);
        }

        foreach (var e in session.Events)
        {
            foreach (var t in new[] { e.StartedAt, e.StartedAt.AddSeconds(e.DurationSec) })
            {
                if (t > start && t < end)
                {
                    moments.Add(t);
                }
            }
        }

        var inputs = ReviewRules.RiskInputs(session.Events).ToList();
        return [.. moments.Select(t => new ScorePoint(t, risk.Calculate(inputs, t).Score))];
    }
}
