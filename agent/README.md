# agent — .NET güvenlik ajanı

Sorumlu: Yağmur Erol (Y1–Y4, Y6, Y7a), Şeyma Mavideniz (Y5, S6, Y7b)

```
agent/
├── AIGuard.Agent.sln
├── Directory.Build.props          Ortak ayarlar: net8.0, Nullable, uyarılar = hata
├── src/
│   ├── AIGuard.Agent.Core/        Ortak çekirdek: yerel uç, eşleştirme, sinyal ve imza, yasaklı yazılım listesi
│   │   ├── AgentHost.cs           Başlangıç: Kestrel yalnızca 127.0.0.1:47321
│   │   ├── LocalRequestGuard.cs   Host + origin kontrolü, CORS ön kontrolü, Private Network Access
│   │   ├── Pairing/               POST /pair → sunucuya TLS ile iletim
│   │   └── Platform/              IDeviceInspector, DeviceIndicator, ForbiddenAppList
│   ├── AIGuard.Agent.Windows/     Windows 10/11 (net8.0-windows): WindowsDeviceInspector, MachineDetector,
│   │                              WindowsSystemProbe (WMI, kayıt defteri, süreçler), forbidden-apps.json (Y3)
│   └── AIGuard.Agent.MacOS/       macOS: MacDeviceInspector, MachineDetector (sanal makine, Y5),
│                                  NetworkDetector, forbidden-apps.json (yazılım ve ağ, S6)
├── tests/AIGuard.Agent.Core.Tests/
├── tests/AIGuard.Agent.MacOS.Tests/     macOS kuralları: sanal makine (Y5), yazılım ve ağ (S6); sahte Mac ile
├── tests/AIGuard.Agent.Windows.Tests/   Windows kuralları (sahte bilgisayarla) + gerçek WMI/kayıt defteri okuması
└── tools/pairing-test.html        Tarayıcı denemesi için test sayfası
```

## Eşleştirme akışı (Y1, FR-11)

1. Oturum `AgentPairing` durumundayken tarayıcı sunucudan tek kullanımlık kod alır: `POST /api/sessions/{id}/agent-code` (adaya gösterilmez).
2. Tarayıcı kodu `POST http://127.0.0.1:47321/pair { "code": "..." }` ile ajana gönderir.
3. Ajan kodu ve işletim sistemini `POST {ApiBaseUrl}/api/agent/pair { "code": "...", "os": "Windows" }` adresine TLS ile iletir; sunucu kodu doğrulayıp oturumla eşleştirir.

Ajan yalnızca loopback'i dinlediği için kod yalnızca aynı bilgisayardaki ajana ulaşır. Kod 256 bit rastgele, 5 dakika geçerli ve tek kullanımlık; başka bilgisayardaki bir ajan onu tahmin edemez. İşletim sistemi adını (`Windows` / `MacOS`) platform projesindeki `IDeviceInspector.Platform` veriyor.

## İmzalı sinyal (Y2, FR-21)

- Eşleşme yanıtıyla gelen 32 baytlık oturum anahtarı yalnızca bellekte tutulur; diske ve loga yazılmaz.
- `Signals/SignalSender` her `SignalIntervalSeconds` saniyede `IDeviceInspector` göstergelerini alır, `Signals/SignalSigner` ile imzalar ve `POST {ApiBaseUrl}/api/agent/signal` adresine gönderir.
- Sunucu 410 dönerse oturum bitmiştir: ajan anahtarı unutur ve göndermeyi bırakır. 401 gönderimi durdurmaz.
- İmza biçimi sunucudaki `AgentSignature` ile aynı olmalı (`docs/MIMARI-KARARLAR.md` K-21); iki taraftaki `Known_answer` testi bunu kontrol eder.

## Cihaz göstergeleri (Y3, FR-18, FR-19)

Sinyaldeki `indicators` alanı: `[{ "type": "...", "name": "...", "detail": "..." }]`. Yalnızca eşleşen göstergeler gönderilir; süreç listesi ajanın dışına çıkmaz ve loga yazılmaz.

| Tür | Ne zaman | Nasıl |
|---|---|---|
| `VM_DETECTED` | Ajan VirtualBox veya VMware sanal makinesinin içinde | WMI (üretici/model), BIOS, konuk eklentisi kayıt anahtarı, konuk sürücüleri. Bilgisayarda VirtualBox/VMware'in yalnızca kurulu olması sayılmaz |
| `HYPERVISOR_INFO` | Hyper-V, WSL2, VBS veya Docker açık (sanal makine değilse) | `Win32_ComputerSystem.HypervisorPresent`; düşük ağırlıklı bilgi, oturumu tek başına bekletmez |
| `FORBIDDEN_APP` | `forbidden-apps.json`'daki bir yazılım çalışıyor | Süreç adları listeyle karşılaştırılır (büyük/küçük harf ve `.exe` farkı yok) |
| `FORBIDDEN_APP` "Uzak Masaüstü oturumu" | Bilgisayara dışarıdan Uzak Masaüstü ile bağlanılmış | `GetSystemMetrics(SM_REMOTESESSION)`; `mstsc.exe` bunu göstermez |
| `VPN_PROXY` (Y4) | Etkin VPN/TUN/TAP bağdaştırıcısı, sistem vekil sunucu ayarı ya da bilinen VPN uygulaması | `NetworkInterface`, WinINet/WinHTTP vekil ayarı, `forbidden-apps.json`'daki `"type": "VPN_PROXY"` kayıtları |

Sanal makine ve hipervizör bir kez; süreç listesi, ağ bağdaştırıcıları ve vekil sunucu `ProcessScanIntervalSeconds` aralıkla; uzak masaüstü her sinyalde denetlenir. Hepsi kullanıcı modunda; yönetici izni gerekmez. Olayları sunucu üretir: aynı gösterge yalnızca ilk görüldüğünde olay olur (K-22).

**Listeye yazılım eklemek:** `forbidden-apps.json`'a `{ "name": "...", "processes": [ "x.exe" ] }` ekleyip ajanı yeniden başlatmak yeterli; yeniden derleme gerekmez. Dosya bozuksa ajan açılmaz.

Deneme sonuçları: [docs/y3-windows-deneme-notlari.md](../docs/y3-windows-deneme-notlari.md)

## macOS cihaz göstergeleri (Y5, S6)

Sinyal biçimi Windows'la aynı (`type`, `name`, `detail`); değerlendirici ekranında iki platform aynı görünür. Hepsi kullanıcı modunda; yönetici izni gerekmez.

| Tür | Ne zaman | Nasıl |
|---|---|---|
| `VM_DETECTED` (Y5) | Ajan Parallels, VMware ya da başka bir Apple sanal makinesinin içinde | `system_profiler SPHardwareDataType` ve `ioreg -rd1 -c IOPlatformExpertDevice`. Apple Silicon konuklarının hepsi Apple Virtualization çatısını kullandığı için model `VirtualMac2,1` / "Apple Virtual Machine 1" çıkar; ürün adı görünmez ve gösterge `"Apple Sanal Makine"` olur. Parallels/VMware adı varsa o yazılır |
| `HYPERVISOR_INFO` (Y5) | Model fiziksel ama `sysctl kern.hv_vmm_present` = 1 | Düşük ağırlıklı bilgi; oturumu tek başına bekletmez |
| `FORBIDDEN_APP` (S6) | `forbidden-apps.json`'daki bir yazılım çalışıyor: AnyDesk, TeamViewer, macOS Ekran Paylaşımı, OBS (sanal kamera), Zoom, Teams, Discord | `ps -axo comm=` çıktısındaki dosya adları listeyle karşılaştırılır (büyük/küçük harf farkı yok) |
| `VPN_PROXY` "VPN bağdaştırıcısı" (S6) | İnternete giden trafik bir tünel arayüzünden (`utun`, `ipsec`, `ppp`, `tun`, `tap`) geçiyor | `route -n get default` **ve** `route -n get 1.1.1.1`. WireGuard/OpenVPN varsayılan rotayı değiştirmeyip `0.0.0.0/1` + `128.0.0.0/1` ekler; ikinci sorgu bunu yakalar |
| `VPN_PROXY` "Vekil sunucu" (S6) | Sistem HTTP/HTTPS/SOCKS vekili ya da otomatik yapılandırma (PAC) betiği tanımlı | `scutil --proxy`; yalnızca üst düzey ve açık girişler okunur, `__SCOPED__` altındakiler yok sayılır |
| `VPN_PROXY` bilinen VPN uygulaması (S6) | OpenVPN, Tunnelblick, WireGuard, NordVPN, ProtonVPN, ExpressVPN, Surfshark, Mullvad, WARP, AnyConnect, GlobalProtect … çalışıyor | `forbidden-apps.json`'daki `"type": "VPN_PROXY"` kayıtları |

**Yanlış alarm kuralı:** macOS'un boştaki `utun0`–`utun5` arayüzleri (iCloud Özel Aktarma, Handoff vb.) VPN sayılmaz; yalnızca trafiğin gittiği rota bir tünelse bildirilir. Hep açık duran servis süreçleri (`warp-svc`, `vpnagentd`, `nordvpnd`) listede yok; VPN o an bağlı değilken sayılmasınlar diye.

Donanım bir kez; süreç listesi ve ağ `ProcessScanIntervalSeconds` aralıkla okunur. Listeye yazılım ekleme Windows'takiyle aynı: `forbidden-apps.json`'a `{ "name": "...", "processes": [ "Uygulama" ] }` eklenir, süreç adı uzantısız ve `ps` çıktısındaki gibi yazılır. Dosya bozuksa ajan açılmaz.

> **Doğrulanacak (Y7b):** "macOS Ekran Paylaşımı" için `Screen Sharing` (uygulama) gerçek Mac'te görüldü. Mac'e dışarıdan bağlanıldığında çalışan sunucu süreçleri (`screensharingd`, `ScreensharingAgent`, `ARDAgent`) henüz gerçek bir bağlantıyla denenmedi.

Denemeler: [Y5](../docs/y5-macos-deneme-notlari.md), [S6](../docs/s6-macos-deneme-notlari.md)

## macOS modülü için sözleşme (Y5)

`AIGuard.Agent.Core` işletim sisteminden bağımsız. Eşleştirme, imzalı sinyal, yerel uç ve ayar doğrulaması macOS'ta değişmeden çalışıyor. macOS modülünün yazması gereken tek parça `IDeviceInspector`.

**Doğrulananlar (24 Kasım 2026):**

- Çekirdeğin 81 testi Linux'ta da geçiyor (.NET 8 SDK container'ı); Windows'a özgü API kullanılmıyor.
- `dotnet publish src/AIGuard.Agent.MacOS -r osx-arm64` ve `-r osx-x64` derleniyor. CI'daki `agent` işi bunu her PR'da kontrol ediyor ("macOS derlemesi" adımı). 24 Kasım 2026'da aynı iki runtime bu Mac'te de derlendi.
- Sunucu `MacOS` platformunu tanıyor (`AgentPlatform.MacOS`).

**Değiştirilmeyecekler** (değişiklik gerekirse Yağmur'la birlikte karar verilir):

| Ne | Kural |
|---|---|
| `IDeviceInspector.Platform` | Tam olarak `"MacOS"`. Eşleştirmede sunucuya bu gidiyor; başka yazım 400 alır |
| `InspectAsync` | Yalnızca eşleşen göstergeler; tam süreç listesi, seri numarası, kullanıcı adı gibi veri gönderilmez (KVKK) |
| Gösterge | `DeviceIndicator(Type, Name, Detail)`. `Type` yalnızca `DeviceIndicator` sabitleri; `Name` en fazla 64, `Detail` en fazla 128 karakter (`DeviceIndicator.Truncate`) |
| Sinyal biçimi ve imza | Çekirdekte (`SignalSigner`); macOS modülü dokunmuyor. Sunucuyla aynı olduğu `Known_answer_matches_the_server` testiyle kontrol ediliyor |
| Aynı gösterge her sinyalde | Sorun değil; sunucu yalnızca ilk görüldüğünde olay yazıyor (K-22) |

**Windows'ta uyduğumuz kurallar; macOS'ta da aynısı beklenir:**

- **Konuk izi ara, kurulum izi arama.** Mac'te Parallels Desktop ya da VMware Fusion'ın **kurulu olması** sanal makinede olunduğu anlamına gelmiyor. `VM_DETECTED` ancak ajan sanal makinenin **içinde** çalışıyorsa gönderilmeli. Örneğin `sysctl kern.hv_vmm_present` = 1, `ioreg` / `system_profiler` model adında "Parallels" ya da "VMware".
- **`Name` sanal makine ürününün adı olsun:** `"Parallels"`, `"VMware"`. Windows'taki `"VMware"` ile aynı yazılırsa değerlendirici ekranında iki platform aynı görünür. `Detail` nereden anlaşıldığını söylesin, ör. `"sysctl:kern.hv_vmm_present, ioreg:model"`.
- **`HYPERVISOR_INFO`** ürün belli değilse ama hipervizör varsa kullanılabilir. Düşük ağırlıklı bir bilgi göstergesi.
- **Sanal makine bilgisi sınav boyunca değişmiyor.** Bir kez hesaplanıp saklanmalı; `sysctl`/`ioreg` her 5 sn'de çalıştırılmamalı (GAB 3.3.3: ortalama işlemci kullanımı %5'in altında).
- **Hata ajanı durdurmasın.** Komut çalışmazsa gösterge yok sayılsın ve uyarı loglansın. `SignalSender` inceleme hatasında yine göstergesiz sinyal gönderiyor, ama inceleme kendi içinde de dayanıklı olmalı.
- **Test edilebilirlik.** Windows'taki gibi okuma ile karar ayrı tutulursa (`IWindowsSystemProbe` + `MachineDetector`), kurallar Mac olmadan da test edilebilir.

**Not:** Aşağıdaki konuk kuralı Apple Silicon'da şöyle uygulanıyor: ürün adı görünmediği için `VirtualMac` modeli de sanal makine sayılıyor (yukarıdaki tablo). Hangi ürün (Parallels, UTM, VMware) olduğu ayırt edilemez; bu sınırlama son raporda belirtilecek. Değerlendirme Y7b'de.

## Kurulum paketi, Windows (Y4)

```powershell
cd agent
./scripts/publish-windows.ps1 -ApiBaseUrl https://api.sinav.ornek.edu.tr -AllowedOrigins https://sinav.ornek.edu.tr
```

Çıktı `agent/dist/windows/aiguard-agent.exe`: tek dosya, .NET kurulumu gerektirmez. Sunucu adresi ve sınav sitesi `deployment.json` olarak exe'nin içine gömülür ve `appsettings.json`'daki değerlerin üzerine yazar. Aday için kurulum kılavuzu (SmartScreen dahil): [docs/ajan-kurulum-windows.md](../docs/ajan-kurulum-windows.md).

## Kurulum paketi, macOS (S6)

```bash
cd agent
./scripts/publish-macos.sh --api-base-url https://api.sinav.ornek.edu.tr --allowed-origin https://sinav.ornek.edu.tr
```

`dist/macos/aiguard-agent-arm64.pkg` (Apple Silicon) ve `aiguard-agent-x64.pkg` (Intel) üretilir; yalnızca birini üretmek için `--arch arm64|x64`, başka klasör için `--output`, birden fazla site için `--allowed-origin`'i tekrarlayın. Betik `dotnet publish -r osx-<arch> --self-contained -p:PublishSingleFile=true` ile tek dosyalık ajanı derler, `deployment.json`'u (Windows'taki gibi) içine gömer ve `pkgbuild` ile `/Applications/AI-Guard Ajanı/` altına `aiguard-agent` ve `Ajanı Başlat.command` kuran paketi yapar. Paket imzasızdır; Gatekeeper uyarısı ve çözümü kılavuzda: [docs/ajan-kurulum-macos.md](../docs/ajan-kurulum-macos.md).

## Ayarlar (`appsettings.json`, "Agent" bölümü)

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `Port` | `47321` | Web önyüzü bu portu sabit biliyor |
| `AllowedOrigins` | `["http://localhost:5173"]` | Ajana kod gönderebilecek siteler; yalnızca platformun alan adı |
| `ApiBaseUrl` | `http://localhost:5080` | Sunucu adresi. `https` zorunlu; `http` yalnızca localhost için |
| `SignalIntervalSeconds` | `5` | İmzalı sinyal aralığı (Y2), 1–15 sn. Sunucu 30 sn sinyal almazsa "sinyal kesildi" yazıyor |
| `ProcessScanIntervalSeconds` | `15` | Süreç listesinin yasaklı yazılımlarla karşılaştırılma aralığı (Y3), 1–300 sn |
| `ForbiddenAppsPath` | `forbidden-apps.json` | Yasaklı yazılım listesi (Y3); göreli yol ajanın klasörüne göre |

Ayar hatalıysa ajan açılmaz. Ortam değişkeniyle de verilebilir: `Agent__ApiBaseUrl`, `Agent__AllowedOrigins__0`.

## Çalıştırma ve test

```bash
cd agent
dotnet run --project src/AIGuard.Agent.Windows     # macOS: src/AIGuard.Agent.MacOS
dotnet test
```

Bilgisayarda yalnızca .NET 9 SDK kurulu olsa da derlenir (hedef net8.0, Windows projesi net8.0-windows; .NET 8 çalışma zamanı gerekir). CI Windows üzerinde `dotnet build` ve `dotnet test` çalıştırır; Windows testleri de orada koşar.

Tarayıcı denemesi ve sonuçlar: [docs/tarayici-deneme-notlari.md](../docs/tarayici-deneme-notlari.md)
