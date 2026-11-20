using AIGuard.Application.Agents;
using AIGuard.Application.Auth;
using AIGuard.Application.Exams;
using AIGuard.Application.Sessions;
using AIGuard.Application.Users;
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

        services.AddScoped<AgentPairingService>();
        services.AddScoped<AgentSignalService>();
        services.AddScoped<AgentSignalMonitor>();

        // E2: kimlik doğrulama, kullanıcı yönetimi ve oturum sahipliği.
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<SessionAccessService>();

        // E3: sınav, soru ve atama yönetimi.
        services.AddScoped<ExamService>();
        services.AddScoped<AssignmentService>();

        return services;
    }
}
