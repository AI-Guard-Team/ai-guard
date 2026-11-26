using AIGuard.Application.Abstractions;
using AIGuard.Application.Events;
using AIGuard.Application.Exams;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Sessions;

/// <summary>
/// Sınav oturumunun akışı (E4, FR-09, FR-26): başlatma, cevap kaydı, teslim ve diğer kartların
/// çağıracağı geçişler. Durum yalnızca <see cref="SessionStateMachine"/> ile değişiyor (K-24).
/// </summary>
/// <remarks>
/// Oturumu değiştiren her işlem oturum satırını kilitliyor (K-18): teslim, otomatik teslim, cevap ve
/// olay aynı anda gelse de sırayla işleniyor.
/// </remarks>
// reviewNotifier isteğe bağlı: testler oturum akışını bildirimsiz kurabiliyor; uygulamada DI her zaman veriyor (A4).
// monitoring de isteğe bağlı: teslimde açık kamera bölümlerini yazıyor (D4, K-45); verilmezse davranış aynı.
public sealed class SessionService(
    IAppDbContext db, EventService events, TimeProvider clock, IReviewNotifier? reviewNotifier = null,
    ISessionMonitoringCloser? monitoring = null)
{
    public async Task<StartSessionResult> StartAsync(Guid examId, Guid candidateId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        var assignment = await db.ExamAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.ExamId == examId && a.UserId == candidateId && a.Role == AssignmentRole.Candidate, ct);
        if (exam is null || assignment is null)
        {
            // Atanmadığı sınavın var olduğu bilgisi verilmiyor (K-23).
            return new(StartSessionStatus.NotFound);
        }

        // İptal edilmemiş oturum en fazla bir tane (K-11). Varsa aday ona dönüyor.
        var existing = await db.ExamSessions.AsNoTracking()
            .Where(s => s.ExamId == examId && s.CandidateId == candidateId && s.State != SessionState.Cancelled)
            .Select(s => new { s.Id, s.State, s.StartedAt })
            .SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            if (SessionStateMachine.Closed.Contains(existing.State))
            {
                return new(StartSessionStatus.Denied, existing.Id, existing.State, StartDenial.AttemptUsed);
            }

            // Sınav başlamışsa pencere kuralı artık geçerli değil; süre zaten işliyor.
            if (existing.StartedAt is null && ExamRules.CanStart(exam, assignment, now) is { } late)
            {
                return new(StartSessionStatus.Denied, existing.Id, existing.State, late);
            }

            return new(StartSessionStatus.Resumed, existing.Id, existing.State);
        }

        if (ExamRules.CanStart(exam, assignment, now) is { } denial)
        {
            return new(StartSessionStatus.Denied, Denial: denial);
        }

        // Kayıt fotoğrafı (referans şablon) kontrolü A3'te eklenecek (FR-04, MIMARI-KARARLAR "A3").
        var session = new ExamSession { ExamId = examId, CandidateId = candidateId, CreatedAt = now, LastSeenAt = now };
        db.ExamSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
        {
            // Aynı aday iki sekmeden aynı anda başlattı; diğer istek oturumu açtı.
            db.ExamSessions.Entry(session).State = EntityState.Detached;
            return await StartAsync(examId, candidateId, ct);
        }

        return new(StartSessionStatus.Created, session.Id, session.State);
    }

    public async Task<SessionView?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var session = await db.ExamSessions.Include(s => s.Exam).SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return null;
        }

        await TouchAsync(session, now, ct);

        // Sorular yalnızca sınav sürerken; izin beklerken gizli (FR-17), başlamadan ve bitince yok.
        var questions = session.State == SessionState.InProgress
            ? await db.Questions.AsNoTracking()
                .Where(q => q.ExamId == session.ExamId)
                .OrderBy(q => q.Order).ThenBy(q => q.Id)
                .Select(q => new SessionQuestion(q.Id, q.Text, q.Options, q.Order))
                .ToListAsync(ct)
            : [];

        var answers = await EffectiveAnswersAsync(sessionId, ct);
        var lastSeq = await db.Answers.Where(a => a.ExamSessionId == sessionId).MaxAsync(a => (long?)a.Seq, ct) ?? 0;

        return new SessionView(session.Id, session.ExamId, session.Exam.Name, session.State, session.Exam.DurationMinutes,
            session.StartedAt, session.EndsAt, now, questions, answers, lastSeq);
    }

    /// <summary>
    /// Sıra numaralı cevap kaydı (FR-09). Her gönderim ayrı satır; bir soru için en yüksek seq geçerli,
    /// böylece çevrimdışı kuyruk eski cevabı geç gönderse de yenisinin üzerine yazamıyor.
    /// </summary>
    public async Task<SessionOperationResult> SaveAnswerAsync(Guid sessionId, AnswerRequest request, CancellationToken ct = default)
    {
        if (request.QuestionId is not { } questionId || request.Seq is not ({ } seq and > 0))
        {
            return SessionOperationResult.Invalid("questionId ve pozitif bir seq zorunludur.");
        }

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.ExamSessions.AnyAsync(s => s.Id == sessionId, ct))
        {
            return SessionOperationResult.NotFound;
        }

        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);

        // Süre yalnızca sunucu saatine göre (FR-09); otomatik teslim görevi henüz çalışmamış olsa da reddediliyor.
        if (session.EndsAt is { } endsAt && now > endsAt)
        {
            return SessionOperationResult.Conflict("Sınav süresi doldu; cevap kabul edilmedi.");
        }

        // Bağlantı kesikken verilen cevaplar dönüşte, yeniden doğrulamadan önce gönderiliyor ve işaretleniyor (FR-09, FR-26).
        if (session.State is not (SessionState.InProgress or SessionState.Disconnected))
        {
            return SessionOperationResult.Conflict(session.State == SessionState.PermissionPending
                ? "Kamera ve mikrofon izni verilene kadar cevap kaydedilemez."
                : "Oturum cevap kabul etmiyor.");
        }

        var question = await db.Questions.AsNoTracking()
            .Where(q => q.Id == questionId && q.ExamId == session.ExamId)
            .Select(q => new { q.Options })
            .SingleOrDefaultAsync(ct);
        // Başka sınavın sorusuna cevap yazılamıyor (S-6).
        if (question is null)
        {
            return SessionOperationResult.Invalid("Soru bu sınava ait değil.");
        }

        if (request.SelectedOption is { } option && (option < 0 || option >= question.Options.Count))
        {
            return SessionOperationResult.Invalid("Seçenek geçersiz.");
        }

        var sameSeq = await db.Answers.AsNoTracking()
            .Where(a => a.ExamSessionId == sessionId && a.Seq == seq)
            .Select(a => new { a.QuestionId, a.SelectedOption })
            .SingleOrDefaultAsync(ct);
        if (sameSeq is not null)
        {
            // Aynı istek tekrar geldi (kuyruk, ağ tekrarı): sorun yok. Aynı seq farklı cevapla geldiyse istemci hatası.
            return sameSeq.QuestionId == questionId && sameSeq.SelectedOption == request.SelectedOption
                ? new(SessionOperationStatus.Duplicate)
                : SessionOperationResult.Conflict("Bu sıra numarası başka bir cevap için kullanılmış.");
        }

        db.Answers.Add(new Answer
        {
            ExamSessionId = sessionId,
            QuestionId = questionId,
            SelectedOption = request.SelectedOption,
            Seq = seq,
            ClientTime = request.ClientTime,
            ReceivedAt = now,
            Unmonitored = session.State == SessionState.Disconnected,
        });
        await TouchAsync(session, now, ct, save: false);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return SessionOperationResult.Ok;
    }

    /// <summary>Adayın teslimi. Süre dolmuşsa da teslim kabul ediliyor; bitiş anı süre sonu oluyor.</summary>
    public async Task<SessionOperationResult> SubmitAsync(Guid sessionId, CancellationToken ct = default)
    {
        SessionReadyForReview? readyForReview = null;
        var result = await LockedAsync(sessionId, async (session, now) =>
        {
            if (!SessionStateMachine.Running.Contains(session.State))
            {
                return SessionOperationResult.Conflict(SessionStateMachine.Closed.Contains(session.State)
                    ? "Sınav zaten teslim edildi."
                    : "Sınav henüz başlamadı.");
            }

            await CompleteAsync(events, session, now, ct, monitoring);
            readyForReview = SessionReadyForReview.For(session);
            return SessionOperationResult.Ok;
        }, ct);

        // Teslim kaydedildikten sonra: bildirim gitmese de teslim geçerli (A4).
        if (result.Status == SessionOperationStatus.Success && readyForReview is not null && reviewNotifier is not null)
        {
            await reviewNotifier.SessionReadyForReviewAsync(readyForReview, ct);
        }

        return result;
    }

    /// <summary>
    /// Kamera/mikrofon izni kaldırıldı (S4, FR-17): InProgress → PermissionPending. Sorular gizleniyor, süre işliyor.
    /// İzinsiz süre bu andan sayılıyor; aynı bildirim tekrar gelirse ilk an korunuyor.
    /// </summary>
    public Task<SessionOperationResult> RevokePermissionAsync(Guid sessionId, CancellationToken ct = default) =>
        LockedAsync(sessionId, (session, now) =>
        {
            if (session.State == SessionState.PermissionPending)
            {
                return Task.FromResult(new SessionOperationResult(SessionOperationStatus.Duplicate));
            }

            if (session.State != SessionState.InProgress || (session.EndsAt is { } endsAt && now > endsAt))
            {
                return Task.FromResult(SessionOperationResult.Conflict("Sınav sürmüyor."));
            }

            session.TransitionTo(SessionState.PermissionPending);
            session.PermissionRevokedAt = now;
            session.LastSeenAt = now;
            return Task.FromResult(SessionOperationResult.Ok);
        }, ct);

    /// <summary>
    /// İzin yeniden verildi (S4, FR-17): PermissionPending → InProgress. İzinsiz süre sunucu saatiyle hesaplanıp
    /// PERMISSION_REVOKED olarak yazılıyor; olay oturumu incelemeye işaretliyor (PermissionGap, K-17).
    /// </summary>
    public Task<SessionOperationResult> RestorePermissionAsync(Guid sessionId, CancellationToken ct = default) =>
        LockedAsync(sessionId, async (session, now) =>
        {
            if (session.State == SessionState.InProgress)
            {
                return new SessionOperationResult(SessionOperationStatus.Duplicate);
            }

            if (session.State != SessionState.PermissionPending)
            {
                return SessionOperationResult.Conflict("Oturum izin beklemiyor.");
            }

            // Süre dolduysa sınava dönülmüyor; otomatik teslim izinsiz süreyi süre sonuna kadar yazıyor.
            if (session.EndsAt is { } endsAt && now > endsAt)
            {
                return SessionOperationResult.Conflict("Sınav süresi doldu.");
            }

            await ClosePermissionGapAsync(events, session, now, ct);
            session.TransitionTo(SessionState.InProgress);
            session.LastSeenAt = now;
            return SessionOperationResult.Ok;
        }, ct);

    /// <summary>
    /// Kimlik doğrulama başarılı: sınav başlıyor (IdentityVerification → InProgress). A3'teki /verify ucu
    /// ilk doğrulama başarılı olunca bunu çağırmalı. Süre ve oturum hakkı bu anda başlıyor (FR-09, FR-10).
    /// </summary>
    public Task<SessionOperationResult> BeginExamAsync(Guid sessionId, CancellationToken ct = default) =>
        LockedAsync(sessionId, async (session, now) =>
        {
            var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == session.ExamId, ct);
            session.TransitionTo(SessionState.InProgress);
            session.StartedAt = now;
            session.EndsAt = now.AddMinutes(exam.DurationMinutes);
            session.LastSeenAt = now;

            var assignment = await db.ExamAssignments.SingleAsync(
                a => a.ExamId == session.ExamId && a.UserId == session.CandidateId && a.Role == AssignmentRole.Candidate, ct);
            assignment.AttemptUsedAt = now;
            return SessionOperationResult.Ok;
        }, ct);

    /// <summary>
    /// Bağlantı kesintisinden dönüş, yeniden doğrulama başarılı (Disconnected → InProgress, FR-26). A3'teki
    /// /verify ucu Reconnect doğrulaması başarılı olunca bunu çağırmalı. Kesinti süresi CONNECTION_LOST olarak yazılıyor.
    /// </summary>
    public async Task<SessionOperationResult> ResumeAfterReconnectAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.ExamSessions.AnyAsync(s => s.Id == sessionId, ct))
        {
            return SessionOperationResult.NotFound;
        }

        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);
        var now = clock.GetUtcNow();
        if (session.EndsAt is { } endsAt && now > endsAt)
        {
            return SessionOperationResult.Conflict("Sınav süresi doldu.");
        }

        var since = session.DisconnectedAt ?? session.LastSeenAt ?? now;
        session.TransitionTo(SessionState.InProgress);
        session.DisconnectedAt = null;
        session.LastSeenAt = now;
        await db.SaveChangesAsync(ct);

        // Kesinti süresiyle birlikte yazılıyor; 5 dakikadan uzun kesinti ayrı tür yerine süreyle ağırlaşıyor (S-13).
        await events.RecordSystemEventAsync(sessionId, new SystemEvent(EventType.CONNECTION_LOST, since, (now - since).TotalSeconds), ct);
        await transaction.CommitAsync(ct);
        return SessionOperationResult.Ok;
    }

    /// <summary>
    /// Teslim ya da süre dolması: bitiş anı süre sonunu geçmiyor. Oturum izin beklerken bitiyorsa izinsiz süre
    /// bitiş anına kadar yazılıyor (FR-17). Oturum satırı kilitli ve bir işlem içinde çağrılmalı.
    /// Açık kamera bölümleri de burada, oturum kapanmadan yazılıyor (D4, K-45).
    /// </summary>
    internal static async Task CompleteAsync(
        EventService events, ExamSession session, DateTimeOffset now, CancellationToken ct, ISessionMonitoringCloser? monitoring = null)
    {
        var endedAt = session.EndsAt is { } endsAt && endsAt < now ? endsAt : now;
        if (monitoring is not null)
        {
            await monitoring.CloseSessionAsync(session.Id, ct);
        }

        await ClosePermissionGapAsync(events, session, endedAt, ct);
        session.TransitionTo(SessionState.Completed);
        session.EndedAt = endedAt;
        session.DisconnectedAt = null;
    }

    /// <summary>
    /// İzinsiz süreyi PERMISSION_REVOKED olarak yazar ve başlangıç anını temizler; izin kaldırılmamışsa bir şey yapmaz.
    /// Olay oturum hâlâ sürerken yazılmalı (kapalı oturuma olay eklenmiyor).
    /// </summary>
    private static async Task ClosePermissionGapAsync(EventService events, ExamSession session, DateTimeOffset until, CancellationToken ct)
    {
        if (session.PermissionRevokedAt is not { } since)
        {
            return;
        }

        // Olay süresinin üst sınırı 1 sa (EventService); daha uzun izinsiz süre sınırda yazılıyor, işaret yine konuyor.
        var seconds = Math.Clamp((until - since).TotalSeconds, 0, EventService.MaxDurationSec);
        var result = await events.RecordSystemEventAsync(
            session.Id, new SystemEvent(EventType.PERMISSION_REVOKED, since, seconds), ct);
        if (result.Status != EventRecordStatus.Recorded)
        {
            throw new InvalidOperationException($"İzinsiz süre kaydedilemedi: {result.Status}.");
        }

        session.PermissionRevokedAt = null;
    }

    private async Task<SessionOperationResult> LockedAsync(
        Guid sessionId, Func<ExamSession, DateTimeOffset, Task<SessionOperationResult>> change, CancellationToken ct)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        if (!await db.ExamSessions.AnyAsync(s => s.Id == sessionId, ct))
        {
            return SessionOperationResult.NotFound;
        }

        await db.LockExamSessionAsync(sessionId, ct);
        var session = await db.ExamSessions.FirstAsync(s => s.Id == sessionId, ct);
        var result = await change(session, clock.GetUtcNow());
        if (result.Status == SessionOperationStatus.Success)
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }

        return result;
    }

    /// <summary>
    /// Adaydan istek geldiğini kaydeder; bağlantı kesintisi tespiti buna dayanıyor (<see cref="SessionTimekeeper"/>).
    /// Sınav sürerken çağrılmalı: oturum ucu, cevap ve tarayıcı olayı.
    /// </summary>
    private async Task TouchAsync(ExamSession session, DateTimeOffset now, CancellationToken ct, bool save = true)
    {
        session.LastSeenAt = now;
        if (save)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Aynı anda başka bir işlem oturumu güncelledi; son görülme anı bir sonraki istekte yazılır.
            }
        }
    }

    private async Task<IReadOnlyList<SessionAnswer>> EffectiveAnswersAsync(Guid sessionId, CancellationToken ct)
    {
        var rows = await db.Answers.AsNoTracking()
            .Where(a => a.ExamSessionId == sessionId)
            .Select(a => new { a.QuestionId, a.SelectedOption, a.Seq })
            .ToListAsync(ct);
        return rows.GroupBy(a => a.QuestionId)
            .Select(g => g.MaxBy(a => a.Seq)!)
            .Select(a => new SessionAnswer(a.QuestionId, a.SelectedOption, a.Seq))
            .ToList();
    }
}
