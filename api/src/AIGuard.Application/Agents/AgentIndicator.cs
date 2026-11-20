using System.Text.Json;
using System.Text.Json.Serialization;
using AIGuard.Domain.Enums;

namespace AIGuard.Application.Agents;

/// <summary>
/// Ajanın bulduğu tek gösterge (Y3, FR-18, FR-19): <c>{ type, name, detail }</c>. Ajan yalnızca
/// eşleşen göstergeleri gönderiyor; tam süreç listesi hiçbir zaman gelmiyor (KVKK).
/// </summary>
/// <param name="Type">Olay türü: <c>VM_DETECTED</c>, <c>HYPERVISOR_INFO</c>, <c>FORBIDDEN_APP</c> veya <c>VPN_PROXY</c> (Y4).</param>
/// <param name="Name">Ne bulunduğu (ör. "VirtualBox", "AnyDesk").</param>
/// <param name="Detail">Nereden anlaşıldığı (ör. "wmi:model", "AnyDesk.exe"); değerlendirici için.</param>
/// <remarks>
/// JSON alan adları sabit: imzalanan metin (<see cref="AgentSignature"/>) bu nesnelerin JSON'unu
/// içeriyor ve ajandaki <c>DeviceIndicator</c> aynı adları kullanıyor.
/// </remarks>
public sealed record AgentIndicator(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("detail")] string? Detail = null)
{
    public const int MaxNameLength = 64;
    public const int MaxDetailLength = 128;

    /// <summary>Ajanın gönderebileceği türler; başka bir tür gelirse sinyal bozuk sayılıyor.</summary>
    public static readonly IReadOnlyDictionary<string, EventType> AllowedTypes = new Dictionary<string, EventType>
    {
        [nameof(EventType.VM_DETECTED)] = EventType.VM_DETECTED,
        [nameof(EventType.HYPERVISOR_INFO)] = EventType.HYPERVISOR_INFO,
        [nameof(EventType.FORBIDDEN_APP)] = EventType.FORBIDDEN_APP,
        [nameof(EventType.VPN_PROXY)] = EventType.VPN_PROXY,
    };

    [JsonIgnore]
    public bool IsWellFormed =>
        Type is not null && AllowedTypes.ContainsKey(Type)
        && Name is { Length: > 0 and <= MaxNameLength }
        && Detail is null or { Length: <= MaxDetailLength };

    /// <summary>
    /// Genel hipervizör (Hyper-V, WSL2, VBS, Docker) tek başına oturumu göstergeli yapmıyor (UC-08 4b);
    /// yalnızca düşük ağırlıklı bilgi olayı.
    /// </summary>
    [JsonIgnore]
    public bool IsInformational => Type == nameof(EventType.HYPERVISOR_INFO);

    /// <summary>Aynı göstergenin sonraki sinyallerde tekrar olay üretmemesi için anahtar (S-11).</summary>
    [JsonIgnore]
    public (string? Type, string? Name) Key => (Type, Name);

    /// <summary>Veritabanında saklanan gösterge JSON'unu okur; okunamazsa boş liste.</summary>
    public static IReadOnlyList<AgentIndicator> ParseStored(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AgentIndicator>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
