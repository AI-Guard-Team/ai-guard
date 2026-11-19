using Microsoft.Extensions.Options;

namespace AIGuard.Agent.Core;

/// <summary>Ajan ayarları (appsettings.json, "Agent" bölümü).</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Tarayıcının eşleştirme kodunu gönderdiği port. Web önyüzü bu değeri sabit biliyor.</summary>
    public const int DefaultPort = 47321;

    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Yerel uca istek gönderebilecek sitelerin origin'leri (ör. https://sinav.ornek.edu.tr).
    /// Yalnızca platformun kendi alan adı olmalı; başka bir site ajana kod gönderememeli.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>Sunucunun kök adresi; eşleştirme kodu {ApiBaseUrl}/api/agent/pair adresine gidiyor.</summary>
    public Uri? ApiBaseUrl { get; set; }
}

internal sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        var errors = new List<string>();

        // 0 yalnızca testlerde: işletim sistemi boş bir port seçiyor.
        if (options.Port is < 0 or > 65535)
        {
            errors.Add("Agent:Port 0–65535 aralığında olmalı.");
        }

        if (options.ApiBaseUrl is not { IsAbsoluteUri: true } api)
        {
            errors.Add("Agent:ApiBaseUrl tanımlı ve mutlak bir adres olmalı.");
        }
        else if (!IsTls(api) && !(api.Scheme == Uri.UriSchemeHttp && api.IsLoopback))
        {
            // Kod ağ üzerinden açık metin giderse yolda okunup başka bir ajan tarafından kullanılabilir.
            // Yalnızca aynı bilgisayardaki geliştirme API'sine (localhost) http izni var; o trafik ağa çıkmıyor.
            errors.Add("Agent:ApiBaseUrl https olmalı (http yalnızca localhost için).");
        }

        if (options.AllowedOrigins.Count == 0)
        {
            errors.Add("Agent:AllowedOrigins en az bir origin içermeli.");
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (LocalRequestGuard.NormalizeOrigin(origin) is null)
            {
                errors.Add($"Agent:AllowedOrigins içindeki '{origin}' geçerli bir origin değil (ör. https://sinav.ornek.edu.tr, yol ve * olmadan).");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsTls(Uri uri) => uri.Scheme == Uri.UriSchemeHttps;
}
