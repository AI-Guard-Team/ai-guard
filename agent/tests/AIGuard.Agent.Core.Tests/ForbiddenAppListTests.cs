using AIGuard.Agent.Core.Platform;

namespace AIGuard.Agent.Core.Tests;

public class ForbiddenAppListTests
{
    private const string Json = """
        // yorum satırları dosyada açıklama için kullanılıyor
        {
          "apps": [
            { "name": "AnyDesk", "processes": [ "AnyDesk.exe" ] },
            { "name": "Microsoft Teams", "processes": [ "Teams.exe", "ms-teams.exe" ] },
            { "name": "Discord", "processes": [ "Discord.exe" ] },
          ]
        }
        """;

    private static readonly ForbiddenAppList List = ForbiddenAppList.Parse(Json);

    [Fact]
    public void Only_matching_apps_are_reported_never_the_process_list()
    {
        // Process.ProcessName ".exe" içermiyor; büyük/küçük harf de farklı olabilir.
        string[] running = ["explorer", "chrome", "anydesk", "svchost", "Code", "notepad"];

        var found = List.Match(running);

        var indicator = Assert.Single(found);
        Assert.Equal(new DeviceIndicator("FORBIDDEN_APP", "AnyDesk", "AnyDesk.exe"), indicator);
        Assert.DoesNotContain(found, i => (i.Detail ?? "").Contains("chrome") || i.Name.Contains("explorer"));
    }

    [Theory]
    [InlineData("Teams", "Teams.exe")]       // klasik Teams
    [InlineData("ms-teams", "ms-teams.exe")] // yeni Teams (kart)
    public void Both_teams_process_names_are_detected(string process, string detail)
    {
        var indicator = Assert.Single(List.Match([process]));

        Assert.Equal(("Microsoft Teams", detail), (indicator.Name, indicator.Detail));
    }

    [Fact]
    public void One_indicator_per_app_even_if_several_of_its_processes_run()
    {
        var indicator = Assert.Single(List.Match(["Teams", "ms-teams", "ms-teams"]));

        Assert.Equal("Teams.exe, ms-teams.exe", indicator.Detail);
    }

    [Fact]
    public void Nothing_running_means_no_indicator()
    {
        Assert.Empty(List.Match(["explorer", "svchost"]));
    }

    [Theory]
    [InlineData("""{ "apps": [] }""")]
    [InlineData("""{ "apps": [ { "name": "", "processes": [ "x.exe" ] } ] }""")]
    [InlineData("""{ "apps": [ { "name": "X", "processes": [] } ] }""")]
    [InlineData("""{ "apps": [ { "name": "X" } ] }""")]
    [InlineData("bozuk")]
    public void Broken_list_fails_loudly_instead_of_silently_detecting_nothing(string json)
    {
        Assert.Throws<InvalidOperationException>(() => ForbiddenAppList.Parse(json));
    }

    [Fact]
    public void Missing_file_fails_at_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ForbiddenAppList.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));

        Assert.Contains("Agent:ForbiddenAppsPath", ex.Message);
    }

    [Fact]
    public void Long_detail_is_cut_to_what_the_server_accepts()
    {
        Assert.Equal(DeviceIndicator.MaxDetailLength, DeviceIndicator.Truncate(new string('x', 500))!.Length);
        Assert.Null(DeviceIndicator.Truncate(null));
    }
}

public class InspectionOptionsTests
{
    private static AgentOptions Valid() => new()
    {
        ApiBaseUrl = new Uri("https://api.ornek.edu.tr"),
        AllowedOrigins = ["https://sinav.ornek.edu.tr"],
    };

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void Process_scan_interval_is_bounded(int seconds, bool ok)
    {
        var options = Valid();
        options.ProcessScanIntervalSeconds = seconds;

        Assert.Equal(ok, new AgentOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Relative_list_path_is_next_to_the_agent()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "forbidden-apps.json"), Valid().ResolveForbiddenAppsPath());
    }
}
