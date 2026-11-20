using System.Text.Json;

namespace AIGuard.Agent.Core.Platform;

/// <summary>
/// Yasaklı yazılım listesi (Y3, FR-19): <c>forbidden-apps.json</c>. Liste yapılandırma dosyasında;
/// yeni bir yazılım eklemek için ajanı yeniden derlemek gerekmiyor.
/// </summary>
/// <remarks>
/// Çalışan süreçlerin adları yalnızca bu listeyle karşılaştırılıyor; sonuçta yalnızca eşleşen
/// yazılımlar var. Süreç listesinin kendisi bu sınıftan dışarı çıkmıyor (KVKK).
/// </remarks>
public sealed class ForbiddenAppList
{
    public sealed record App(string Name, IReadOnlyList<string> Processes);

    private sealed record FileModel(List<FileApp>? Apps);

    private sealed record FileApp(string? Name, List<string>? Processes);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private ForbiddenAppList(IReadOnlyList<App> apps) => Apps = apps;

    public IReadOnlyList<App> Apps { get; }

    /// <summary>Dosyayı okur. Dosya yoksa ya da bozuksa hata: tespit sessizce kapanmasın.</summary>
    public static ForbiddenAppList Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Yasaklı yazılım listesi bulunamadı: {path} (Agent:ForbiddenAppsPath).");
        }

        return Parse(File.ReadAllText(path), path);
    }

    public static ForbiddenAppList Parse(string json, string source = "forbidden-apps.json")
    {
        FileModel? model;
        try
        {
            model = JsonSerializer.Deserialize<FileModel>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source} okunamadı: {ex.Message}", ex);
        }

        var apps = new List<App>();
        foreach (var app in model?.Apps ?? [])
        {
            var name = app.Name?.Trim();
            var processes = (app.Processes ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
            if (string.IsNullOrEmpty(name) || name.Length > DeviceIndicator.MaxNameLength || processes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{source}: her yazılımın adı (en fazla {DeviceIndicator.MaxNameLength} karakter) ve en az bir süreç adı olmalı.");
            }

            apps.Add(new App(name, processes));
        }

        if (apps.Count == 0)
        {
            throw new InvalidOperationException($"{source}: liste boş.");
        }

        return new ForbiddenAppList(apps);
    }

    /// <summary>
    /// Çalışan süreç adlarını listeyle karşılaştırır (büyük/küçük harf ve ".exe" farkı yok sayılır).
    /// Her yazılım için en fazla bir gösterge; ayrıntıda eşleşen süreç adları.
    /// </summary>
    public IReadOnlyList<DeviceIndicator> Match(IEnumerable<string> runningProcessNames)
    {
        var running = runningProcessNames.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Apps
            .Select(app => (app, matched: app.Processes.Where(p => running.Contains(Normalize(p))).ToList()))
            .Where(x => x.matched.Count > 0)
            .Select(x => new DeviceIndicator(DeviceIndicator.ForbiddenApp, x.app.Name,
                DeviceIndicator.Truncate(string.Join(", ", x.matched))))
            .ToList();
    }

    /// <summary>Process.ProcessName ".exe" içermiyor; listede yazılmış olabilir.</summary>
    private static string Normalize(string name)
    {
        var n = name.Trim();
        return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
    }
}
