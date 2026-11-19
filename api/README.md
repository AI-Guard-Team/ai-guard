# api — ASP.NET Core Web API

Sorumlu: Esma İrem Öztürk (E2–E4, E6), Ayşe Nur Kendirci (A1–A5)

Planlanan yapı (A1 / E2 kartlarıyla oluşturulacak):

```
api/
├── AIGuard.sln
├── src/
│   ├── AIGuard.Api/             Controller'lar, SignalR hub, Program.cs
│   ├── AIGuard.Application/     Servisler, DTO'lar, iş kuralları
│   ├── AIGuard.Domain/          Varlıklar, SessionState ve EventType enum'ları
│   └── AIGuard.Infrastructure/  DbContext, EF Core göçleri, dış servis istemcileri
└── tests/
    ├── AIGuard.UnitTests/
    └── AIGuard.IntegrationTests/
```

Çözüm dosyası (`AIGuard.sln`) bu klasörün kökünde olmalı; CI `api/` içinde `dotnet build` ve `dotnet test` çalıştırır.
