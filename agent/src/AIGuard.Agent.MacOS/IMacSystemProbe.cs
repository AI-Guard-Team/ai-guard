namespace AIGuard.Agent.MacOS;

/// <summary>
/// macOS'tan okunan donanım. Alanlar okunamazsa null. Seri numarası burada yok (KVKK).
/// </summary>
/// <param name="HypervisorPresent"><c>sysctl kern.hv_vmm_present</c>. 1 ise çekirdek bir hipervizörün konuğu.</param>
/// <param name="ModelName"><c>system_profiler SPHardwareDataType</c> içindeki Model Name.</param>
/// <param name="ModelIdentifier">Aynı çıktıdaki Model Identifier.</param>
/// <param name="IoManufacturer"><c>IOPlatformExpertDevice</c> manufacturer.</param>
/// <param name="IoProduct">Aynı aygıttaki product-name.</param>
/// <param name="IoModel">Aynı aygıttaki model.</param>
internal sealed record MacHardware(
    bool? HypervisorPresent = null,
    string? ModelName = null,
    string? ModelIdentifier = null,
    string? IoManufacturer = null,
    string? IoProduct = null,
    string? IoModel = null);

/// <summary>
/// Sistem vekil sunucu ayarı (S6). Boş alan ayar yok demek. "Ayarları otomatik algıla" (WPAD) varsayılan
/// açık olduğu için burada yok; gösterge sayılmıyor.
/// </summary>
/// <param name="Http"><c>HTTPEnable</c> açıksa "adres:port".</param>
/// <param name="Https"><c>HTTPSEnable</c> açıksa "adres:port".</param>
/// <param name="Socks"><c>SOCKSEnable</c> açıksa "adres:port".</param>
/// <param name="PacUrl"><c>ProxyAutoConfigEnable</c> açıksa kurulum betiği (PAC) adresi.</param>
internal sealed record MacProxySettings(string? Http = null, string? Https = null, string? Socks = null, string? PacUrl = null);

/// <summary>
/// İşletim sisteminden okunan ham bilgiler. Kurallar <see cref="MachineDetector"/>,
/// <see cref="NetworkDetector"/> ve <see cref="MacDeviceInspector"/> üzerinden çalışıyor; testlerde sahte
/// uygulama veriliyor (Windows'taki <c>IWindowsSystemProbe</c> gibi).
/// </summary>
/// <remarks>Çağrı kullanıcı modunda. Hata fırlatmıyor; okunamayan alan null (ya da boş liste) kalıyor.</remarks>
internal interface IMacSystemProbe
{
    MacHardware ReadHardware();

    /// <summary>
    /// Çalışan süreçlerin adları (yürütülebilir dosya adı). Yalnızca yasaklı yazılım listesiyle
    /// karşılaştırmak için; hiçbir yere gönderilmiyor ve loglanmıyor (KVKK).
    /// </summary>
    IReadOnlyList<string> RunningProcessNames();

    /// <summary>
    /// <c>route -n get {hedef}</c> ile bu hedefe giden trafiğin çıkış arayüzü (ör. <c>en0</c>, <c>utun4</c>).
    /// Hedef <c>default</c> ya da bir IP adresi. Hiçbir paket gönderilmiyor; yalnızca yönlendirme tablosu okunuyor.
    /// </summary>
    string? RouteInterface(string destination);

    /// <summary><c>scutil --proxy</c>: ağ ayarlarındaki sistem vekil sunucusu.</summary>
    MacProxySettings ReadProxySettings();
}
