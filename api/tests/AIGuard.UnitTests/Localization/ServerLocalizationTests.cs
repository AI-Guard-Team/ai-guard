using System.Text.RegularExpressions;
using AIGuard.Api.Localization;
using AIGuard.Application.Agents;
using AIGuard.Application.Localization;
using AIGuard.Application.Risk;
using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using AIGuard.Infrastructure.Email;
using Microsoft.AspNetCore.Mvc;

namespace AIGuard.UnitTests.Localization;

/// <summary>K-48: sunucu metinlerinin İngilizcesi ve K-49 yardımcıları.</summary>
public partial class ServerLocalizationTests
{
    [Theory]
    [InlineData("", "tr-TR")]
    [InlineData("en-US,en;q=0.9", "en-GB")]
    [InlineData("tr-TR,tr;q=0.9,en;q=0.8", "tr-TR")]
    [InlineData("de-DE,en;q=0.5", "en-GB")]
    [InlineData("de-DE", "tr-TR")]
    [InlineData("tr;q=0.4,en;q=0.9", "en-GB")]
    [InlineData("çöp;;;q=x", "tr-TR")]
    public void Accept_language_picks_turkish_or_english(string header, string expected)
    {
        Assert.Equal(expected, UiLanguageMiddleware.Pick(header).Name);
    }

    [Fact]
    public void Problem_details_are_translated_only_for_english_requests()
    {
        var english = InCulture("en-GB", () =>
        {
            var p = new ProblemDetails { Title = "Giriş başarısız.", Detail = "Çok fazla hatalı giriş denemesi. 15 dakika sonra tekrar deneyin." };
            ServerTexts.Localize(p);
            return p;
        });
        var turkish = InCulture("tr-TR", () =>
        {
            var p = new ProblemDetails { Title = "Giriş başarısız." };
            ServerTexts.Localize(p);
            return p;
        });
        var unknown = InCulture("en-GB", () =>
        {
            var p = new ProblemDetails { Title = "Bilinmeyen bir metin." };
            ServerTexts.Localize(p);
            return p;
        });

        Assert.Equal("Sign-in failed.", english.Title);
        Assert.Contains("15", english.Detail);
        Assert.DoesNotContain("deneyin", english.Detail);
        Assert.Equal("Giriş başarısız.", turkish.Title);
        Assert.Equal("Bilinmeyen bir metin.", unknown.Title); // çevirisi yoksa Türkçe kalır, boşalmaz
    }

    /// <summary>
    /// Kaynak koddaki her sabit Türkçe hata metninin (ProblemDetails başlığı/ayrıntısı, servis hata dönüşleri)
    /// İngilizcesi olmalı; yeni bir metin eklenip çevirisi unutulursa bu test kırılır.
    /// </summary>
    [Fact]
    public void Every_turkish_error_literal_in_source_has_an_english_translation()
    {
        var src = Path.Combine(RepoApiDir(), "src");
        var files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains("Migrations")
                        && !f.EndsWith("ServerTexts.cs"))
            .ToList();

        var found = files
            .SelectMany(f => ErrorLiteral().Matches(File.ReadAllText(f)).Select(m => (File: Path.GetFileName(f), Text: m.Groups["text"].Value)))
            .Where(x => !x.Text.Contains('{'))
            .Distinct()
            .ToList();

        var missing = found.Where(x => !ServerTexts.IsKnown(x.Text)).Select(x => $"{x.File}: {x.Text}").ToList();

        Assert.True(found.Count > 50, $"Tarama metin bulamadı ({found.Count}); kalıp bozulmuş olabilir.");
        Assert.True(missing.Count == 0, "İngilizcesi olmayan metinler:\n" + string.Join("\n", missing));
    }

    [Theory]
    [InlineData("3. dakikada 8 sn telefon görüldü", "In minute 3, for 8 s, phone seen")]
    [InlineData(null, null)]
    public void Stored_reason_is_rendered_in_english(string? stored, string? expected)
    {
        var label = ReasonFormatter.Label(EventType.PHONE);
        stored = stored?.Replace("telefon görüldü", label);
        var result = InCulture("en-GB", () => ReasonFormatter.Localize(stored));

        if (expected is null)
        {
            Assert.Null(result);
            return;
        }

        Assert.StartsWith("In minute 3, for 8 s, ", result);
        Assert.DoesNotContain("dakikada", result);
    }

    [Fact]
    public void Stored_reason_stays_turkish_for_turkish_requests()
    {
        var stored = ReasonFormatter.Format(EventType.TAB_BLUR, DateTimeOffset.UnixEpoch.AddMinutes(4), 0, DateTimeOffset.UnixEpoch);
        Assert.Equal(stored, InCulture("tr-TR", () => ReasonFormatter.Localize(stored)));
        Assert.DoesNotContain("dakikada", InCulture("en-GB", () => ReasonFormatter.Localize(stored)));
    }

    [Fact]
    public void Agent_advice_follows_request_language()
    {
        var indicator = new AgentIndicator("VPN_PROXY", "WireGuard");
        var tr = InCulture("tr-TR", () => IndicatorAdvice.For(indicator));
        var en = InCulture("en-GB", () => IndicatorAdvice.For(indicator));

        Assert.Contains("VPN veya vekil", tr.Message);
        Assert.Equal("A VPN or proxy server is active: WireGuard.", en.Message);
        Assert.Equal("WireGuard", en.Name);
    }

    [Fact]
    public void Email_templates_escape_names_and_carry_both_languages()
    {
        var message = EmailTemplates.Invite("<img src=x onerror=alert(1)>", "https://aiguard.test/davet?token=a&b", DateTimeOffset.UnixEpoch);

        Assert.DoesNotContain("<img", message.Html);
        Assert.Contains("&lt;img", message.Html);
        Assert.Contains("token=a&amp;b", message.Html);
        Assert.Contains("Hesabımı etkinleştir", message.Html);
        Assert.Contains("Activate my account", message.Html);
        Assert.Contains("https://aiguard.test/davet?token=a&b", message.Text);
    }

    [Fact]
    public void Exam_override_replaces_only_its_own_rule()
    {
        var platform = new RiskOptions { SessionThreshold = 60 };
        platform.Events[EventType.PHONE] = new EventRiskRule { Weight = 5, DecayPerMinute = 0.02, Threshold = 30 };
        platform.Events[EventType.TAB_BLUR] = new EventRiskRule { Weight = 1, DecayPerMinute = 0.05 };

        var merged = ExamRiskService.Merge(platform, 80, [new RiskSetting { EventType = EventType.PHONE, Weight = 9, Decay = 0, Threshold = 0 }]);

        Assert.Equal(80, merged.SessionThreshold);
        Assert.Equal(9, merged.Events[EventType.PHONE].Weight);
        Assert.Null(merged.Events[EventType.PHONE].Threshold); // 0 = eşik yok
        Assert.Equal(1, merged.Events[EventType.TAB_BLUR].Weight);
        Assert.Equal(5, platform.Events[EventType.PHONE].Weight); // platform ayarı değişmedi
        Assert.Equal(60, ExamRiskService.Merge(platform, null, []).SessionThreshold);
    }

    private static T InCulture<T>(string name, Func<T> action)
    {
        var previous = ApiLanguage.IsEnglish;
        ApiLanguage.Set(name.StartsWith("en", StringComparison.Ordinal));
        try
        {
            return action();
        }
        finally
        {
            ApiLanguage.Set(previous);
        }
    }

    private static string RepoApiDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AIGuard.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("AIGuard.sln bulunamadı.");
    }

    // title:/detail: adlı argümanlar ve servislerin hata dönüşleri (Fail/Invalid/NotFound(...)/Error = ...).
    [GeneratedRegex("""(?:\b(?:title|detail):\s*|\b(?:Fail|Invalid|Rejected|Error)\((?:[\w.]+,\s*)?|Error\s*=\s*)"(?<text>[^"]+)""")]
    private static partial Regex ErrorLiteral();
}
