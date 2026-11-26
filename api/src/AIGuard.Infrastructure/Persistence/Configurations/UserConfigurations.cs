using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIGuard.Infrastructure.Persistence.Configurations;

// Tablo ve sütun adları snake_case (NormalizedEmail -> normalized_email);
// check kısıtlarındaki SQL'de de bu adlar kullanılıyor.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.FullName).HasMaxLength(200);
        b.Property(u => u.Email).HasMaxLength(320); // RFC 5321 üst sınırı
        b.Property(u => u.NormalizedEmail).HasMaxLength(320);
        b.Property(u => u.PasswordHash).HasMaxLength(512);
        b.Property(u => u.InviteTokenHash).HasMaxLength(128);
        b.Property(u => u.PasswordResetTokenHash).HasMaxLength(128);

        // Aynı e-postayla iki hesap açılamaz (büyük/küçük harf duyarsız).
        b.HasIndex(u => u.NormalizedEmail).IsUnique();

        // Davet bağlantısı özet üzerinden aranıyor. Boş değerler benzersizlik kuralına takılmıyor.
        b.HasIndex(u => u.InviteTokenHash).IsUnique();
        b.HasIndex(u => u.PasswordResetTokenHash).IsUnique();

        b.ToTable(t => t.HasCheckConstraint("ck_users_failed_login_count", "failed_login_count >= 0"));
    }
}

/// <summary>Platform ayarları (K-49): tek satır; uzunluklar ayar uçlarının doğrulamasıyla aynı.</summary>
internal sealed class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> b)
    {
        b.ToTable("platform_settings");
        b.Property(s => s.AlternativeMethodText).HasMaxLength(PlatformSettings.MaxTextLength);
        b.Property(s => s.SupportEmail).HasMaxLength(320);
        b.Property(s => s.SupportPhone).HasMaxLength(PlatformSettings.MaxPhoneLength);
        b.Property(s => s.AgentDownloadUrlWindows).HasMaxLength(PlatformSettings.MaxUrlLength);
        b.Property(s => s.AgentDownloadUrlMac).HasMaxLength(PlatformSettings.MaxUrlLength);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(t => t.TokenHash).HasMaxLength(128);
        b.HasIndex(t => t.TokenHash).IsUnique();

        // xmin: tek kullanımlık belirteç eşzamanlı iki istekle iki kez yenilenemesin (E2).
        b.Property(t => t.Version).IsRowVersion();

        b.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(a => a.Action).HasMaxLength(100);
        b.Property(a => a.TargetType).HasMaxLength(100);
        b.Property(a => a.OldValue).HasColumnType("jsonb");
        b.Property(a => a.NewValue).HasColumnType("jsonb");

        b.HasIndex(a => a.OccurredAt);
        b.HasIndex(a => new { a.TargetType, a.TargetId });

        // SetNull yerine Restrict: SetNull, kullanıcı silinince denetim satırını güncellerdi.
        b.HasOne(a => a.ActorUser)
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
