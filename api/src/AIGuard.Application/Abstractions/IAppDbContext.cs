using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
