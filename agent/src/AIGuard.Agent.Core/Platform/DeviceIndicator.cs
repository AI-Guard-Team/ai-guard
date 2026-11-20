using System.Text.Json.Serialization;

namespace AIGuard.Agent.Core.Platform;

/// <summary>
/// Bulunan tek gösterge (Y3, FR-18, FR-19): sunucuya <c>{ type, name, detail }</c> olarak gidiyor.
/// Yalnızca eşleşen göstergeler gönderiliyor; tam süreç listesi hiçbir zaman gönderilmiyor (KVKK).
/// </summary>
/// <param name="Type">Olay türü (ortak ad): <see cref="VmDetected"/>, <see cref="HypervisorInfo"/>, <see cref="ForbiddenApp"/> veya <see cref="VpnProxy"/> (Y4).</param>
/// <param name="Name">Ne bulunduğu (ör. "VirtualBox", "AnyDesk"); en fazla 64 karakter.</param>
/// <param name="Detail">Nereden anlaşıldığı (ör. "wmi:model", "AnyDesk.exe"); en fazla 128 karakter.</param>
/// <remarks>
/// JSON alan adları sabit: imzalanan metin bu nesnelerin JSON'unu içeriyor ve sunucudaki
/// <c>AgentIndicator</c> aynı adları kullanıyor (K-21, K-22).
/// </remarks>
public sealed record DeviceIndicator(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("detail")] string? Detail = null)
{
    public const string VmDetected = "VM_DETECTED";
    public const string HypervisorInfo = "HYPERVISOR_INFO";
    public const string ForbiddenApp = "FORBIDDEN_APP";
    public const string VpnProxy = "VPN_PROXY";

    public const int MaxNameLength = 64;
    public const int MaxDetailLength = 128;

    /// <summary>Ayrıntıyı sunucunun kabul ettiği uzunluğa kısaltır.</summary>
    public static string? Truncate(string? detail) =>
        detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail;
}
