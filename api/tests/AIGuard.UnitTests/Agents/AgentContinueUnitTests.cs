using System.Net;
using AIGuard.Application.Agents;
using AIGuard.Application.Events;
using Microsoft.Extensions.Options;

namespace AIGuard.UnitTests.Agents;

public class IndicatorAdviceTests
{
    [Theory]
    [InlineData("VM_DETECTED", "VirtualBox", "sanal makine", "kendi bilgisayarınızdan")]
    [InlineData("FORBIDDEN_APP", "Discord", "Discord", "Discord uygulamasını tamamen kapatın")]
    [InlineData("FORBIDDEN_APP", "Uzak Masaüstü oturumu", "Uzak Masaüstü ile bağlanılmış", "bilgisayarın başından")]
    [InlineData("VPN_PROXY", "WireGuard", "VPN veya vekil sunucu", "vekil sunucu (proxy) ayarını kapatın")]
    public void Every_blocking_indicator_has_a_reason_and_a_fix_for_the_candidate(string type, string name, string inMessage, string inFix)
    {
        var advice = IndicatorAdvice.For(new AgentIndicator(type, name));

        Assert.Equal((type, name), (advice.Type, advice.Name));
        Assert.Contains(inMessage, advice.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(inFix, advice.Fix);
    }

    [Fact]
    public void Vpn_indicator_from_the_agent_is_accepted()
    {
        Assert.True(new AgentIndicator("VPN_PROXY", "OpenVPN", "TAP-Windows Adapter V9").IsWellFormed);
        Assert.False(new AgentIndicator("VPN_PROXY", "OpenVPN").IsInformational);
    }
}

public class VpnIpBlockListTests
{
    private static VpnIpBlockList List(params VpnIpBlock[] blocks) =>
        new(Options.Create(new VpnIpBlockOptions { Blocks = [.. blocks] }));

    [Theory]
    [InlineData("198.51.100.1", true)]
    [InlineData("198.51.100.255", true)]
    [InlineData("::ffff:198.51.100.9", true)] // IPv4'e eşlenmiş IPv6
    [InlineData("198.51.101.1", false)]
    [InlineData("2001:db8::1", true)]
    [InlineData("2001:db9::1", false)]
    public void Ip_is_matched_against_ipv4_and_ipv6_blocks(string ip, bool match)
    {
        var list = List(new("198.51.100.0/24", "A"), new("2001:db8::/32", "B"));

        Assert.Equal(match, list.Match(IPAddress.Parse(ip)) is not null);
    }

    [Theory]
    [InlineData("198.51.100.0/24", "Örnek", true)]
    [InlineData("198.51.100.0/33", "Örnek", false)]
    [InlineData("vpn.example", "Örnek", false)]
    [InlineData("198.51.100.0/24", "", false)]
    public void Broken_list_stops_the_api_at_startup(string cidr, string label, bool ok)
    {
        var result = new VpnIpBlockOptionsValidator().Validate(null, new VpnIpBlockOptions { Blocks = [new(cidr, label)] });

        Assert.Equal(ok, result.Succeeded);
    }
}
