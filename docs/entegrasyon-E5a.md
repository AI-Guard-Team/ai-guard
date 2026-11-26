# 1. Entegrasyon Kontrol Noktası (E5a)

Amaç: Sprint 1'de ayrı ayrı yazılan parçaların birlikte çalıştığını uçtan uca göstermek (Talimat 23. adım, GAB 3.3.4).

Akış: **davet → şifre → giriş → kayıt fotoğrafı → sınava başla → rıza → ajan eşleştirme → ajan sinyali → kimlik doğrulama → sınav → teslim**

## Otomatik uçtan uca test

`api/tests/AIGuard.IntegrationTests/E2E/CandidateFlowE2ETests.cs`

- Akışın her adımı **yalnızca HTTP üzerinden**, ekibin gerçek uçlarıyla yapılıyor (E2, E3, E4, A3, Y1, Y2).
- Sahte olan yalnızca iki dış bağımlılık var: görüntü servisi (ayrı Python süreci) ve kanıt karesi dosya deposu. Ajanın yaptığını test yapıyor: kodu `/api/agent/pair`'e iletiyor ve dönen oturum anahtarıyla HMAC imzalı sinyal gönderiyor.
- Sonunda veritabanında akışın izleri kontrol ediliyor: oturum `Completed`, cevap kaydı, rıza kaydı, ilk doğrulama, ajan platformu, kullanılmış oturum hakkı, yalnızca şifreli yüz vektörü, giriş denetim kaydı. Ayrıca aynı sınava ikinci kez başlanamadığı doğrulanıyor.
- Bir adım bozulursa test o adımın adıyla düşüyor, ör. `E2E adımı "rıza": beklenen 200, gelen 409. Yanıt: …`.
- Entegrasyon testi projesinde olduğu için **CI'da her PR'da çalışıyor** (`api` işi, PostgreSQL servisiyle).

Yerelde çalıştırma:

```powershell
cd api
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<şifre>"
dotnet test tests/AIGuard.IntegrationTests --filter "FullyQualifiedName~E2E"
```

## Ekip denemesi (Yağmur'un bilgisayarı, Docker Compose)

Testin sahte bıraktığı parçalar (gerçek görüntü servisi, gerçek ajan, tarayıcı) bu denemede birlikte sınanıyor.

1. `docker compose --env-file .env up -d --build` → `http://localhost:8080/health` = Healthy
2. Yönetici (`admin@aiguard.local`) Swagger'dan aday davet eder; davet bağlantısı `docker compose logs api` çıktısında
3. Aday tarayıcıda şifresini belirler, giriş yapar, kayıt fotoğrafını yükler (gerçek yüz; `vision` servisi vektör üretir)
4. Yönetici sınav, soru ve atama oluşturur (Swagger)
5. Aday sınava başlar → rıza → Windows ajanı (Yağmur) / macOS ajanı (Şeyma) eşleşir → kamera ile doğrulama → sınav → teslim
6. Ters senaryolar: telefondan gösterilen fotoğrafla doğrulama reddedilmeli; ajan kapalıyken "ajansız devam" seçeneği görünmeli

## Bulunan hatalar ve sorumluları

| # | Bulgu | Etki | Sorumlu / kart | Durum |
|---|---|---|---|---|
| B-1 | `EVIDENCE_ENCRYPTION_KEY` eksikken API açılıyor, ilk kayıt fotoğrafı yüklemesinde **500** dönüyor. Anahtar `AddInfrastructure`'da tembel (ilk kullanımda) okunuyor; `JWT_SECRET` ise açılışta kontrol ediliyor | Eksik ayar ancak aday fotoğraf yüklerken fark ediliyor; adaya anlamsız hata | Ayşe (A3) | Açık: `AesGcmFieldEncryptor.FromConfiguration` açılışta çağrılmalı (JWT ile aynı davranış) |
| B-2 | Oturum rıza metninde (`consent_texts`, `kvkk-session-v1`) "**gözetmen**" geçiyor ("nihai karar gözetmene aittir") | GAB'da canlı gözetmen yok; rol adı "değerlendirici" (K-01). Aday yanlış bilgilendiriliyor | Ayşe (A5) | Açık: metin değişmez kayıt olduğu için yeni sürüm (`kvkk-session-v2`) yayımlanmalı |
| B-3 | Yerelde tüm entegrasyon testleri birlikte çalışınca test sınıfı temizliği (`DROP DATABASE … WITH (FORCE)`) zaman aşımına uğruyor; geride `aiguard_it_*` veritabanları kalıyor ve sonraki çalıştırmalar daha çok düşüyor. CI'da görülmüyor | Ekip yerelde "testler kırık" sanabilir | Ayşe (A1, `PostgresFixture`) | Açık: temizlik komutuna daha uzun `CommandTimeout`; geçici çözüm aşağıda |
| B-4 | Önyüz rıza isteğinde `granted` gönderiyordu, API `accepted` bekliyor | Rıza adımı 400 | Şeyma (S2) | **Çözüldü** (`web/src/api/sessions.ts` artık `accepted` gönderiyor) |

**B-3 geçici çözüm** (artık test veritabanlarını silmek için):

```powershell
$env:PGPASSWORD = "<şifre>"
$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
& $psql -h localhost -U postgres -d postgres -tAc "select datname from pg_database where datname like 'aiguard_it_%'" |
  ForEach-Object { if ($_) { & $psql -h localhost -U postgres -d postgres -c "drop database if exists ""$_"" with (force)" } }
```

## Sonuç

- Otomatik E2E testi: **geçiyor** (11 adım, tek test, yaklaşık 4 sn).
- Temel akış kod düzeyinde uçtan uca çalışıyor; gerçek görüntü servisi ve ajanla deneme Yağmur'un bilgisayarında ekip toplantısında yapılacak, sonuçları bu belgeye eklenecek.
