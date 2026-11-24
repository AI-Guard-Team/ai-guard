using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AIGuard.Application.Abstractions;

/// <summary>
/// Servislerin veritabanına eriştiği arayüz; AppDbContext bunu uyguluyor.
/// Böylece Application katmanı Infrastructure'a bağlanmadan sorgu yazabiliyor (K-14).
/// Yeni bir varlık eklenince DbSet buraya da eklenmeli.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Exam> Exams { get; }
    DbSet<Question> Questions { get; }
    DbSet<ExamAssignment> ExamAssignments { get; }
    DbSet<ExamSession> ExamSessions { get; }
    DbSet<Answer> Answers { get; }
    DbSet<ConsentRecord> ConsentRecords { get; }
    DbSet<ReferenceFaceTemplate> ReferenceFaceTemplates { get; }
    DbSet<Verification> Verifications { get; }
    DbSet<Event> Events { get; }
    DbSet<EvidenceFrame> EvidenceFrames { get; }
    DbSet<AgentPairing> AgentPairings { get; }
    DbSet<AgentSignal> AgentSignals { get; }
    DbSet<RiskSetting> RiskSettings { get; }
    DbSet<ReviewerDecision> ReviewerDecisions { get; }
    DbSet<ReviewerNote> ReviewerNotes { get; }
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>İşlem (transaction) başlatmak için.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Oturum satırını açık işlem bitene kadar kilitliyor (SELECT … FOR UPDATE) ve izlenen
    /// kopyayı veritabanındaki güncel hâliyle yeniliyor. Aynı oturuma eşzamanlı yazanları
    /// sıraya sokmak için; çağıran açık bir işlem içinde olmalı.
    /// </summary>
    Task LockExamSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hata bir benzersizlik kısıtı ihlalinden mi geliyor (PostgreSQL 23505, K-11). Servisler
    /// eşzamanlı iki isteğin ikincisini anlamlı bir yanıta (ör. 409) çevirebilsin diye; Npgsql'e
    /// bağlı kontrol Infrastructure'da kalıyor (K-14).
    /// </summary>
    bool IsUniqueViolation(DbUpdateException exception);
}
