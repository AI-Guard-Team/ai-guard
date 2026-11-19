using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamAssignment> ExamAssignments => Set<ExamAssignment>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<ReferenceFaceTemplate> ReferenceFaceTemplates => Set<ReferenceFaceTemplate>();
    public DbSet<Verification> Verifications => Set<Verification>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EvidenceFrame> EvidenceFrames => Set<EvidenceFrame>();
    public DbSet<AgentPairing> AgentPairings => Set<AgentPairing>();
    public DbSet<AgentSignal> AgentSignals => Set<AgentSignal>();
    public DbSet<RiskSetting> RiskSettings => Set<RiskSetting>();
    public DbSet<ReviewerDecision> ReviewerDecisions => Set<ReviewerDecision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public async Task LockExamSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Oturum kilidi yalnızca açık bir işlem içinde alınabilir.");
        }

        await Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM exam_sessions WHERE id = {sessionId} FOR UPDATE", cancellationToken);

        // Kilit alınmadan önce okunmuş bir kopya varsa güncel değerlerle yeniliyoruz;
        // yoksa eski xmin ile yazmaya çalışıp çakışma hatası alırdık.
        var tracked = ChangeTracker.Entries<ExamSession>().FirstOrDefault(e => e.Entity.Id == sessionId);
        if (tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enum'lar metin olarak saklanıyor (K-04). SessionFlags bilerek yok, sayı olarak kalıyor.
        configurationBuilder.Properties<SessionState>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<EventType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ExamType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<AssignmentRole>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<EventSource>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ConsentType>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<VerificationKind>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ReviewDecisionType>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<AgentPlatform>().HaveConversion<string>().HaveMaxLength(16);
    }
}
