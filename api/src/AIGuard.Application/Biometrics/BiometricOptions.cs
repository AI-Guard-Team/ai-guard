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

    // Rıza metinlerinin güncel sürümü A5'ten beri ayarlarda değil, consent_texts tablosunda (K-32).
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

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
