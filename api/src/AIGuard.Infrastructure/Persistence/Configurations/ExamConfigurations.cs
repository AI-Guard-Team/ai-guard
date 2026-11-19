using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIGuard.Infrastructure.Persistence.Configurations;

internal sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> b)
    {
        b.Property(e => e.Name).HasMaxLength(200);

        // Uygulamadaki doğrulamaya ek olarak veritabanında da kontrol ediliyor (K-11).
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_exams_duration_positive", "duration_minutes > 0");
            t.HasCheckConstraint("ck_exams_window_order", "window_end_at > window_start_at");
            t.HasCheckConstraint("ck_exams_retention_positive", "retention_days > 0");
        });
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.HasOne(q => q.Exam)
            .WithMany(e => e.Questions)
            .HasForeignKey(q => q.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Benzersiz değil; sıralama değişirken iki soru kısa süre aynı sırada olabilir.
        b.HasIndex(q => new { q.ExamId, q.Order });

        // En az iki seçenek olmalı ve doğru seçenek bunlardan biri olmalı.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_questions_min_two_options", "cardinality(options) >= 2");
            t.HasCheckConstraint(
                "ck_questions_correct_option_in_range",
                "correct_option_index >= 0 AND correct_option_index < cardinality(options)");
        });
    }
}

internal sealed class ExamAssignmentConfiguration : IEntityTypeConfiguration<ExamAssignment>
{
    public void Configure(EntityTypeBuilder<ExamAssignment> b)
    {
        b.HasOne(a => a.Exam)
            .WithMany(e => e.Assignments)
            .HasForeignKey(a => a.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.User)
            .WithMany(u => u.Assignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bir kullanıcı bir sınava bir kez atanır.
        b.HasIndex(a => new { a.ExamId, a.UserId }).IsUnique();
    }
}
