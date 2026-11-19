using Microsoft.AspNetCore.HttpOverrides;

namespace AIGuard.Api;

/// <summary>
/// API'nin önündeki vekil sunucudan (Docker Compose'da web/nginx) gelen X-Forwarded-For başlığının
/// yalnızca güvenilen adresten kabul edilmesi (E1b, K-16).
/// </summary>
/// <remarks>
/// Vekil arkasında RemoteIpAddress her istekte vekilin IP'si olur; SessionIpGuard (A2) bütün
/// adayları aynı IP'den geliyor sanar ve IP_CHANGE hiç üretilmez. Başlığı herkesten kabul etmek de
/// olmaz: aday başlığı kendisi yazıp IP değişimini gizleyebilir. Bu yüzden yalnızca ayarda adı
/// geçen ağdan gelen başlık işleniyor. Ayar yoksa (Docker'sız yerel çalışma) hiçbir şey değişmiyor.
/// </remarks>
public static class TrustedProxySetup
{
    public const string SectionName = "ForwardedHeaders:KnownNetworks";

    /// <returns>Güvenilen ağ tanımlıysa true; o zaman <c>app.UseForwardedHeaders()</c> çağrılmalı.</returns>
    public static bool AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var networks = configuration.GetSection(SectionName).Get<string[]>() ?? [];
        if (networks.Length == 0)
        {
            return false;
        }

        // Hatalı yazılmış bir ağ sessizce yok sayılırsa IP kontrolü fark edilmeden bozulur; açılışta dursun.
        var parsed = networks.Select(n => System.Net.IPNetwork.TryParse(n, out var net)
                ? net
                : throw new InvalidOperationException($"{SectionName} içindeki '{n}' geçerli bir ağ değil (ör. 172.30.47.10/32)."))
            .ToList();

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Varsayılan yalnızca loopback'e güveniyor; onun yerine yalnızca verilen ağlar.
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
            foreach (var net in parsed)
            {
                o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(net.BaseAddress, net.PrefixLength));
            }

            // Yalnızca tek vekil var; zincirdeki son adres dışındakiler istemcinin yazdığı değerler.
            o.ForwardLimit = 1;
        });
        return true;
    }
}
