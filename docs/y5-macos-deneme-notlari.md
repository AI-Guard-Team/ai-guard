# Y5 · macOS ajanı deneme notları

Ortak çekirdeğin bu Mac'te derlenmesi, yerel eşleştirme ucu ve sanal makine göstergesi. Kurallar: `docs/MIMARI-KARARLAR.md` K-22 (macOS paragrafı) ve `agent/README.md`.

*Son güncelleme: 24 Kasım 2026 (Y5)*

## Ortam

- Fiziksel bilgisayar: MacBook Air, Apple M2, macOS (Model Identifier `Mac14,2`).
- Parallels Desktop ve VMware Fusion kurulu değil; ajan sanal makinenin içinde değil.
- API `http://localhost:5080`, vision `http://127.0.0.1:8000`, web `http://localhost:5173`.
- Ajan: `dotnet run --project src/AIGuard.Agent.MacOS -c Release`, kullanıcı modu, yönetici izni yok. `http://127.0.0.1:47321`.

## Derleme

| Deneme | Sonuç |
|---|---|
| `dotnet build -c Release -r osx-arm64` | Hatasız |
| `dotnet build -c Release -r osx-x64` | Hatasız |
| `dotnet test tests/AIGuard.Agent.MacOS.Tests -c Release` | 10 test geçti |

CI'daki agent işi aynı iki runtime'ı `dotnet publish --self-contained` ile de üretiyor.

## Fiziksel Mac (kabul: kurulum izi sanal makine sayılmaz)

| Okuma | Değer |
|---|---|
| `sysctl -n kern.hv_vmm_present` | `0` |
| `system_profiler` Model Name | MacBook Air |
| `system_profiler` Model Identifier | Mac14,2 |
| `ioreg -rd1 -c IOPlatformExpertDevice` | manufacturer `Apple Inc.`, model `Mac14,2`; product-name yok |
| Gösterge | Yok. `VM_DETECTED` ve `HYPERVISOR_INFO` üretilmedi |

Seri numarası okunmuyor ve loglanmıyor. `system_profiler` çıktısının tamamı ajana alınmıyor; yalnızca model adı ve model kimliği ayrıştırılıyor.

## Yerel eşleştirme ucu

Ajan `http://127.0.0.1:47321` üzerinde dinledi.

| İstek | Sonuç |
|---|---|
| `OPTIONS /pair`, `Origin: http://localhost:5173` | 204; `Access-Control-Allow-Private-Network: true` |
| `POST /pair` aynı origin, gövde `{"code":"deneme-kodu-1234567890"}` | 502 `{"status":"unavailable"}` (API kapalı) |
| Origin olmadan `POST /pair` | 403 |

Kod ve imza loga yazılmadı.

## Tarayıcıdan eşleştirme (24 Kasım 2026)

Aday `aday2@aiguard.local`. Kayıt fotoğrafı yüklendi, örnek sınav oturumu açıldı, rıza kabul edildi. Sınav sayfasındaki **Ajanla eşleştir** düğmesi kodu ajana gönderdi.

| Kontrol | Sonuç |
|---|---|
| Ajan günlüğü | `Eşleşme kuruldu (oturum cb77d3ff-d2df-45f8-9a8a-b3af966ac165)` |
| `exam_sessions.state` | `IdentityVerification` |
| `agent_pairings.platform` | `MacOS`, `paired_at` dolu |
| `agent_signals.indicators` | `[]` (altı sinyal, imza geçerli) |
| `VM_DETECTED` / `HYPERVISOR_INFO` olayı | Yok |

Fiziksel Mac'te gösterge gitmemesi beklenen sonuç. Sayfa kimlik doğrulama adımına geçti; kamera bu kart için açılmadı.

## Parallels Desktop macOS konuğu (25 Kasım 2026)

Ortam: M2 Mac üzerinde Parallels Desktop 14 günlük deneme sürümü; konuk macOS 26.6.2 (Apple Silicon). VMware Fusion, Apple Silicon'da macOS konuğunu desteklemediği için denenmedi. Ajan konukta `osx-arm64` tek dosya olarak çalıştı. Konukta `localhost:5173`, ana Mac'teki web sunucusuna küçük bir yönlendirici ile bağlandı; ajan API'ye yine yalnızca `localhost` üzerinden gitti (https dışında yalnızca localhost kuralı korundu).

Konuğun kendini tanıtışı (`system_profiler`): `Model Name: Apple Virtual Machine 1`, `Model Identifier: VirtualMac2,1`. `/Library` altında ve `ioreg` içinde "Parallels" izi yok. Yani Apple Silicon'da Parallels, UTM ve VMware macOS konuğu Apple'ın Virtualization çerçevesini kullanıyor ve ürün adını göstermiyor.

Bu yüzden kural genişletildi: ürün adı varsa (`Parallels`, `VMware`; Intel konuklar) o ad, yoksa ve model `VirtualMac` / `Apple Virtual Machine` ise `VM_DETECTED` ve ad `Apple Sanal Makine`. Fiziksel Mac bu modeli vermez. `kern.hv_vmm_present` tek başına (model fiziksel Mac) yine yalnızca `HYPERVISOR_INFO`.

Aday `aday2@aiguard.local`, oturum `96f3dd2f-c454-4cf9-a9f4-a346fd79c207`.

| Kontrol | Sonuç |
|---|---|
| `agent_pairings.platform` | `MacOS`, `paired_at` dolu |
| `events` | `VM_DETECTED`, kaynak `Device`, skor katkısı 10 |
| Olay `payload` | `{"name": "Apple Sanal Makine", "detail": "sysctl:kern.hv_vmm_present, system_profiler:model, ioreg:model"}` |
| `agent_signals.indicators` | `[{"type": "VM_DETECTED", "name": "Apple Sanal Makine", ...}]` |
| Sinyaller | 23, hepsinin imzası geçerli |
| `exam_sessions.flags` | 1 (göstergeli); durum `AgentPairing`, aday gerekçeyle devam edebilir |
| Sayfa | "Sınav bir sanal makinenin içinde açılmış görünüyor (Apple Sanal Makine)" uyarısı göründü |

Birim testleri: `dotnet test agent/tests/AIGuard.Agent.MacOS.Tests` 13 test geçti (Apple Silicon konuğu, sysctl okunamaması, ürün adı önceliği, tek başına hipervizör bayrağı).

Sınırlılık (final raporu için): Apple Silicon'da macOS konuğunun hangi üründe (Parallels, UTM, VMware) çalıştığı konuktan ayırt edilemiyor; ajan konuğun sanal olduğunu `Apple Sanal Makine` adıyla bildiriyor. Ürün adı yalnızca Intel Mac konuklarında görünebilir.

## Henüz yapılmayan denemeler

- **VMware Fusion konuğu:** Apple Silicon'da macOS konuğu desteklenmediği için bu Mac'te denenemez; yalnızca birim testinde (`VMware` modeli) doğrulandı.
- **Intel Mac üzerinde Parallels konuğu:** donanım yok; birim testinde doğrulandı.
