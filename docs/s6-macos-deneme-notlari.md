# S6 · macOS yazılım ve ağ tespiti, kurulum paketi · deneme notları

Tarih: 25–26 Kasım 2026. Deneme makinesi: Apple Silicon Mac (arm64, macOS 26.5.1), fiziksel; VPN, vekil sunucu ve yasaklı yazılım kapalı. Sorumlu: Şeyma Mavideniz.

## Ne yapıldı

| Parça | Dosya |
|---|---|
| Yasaklı yazılım listesi (7 yazılım + 14 VPN uygulaması) | `agent/src/AIGuard.Agent.MacOS/forbidden-apps.json` |
| Ağ tespiti: VPN tüneli, sistem vekil sunucusu, PAC betiği | `NetworkDetector.cs`, `MacSystemProbe.cs` |
| Denetçi: Y5 sanal makine + yazılım + ağ | `MacDeviceInspector.cs` |
| `.pkg` üretimi (Apple Silicon ve Intel) | `agent/scripts/publish-macos.sh` |
| Adaya kurulum kılavuzu, Gatekeeper notu | `docs/ajan-kurulum-macos.md` |

Sinyal biçimi Windows'la (Y2/Y3/Y4) aynıdır; çekirdeğe dokunulmadı.

## Doğrulama

### Birim testleri

`dotnet test agent/tests/AIGuard.Agent.MacOS.Tests`: **65 test geçti** (Y5 testleri dahil). Çekirdek testleri: 81 geçti. İki runtime (`osx-arm64`, `osx-x64`) uyarısız derleniyor.

S6'nın kapsadığı kurallar (sahte Mac ile):

- Kartta istenen her yazılım (AnyDesk, TeamViewer, Ekran Paylaşımı, OBS, Zoom, Teams, Discord) çalışınca `FORBIDDEN_APP`; süreç adı büyük/küçük harf farkından etkilenmiyor; sıradan bir masaüstü (Safari, Finder, Terminal …) gösterge üretmiyor.
- VPN uygulaması listede `VPN_PROXY`, yasaklı yazılım `FORBIDDEN_APP` olarak ayrılıyor.
- Genel adrese (`1.1.1.1`) giden rota `utun`/`ipsec`/`ppp`/`tun`/`tap` ise `VPN_PROXY`; yalnızca varsayılan rota tünelse de, yalnızca `1.1.1.1` rotası tünelse de (WireGuard tarzı bölünmüş rota) yakalanıyor. Rota `en0` ise tünel arayüzleri var olsa da gösterge yok.
- Sistem HTTP/HTTPS/SOCKS vekili ve PAC betiği ayrı göstergeler; kapalı (`Enable : 0`) ve `__SCOPED__` altındaki girişler sayılmıyor.
- Süreç + ağ okuması `ProcessScanIntervalSeconds` aralıkla (her sinyalde değil); kapatılan uygulama bir sonraki taramada göstergeden düşüyor; sinyale yalnızca eşleşen adlar giriyor, süreç listesi girmiyor. Rota okunamazsa ajan hata vermiyor.
- `scutil`, `route`, `ps` çıktı ayrıştırıcıları gerçek çıktı örnekleriyle ve bu Mac'te canlı bir duman testiyle deneniyor.

### Gerçek Mac'te ajanın çıktısı

Ajanın gerçek `MacSystemProbe` sınıfı fiziksel Mac'te iki kez çalıştırıldı:

| Durum | Ajanın bildirdiği |
|---|---|
| Hiçbir şey açık değil | `[]` (boş): **yanlış alarm yok** |
| "Discord" adında bir süreç açıkken | `FORBIDDEN_APP / Discord` |

Yanlış alarm denemesinin dayandığı değerler:

- `route -n get default` ve `route -n get 1.1.1.1` → `interface: en0`
- `ifconfig`'te `utun0`–`utun5` var, hiçbirinde trafik rotası yok. Bunlar macOS'un kendi boştaki tünelleri; bu yüzden "tünel arayüzü var mı" diye bakmıyoruz, "trafik oradan mı gidiyor" diye bakıyoruz.
- `scutil --proxy` → vekil sunucu açık değil.

Sahte süreç, `/bin/sleep`'in kopyası `Discord` adıyla kaydedilip ad-hoc imzalanarak (`codesign -s -`) açıldı; imzasız kopyayı macOS çalıştırmıyor.

### Kurulum paketi

`publish-macos.sh --api-base-url … --allowed-origin …` iki paketi 13 saniyede üretti:

| Paket | Boyut | İkili |
|---|---|---|
| `aiguard-agent-arm64.pkg` | 38 MB | Mach-O arm64 |
| `aiguard-agent-x64.pkg` | 40 MB | Mach-O x86_64 |

- `pkgutil --payload-files`: yalnızca `/Applications/AI-Guard Ajanı/aiguard-agent` ve `Ajanı Başlat.command`.
- `pkgutil --expand-full` ile açılan ajan çalıştırıldı: banner yazıldı, yalnızca `127.0.0.1:47321` dinledi; `appsettings.json`, `deployment.json` ve `forbidden-apps.json` tek dosyanın içinden okundu (`~/.net/aiguard-agent` altına çıkarıldı).
- Paketin gerçekten `installer` ile kurulumu yapılmadı (`/Applications` yönetici parolası ister); yalnızca içeriği açılıp ajan çalıştırıldı.

## Bilinen sınırlar ve açık kalanlar

| Konu | Durum |
|---|---|
| Gatekeeper | Paket Developer ID ile imzalı/noter onaylı değil (Apple Developer hesabı yok). Kılavuz Sistem Ayarları → Gizlilik ve Güvenlik → "Yine de Aç" yolunu anlatıyor. Bu yol bu Mac'te baştan sona denenmedi, kılavuz Apple'ın belgelenmiş akışına dayanıyor |
| macOS Ekran Paylaşımı süreç adları | **Doğrulandı:** Screen Sharing uygulaması açıkken `ps` çıktısında süreç adı tam olarak `Screen Sharing` (26 Kasım 2026, bu Mac). Mac kendi ekranına bağlanamadığı için ("You cannot control your own screen") **sunucu tarafı** (`screensharingd`, `ScreensharingAgent`, `ARDAgent`) bu denemede görülemedi; Y7b'de Parallels konuğu Mac'e `vnc://` ile bağlanınca doğrulanacak |
| Gerçek VPN | Bu denemede gerçek bir VPN sağlayıcısına bağlanılmadı; tünel kararı rota çıktısı örnekleriyle (WireGuard, OpenVPN, Cloudflare WARP biçimi) test edildi. Gerçek VPN ile deneme Y7b ortam matrisinde |
| Sistem vekil sunucusu | `scutil --proxy` ayrıştırması gerçek çıktı örnekleriyle test edildi; Mac'in ağ ayarı değiştirilerek canlı denenmedi |
| Apple Silicon sanal makine | Ürün (Parallels/UTM/VMware) ayırt edilemiyor, hepsi "Apple Sanal Makine" (Y5 notuna bakın) |
| Web sihirbazı metni | Sihirbazdaki indirme/ipucu metinleri Windows'a göre ("aiguard-agent.exe"). macOS için ayrı metin ve `.pkg` indirme bağlantısı ön yüz ekibiyle konuşulmalı; bu kartta ön yüze dokunulmadı |
| Dosya sistemi | Proje `~/Desktop` altında olduğundan iCloud "Mac'i optimize et" dosyaları buluta almıştı (`dataless`); `git`/`dotnet` zaman aşımına uğradı. Çözüm: dosyaları indirmek ya da projeyi Desktop dışına taşımak |
