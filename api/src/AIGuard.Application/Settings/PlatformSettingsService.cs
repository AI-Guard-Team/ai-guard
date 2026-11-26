using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIGuard.Application.Abstractions;
using AIGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIGuard.Application.Settings;

/// <summary>Platform ayarları (K-49). Boş alan "ayarlanmadı" demek; önyüz o durumda varsayılan metni gösteriyor.</summary>
public sealed record PlatformSettingsView(
    string? AlternativeMethodText,
    string? SupportEmail,
    string? SupportPhone,
    string? AgentDownloadUrlWindows,
    string? AgentDownloadUrlMac,
    DateTimeOffset? UpdatedAt);

public sealed record PlatformSettingsRequest(
    string? AlternativeMethodText,
    string? SupportEmail,
    string? SupportPhone,
    string? AgentDownloadUrlWindows,
    string? AgentDownloadUrlMac);

public sealed record PlatformSettingsResult(bool Success, string? Error = null);

/// <summary>
/// Alternatif sınav yöntemi bilgisi, destek iletişimi ve ajan indirme adresleri (Stitch 17 ve 31). Tek satır;
/// ilk kayıtta oluşuyor. Değişiklik denetim kaydına (eski → yeni) yazılıyor.
/// </summary>
public sealed partial class PlatformSettingsService(IAppDbContext db, TimeProvider clock)
{
    [GeneratedRegex(@"^[0-9 +()\-]{3,40}$")]
    private static partial Regex PhonePattern();

    public async Task<PlatformSettingsView> GetAsync(CancellationToken ct = default)
    {
        var row = await db.PlatformSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        return row is null
            ? new PlatformSettingsView(null, null, null, null, null, null)
            : new PlatformSettingsView(row.AlternativeMethodText, row.SupportEmail, row.SupportPhone, row.AgentDownloadUrlWindows, row.AgentDownloadUrlMac, row.UpdatedAt);
    }

    public async Task<PlatformSettingsResult> UpdateAsync(Guid actorId, PlatformSettingsRequest request, CancellationToken ct = default)
    {
        var text = Clean(request.AlternativeMethodText);
        if (text is { Length: > PlatformSettings.MaxTextLength })
        {
            return new(false, "Alternatif yöntem metni en fazla 4000 karakter olabilir.");
        }

        var email = Clean(request.SupportEmail);
        if (email is not null && (email.Length > 320 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
        {
            return new(false, "Destek e-postası geçerli bir adres olmalıdır.");
        }

        var phone = Clean(request.SupportPhone);
        if (phone is not null && !PhonePattern().IsMatch(phone))
        {
            return new(false, "Destek telefonu en fazla 40 karakter olabilir ve yalnızca rakam, boşluk, +, -, ( ) içerebilir.");
        }

        var windows = Clean(request.AgentDownloadUrlWindows);
        var mac = Clean(request.AgentDownloadUrlMac);
        if (!IsSafeDownloadUrl(windows) || !IsSafeDownloadUrl(mac))
        {
            return new(false, "İndirme adresi https ile başlayan geçerli bir adres olmalıdır.");
        }

        var now = clock.GetUtcNow();
        var row = await db.PlatformSettings.SingleOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, ct);
        var before = row is null ? null : Snapshot(row);
        if (row is null)
        {
            row = new PlatformSettings { Id = PlatformSettings.SingletonId };
            db.PlatformSettings.Add(row);
        }

        row.AlternativeMethodText = text;
        row.SupportEmail = email;
        row.SupportPhone = phone;
        row.AgentDownloadUrlWindows = windows;
        row.AgentDownloadUrlMac = mac;
        row.UpdatedAt = now;
        row.UpdatedById = actorId;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "settings.update",
            TargetType = nameof(PlatformSettings),
            TargetId = row.Id,
            OldValue = before is null ? null : JsonSerializer.Serialize(before),
            NewValue = JsonSerializer.Serialize(Snapshot(row)),
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        return new(true);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Yalnızca https; javascript:, data: gibi şemalar ve göreli adres reddediliyor (bağlantı adaya tıklatılıyor).</summary>
    private static bool IsSafeDownloadUrl(string? value) =>
        value is null
        || (value.Length <= PlatformSettings.MaxUrlLength
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrEmpty(uri.Host));

    // Metnin tamamı yerine uzunluğu: denetim kaydı şişmesin, içerik zaten ayarda duruyor.
    private static object Snapshot(PlatformSettings s) => new
    {
        alternativeMethodTextLength = s.AlternativeMethodText?.Length ?? 0,
        supportEmail = s.SupportEmail,
        supportPhone = s.SupportPhone,
        agentDownloadUrlWindows = s.AgentDownloadUrlWindows,
        agentDownloadUrlMac = s.AgentDownloadUrlMac,
    };
}
