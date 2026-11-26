using System.Net.Http.Json;
using System.Text.Json;
using AIGuard.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIGuard.IntegrationTests.Email;

/// <summary>
/// Gerçek SMTP gönderimi (K-49). Yalnızca <c>SMTP_TEST_HOST</c> verilmişse çalışır; yerelde Mailpit ile:
/// <c>docker run -d -p 1025:1025 -p 8025:8025 axllent/mailpit</c>, sonra <c>SMTP_TEST_HOST=localhost</c>.
/// CI'da atlanır.
/// </summary>
public class SmtpEmailSenderTests
{
    private static readonly string? Host = Environment.GetEnvironmentVariable("SMTP_TEST_HOST");

    [SkippableFact]
    public async Task Invite_is_delivered_with_link_and_escaped_name()
    {
        Skip.If(string.IsNullOrEmpty(Host), "SMTP_TEST_HOST yok.");
        var email = $"{Guid.NewGuid():N}@test.local";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SMTP_HOST"] = Host,
            ["SMTP_PORT"] = "1025",
            ["SMTP_SECURITY"] = "None",
            ["SMTP_FROM"] = "AI-Guard <no-reply@aiguard.test>",
            ["Web:BaseUrl"] = "https://aiguard.test",
        }).Build();
        var sender = new SmtpEmailSender(SmtpSettings.FromConfiguration(configuration)!, configuration, NullLogger<SmtpEmailSender>.Instance);

        await sender.SendInviteAsync(email, "Ada <b>Yılmaz</b>", "belirtec+/=", DateTimeOffset.UtcNow.AddHours(72));

        using var http = new HttpClient { BaseAddress = new Uri($"http://{Host}:8025") };
        var search = await http.GetFromJsonAsync<JsonElement>($"/api/v1/search?query=to:{email}");
        var id = search.GetProperty("messages")[0].GetProperty("ID").GetString();
        var message = await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}");
        var html = message.GetProperty("HTML").GetString()!;
        var text = message.GetProperty("Text").GetString()!;

        Assert.Contains("Activate your AI-Guard account", message.GetProperty("Subject").GetString());
        Assert.Contains("https://aiguard.test/set-password?token=belirtec%2B%2F%3D", text);
        Assert.DoesNotContain("<b>Yılmaz</b>", html);
        Assert.Contains("&lt;b&gt;", html);
    }
}
