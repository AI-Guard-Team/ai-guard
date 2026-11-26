using AIGuard.Domain.Common;

namespace AIGuard.Domain.Entities;

/// <summary>
/// Platform geneli ayarlar (K-49); tek satır (<see cref="SingletonId"/>). Alternatif sınav yöntemi bilgisi, destek
/// iletişimi ve güvenlik ajanının indirme adresleri. Boş alan önyüzde varsayılan metinle gösteriliyor.
/// </summary>
public class PlatformSettings : Entity
{
    public static readonly Guid SingletonId = new("6f1f7a5e-0b1c-4e7a-9c3d-5a1f2e8b9c01");

    public const int MaxTextLength = 4000;
    public const int MaxPhoneLength = 40;
    public const int MaxUrlLength = 500;

    /// <summary>Rızayı reddeden adaya gösterilen alternatif yöntem açıklaması (Stitch 17).</summary>
    public string? AlternativeMethodText { get; set; }

    public string? SupportEmail { get; set; }

    public string? SupportPhone { get; set; }

    public string? AgentDownloadUrlWindows { get; set; }

    public string? AgentDownloadUrlMac { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }
}
