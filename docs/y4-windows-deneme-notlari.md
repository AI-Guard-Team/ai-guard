# Y4 · Windows deneme notları (ağ göstergeleri, devam akışı, kurulum paketi)

Kurallar ve gerekçeler: `docs/MIMARI-KARARLAR.md` K-25. Y3'teki denemeler: `docs/y3-windows-deneme-notlari.md`.

*Son güncelleme: 20 Kasım 2026 (Y4)*

## Ortam

- Fiziksel bilgisayar, Windows 11 Home. Hyper-V, WSL2, Docker Desktop açık.
- VirtualBox ve VMware Workstation kurulu: Host-Only ve VMnet bağdaştırıcıları etkin.
- API Docker'da (Development, Y4 dalı).
- Ajan, `publish-windows.ps1` ile üretilen tek dosyalık `aiguard-agent.exe`. Ayrı bir "indirilenler" klasöründen, o klasör çalışma klasörüyken açıldı; .NET SDK'sına ihtiyaç duymadı.

## Kurulum paketi

| Deneme | Sonuç |
|---|---|
| Paket çıktısı | Tek dosya: `aiguard-agent.exe`, 45,3 MB; yanında başka dosya yok |
| Exe'yi başka bir klasörden açma | Gömülü `appsettings.json`, `forbidden-apps.json` ve `deployment.json` okundu; ajan açıldı |
| Açılış penceresi | "AI-Guard güvenlik ajanı çalışıyor. Sınav bitene kadar bu pencereyi kapatmayın." (Türkçe karakterler doğru) |
| Aday adımları | 1) indir, 2) çalıştır; eşleştirme sınav sayfasından (SmartScreen onayı hariç, GAB 3.3.1 en fazla üç) |

SmartScreen uyarısı exe internetten indirildiğinde çıkıyor ("Web işareti"). Bu denemede dosya yerelde üretildiği için uyarı görülmedi; kılavuzdaki adımlar (`docs/ajan-kurulum-windows.md`) gerçek indirmeyle bir kez denenmeli.

## Ağ göstergeleri

| Deneme | Sonuç |
|---|---|
| Hyper-V vEthernet, VirtualBox Host-Only, VMware VMnet1/3/4/5/8 bağdaştırıcıları açıkken | VPN göstergesi yok |
| Sistem vekil sunucu ayarı yokken | Gösterge yok (WinINet ve WinHTTP okuması yönetici izni olmadan çalıştı) |
| Adı `wireguard.exe` yapılmış zararsız bir komut satırı programı açıldı | 18 sn içinde `VPN_PROXY` "WireGuard" (`wireguard.exe`), kaynak `Network` |

## Devam akışı

| Deneme | Sonuç |
|---|---|
| `GET /api/sessions/{id}/agent` (AnyDesk, Teams ve "WireGuard" açık) | Üç gösterge, her biri neden ve çözüm adımıyla; hipervizör listede yok; `canContinueIndicated: true`, `canContinueWithoutAgent: false` |
| Ajan çalışırken `mode: "noAgent"` | 409 |
| `mode: "indicated"` gerekçeyle | 200; oturum `IdentityVerification`, işaret `Indicated`, gerekçe oturumda; denetim kaydında `session.continue_indicated` ve üç gösterge |
| Ajanı hiç çalışmayan ikinci aday: durum | `paired: false`, `canContinueWithoutAgent: true` |
| `mode: "noAgent"` gerekçeyle | 200; oturum `IdentityVerification`, işaret `NoAgent`; denetim kaydında `session.continue_no_agent` |

## İşlemci kullanımı

Paketlenmiş ajan, eşleşmiş ve sinyal gönderirken 60 sn: 313 ms işlemci süresi, tek çekirdeğe göre %0,52; bellek 77 MB.

## Henüz yapılmayan denemeler

- **Gerçek OpenVPN ya da WireGuard bağlantısı (kabul kriteri):** Bu bilgisayarda kurulu değil. Kurulup bir tünel açıldığında hem uygulama süreci hem de etkin bağdaştırıcı (TAP-Windows / Wintun / WireGuard Tunnel) `VPN_PROXY` üretmeli. Bağlantı kesilince bağdaştırıcı göstergesi kaybolmalı.
- **Sistem vekil sunucu ayarı:** Ayarlar → Ağ ve İnternet → Ara sunucu'da elle vekil girilince "Vekil sunucu (Windows ayarı)" gelmeli. Bu bilgisayarın internet bağlantısını bozmamak için denenmedi; kural testlerle sınandı.
- **İndirilmiş exe'de SmartScreen:** Exe bir web sunucusundan indirilip çalıştırılınca uyarı ve "Ek bilgi → Yine de çalıştır" adımları.

Sonuçlar bu belgeye eklenecek.
