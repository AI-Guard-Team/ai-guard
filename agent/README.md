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
│   └── AIGuard.Agent.MacOS/       macOS giriş noktası + MacDeviceInspector (göstergeler henüz yok)
├── tests/AIGuard.Agent.Core.Tests/
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

## Kurulum paketi (Y4)

```powershell
cd agent
./build/publish-windows.ps1 -ApiBaseUrl https://api.sinav.ornek.edu.tr -AllowedOrigins https://sinav.ornek.edu.tr
```

Çıktı `agent/dist/windows/aiguard-agent.exe`: tek dosya, .NET kurulumu gerektirmez. Sunucu adresi ve sınav sitesi `deployment.json` olarak exe'nin içine gömülür ve `appsettings.json`'daki değerlerin üzerine yazar. Aday için kurulum kılavuzu (SmartScreen dahil): [docs/ajan-kurulum-windows.md](../docs/ajan-kurulum-windows.md).

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
