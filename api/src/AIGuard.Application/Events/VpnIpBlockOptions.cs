using System.Net;
using Microsoft.Extensions.Options;

namespace AIGuard.Application.Events;

/// <summary>
/// Bilinen VPN ve veri merkezi IP blokları (Y4, GAB FR-23 "İstenen"): <c>vpn-ip-blocks.json</c>.
/// Adayın IP'si bu bloklardan birindeyse <c>VPN_PROXY</c> olayı (kaynak: network).
/// </summary>
public sealed class VpnIpBlockOptions
{
    public const string SectionName = "VpnIpBlocks";

    public List<VpnIpBlock> Blocks { get; set; } = [];
}

/// <param name="Cidr">Ör. <c>198.51.100.0/24</c> veya <c>2001:db8::/32</c>.</param>
/// <param name="Label">Değerlendiricinin göreceği ad (ör. sağlayıcı).</param>
public sealed record VpnIpBlock(string Cidr, string Label);

/// <summary>Listedeki blokları bir kez ayrıştırıp IP'yi karşılaştırır.</summary>
public sealed class VpnIpBlockList
{
    private readonly (IPNetwork Network, VpnIpBlock Block)[] _blocks;

    public VpnIpBlockList(IOptions<VpnIpBlockOptions> options)
    {
        _blocks = options.Value.Blocks.Select(b => (IPNetwork.Parse(b.Cidr), b)).ToArray();
    }

    public VpnIpBlock? Match(IPAddress ip)
    {
        var address = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        foreach (var (network, block) in _blocks)
        {
            if (network.BaseAddress.AddressFamily == address.AddressFamily && network.Contains(address))
            {
                return block;
            }
        }

        return null;
    }
}

/// <summary>Bozuk CIDR ya da adsız blok varsa API açılmıyor (riskconfig.json gibi).</summary>
public sealed class VpnIpBlockOptionsValidator : IValidateOptions<VpnIpBlockOptions>
{
    public ValidateOptionsResult Validate(string? name, VpnIpBlockOptions options)
    {
        var errors = options.Blocks
            .Where(b => string.IsNullOrWhiteSpace(b.Label) || !IPNetwork.TryParse(b.Cidr, out _))
            .Select(b => $"vpn-ip-blocks.json: '{b.Cidr}' geçerli bir CIDR değil ya da adı boş.")
            .ToList();

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
