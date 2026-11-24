# Y5 · macOS ajanı deneme notları

Ortak çekirdeğin bu Mac'te derlenmesi, yerel eşleştirme ucu ve sanal makine göstergesi. Kurallar: `docs/MIMARI-KARARLAR.md` K-22 (macOS paragrafı) ve `agent/README.md`.

*Son güncelleme: 24 Kasım 2026 (Y5)*

## Ortam

- Fiziksel bilgisayar: MacBook Air, Apple M2, macOS (Model Identifier `Mac14,2`).
- Parallels Desktop ve VMware Fusion kurulu değil; ajan sanal makinenin içinde değil.
- API bu denemede ayakta değildi (`http://localhost:5080`).
- Ajan: `dotnet run --project src/AIGuard.Agent.MacOS -c Release`, kullanıcı modu, yönetici izni yok.

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

Kod ve imza loga yazılmadı. Tarayıcıdan oturum eşleştirip göstergenin sunucuda olay olması, API ayaktayken ayrıca denenmeli.

## Henüz yapılmayan denemeler

Bu bilgisayarda yapılamadı:

- **Parallels veya VMware Fusion konuğu:** macOS sanal makinede ajan `VM_DETECTED` üretmeli; ad `Parallels` veya `VMware`. Ayrıntıda `sysctl:kern.hv_vmm_present`, `system_profiler:model`, `ioreg:model` den hangileri varsa görünmeli. Ana bilgisayarda bu uygulamaların kurulu olması tek başına gösterge üretmemeli.
- **Ürünü belli olmayan konuk** (UTM, `VirtualMac`): yalnızca `HYPERVISOR_INFO`, ad `Hipervizör`.
- **API ayaktayken tarayıcı eşleştirmesi:** sınav sayfası kodu ajana iletmeli; fiziksel Mac'te gösterge gitmemeli, konukta `VM_DETECTED` olay olmalı.

Sonuçlar bu belgeye eklenecek.
