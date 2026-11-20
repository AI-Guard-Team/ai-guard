using System.Net;
using AIGuard.Agent.Core.Pairing;
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

    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOptions<AgentOptions>()
            .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
            .ValidateOnStart(); // ayar hatalıysa ajan açılmıyor; yanlış adrese kod gönderilmesin
        builder.Services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();

        builder.Services.AddSingleton<PairingState>();
        builder.Services.AddHttpClient<IPairingClient, HttpPairingClient>((sp, http) =>
        {
            var api = sp.GetRequiredService<IOptions<AgentOptions>>().Value.ApiBaseUrl!;
            // Sondaki "/" olmazsa göreli adres son yol parçasının yerine geçer (…/aiguard → …/api).
            http.BaseAddress = new Uri(api.AbsoluteUri.TrimEnd('/') + "/");
            http.Timeout = ServerTimeout;
        });

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

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.UseMiddleware<LocalRequestGuard>();
        app.MapPost("/pair", PairingEndpoint.HandleAsync);

        return app;
    }
}
