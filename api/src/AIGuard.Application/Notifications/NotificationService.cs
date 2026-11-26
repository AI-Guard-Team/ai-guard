using AIGuard.Application.Abstractions;
using AIGuard.Application.Exams;
using AIGuard.Application.Review;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Notifications;

public enum NotificationKind
{
    /// <summary>Değerlendirici: dosya kararınızı bekliyor.</summary>
    SessionAwaitingReview,

    /// <summary>Yönetici: sınavda karar bekleyen dosyalar (sayı).</summary>
    ExamAwaitingReview,

    /// <summary>Yönetici: yaklaşan ya da açık sınavda soru yok.</summary>
    ExamWithoutQuestions,

    /// <summary>Yönetici: yaklaşan ya da açık sınava aday atanmamış.</summary>
    ExamWithoutCandidates,

    /// <summary>Yönetici: süresi dolmuş davetler (sayı).</summary>
    InvitesExpired,

    /// <summary>Aday: sınav 24 saat içinde açılıyor.</summary>
    ExamOpensSoon,

    /// <summary>Aday: sınav şu an başlatılabilir.</summary>
    ExamOpenNow,

    /// <summary>Aday: yaklaşan sınav var ama kayıt fotoğrafı yok.</summary>
    PhotoMissing,
}

/// <summary>
/// Tek bildirim. Metin önyüzde türe ve alanlara göre iki dilde kuruluyor; sunucu yalnızca veri veriyor.
/// <paramref name="Id"/> kararlı: aynı durum her istekte aynı kimlikle gelir, önyüz "okundu"yu bununla tutar.
/// </summary>
public sealed record NotificationItem(
    string Id,
    NotificationKind Kind,
    DateTimeOffset At,
    Guid? ExamId = null,
    string? ExamName = null,
    Guid? SessionId = null,
    string? CandidateName = null,
    int? Count = null);

/// <summary>
/// Bildirimler (K-49, kabuktaki zil). Ayrı tablo yok: her istekte mevcut veriden türetiliyor, böylece bildirim
/// verinin kendisiyle çelişemiyor (karar verilen dosya bildirimden kendiliğinden düşer). Okundu bilgisi tarayıcıda.
/// </summary>
public sealed class NotificationService(IAppDbContext db, TimeProvider clock)
{
    private const int MaxItems = 20;
    private static readonly TimeSpan Soon = TimeSpan.FromHours(24);
    private static readonly TimeSpan AdminHorizon = TimeSpan.FromHours(72);

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(Guid userId, UserRole role, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var items = role switch
        {
            UserRole.Reviewer => await ForReviewerAsync(userId, ct),
            UserRole.Admin => await ForAdminAsync(now, ct),
            _ => await ForCandidateAsync(userId, now, ct),
        };
        return [.. items.OrderByDescending(i => i.At).Take(MaxItems)];
    }

    private async Task<List<NotificationItem>> ForReviewerAsync(Guid userId, CancellationToken ct)
    {
        var examIds = db.ExamAssignments.Where(a => a.UserId == userId && a.Role == AssignmentRole.Reviewer).Select(a => a.ExamId);
        var waiting = await db.ExamSessions.AsNoTracking()
            .Where(s => examIds.Contains(s.ExamId) && s.State == SessionState.Completed && s.Flags != SessionFlags.None)
            .OrderByDescending(s => s.EndedAt)
            .Take(MaxItems)
            .Select(s => new { s.Id, s.ExamId, ExamName = s.Exam.Name, Candidate = s.Candidate.FullName, s.EndedAt, s.CreatedAt })
            .ToListAsync(ct);

        return [.. waiting.Select(s => new NotificationItem(
            $"review:{s.Id}", NotificationKind.SessionAwaitingReview, s.EndedAt ?? s.CreatedAt, s.ExamId, s.ExamName, s.Id, s.Candidate))];
    }

    private async Task<List<NotificationItem>> ForAdminAsync(DateTimeOffset now, CancellationToken ct)
    {
        var items = new List<NotificationItem>();
        var horizon = now + AdminHorizon;
        var exams = await db.Exams.AsNoTracking()
            .Where(e => e.WindowEndAt > now && e.WindowStartAt < horizon)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.WindowStartAt,
                Questions = e.Questions.Count,
                Candidates = e.Assignments.Count(a => a.Role == AssignmentRole.Candidate),
            })
            .ToListAsync(ct);

        foreach (var e in exams)
        {
            if (e.Questions == 0)
            {
                items.Add(new($"noquestions:{e.Id}", NotificationKind.ExamWithoutQuestions, e.WindowStartAt, e.Id, e.Name));
            }

            if (e.Candidates == 0)
            {
                items.Add(new($"nocandidates:{e.Id}", NotificationKind.ExamWithoutCandidates, e.WindowStartAt, e.Id, e.Name));
            }
        }

        var waiting = await db.ExamSessions.AsNoTracking()
            .Where(s => s.State == SessionState.Completed && s.Flags != SessionFlags.None)
            .GroupBy(s => new { s.ExamId, s.Exam.Name })
            .Select(g => new { g.Key.ExamId, g.Key.Name, Count = g.Count(), Latest = g.Max(s => s.EndedAt) })
            .ToListAsync(ct);
        items.AddRange(waiting.Select(w => new NotificationItem(
            $"examreview:{w.ExamId}:{w.Count}", NotificationKind.ExamAwaitingReview, w.Latest ?? now, w.ExamId, w.Name, Count: w.Count)));

        var expired = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.InviteAcceptedAt == null && u.InviteExpiresAt != null && u.InviteExpiresAt <= now)
            .Select(u => u.InviteExpiresAt!.Value)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            items.Add(new($"invites:{expired.Count}", NotificationKind.InvitesExpired, expired.Max(), Count: expired.Count));
        }

        return items;
    }

    private async Task<List<NotificationItem>> ForCandidateAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var items = new List<NotificationItem>();
        var exams = await db.ExamAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Role == AssignmentRole.Candidate && a.AttemptUsedAt == null && a.Exam.WindowEndAt > now)
            .Select(a => a.Exam)
            .ToListAsync(ct);
        var startedExamIds = await db.ExamSessions.AsNoTracking()
            .Where(s => s.CandidateId == userId && s.StartedAt != null)
            .Select(s => s.ExamId)
            .ToListAsync(ct);

        var pending = exams.Where(e => !startedExamIds.Contains(e.Id)).ToList();
        foreach (var exam in pending)
        {
            if (exam.WindowStartAt > now && exam.WindowStartAt - now <= Soon)
            {
                items.Add(new($"opens:{exam.Id}", NotificationKind.ExamOpensSoon, exam.WindowStartAt, exam.Id, exam.Name));
            }
            else if (exam.WindowStartAt <= now && now <= ExamRules.LatestStartAt(exam))
            {
                items.Add(new($"open:{exam.Id}", NotificationKind.ExamOpenNow, exam.WindowStartAt, exam.Id, exam.Name));
            }
        }

        var enrolled = await db.ReferenceFaceTemplates.AnyAsync(t => t.UserId == userId, ct);
        if (!enrolled && pending.Count > 0)
        {
            items.Add(new("photo", NotificationKind.PhotoMissing, pending.Min(e => e.WindowStartAt), Count: pending.Count));
        }

        return items;
    }
}
