using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIGuard.Infrastructure.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.Property(e => e.Payload).HasColumnType("jsonb");
        b.Property(e => e.Reason).HasMaxLength(1000);

        b.HasOne(e => e.ExamSession)
            .WithMany(s => s.Events)
            .HasForeignKey(e => e.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(e => e.FalseAlarmMarkedBy)
            .WithMany()
            .HasForeignKey(e => e.FalseAlarmMarkedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Zaman çizelgesi ve risk hesabı oturumun olaylarını zamana göre okur.
        b.HasIndex(e => new { e.ExamSessionId, e.StartedAt });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_events_duration_non_negative", "duration_sec >= 0");
            t.HasCheckConstraint("ck_events_confidence_range", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
            // Yanlış alarm işaretlendiyse kimin ve ne zaman işaretlediği zorunludur (A4).
            t.HasCheckConstraint(
                "ck_events_false_alarm_marker",
                "is_false_alarm = false OR (false_alarm_marked_by_id IS NOT NULL AND false_alarm_marked_at IS NOT NULL)");
        });
    }
}

internal sealed class EvidenceFrameConfiguration : IEntityTypeConfiguration<EvidenceFrame>
{
    public void Configure(EntityTypeBuilder<EvidenceFrame> b)
    {
        b.Property(f => f.StorageKey).HasMaxLength(300);
        b.HasIndex(f => f.StorageKey).IsUnique();

        // Olay silinince kayıt da siliniyor; dosyayı A5'teki silme servisi kaldırıyor.
        b.HasOne(f => f.Event)
            .WithOne(e => e.EvidenceFrame)
            .HasForeignKey<EvidenceFrame>(f => f.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // A5'teki silme görevi süresi dolan kayıtları bu sütunla buluyor.
        b.HasIndex(f => f.DeleteAfter);
    }
}

internal sealed class RiskSettingConfiguration : IEntityTypeConfiguration<RiskSetting>
{
    public void Configure(EntityTypeBuilder<RiskSetting> b)
    {
        b.HasOne(r => r.Exam)
            .WithMany()
            .HasForeignKey(r => r.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // NULLS NOT DISTINCT: sistem geneli ayarlarda (ExamId boş) da olay türü başına tek satır.
        b.HasIndex(r => new { r.ExamId, r.EventType }).IsUnique().AreNullsDistinct(false);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_risk_settings_weight_non_negative", "weight >= 0");
            t.HasCheckConstraint("ck_risk_settings_decay_non_negative", "decay >= 0");
            t.HasCheckConstraint("ck_risk_settings_threshold_non_negative", "threshold >= 0");
        });
    }
}

internal sealed class AgentPairingConfiguration : IEntityTypeConfiguration<AgentPairing>
{
    public void Configure(EntityTypeBuilder<AgentPairing> b)
    {
        b.Property(p => p.PairingCodeHash).HasMaxLength(128);
        b.HasIndex(p => p.PairingCodeHash).IsUnique();

        b.HasOne(p => p.ExamSession)
            .WithOne(s => s.AgentPairing)
            .HasForeignKey<AgentPairing>(p => p.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AgentSignalConfiguration : IEntityTypeConfiguration<AgentSignal>
{
    public void Configure(EntityTypeBuilder<AgentSignal> b)
    {
        b.Property(s => s.Nonce).HasMaxLength(128);
        b.Property(s => s.Indicators).HasColumnType("jsonb");

        b.HasOne(s => s.AgentPairing)
            .WithMany(p => p.Signals)
            .HasForeignKey(s => s.AgentPairingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Aynı nonce ile gelen sinyal ikinci kez kaydedilemiyor (tekrar gönderime karşı).
        b.HasIndex(s => new { s.AgentPairingId, s.Nonce }).IsUnique();
    }
}

internal sealed class ReviewerDecisionConfiguration : IEntityTypeConfiguration<ReviewerDecision>
{
    public void Configure(EntityTypeBuilder<ReviewerDecision> b)
    {
        b.Property(d => d.Note).HasMaxLength(4000);

        b.HasOne(d => d.ExamSession)
            .WithOne(s => s.ReviewerDecision)
            .HasForeignKey<ReviewerDecision>(d => d.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(d => d.Reviewer)
            .WithMany()
            .HasForeignKey(d => d.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
