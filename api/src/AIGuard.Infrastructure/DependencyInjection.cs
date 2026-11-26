using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using AIGuard.Infrastructure.Email;
using AIGuard.Infrastructure.Persistence;
using AIGuard.Infrastructure.Persistence.Interceptors;
using AIGuard.Infrastructure.Persistence.Seed;
using AIGuard.Infrastructure.Reports;
using AIGuard.Infrastructure.Security;
using AIGuard.Infrastructure.Storage;
using AIGuard.Infrastructure.Vision;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Koda gömülü varsayılan bağlantı yok; ayar eksikse uygulama açılışta duruyor.
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default tanımlı değil. ConnectionStrings__Default ortam değişkeni " +
                "veya dotnet user-secrets ile ayarlayın (bkz. README).");
        }

        services.AddSingleton<AppendOnlyInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
            ConfigureDbContext(options, connectionString)
                .AddInterceptors(sp.GetRequiredService<AppendOnlyInterceptor>()));

        // Servisler arayüzü istediğinde aynı scope'taki AppDbContext örneği dönüyor.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // Y2: oturum anahtarları süreç boyunca tek depoda (K-21).
        services.AddSingleton<IAgentKeyStore, InMemoryAgentKeyStore>();

        // E2: JWT anahtarı eksik ya da kısaysa uygulama açılışta duruyor (bağlantı dizesiyle aynı davranış).
        services.AddSingleton(JwtSettings.FromConfiguration(configuration));
        services.AddSingleton<ITokenService, JwtTokenService>();
        // K-49: SMTP_HOST tanımlıysa gerçek gönderim; değilse bağlantı loga (geliştirme ve CI).
        if (SmtpSettings.FromConfiguration(configuration) is { } smtp)
        {
            services.AddSingleton(smtp);
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        // Şifreleme anahtarı ve görüntü servisi ayarları ilk kullanımda okunuyor; eksikse ilgili uç
        // hata veriyor ama uygulamanın geri kalanı açılıyor. Docker Compose ve CI bu değişkenleri
        // zaten zorunlu tutuyor.
        services.AddSingleton<IFieldEncryptor>(_ => AesGcmFieldEncryptor.FromConfiguration(configuration));
        services.AddSingleton<IEvidenceStore>(sp =>
            FileEvidenceStore.FromConfiguration(configuration, sp.GetRequiredService<IFieldEncryptor>()));
        services.AddHttpClient<IFaceService, VisionFaceService>(client => VisionFaceService.Configure(client, configuration));

        // D4: sınav sırası kare analizi (IMediaAnalyzer'ın uygulaması Application'da, CameraMonitor). Yüz uçlarından
        // ayrı istemci: zaman aşımı kısa (CameraMonitoring:VisionTimeoutSeconds), anahtar yoksa istek gönderilmiyor.
        services.AddHttpClient<IFrameAnalysisService, VisionFrameAnalysisService>((sp, client) =>
            VisionFrameAnalysisService.Configure(client, configuration, sp.GetRequiredService<IOptions<CameraMonitorOptions>>().Value));

        // A5 (FR-34): aday raporunun Excel ve PDF hâli. Durumsuz; rapor ucu istenen biçimi Format'a göre seçiyor.
        services.AddSingleton<IReportExporter, ExcelReportExporter>();
        services.AddSingleton<IReportExporter, PdfReportExporter>();

        services.AddScoped<DevDataSeeder>();

        return services;
    }

    // Uygulama, testler ve dotnet ef aynı ayarları kullansın diye tek yerde.
    public static DbContextOptionsBuilder ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
}
