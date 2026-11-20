# Y3 · Windows ajanı deneme notları

Ajanın sanal makine, hipervizör ve yasaklı yazılım göstergelerinin gerçek bir Windows bilgisayarında denenmesi. Kurallar ve gerekçeler: `docs/MIMARI-KARARLAR.md` K-22.

*Son güncelleme: 20 Kasım 2026 (Y3)*

## Ortam

- Fiziksel bilgisayar, Windows 11 Home, 20 mantıksal çekirdek.
- Hyper-V platformu, WSL2 ve Docker Desktop açık.
- API Docker'da (Development, Y3 dalı), ajan Windows'ta (`aiguard-agent.exe`, Release, kullanıcı modunda, yönetici izni yok).
- Oturum veritabanında `AgentPairing` durumunda açıldı, ajan Y1 akışıyla eşleştirildi.

## Fiziksel bilgisayar, Hyper-V/WSL2 açık (kabul kriteri)

| Deneme | Sonuç |
|---|---|
| Sanal makine göstergesi | Yok: `VM_DETECTED` üretilmedi |
| Hipervizör göstergesi | `HYPERVISOR_INFO`, ayrıntı `Hyper-V, WSL, Docker Desktop` |
| Yalnızca hipervizör varken oturum | `AgentPairing → IdentityVerification` geçti; risk skoru 0,5, oturum işareti yok (UC-08 4b) |
| Bilgisayarda açık olan AnyDesk ve yeni Teams | `FORBIDDEN_APP` "AnyDesk" (`AnyDesk.exe`) ve "Microsoft Teams" (`ms-teams.exe`); oturum `AgentPairing`'de kaldı (gerekçeyle devam Y4'te) |

## Süreç taraması ve tekrar

Yalnızca Discord'u içeren bir listeyle (`Agent__ForbiddenAppsPath`), Discord yerine adı `Discord.exe` yapılmış zararsız bir komut satırı programı çalıştırılarak:

| Deneme | Sonuç |
|---|---|
| "Discord" açıldıktan sonra olayın gelmesi | 14 sn (süreç taraması 15 sn aralıkla + 5 sn sinyal aralığı) |
| Discord açık kalırken 9 sinyal | 1 `FORBIDDEN_APP`, 1 `HYPERVISOR_INFO` olayı; her sinyalde yeni olay yok (S-11) |
| Veritabanındaki sinyal kaydı | Yalnızca eşleşen göstergeler: `[{"type":"HYPERVISOR_INFO",...},{"type":"FORBIDDEN_APP","name":"Discord",...}]` |
| Ajan logu | Süreç adı, anahtar veya imza yok; yalnızca eşleşme satırı |

## İşlemci kullanımı (GAB 3.3.3)

Ajan eşleşmiş ve sinyal gönderirken 60 sn ölçüldü (`Get-Process aiguard-agent`, `TotalProcessorTime` farkı):

| Ölçüm | Değer |
|---|---|
| 60 sn'de işlemci süresi | 672 ms |
| Tek çekirdeğe göre | %1,12 |
| Bütün işlemciye göre (20 çekirdek) | %0,06 |
| Bellek | 73 MB |

Hedef ortalama %5'in altı; tek çekirdeğe göre hesaplansa bile karşılanıyor.

## Henüz yapılmayan denemeler

Bu bilgisayarda yapılamadı; kabul kriteri için test sanal makinesinde yapılmalı:

- **VirtualBox ve VMware konuğu:** Windows 10/11 sanal makinede (konuk eklentileri kurulu ve kurulu değilken) ajan `VM_DETECTED` üretmeli; ayrıntıda `wmi`, `bios`, `registry`, `service:...` görünmeli. Ana bilgisayarda VirtualBox/VMware'in kurulu olması tek başına gösterge üretmemeli.
- **Bilgisayara dışarıdan Uzak Masaüstü:** Windows Home Uzak Masaüstü bağlantısı kabul etmiyor. Pro/Enterprise bir bilgisayara (ya da sanal makineye) başka bir bilgisayardan `mstsc` ile bağlanıp ajan o oturumda çalıştırılınca `FORBIDDEN_APP` "Uzak Masaüstü oturumu" gelmeli.
- **Gerçek Discord, Zoom, TeamViewer, OBS:** Süreç adları kurallarda ve testlerde var; gerçek uygulamayla bir kez denenmeli. OBS için sanal kamera açıkken `obs64.exe` beklenir.

Sonuçlar bu belgeye eklenecek.
