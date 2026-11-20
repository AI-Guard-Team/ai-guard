using Microsoft.Extensions.Options;

namespace AIGuard.Application.Biometrics;

/// <summary>Kayıt fotoğrafı ve sınav başı doğrulama ayarları (appsettings.json, "Biometrics").</summary>
public sealed class BiometricOptions
{
    public const string SectionName = "Biometrics";

    // Canlılık ve benzerlik eşikleri burada değil, görüntü servisinin config.yaml dosyasında (S-19);
    // eşik tek yerde dursun, D2/D6'daki ayar orada yapılsın.

    /// <summary>Aynı oturumda iki doğrulama denemesi arasındaki en kısa süre.</summary>
    public int VerificationCooldownSeconds { get; set; } = 5;

    /// <summary>Adayın onayladığı biyometrik rıza metninin güncel sürümü (A5'te metinlerle birlikte yönetilecek).</summary>
    public string BiometricConsentTextVersion { get; set; } = "kvkk-biometric-v1";

    public string SessionConsentTextVersion { get; set; } = "kvkk-session-v1";
}

public sealed class BiometricOptionsValidator : IValidateOptions<BiometricOptions>
{
    public ValidateOptionsResult Validate(string? name, BiometricOptions o)
    {
        var errors = new List<string>();

        if (o.VerificationCooldownSeconds < 0)
        {
            errors.Add("Biometrics:VerificationCooldownSeconds negatif olamaz.");
        }

        if (string.IsNullOrWhiteSpace(o.BiometricConsentTextVersion) || string.IsNullOrWhiteSpace(o.SessionConsentTextVersion))
        {
            errors.Add("Rıza metni sürümleri boş olamaz.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
