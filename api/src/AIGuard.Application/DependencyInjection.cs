using AIGuard.Application.Abstractions;
using AIGuard.Application.Agents;
using AIGuard.Application.Monitoring;
using AIGuard.Application.Audit;
using AIGuard.Application.Auth;
using AIGuard.Application.Exams;
using AIGuard.Application.Sessions;
using AIGuard.Application.Users;
using AIGuard.Application.Biometrics;
using AIGuard.Application.ConsentTexts;
using AIGuard.Application.DataProtection;
using AIGuard.Application.Events;
using AIGuard.Application.Review;
using AIGuard.Application.Risk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Süre hesapları DateTimeOffset.UtcNow yerine TimeProvider ile yapılıyor;
        // testlerde saati ileri alabilmek için (K-15).
        services.AddSingleton(TimeProvider.System);

        // Risk ayarları Api'de riskconfig.json'dan bağlanıyor; burada doğrulanıyor.
        services.AddSingleton<IValidateOptions<RiskOptions>, RiskOptionsValidator>();
        services.AddSingleton(sp => new RiskEngine(sp.GetRequiredService<IOptions<RiskOptions>>().Value));

        services.AddScoped<EventService>();
        services.AddScoped<SessionIpGuard>();

        // Y4 (FR-23): VPN/veri merkezi IP blokları Api'de vpn-ip-blocks.json'dan bağlanıyor.
        services.AddSingleton<IValidateOptions<VpnIpBlockOptions>, VpnIpBlockOptionsValidator>();
        services.AddSingleton<VpnIpBlockList>();

        services.AddScoped<AgentPairingService>();
        services.AddScoped<AgentSignalService>();
        services.AddScoped<AgentSignalMonitor>();
        services.AddScoped<AgentContinueService>();

        // E2: kimlik doğrulama, kullanıcı yönetimi ve oturum sahipliği.
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<SessionAccessService>();

        // E3: sınav, soru ve atama yönetimi.
        services.AddScoped<ExamService>();
        services.AddScoped<AssignmentService>();

        // E4: oturum akışı ve süre denetimi.
        services.AddScoped<SessionService>();
        services.AddScoped<SessionTimekeeper>();

        // S4: sınav sırası kamera karesi ve ses parçası; hız sınırı bellekte, uygulama boyunca tek.
        services.AddSingleton<SessionMediaRateLimiter>();
        services.AddScoped<SessionMediaService>();

        // D4: sınav sırası kamera analizi ve olay kuralları (K-45). Oturum durumları bellekte, uygulama boyunca tek
        // (S-17). Eşikler "CameraMonitoring" bölümünden Api'de bağlanıyor; varsayılanlar sınıfta.
        services.AddOptions<CameraMonitorOptions>();
        services.AddSingleton<IValidateOptions<CameraMonitorOptions>, CameraMonitorOptionsValidator>();
        services.AddSingleton<CameraMonitorStore>();
        services.AddScoped<ICameraMonitorStorage, CameraMonitorStorage>();
        services.AddScoped<Func<ICameraMonitorStorage>>(sp => () => sp.GetRequiredService<ICameraMonitorStorage>());
        services.AddScoped<EncryptionKeyConfigured>(sp => () => EncryptionKey.IsConfigured(sp));
        services.AddScoped<IMediaAnalyzer, CameraMonitor>();
        services.AddScoped<CameraEpisodeCloser>();
        services.AddScoped<ISessionMonitoringCloser>(sp => sp.GetRequiredService<CameraEpisodeCloser>());

        // A3: kayıt fotoğrafı, oturum rızası ve sınav başı doğrulama.
        // Eşikler appsettings.json'daki "Biometrics" bölümünden; Api'de bağlanıyor.
        services.AddSingleton<IValidateOptions<BiometricOptions>, BiometricOptionsValidator>();
        services.AddScoped<BiometricEnrollmentService>();
        services.AddScoped<SessionVerificationService>();

        // A4: değerlendirici paneli, aday raporu ve kanıt karesi bağlantıları.
        // İmza anahtarı süreç belleğinde; tek örnek olmalı ki detay ucunun verdiği bağlantı doğrulanabilsin.
        services.AddSingleton<EvidenceLinkSigner>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ReportService>();

        // A5: veri koruma. Saklama süresi görevi, rıza metinleri ve denetim kaydının görüntülenmesi.
        services.AddScoped<DataRetentionService>();
        services.AddScoped<ConsentTextService>();
        services.AddScoped<AuditLogService>();

        return services;
    }
}
