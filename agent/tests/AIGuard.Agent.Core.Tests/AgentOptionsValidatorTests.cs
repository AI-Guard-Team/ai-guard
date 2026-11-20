namespace AIGuard.Agent.Core.Tests;

public class AgentOptionsValidatorTests
{
    private static AgentOptions Valid() => new()
    {
        ApiBaseUrl = new Uri("https://api.ornek.edu.tr"),
        AllowedOrigins = ["https://sinav.ornek.edu.tr"],
    };

    private static bool IsValid(AgentOptions options) =>
        new AgentOptionsValidator().Validate(null, options).Succeeded;

    [Fact]
    public void Https_server_and_platform_origin_are_valid()
    {
        Assert.True(IsValid(Valid()));
    }

    [Theory]
    [InlineData("http://api.ornek.edu.tr")] // kod ağda açık metin gider
    [InlineData("http://192.168.1.10:5080")]
    [InlineData("ftp://api.ornek.edu.tr")]
    public void Server_without_tls_is_invalid(string url)
    {
        var options = Valid();
        options.ApiBaseUrl = new Uri(url);

        Assert.False(IsValid(options));
    }

    [Theory]
    [InlineData("http://localhost:5080")]
    [InlineData("http://127.0.0.1:8080")]
    public void Plain_http_is_allowed_only_for_a_local_development_api(string url)
    {
        var options = Valid();
        options.ApiBaseUrl = new Uri(url);

        Assert.True(IsValid(options));
    }

    [Fact]
    public void Missing_server_address_is_invalid()
    {
        var options = Valid();
        options.ApiBaseUrl = null;

        Assert.False(IsValid(options));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.ornek.edu.tr")]
    [InlineData("https://sinav.ornek.edu.tr/sinav")]
    [InlineData("sinav.ornek.edu.tr")]
    [InlineData("file:///C:/sayfa.html")]
    public void Wildcard_path_or_non_web_origins_are_invalid(string origin)
    {
        var options = Valid();
        options.AllowedOrigins = [origin];

        Assert.False(IsValid(options));
    }

    [Fact]
    public void At_least_one_origin_is_required()
    {
        var options = Valid();
        options.AllowedOrigins = [];

        Assert.False(IsValid(options));
    }

    [Theory]
    [InlineData("https://Sinav.Ornek.edu.tr", "https://sinav.ornek.edu.tr")]
    [InlineData("https://sinav.ornek.edu.tr:443", "https://sinav.ornek.edu.tr")]
    [InlineData("https://sinav.ornek.edu.tr/", "https://sinav.ornek.edu.tr")]
    [InlineData("http://localhost:5173", "http://localhost:5173")]
    public void Origins_are_normalized_like_the_browser_sends_them(string configured, string expected)
    {
        Assert.Equal(expected, LocalRequestGuard.NormalizeOrigin(configured));
    }
}
