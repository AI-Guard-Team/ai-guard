using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIGuard.Infrastructure.Persistence.Configurations;

internal sealed class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> b)
    {
        // Oturumu olan sınav silinemiyor; oturumlar rapor ve itiraz için gerekli.
        b.HasOne(s => s.Exam)
            .WithMany(e => e.Sessions)
            .HasForeignKey(s => s.ExamId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(s => s.Candidate)
            .WithMany()
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Aday başına tek oturum; iptal edilen oturum hakkı tüketmiyor (FR-10).
        b.HasIndex(s => new { s.ExamId, s.CandidateId })
            .IsUnique()
            .HasFilter("state <> 'Cancelled'");

        // Otomatik teslim görevi süresi dolan InProgress oturumları bu indeksle arıyor (E4).
        b.HasIndex(s => new { s.State, s.EndsAt });

        b.Property(s => s.ContinueJustification).HasMaxLength(2000);

        // Npgsql'de uint + IsRowVersion, PostgreSQL'in xmin sütununa eşleniyor.
        b.Property(s => s.Version).IsRowVersion();

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_exam_sessions_risk_score_non_negative", "risk_score >= 0");
            t.HasCheckConstraint("ck_exam_sessions_verification_attempts_non_negative", "verification_attempts >= 0");
        });
    }
}

internal sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> b)
    {
        b.HasOne(a => a.ExamSession)
            .WithMany(s => s.Answers)
            .HasForeignKey(a => a.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cevaplanmış soru silinemiyor.
        b.HasOne(a => a.Question)
            .WithMany()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Çevrimdışı kuyruk aynı cevabı tekrar gönderirse ikinci kayıt oluşmasın.
        b.HasIndex(a => new { a.ExamSessionId, a.Seq }).IsUnique();

        // "Her soru için en yüksek seq" sorgusu için.
        b.HasIndex(a => new { a.ExamSessionId, a.QuestionId });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_answers_seq_non_negative", "seq >= 0");
            t.HasCheckConstraint("ck_answers_selected_option_non_negative", "selected_option IS NULL OR selected_option >= 0");
        });
    }
}

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> b)
    {
        b.Property(c => c.TextVersion).HasMaxLength(50);

        // Rıza kayıtları KVKK açısından ispat; başka bir kayıtla birlikte otomatik silinmemeli.
        b.HasOne(c => c.User)
            .WithMany(u => u.ConsentRecords)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(c => c.ExamSession)
            .WithMany(s => s.ConsentRecords)
            .HasForeignKey(c => c.ExamSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(c => new { c.UserId, c.Type });

        // Oturum rızası bir oturuma bağlı olmalı, biyometrik rıza olmamalı.
        b.ToTable(t => t.HasCheckConstraint(
            "ck_consent_records_session_matches_type",
            "(type = 'Session' AND exam_session_id IS NOT NULL) OR (type = 'Biometric' AND exam_session_id IS NULL)"));
    }
}

internal sealed class ReferenceFaceTemplateConfiguration : IEntityTypeConfiguration<ReferenceFaceTemplate>
{
    public void Configure(EntityTypeBuilder<ReferenceFaceTemplate> b)
    {
        // Aday başına tek şablon; kullanıcı silinirse şablon da siliniyor.
        b.HasOne(r => r.User)
            .WithOne(u => u.ReferenceFaceTemplate)
            .HasForeignKey<ReferenceFaceTemplate>(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.ConsentRecord)
            .WithMany()
            .HasForeignKey(r => r.ConsentRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        // 512 boyutlu vektör şifreli hâliyle ~2 KB. Sınır, buraya yanlışlıkla fotoğraf
        // gibi büyük bir verinin yazılmasını engelliyor.
        b.ToTable(t => t.HasCheckConstraint(
            "ck_reference_face_templates_vector_size",
            "octet_length(encrypted_vector) BETWEEN 1 AND 8192"));
    }
}

internal sealed class VerificationConfiguration : IEntityTypeConfiguration<Verification>
{
    public void Configure(EntityTypeBuilder<Verification> b)
    {
        b.HasOne(v => v.ExamSession)
            .WithMany(s => s.Verifications)
            .HasForeignKey(v => v.ExamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(v => new { v.ExamSessionId, v.CreatedAt });
    }
}

internal sealed class ConsentTextConfiguration : IEntityTypeConfiguration<ConsentText>
{
    // İlk sürümlerin yayım anı: A3'te rıza akışının devreye girdiği gün.
    private static readonly DateTimeOffset InitialPublishedAt = new(2026, 11, 23, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<ConsentText> b)
    {
        b.Property(t => t.Version).HasMaxLength(50);
        b.Property(t => t.Title).HasMaxLength(200);

        // Aynı sürüm adı iki kez kullanılamaz; rıza kaydındaki sürüm tek bir metni gösteriyor.
        b.HasIndex(t => t.Version).IsUnique();
        b.HasIndex(t => new { t.Type, t.PublishedAt });

        // İlk metinler göçle geliyor; sonraki sürümler yeni göçle eklenmeli (K-32).
        b.HasData(
            new ConsentText
            {
                Id = new Guid("6f1e2d0a-5b7c-4c39-9a51-0c2f6d8e1a01"),
                Type = ConsentType.Biometric,
                Version = ConsentText.InitialBiometricVersion,
                Title = "Biyometrik veri aydınlatma metni ve açık rıza",
                Body = ConsentTextBodies.BiometricV1,
                PublishedAt = InitialPublishedAt,
            },
            new ConsentText
            {
                Id = new Guid("6f1e2d0a-5b7c-4c39-9a51-0c2f6d8e1a02"),
                Type = ConsentType.Session,
                Version = ConsentText.InitialSessionVersion,
                Title = "Sınav izleme aydınlatma metni ve açık rıza",
                Body = ConsentTextBodies.SessionV1,
                PublishedAt = InitialPublishedAt,
            });
    }
}
