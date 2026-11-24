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
/// İşletim sisteminden okunan ham bilgiler. Kurallar <see cref="MachineDetector"/> üzerinden
/// çalışıyor; testlerde sahte uygulama veriliyor (Windows'taki <c>IWindowsSystemProbe</c> gibi).
/// </summary>
/// <remarks>Çağrı kullanıcı modunda. Hata fırlatmıyor; okunamayan alan null kalıyor.</remarks>
internal interface IMacSystemProbe
{
    MacHardware ReadHardware();
}
