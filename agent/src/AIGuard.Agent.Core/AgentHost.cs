using System.Net;
using AIGuard.Agent.Core.Pairing;
using AIGuard.Agent.Core.Signals;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.Agent.Core;

/// <summary>
/// Windows ve macOS ajanlarının ortak başlangıcı. Platform projesi yalnızca kendi
/// <see cref="Platform.IDeviceInspector"/> uygulamasını kaydediyor.
/// </summary>
public static class AgentHost
{
    /// <summary>Gövde yalnızca { "code": "..." }; büyük gövdeyle ajanı yorma girişimine karşı.</summary>
    public const long MaxRequestBodyBytes = 1024;

    public static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Kurulum paketi oluşturulurken yazılan ayar dosyası (agent/scripts/publish-windows.ps1).</summary>
    public const string DeploymentSettingsFile = "deployment.json";

    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        // Ayar dosyaları exe'nin yanında (tek dosyalık pakette exe'nin açıldığı klasörde) aranıyor; aday exe'yi
        // İndirilenler'den çift tıklayıp açtığında çalışma klasörü farklı olabilir (Y4).
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

        // Kurulum paketine gömülen kurum ayarları (sunucu adresi, sınav sitesi); appsettings.json'ın üzerine yazar (Y4).
        builder.Configuration.AddJsonFile(DeploymentSettingsFile, optional: true, reloadOnChange: false);

        builder.Services.AddOptions<AgentOptions>()
            .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
            .ValidateOnStart(); // ayar hatalıysa ajan açılmıyor; yanlış adrese kod gönderilmesin
        builder.Services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();

        builder.Services.AddSingleton<PairingState>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Y2: eşleşme kurulunca belirli aralıkla imzalı sinyal.
        builder.Services.AddHttpClient<ISignalClient, HttpSignalClient>(ConfigureApiClient);
        builder.Services.AddHostedService<SignalSender>();
        builder.Services.AddHttpClient<IPairingClient, HttpPairingClient>(ConfigureApiClient);

        builder.WebHost.ConfigureKestrel((context, kestrel) =>
        {
            var port = context.Configuration.GetValue($"{AgentOptions.SectionName}:{nameof(AgentOptions.Port)}", AgentOptions.DefaultPort);

            // Yalnızca loopback (127.0.0.1): ağdaki başka bir bilgisayar bu porta bağlanamaz.
            // Kod tarayıcıdan ajana yalnızca bu yoldan geldiği için "aynı bilgisayar" kanıtı buna dayanıyor.
            // ASPNETCORE_URLS gibi ayarlar bu satırla geçersiz kılınıyor.
            kestrel.Listen(IPAddress.Loopback, port);
            kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
            kestrel.AddServerHeader = false;
        });

        return builder;
    }

    // Eşleştirme ve sinyal aynı sunucuya, aynı TLS kuralıyla gidiyor (https zorunluluğu AgentOptionsValidator'da).
    private static void ConfigureApiClient(IServiceProvider sp, HttpClient http)
    {
        var api = sp.GetRequiredService<IOptions<AgentOptions>>().Value.ApiBaseUrl!;
        // Sondaki "/" olmazsa göreli adres son yol parçasının yerine geçer (…/aiguard → …/api).
        http.BaseAddress = new Uri(api.AbsoluteUri.TrimEnd('/') + "/");
        http.Timeout = ServerTimeout;
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.UseMiddleware<LocalRequestGuard>();
        app.MapPost("/pair", PairingEndpoint.HandleAsync);

        return app;
    }
}
