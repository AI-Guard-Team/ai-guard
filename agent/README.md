# agent — .NET güvenlik ajanı

Sorumlu: Yağmur Erol (Y1–Y4, Y6, Y7a), Şeyma Mavideniz (Y5, S6, Y7b)

```
agent/
├── AIGuard.Agent.sln
├── Directory.Build.props          Ortak ayarlar: net8.0, Nullable, uyarılar = hata
├── src/
│   ├── AIGuard.Agent.Core/        Ortak çekirdek: yerel uç, eşleştirme, (Y2) sinyal ve imza
│   │   ├── AgentHost.cs           Başlangıç: Kestrel yalnızca 127.0.0.1:47321
│   │   ├── LocalRequestGuard.cs   Host + origin kontrolü, CORS ön kontrolü, Private Network Access
│   │   ├── Pairing/               POST /pair → sunucuya TLS ile iletim
│   │   └── Platform/IDeviceInspector.cs   İşletim sistemine özgü inceleme arayüzü
│   ├── AIGuard.Agent.Windows/     Windows 10/11 giriş noktası + WindowsDeviceInspector (Y3)
│   └── AIGuard.Agent.MacOS/       macOS giriş noktası + MacDeviceInspector (Y3)
├── tests/AIGuard.Agent.Core.Tests/
└── tools/pairing-test.html        Tarayıcı denemesi için test sayfası
```

## Eşleştirme akışı (Y1, FR-11)

1. Oturum `AgentPairing` durumundayken tarayıcı sunucudan tek kullanımlık kod alır: `POST /api/sessions/{id}/agent-code` (adaya gösterilmez).
2. Tarayıcı kodu `POST http://127.0.0.1:47321/pair { "code": "..." }` ile ajana gönderir.
3. Ajan kodu ve işletim sistemini `POST {ApiBaseUrl}/api/agent/pair { "code": "...", "os": "Windows" }` adresine TLS ile iletir; sunucu kodu doğrulayıp oturumla eşleştirir.

Ajan yalnızca loopback'i dinlediği için kod yalnızca aynı bilgisayardaki ajana ulaşır. Kod 256 bit rastgele, 5 dakika geçerli ve tek kullanımlık; başka bilgisayardaki bir ajan onu tahmin edemez. İşletim sistemi adını (`Windows` / `MacOS`) platform projesindeki `IDeviceInspector.Platform` veriyor.

## Ayarlar (`appsettings.json`, "Agent" bölümü)

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `Port` | `47321` | Web önyüzü bu portu sabit biliyor |
| `AllowedOrigins` | `["http://localhost:5173"]` | Ajana kod gönderebilecek siteler; yalnızca platformun alan adı |
| `ApiBaseUrl` | `http://localhost:5080` | Sunucu adresi. `https` zorunlu; `http` yalnızca localhost için |

Ayar hatalıysa ajan açılmaz. Ortam değişkeniyle de verilebilir: `Agent__ApiBaseUrl`, `Agent__AllowedOrigins__0`.

## Çalıştırma ve test

```bash
cd agent
dotnet run --project src/AIGuard.Agent.Windows     # macOS: src/AIGuard.Agent.MacOS
dotnet test
```

Bilgisayarda yalnızca .NET 9 SDK kurulu olsa da derlenir (hedef net8.0, .NET 8 çalışma zamanı gerekir). CI Windows üzerinde `dotnet build` ve `dotnet test` çalıştırır.

Tarayıcı denemesi ve sonuçlar: [docs/tarayici-deneme-notlari.md](../docs/tarayici-deneme-notlari.md)
