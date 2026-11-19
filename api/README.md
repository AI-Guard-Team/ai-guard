# api — ASP.NET Core Web API

Sorumlu: Esma İrem Öztürk (E2–E4, E6), Ayşe Nur Kendirci (A1–A5)

```
api/
├── AIGuard.sln
├── global.json                  SDK 8'e sabitler (bilgisayarda 9/10 kurulu olsa da)
├── Directory.Build.props        Ortak ayarlar: net8.0, Nullable, uyarılar = hata
├── .config/dotnet-tools.json    dotnet-ef 8.0.11 (dotnet tool restore)
├── src/
│   ├── AIGuard.Api/             Program.cs, /health, Controllers/, Filters/, riskconfig.json. İnce katman.
│   ├── AIGuard.Application/     Abstractions/, Events/ (EventService, SessionIpGuard), Risk/ (RiskCalculator)
│   ├── AIGuard.Domain/          Entities/, Enums/, Events/EventCatalog
│   └── AIGuard.Infrastructure/  AppDbContext, Configurations, Migrations, Interceptors, Seed, Security
└── tests/
    ├── AIGuard.UnitTests/         Veritabanı gerektirmez
    └── AIGuard.IntegrationTests/  Gerçek PostgreSQL (ConnectionStrings__Default yoksa atlanır)
```

Yeni bir özelliğin hangi katmana yazılacağı: [MIMARI-KARARLAR.md, K-14](../docs/MIMARI-KARARLAR.md#k-14--katmanlar)

Kurulum ve komutlar: kökteki [README.md](../README.md) · Kararlar: [docs/MIMARI-KARARLAR.md](../docs/MIMARI-KARARLAR.md) · Model: [docs/er-diagram.md](../docs/er-diagram.md)
