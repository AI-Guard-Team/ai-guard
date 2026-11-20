using AIGuard.Application.Agents;
using AIGuard.Application.Auth;
using AIGuard.Application.Exams;
using AIGuard.Application.Sessions;
using AIGuard.Application.Users;
using AIGuard.Application.Biometrics;
using AIGuard.Application.Events;
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

        // A3: kayıt fotoğrafı, oturum rızası ve sınav başı doğrulama.
        // Eşikler appsettings.json'daki "Biometrics" bölümünden; Api'de bağlanıyor.
        services.AddSingleton<IValidateOptions<BiometricOptions>, BiometricOptionsValidator>();
        services.AddScoped<BiometricEnrollmentService>();
        services.AddScoped<SessionVerificationService>();

        return services;
    }
}
