using AIGuard.Application;
using AIGuard.Application.Abstractions;
using AIGuard.Application.Monitoring;
using AIGuard.Application.Sessions;
using AIGuard.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AIGuard.UnitTests.Monitoring;

/// <summary>Kamera izlemenin kayıtları ve ayar doğrulaması (veritabanına bağlanmaz).</summary>
public class CameraMonitorSetupTests
{
    private static ServiceProvider BuildProvider(string? encryptionKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=1;Database=none;Username=none",
                ["JWT_SECRET"] = "unit-tests-only-jwt-secret-0123456789abcdef",
                ["EVIDENCE_ENCRYPTION_KEY"] = encryptionKey,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        // Risk ayarları Api'de riskconfig.json'dan; burada testlerin sabit ayarı.
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(Risk.RiskTestData.Options()));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public void Camera_monitor_replaces_the_pending_analyzer()
    {
        using var provider = BuildProvider(Convert.ToBase64String(new byte[32]));
        using var scope = provider.CreateScope();

        Assert.IsType<CameraMonitor>(scope.ServiceProvider.GetRequiredService<IMediaAnalyzer>());
        Assert.IsType<CameraEpisodeCloser>(scope.ServiceProvider.GetRequiredService<ISessionMonitoringCloser>());
        Assert.Same(provider.GetRequiredService<CameraMonitorStore>(), provider.GetRequiredService<CameraMonitorStore>());
    }

    [Fact]
    public void Session_service_resolves_without_an_encryption_key()
    {
        // Kapatıcı depolamayı ancak yazacak bölüm olunca çözümlüyor (K-26: anahtarsız bilgisayarda teslim çalışmalı).
        using var provider = BuildProvider(encryptionKey: null);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SessionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SessionTimekeeper>());
    }

    [Fact]
    public void Default_thresholds_match_the_contract()
    {
        var o = new CameraMonitorOptions();

        // docs/contracts.md 5. bölüm: 3 ölçüm, 5 ölçüm, telefon 3 sn, kimlik 60 sn.
        Assert.Equal((3, 5, 3d, 60d), (o.FaceMissingMeasurements, o.GazeAwayMeasurements, o.PhoneIntervalSeconds, o.IdentityIntervalSeconds));
        Assert.True(new CameraMonitorOptionsValidator().Validate(null, o).Succeeded);
    }

    [Theory]
    [InlineData(0, 5, 10, 3, 120)]
    [InlineData(3, 0, 10, 3, 120)]
    [InlineData(3, 5, 3, 3, 120)]   // kesilme süresi zaman aşımından kısa olamaz
    [InlineData(3, 5, 10, 3, 10)]   // boşta silme kesilmeden önce olamaz
    public void Inconsistent_settings_are_rejected(int faceMissing, int gaze, double stale, double timeout, double idle)
    {
        var o = new CameraMonitorOptions
        {
            FaceMissingMeasurements = faceMissing,
            GazeAwayMeasurements = gaze,
            StaleAfterSeconds = stale,
            VisionTimeoutSeconds = timeout,
            IdleRemoveAfterSeconds = idle,
        };

        Assert.True(new CameraMonitorOptionsValidator().Validate(null, o).Failed);
    }

    [Fact]
    public void Options_resolve_with_defaults_without_configuration()
    {
        using var provider = BuildProvider(null);

        Assert.Equal(3, provider.GetRequiredService<IOptions<CameraMonitorOptions>>().Value.FaceMissingMeasurements);
    }
}
