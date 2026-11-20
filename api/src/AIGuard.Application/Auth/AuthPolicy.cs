namespace AIGuard.Application.Auth;

/// <summary>Kimlik doğrulama süreleri ve sınırları (E2, FR-01–FR-02). Kararlar: MIMARI-KARARLAR K-20.</summary>
public static class AuthPolicy
{
    /// <summary>Erişim belirteci (JWT) ömrü; kartta 15 dakika.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Yenileme belirteci ömrü. Kartta süre yok; 7 gün seçildi. Belirteç her kullanımda
    /// yenilendiği için etkin kullanıcı oturumu bu süreyle kesilmiyor, yalnızca 7 gün hiç
    /// kullanılmayan oturum kapanıyor.
    /// </summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    /// <summary>Davet bağlantısı ömrü; kartta örnek olarak 72 saat.</summary>
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromHours(72);

    /// <summary>Bu sayıda art arda hatalı girişte hesap geçici olarak kilitleniyor (kart: 5).</summary>
    public const int MaxFailedLogins = 5;

    /// <summary>Geçici kilit süresi. Kartta süre yok; kaba kuvveti yavaşlatıp gerçek kullanıcıyı uzun bekletmeyecek kadar.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // NIST SP 800-63B: en az 8 karakter; karmaşıklık kuralı yerine uzunluk.
    public const int MinPasswordLength = 8;

    // PBKDF2'ye çok uzun girdi verip sunucuyu yorma girişimine karşı üst sınır.
    public const int MaxPasswordLength = 128;

    /// <summary>Şifre kurala uymuyorsa adaya gösterilecek Türkçe mesaj, uyuyorsa null.</summary>
    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return $"Şifre en az {MinPasswordLength} karakter olmalıdır.";
        }

        if (password.Length > MaxPasswordLength)
        {
            return $"Şifre en fazla {MaxPasswordLength} karakter olabilir.";
        }

        // Yalnızca boşluktan oluşan şifre uzunluk kuralını anlamsız kılar.
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Şifre yalnızca boşluktan oluşamaz.";
        }

        return null;
    }
}
