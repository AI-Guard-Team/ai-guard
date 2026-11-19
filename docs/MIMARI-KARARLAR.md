# Arka Uç Mimari Kararları

Bu belgede `api/` tarafında aldığımız tasarım kararlarını ve gerekçelerini topluyoruz. Amaç, bir karta başlayan herkesin aynı kurallarla ilerlemesi ve "bu neden böyle yapılmış?" sorusunun cevabını tek yerde bulabilmesi.

Bir kararı değiştirmek gerekirse önce ekipte konuşalım, sonra belgeyi ve kodu aynı PR'da güncelleyelim. Yeni karar eklerken sıradaki numarayı kullanın; eski numaraları değiştirmeyin, çünkü kod yorumlarında bu numaralara atıf var.

*Son güncelleme: 20 Kasım 2026 (Y1)*

---

## Özet

| Konu | Kural |
|---|---|
| Kaynak belgeler | Güncel GAB, Talimat ve imzalı Öneri Formu (20.10.2026) ile Trello kartları; adlarda kartlar esas (K-01) |
| Sürümler | .NET 8 (`api/global.json`), EF Core 8.0.11, Npgsql 8.0.11 |
| Katmanlar | Api → Application → Domain, Infrastructure → Application + Domain (K-14) |
| Veri erişimi | Servisler `IAppDbContext` kullanır; ayrı repository sınıfı yok (K-14) |
| Kimlikler | Tüm tablolarda `Guid` (K-02) |
| Adlandırma | Tablo ve sütunlar snake_case (K-03) |
| Enum'lar | Veritabanında metin; yalnızca `SessionFlags` sayı (K-04) |
| Ortak adlar | `EventType` ve `SessionState` kartlardaki yazımla aynı (K-05) |
| Zaman | Her yerde UTC `DateTimeOffset`; iş kuralına giren zamanlar `TimeProvider` ile (K-15) |
| Gizli değerler | Belirteçler özet, anahtar ve vektörler şifreli (K-08) |
| Şifre | Yalnızca `IPasswordHasher` üzerinden (K-06) |
| Denetim kaydı | Yalnızca ekleme (K-10) |
| Örnek veri | Göçte değil, Development'ta açılışta (K-06) |
| Göçler | Otomatik uygulanmaz, `dotnet ef database update` ile (K-07) |
| Testler | Birim testleri veritabanısız; entegrasyon testleri gerçek PostgreSQL ile (K-09) |
| Olay kaynağı | Sunucu belirliyor; tarayıcı yalnızca 5 tarayıcı olayını gönderebiliyor (K-16) |
| Risk skoru | Olaylardan saf fonksiyonla hesaplanıyor; ağırlıklar `riskconfig.json`'da (K-17) |
| Aynı oturuma eşzamanlı yazma | Oturum satırı kilitleniyor (`LockExamSessionAsync`) (K-18) |
| Tarayıcı–ajan eşleştirme | Tek kullanımlık kod yalnızca 127.0.0.1 üzerinden ajana gidiyor; ajan kodu TLS ile sunucuya iletiyor (K-19) |

---

## K-01 · Kaynak belgeler

Geçerli belgeler şunlar:

- Gereksinim Analiz Belgesi (GAB)
- Geliştirme Talimatı (YM491-INST-001, revizyon tarihi 20.10.2026)
- İmzalı Proje Öneri Formu (20.10.2026)
- Trello kartları

Hepsinde kapsam aynı:

- Canlı gözetmen yok; değerlendirici (`Reviewer`) riskli ve işaretli oturumları sınav bitince inceliyor.
- Aday başvuruda kayıt fotoğrafı yüklüyor.
- Güvenlik ajanı Windows ve macOS'ta çalışıyor.
- Sınavlar gün aralıklı erişim penceresinde, aday başına tek oturum hakkıyla yapılıyor.

Uç, olay türü ve durum adlarında Trello kartlarındaki ortak adlar esas. GAB'daki Türkçe durum adlarının karşılıkları:

| GAB | Kod (`SessionState`) |
|---|---|
| Rıza Bekliyor | `ConsentPending` |
| Ajan Eşleştirme | `AgentPairing` |
| Kimlik Doğrulama | `IdentityVerification` |
| Sürüyor | `InProgress` |
| İzin Bekliyor | `PermissionPending` |
| Bağlantı Kesik | `Disconnected` |
| Tamamlandı | `Completed` |
| İptal | `Cancelled` |
| Karara Bağlandı | `Decided` |

Daha önce dolaşan eski sürümlerde (GAB 1.0 ve 24.10.2026 tarihli öneri formu) canlı gözetmen paneli, kimlik kartıyla referans onayı ve yalnızca Windows ajanı vardı. Bu sürümler artık geçerli değil.

---

## K-02 · Kimlikler Guid

Tüm varlıklar `Domain/Common/Entity` sınıfından türüyor ve `Guid Id` taşıyor.

Kimlikler URL'lerde görünüyor (`/api/sessions/{id}`). Ardışık sayı kullansaydık başka oturumların kimliklerini tahmin etmek kolaylaşırdı. Yine de Guid tek başına koruma sayılmaz; her uç rol ve sahiplik kontrolünü kendisi yapmalı (ör. değerlendirici yalnızca atandığı sınavı görür).

---

## K-03 · snake_case adlandırma

`EFCore.NamingConventions` paketiyle tablo, sütun, indeks ve kısıt adları snake_case oluşuyor (`exam_sessions.candidate_id`). PostgreSQL büyük harfli adlar için tırnak istediğinden, psql veya analiz betiklerinde (A6, D7) SQL yazmak böyle daha rahat.

Ham SQL ve `HasCheckConstraint` içinde de snake_case ad yazmak gerekiyor (`duration_minutes > 0`).

---

## K-04 · Enum'lar metin olarak saklanıyor

`AppDbContext.ConfigureConventions` içinde tüm enum'lar `varchar` olarak eşleniyor. Böylece veritabanında `state = 'InProgress'` görüyoruz ve enum'a araya yeni bir üye eklemek eski kayıtları bozmuyor.

`SessionFlags` istisna: birden fazla bayrak aynı anda olabildiği için (`Risky | NoAgent`) sayı olarak tutuluyor ve `(flags & 1) <> 0` gibi sorgulanıyor.

Dikkat edilmesi gerekenler:

- Bir enum üyesinin adını değiştirmek veri göçü gerektirir.
- `SessionFlags`'e yeni değer eklerken yeni bir bit kullanın (16, 32…); mevcut değerler değişmemeli.
- Yeni bir enum eklerseniz `ConfigureConventions`'a da ekleyin. Unutulursa `ModelSecurityTests` hata verir.

---

## K-05 · Ortak adlar

`EventType` üyeleri kartlardaki gibi büyük harf ve alt çizgiyle (`FACE_MISSING`), `SessionState` üyeleri PascalCase (`ConsentPending`) yazıldı. Aynı adlar tarayıcı, görüntü servisi, ajan ve veritabanı arasında dolaştığı için her katmanda çeviri yapmak yerine tek yazımı koruyoruz. `EnumContractTests` bu listeleri kartlarla karşılaştırıyor.

- **E2/E3:** JSON'da `JsonStringEnumConverter` adlandırma politikası olmadan kullanılmalı, yoksa `"FACE_MISSING"` başka bir biçime dönüşür.
- **D1:** `contracts.md`'deki olay adları bu listeyle aynı olmalı.

---

## K-06 · Örnek veri ve şifreler

`DevDataSeeder` yalnızca Development ortamında, API açılırken çalışıyor. Veritabanında kullanıcı varsa hiçbir şey yapmıyor. Kartta istenen veriyi oluşturuyor: 1 yönetici, 1 değerlendirici, 2 aday, 3 soruluk 1 sınav.

Örnek veriyi göçe (`HasData`) koymadık, çünkü göçteki şifre özeti production veritabanına da giderdi. Şifre koda yazılmıyor; `Seed:DefaultPassword` ayarından okunuyor (en az 12 karakter). Ayar yoksa kullanıcılar şifresiz oluşuyor ve giriş yapılamıyor.

```powershell
cd api
dotnet user-secrets set "Seed:DefaultPassword" "<en-az-12-karakter>" --project src/AIGuard.Api
```

Örnek hesaplar: `admin@aiguard.local`, `reviewer@aiguard.local`, `aday1@aiguard.local`, `aday2@aiguard.local`.

Şifre özetleme tek bir arayüzden yapılıyor: `Application/Abstractions/IPasswordHasher`. Şu anki uygulaması `Infrastructure/Security/IdentityPasswordHasher` (ASP.NET Core Identity, PBKDF2). E2'de giriş ve şifre belirleme de bu arayüzü kullanırsa, ileride BCrypt veya Argon2'ye geçmek yalnızca bu sınıfı değiştirmek demek olur. `Verify` bozuk bir özetle karşılaşırsa istisna fırlatmıyor, `false` dönüyor.

---

## K-07 · Göçler

API açılırken göç uygulamıyor; şema değişiklikleri `dotnet ef database update` ile elle yapılıyor. Birden fazla API örneği aynı anda göç çalıştırmaya kalkabileceği ve hatalı bir göçün fark edilmeden veriyi değiştirebileceği için bu yolu seçtik. Docker Compose tarafında (E1b) ayrı bir migrate adımı daha uygun olur.

Göç eklemek için:

```powershell
cd api
dotnet tool restore
dotnet ef migrations add <KartKodu_KisaAd> --project src/AIGuard.Infrastructure --startup-project src/AIGuard.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/AIGuard.Infrastructure --startup-project src/AIGuard.Api
```

- `dotnet tool restore` repodaki `dotnet-ef` sürümünü (8.0.11) kuruyor; bilgisayarınızda başka bir sürüm olsa da herkes aynısını kullanıyor.
- `migrations add` veritabanına bağlanmıyor (`DesignTimeDbContextFactory`), `database update` bağlanıyor.
- İki kişi aynı anda göç eklerse `AppDbContextModelSnapshot.cs` çakışır. Göç eklemeden önce `dev`'i çekin; çakışma olursa kendi göçünüzü `migrations remove` ile kaldırıp güncel `dev` üzerinde tekrar ekleyin.
- Modeli değiştirip göç eklemeyi unutursanız `Migrations_are_in_sync_with_model` testi CI'da hata verir.

---

## K-08 · Gizli değerler

| Değer | Saklama | Gerekçe |
|---|---|---|
| Şifre | `User.PasswordHash` | Tek yönlü, tuzlu ve yavaş özet |
| Davet belirteci, yenileme belirteci, eşleştirme kodu | SHA-256 özeti (`…Hash` alanları) | Rastgele ve uzun değerler; veritabanı ele geçse bile kullanılamaz. Arama özet üzerinden yapılır |
| Ajan oturum anahtarı | `AgentPairing.EncryptedSessionKey`, AES-GCM | HMAC imzasını doğrulamak için anahtarın kendisi gerekir, özet yetmez |
| Yüz vektörü | `ReferenceFaceTemplate.EncryptedVector`, AES-GCM | KVKK kapsamında özel nitelikli veri |
| Kanıt karesi | Dosya deposunda şifreli; tabloda yalnızca `StorageKey` | Büyük dosyaları veritabanı yedeklerinin dışında tutmak için |

`KeyVersion` alanları anahtar değiştirildiğinde eski kayıtların hangi anahtarla açılacağını belirtiyor. Anahtarlar ortam değişkenlerinden okunuyor.

GAB'da ajan için "oturum anahtarı özeti" yazıyor, fakat özetten HMAC imzası doğrulanamadığı için anahtarı şifreli tutuyoruz. Oturum bittiğinde (Y2) bu alan boşaltılmalı. AES-GCM yardımcı sınıfı A3'te yazılacak ve tüm şifreli alanlar için ortak kullanılacak.

---

## K-09 · Testler

| Proje | Kapsam | Veritabanı |
|---|---|---|
| `AIGuard.UnitTests` | Katman kuralları, servis kayıtları, enum listeleri, model kuralları (indeksler, şablonda fotoğraf alanı olmaması, düz metin gizli değer olmaması, göç-model uyumu), denetim kaydı koruması, şifre özetleme | Gerekmiyor |
| `AIGuard.IntegrationTests` | Göçlerin boş veritabanına uygulanması, kısıtların gerçekten çalışması, eşzamanlı güncelleme, örnek veri | PostgreSQL 16 |

Docker herkeste olmadığı için Testcontainers kullanmıyoruz. Entegrasyon testleri `ConnectionStrings__Default` değişkenindeki sunucuya bağlanıyor, her test sınıfı için `aiguard_it_<guid>` adında geçici bir veritabanı açıp sonunda siliyor. Bunun için kullanıcının `CREATEDB` yetkisi olmalı. Değişken tanımlı değilse bu testler atlanıyor; CI'da postgres servisi olduğu için orada her zaman çalışıyorlar.

```powershell
cd api
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<şifre>"
dotnet test
```

---

## K-10 · Denetim kaydı

`AuditLog` yalnızca eklemeli olacak şekilde iki adımda korunuyor:

1. **A1:** Özellikler `init`. `AppendOnlyAuditLogInterceptor` güncelleme veya silme içeren bir `SaveChanges` çağrısını reddediyor. `audit_logs`'a giden yabancı anahtarlar `Restrict`, çünkü `Cascade` ya da `SetNull` kaydı dolaylı yoldan değiştirebilirdi.
2. **A5:** Uygulamanın veritabanı kullanıcısından `audit_logs` üzerindeki `UPDATE`/`DELETE` yetkisi alınacak. `ExecuteUpdate`, `ExecuteDelete` ve ham SQL interceptor'a uğramadığı için bu ikinci adım gerekli.

`OldValue`/`NewValue` alanlarına şifre özeti, belirteç, anahtar veya yüz vektörü yazılmamalı.

---

## K-11 · Veritabanı kısıtları

Kuralların bir kısmını uygulama kodunun yanında veritabanında da tanımladık. Eşzamanlı iki istek uygulama kontrolünü birlikte geçse bile veritabanı ikincisini reddediyor.

| Kısıt | Sağladığı | Kart |
|---|---|---|
| `exam_sessions (exam_id, candidate_id)` benzersiz, iptal edilenler hariç (`WHERE state <> 'Cancelled'`) | Aday başına tek oturum. Rızayı reddeden adayın iptal edilen oturumu hakkı tüketmiyor (FR-10); hakkın kullanıldığı an `ExamAssignment.AttemptUsedAt`'te | E4 |
| `agent_signals (agent_pairing_id, nonce)` benzersiz | Sinyal tekrar gönderilemez | Y2 |
| `answers (exam_session_id, seq)` benzersiz | Aynı cevap iki kez kaydedilmez | E4, S3 |
| `users.normalized_email` benzersiz | Büyük/küçük harf farkıyla ikinci hesap açılamaz | E2 |
| `refresh_tokens.token_hash`, `users.invite_token_hash`, `agent_pairings.pairing_code_hash` benzersiz | Belirteçle arama tek kayıt döner | E2, Y1 |
| `reference_face_templates.user_id` benzersiz | Aday başına tek şablon | A3 |
| `reviewer_decisions.exam_session_id` benzersiz | Oturum başına en fazla bir karar | A4 |
| `evidence_frames.event_id` benzersiz | Olay başına en fazla bir kanıt | A5 |
| `risk_settings (exam_id, event_type)` benzersiz, NULLS NOT DISTINCT | Olay türü başına tek ayar | A2 |
| `ck_questions_*` | En az iki seçenek, doğru seçenek aralıkta | E3 |
| `ck_exams_*` | Süre > 0, pencere bitişi > başlangıç, saklama > 0 | E3 |
| `ck_consent_records_session_matches_type` | Oturum rızası bir oturuma bağlı, biyometrik rıza değil | A3 |
| `ck_reference_face_templates_vector_size` | Şablona 8 KB'tan büyük veri yazılamaz | A3 |
| `ck_events_false_alarm_marker` | Yanlış alarmı kimin ve ne zaman işaretlediği zorunlu | A4 |
| `exam_sessions.xmin` | Aynı oturumu eşzamanlı güncelleyen ikinci istek `DbUpdateConcurrencyException` alır | E4 |

Benzersizlik ihlali `PostgresException.SqlState == "23505"`, check ihlali `"23514"` olarak geliyor. Uçlarda bunları 409/400 gibi anlamlı yanıtlara çevirmek gerekiyor; veritabanının hata mesajı istemciye gönderilmemeli.

---

## K-12 · Silme davranışları

| İlişki | Davranış | Gerekçe |
|---|---|---|
| Sınav → soru, atama, risk ayarı | Cascade | Sınav olmadan anlamları yok |
| Sınav → oturum | Restrict | Oturumlar rapor ve itiraz için gerekli |
| Oturum → cevap, olay, doğrulama, ajan eşleştirme, karar | Cascade | Saklama süresi dolunca oturumla birlikte silinebilir |
| Olay → kanıt karesi | Cascade | Dosyanın kendisini A5'teki silme servisi siler |
| Kullanıcı → yenileme belirteci, yüz şablonu | Cascade | Gereksiz veri bırakmamak için |
| Kullanıcı → oturum, atama, rıza, karar, denetim | Restrict | Kullanıcılar silinmiyor, pasife alınıyor (`IsActive = false`) |
| Rıza kaydı → kullanıcı / oturum | Restrict | Rıza kaydı KVKK açısından ispat |
| Cevap → soru | Restrict | Cevaplanmış soru silinirse cevaplar anlamsızlaşır |

---

## K-13 · Diğer kartlar için eklenen alanlar

A1 kartında geçmeyen bazı alanları, ilgili kartlar başladığında yeni göç gerekmesin diye şimdiden ekledik. Kullanmadığınız bir alan olursa silmeden önce haber verin.

| Alan | Kart |
|---|---|
| `User.InviteTokenHash`, `InviteExpiresAt`, `InviteAcceptedAt`, `FailedLoginCount`, `LockoutEndAt`; `RefreshToken.ReplacedByTokenId` | E2 |
| `Exam.Type`, `RetentionDays` | E3 |
| `ExamSession.EndsAt`, `Version`; `Answer.Seq`, `ClientTime`, `Unmonitored` | E4 |
| `ExamSession.Flags`, `InitialIpAddress`, `RiskScore`; `Event.ScoreContribution`, `Reason`, `ReceivedAt` | A2 (`ReceivedAt` A6'daki gecikme ölçümü için de kullanılacak) |
| `ExamSession.VerificationAttempts`; `ConsentRecord.TextVersion` | A3 |
| `Event.IsFalseAlarm`, `FalseAlarmMarkedById`, `FalseAlarmMarkedAt` | A4 |
| `EvidenceFrame.StorageKey`, `KeyVersion`, `DeleteAfter` | A5 |
| `ExamSession.ContinueJustification` | Y4 |
| `AgentPairing.EncryptedSessionKey`, `LastSignalAt`; `AgentSignal.SignatureValid` | Y2 |
| `AgentPairing.Platform` (Windows / macOS) | Y1, Y7a, Y7b (GAB nesne modeli) |
| `ExamAssignment.AttemptUsedAt` (oturum hakkı kullanıldı mı) | E4 (GAB nesne modeli, FR-10) |
| `SessionFlags.PermissionGap` (izinsiz geçen süre) | A2, S4 (GAB FR-17) |

GAB'daki "Müdahale" varlığını canlı gözetim kapsamdan çıktığı için eklemedik.

---

## K-14 · Katmanlar

| Proje | İçerik |
|---|---|
| `AIGuard.Api` | Controller'lar, SignalR hub'ları, `Program.cs`. İsteği alıp servisi çağırır, sonucu HTTP yanıtına çevirir. |
| `AIGuard.Application` | Servisler (iş kuralları), DTO'lar ve `Abstractions/` altındaki arayüzler |
| `AIGuard.Domain` | Varlıklar ve enum'lar; hiçbir pakete bağlı değil |
| `AIGuard.Infrastructure` | `AppDbContext`, göçler, PostgreSQL ayarları, şifreleme, dosya deposu, dış servis istemcileri |

Bağımlılıklar yalnızca `Api → Application → Domain` ve `Infrastructure → Application + Domain` yönünde. `ArchitectureTests` ters yöndeki bir referansı yakalıyor.

Örnek olarak E3'teki sınav oluşturma şöyle yerleşir:

1. `Application/Exams/ExamService.cs`: kurallar ve `IAppDbContext` ile kayıt. İstek ve yanıt DTO'ları aynı klasörde.
2. `Application/DependencyInjection.cs`: `services.AddScoped<ExamService>();`
3. `Api/Controllers/ExamsController.cs`: `ExamService`'i çağırır, yetki özniteliği burada.
4. Dış bir sisteme bağlanmak gerekiyorsa (görüntü servisi, e-posta vb.) arayüz `Application/Abstractions/` altına, uygulaması `Infrastructure/` altına yazılır.

**Neden `IAppDbContext`:** Servislerin Npgsql'e veya göç ayrıntılarına bağlanmadan sorgu yazabilmesi için. `DbSet` zaten repository, `SaveChangesAsync` da unit of work işlevi gördüğünden ayrıca repository sınıfı yazmadık. Bunun karşılığında Application, EF Core'un sağlayıcıdan bağımsız paketine bağlı. Yeni bir varlık eklerken `DbSet`'i hem `AppDbContext`'e hem `IAppDbContext`'e eklemek gerekiyor; unutulursa test hata verir.

**Varlıklar:** Varlıkların özellikleri dışarıdan değiştirilebilir, kurallar servislerde duruyor. Proje süresi ve ekip büyüklüğü düşünülünce bunu yeterli bulduk. Tek istisna önerimiz oturum durumları: E4'teki geçiş kurallarının Domain'de tek bir sınıfta toplanması (ör. `Domain/Sessions/SessionStateMachine.cs`) ve `State` alanının yalnızca oradan değiştirilmesi. Böylece her geçiş veritabanı olmadan test edilebilir.

**Hatalar:** Kısıt ihlalleri ve beklenen iş hataları servis katmanında anlamlı hatalara çevrilmeli. Ortak hata biçimi (ProblemDetails) E2/E3 ile birlikte kurulacak.

---

## K-15 · Zaman

İş kuralına giren zamanlar (sınav bitişi, belirteç ve davet süreleri, kilit bitişi, saklama süresi, ajan sinyal zaman aşımı) `TimeProvider` üzerinden alınıyor. `AddApplication` içinde `TimeProvider.System` kayıtlı; servisler bunu constructor'dan alıp `GetUtcNow()` kullanıyor.

Bu sayede "süre dolunca otomatik teslim" (E4) veya "saklama süresi dolan veriler silinir" (A5) gibi testler `FakeTimeProvider` ile saati ileri alarak, beklemeden yazılabiliyor (`Microsoft.Extensions.TimeProvider.Testing` paketi).

`CreatedAt`, `ReceivedAt` gibi yalnızca kaydın oluştuğu anı tutan alanlarda `DateTimeOffset.UtcNow` varsayılanı kalabilir; bu alanlara göre bir karar verilmiyor.

---

## K-16 · Olayları kim üretiyor

Kartta olay ucunun gövdesinde `type` ve `source` alması yazıyor. Bunu olduğu gibi uygularsak adayın tarayıcısı kendi adına kamera veya ajan olayı gönderebilir, kaynağı taklit edebilir ya da olayları hiç göndermeyebilir. İstemciden gelen bilgiye güvenmeme ilkesi gereği şöyle ayırdık:

| Olaylar | Üreten | Yol |
|---|---|---|
| `TAB_BLUR`, `FULLSCREEN_EXIT`, `COPY_PASTE`, `RIGHT_CLICK`, `PERMISSION_REVOKED` | Tarayıcı | `POST /api/sessions/{id}/events` → `EventService.RecordBrowserEventAsync` |
| Kamera ve ses olayları | API, görüntü servisinin sonucuna göre (D4, D5) | `EventService.RecordSystemEventAsync` |
| Ajan olayları | API, imzalı sinyal doğrulandıktan sonra (Y2–Y4) | `EventService.RecordSystemEventAsync` |
| `IP_CHANGE` | API (`SessionIpGuard`) | `EventService.RecordSystemEventAsync` |
| `CONNECTION_LOST`, `MONITORING_GAP` | API (E4, vision istemcisi) | `EventService.RecordSystemEventAsync` |

- Kaynak her zaman `Domain/Events/EventCatalog` üzerinden belirleniyor; istekteki `source` alanı yok sayılıyor.
- Tarayıcı olayının başlangıç zamanı da istemciden alınmıyor: `alınma anı − süre`.
- Tarayıcı olaylarında sunucu tarafında hız sınırı var: aynı oturum ve aynı türden saniyede en fazla bir olay. S4'teki tarayıcı sınırlamasına güvenilmiyor.
- Tarayıcı olaylarında serbest metin/JSON alanı yok. Değerlendirici ekranında gösterilecek veriyi aday belirleyemesin (XSS) ve pano içeriği gibi kişisel veri yanlışlıkla kaydedilmesin. İstekte gönderilen fazladan alanlar yok sayılıyor.
- Uçta gövde en fazla 2 KB (`[RequestSizeLimit]`). Bu sınır Kestrel'de uygulanıyor, TestServer'da uygulanmıyor; bu yüzden elle doğrulandı.
- Sunucu içi olaylarda başlangıç zamanı geçmişte olabilir (ör. kare analizi), 5 saniyeden fazla ileride olamaz. Birkaç saniye ileri tarihli olay "şimdi" sayılıyor ki skora hemen girsin.
- Sunucu içi olaylardaki ek bilgi (`payload`) en fazla 4 KB ve geçerli bir JSON nesnesi/dizisi olmalı. Bu veri yalnızca sunucu kodundan geliyor.
- Uç, skoru veya katkıyı yanıtta döndürmüyor; aday hangi davranışın ne kadar puan getirdiğini görmemeli.

**Sınırlılık:** Tarayıcının bildirdiği süreye (`durationSec`) güvenilemez; aday sekme değiştirme süresini 0 gösterebilir. Tarayıcıyı aday kontrol ettiği için olayı hiç göndermemesi de mümkün. Bu, istemci tarafı izlemenin bilinen sınırı (GAB 1.2). Ağırlıklar bu yüzden kısa olayları da anlamlı puanlıyor.

**E2'ye not:** Kimlik doğrulama gelene kadar uç `[DevelopmentOnly]` ile yalnızca Development'ta açık. E2 birleşince bu öznitelik kaldırılıp aday rolü ve "oturum bu adaya mı ait" kontrolü eklenmeli.

**IP kontrolü (FR-22):** `SessionIpGuard.CheckAsync` oturumla ilgili her istekte çağrılmalı (E4'ün uçları dahil). İlk IP oturuma yazılıyor; farklı bir IP gelince bir kez `IP_CHANGE` üretiliyor. `::ffff:1.2.3.4` ile `1.2.3.4` aynı sayılıyor. API proxy arkasındaysa `X-Forwarded-For` yalnızca bilinen proxy'den kabul edilmeli (E1b).

---

## K-17 · Risk skoru

Skor `Application/Risk/RiskEngine` içinde, veritabanına dokunmayan bir fonksiyonla hesaplanıyor. Aynı olaylar ve aynı an için her zaman aynı sonuç çıkıyor; rapor ve inceleme ekranları (A4) skoru istedikleri an için yeniden hesaplayabiliyor.

```
katkı = ağırlık × süreÇarpanı × tekrarÇarpanı × e^(−azalma × geçen dakika)
skor  = Σ katkı
```

| Bileşen | Tanım | Gerekçe |
|---|---|---|
| süreÇarpanı | Anlık olayda 1, süreli olayda `1 + ln(1 + süre / 10 sn)` | Uzun olay daha ağır, ama 10 dakikalık bakış 60 kat ağır olmuyor |
| tekrarÇarpanı | Aynı türün n. tekrarında `min(1 + 0,25 × (n − 1), 2)` | Tekrarlayan davranış daha şüpheli, ama sınırsız büyümüyor |
| geçen dakika | Olayın **bitişinden** değerlendirme anına | Süren bir olay bitmeden azalmaya başlamıyor |
| `MaxTotal` | Türün toplam katkısının üst sınırı | `HYPERVISOR_INFO` gibi bilgi olayları ajan her sinyalde bildirse bile oturumu riskli yapamıyor |
| Tür eşiği (`Threshold`) | Türün toplam katkısı bu değere ulaşırsa oturum riskli | Sanal makine, yasaklı uygulama, kimlik uyuşmazlığı ve geçersiz ajan imzası tek başına incelemeyi gerektiriyor |
| Oturum eşiği | Skor ≥ `SessionThreshold` (varsayılan 10) ise riskli | Küçük olayların birikmesi |

- **`Risky` kalıcı:** Bir kez konunca skor zamanla düşse de kalkmıyor; değerlendirici neden riskli olduğunu görebilmeli.
- **Yanlış alarm:** Yanlış alarm işaretli olaylar (A4) skora girmiyor.
- **Sınav bitince:** Skor bitiş anında sabitleniyor; rapor ekranında sonradan azalmıyor.
- **`MONITORING_GAP`:** Ağırlığı 0. Bu adayın değil sistemin sorunu; yalnızca `MonitoringGap` işaretini koyuyor.
- **`PERMISSION_REVOKED`:** Puan eklemenin yanında `PermissionGap` işaretini koyuyor. GAB'a göre izinsiz geçen süresi olan oturum, risk skorundan bağımsız olarak incelemeye düşüyor (FR-17).
- **Kayıtlı katkı:** `Event.ScoreContribution`, olayın kaydedildiği andaki katkısı. Skorla aynı hesaptan alınıyor (tekrar ve tür üst sınırı dahil); üst sınıra takılmış bir olayda 0 görünüyor. Zamanla azalmış değeri görmek için hesaplayıcı istenen an için yeniden çalıştırılmalı.
- **Eşik kararı:** Yuvarlanmamış skorla veriliyor; 9,99996 gibi bir değer "riskli" sayılmıyor. Saklanan skor 4 basamağa yuvarlanıyor.
- **Gerekçe metni:** Sınavın başladığı ana göre yazılıyor ("12. dakikada 8 sn telefon görüldü"); sınav başlamadan önceki olaylarda "Sınav başlamadan önce …".

**Ayarlar:** Ağırlıklar `api/src/AIGuard.Api/riskconfig.json` dosyasında. Her olay türü için ayar zorunlu; eksik veya hatalı değer varsa uygulama açılmıyor (`RiskOptionsValidator`). Şu anki değerler başlangıç tahmini; pilot oturumda ayarlanıp deneyler boyunca sabit tutulacak (Talimat, 9. hafta). Testler kendi sabit ayarlarını kullandığı için ağırlıkları değiştirmek testleri bozmuyor; yalnızca `RiskConfigTests` yayındaki dosyanın tutarlılığını kontrol ediyor.

---

## K-18 · Aynı oturuma eşzamanlı olay yazımı

Bir oturuma aynı saniyede kamera, ses ve tarayıcı olayı gelebiliyor. Her olay oturumun `RiskScore` ve `Flags` alanlarını güncellediği için `xmin` iyimser kilidi çakışma hatası veriyordu. Yeniden deneme yükü yalnızca azaltıyordu, çözmüyordu.

Bu yüzden `EventService` her olayı bir işlem içinde yazıyor ve önce `IAppDbContext.LockExamSessionAsync` ile oturum satırını kilitliyor (`SELECT … FOR UPDATE`). Aynı oturuma gelen yazmalar sıraya giriyor; farklı oturumlar birbirini beklemiyor. Bu sayede:

- Skor her zaman o ana kadarki tüm olaylarla hesaplanıyor.
- Hız sınırı kontrolü eşzamanlı isteklerde atlatılamıyor.

`LockExamSessionAsync` PostgreSQL'e özgü SQL içerdiği için Infrastructure'da, `IAppDbContext` arkasında duruyor.

**E4'e not:** Oturum durumunu değiştiren işlemler (teslim, otomatik teslim, bağlantı kesildi) de aynı kilidi kullanırsa olay yazımıyla çakışmaz. Kullanmazsa `DbUpdateConcurrencyException` alabilir ve yeniden denemesi gerekir.

---

## K-19 · Tarayıcı ile ajanın eşleştirilmesi

FR-11: sınavı açan tarayıcı ile güvenlik ajanının aynı bilgisayarda çalıştığı doğrulanmalı. Akış:

1. Tarayıcı `POST /api/sessions/{id}/agent/pairing-code` ile kod alıyor. Kod adaya gösterilmiyor.
2. Tarayıcı kodu `POST http://127.0.0.1:47321/pair` ile ajana gönderiyor.
3. Ajan kodu `POST /api/agent/pair` ile TLS üzerinden sunucuya iletiyor; sunucu eşleşmeyi kaydediyor.

**"Aynı bilgisayar" kanıtı neye dayanıyor:** Ajan yalnızca `127.0.0.1`'i dinliyor; ağdaki başka bir bilgisayar bu porta bağlanamıyor. Kod tarayıcıdan yalnızca bu yolla çıktığı için kodu sunucuya getirebilen ajan, tarayıcıyla aynı bilgisayardadır. Başka bilgisayardaki bir ajanın tek şansı kodu tahmin etmek. Kod 256 bit rastgele olduğu için bu pratikte imkânsız, bu yüzden deneme sayısı sınırı koymadık.

| Kural | Gerekçe |
|---|---|
| Kod 2 dakika geçerli, tek kullanımlık | Tarayıcı kodu hemen gönderiyor; sızan bir kodun işe yarayacağı süre kısa kalıyor |
| Veritabanında yalnızca kodun SHA-256 özeti | K-08 ile aynı: veritabanı okunsa bile geçerli kod elde edilemiyor |
| Yeni kod istenince eskisi geçersiz; oturum başına tek `AgentPairing` kaydı | Ajan yeniden başlarsa tarayıcı yeni kodla tekrar eşleştirebilsin |
| Tek kullanım, oturum satırı kilitlenerek sağlanıyor (K-18) | Aynı kodla eşzamanlı iki istekten yalnızca biri eşleşiyor. `ExecuteUpdate` Application'ı ilişkisel EF paketine bağlayacağı için kullanılmadı |
| Red nedeni (yok / süresi dolmuş / kullanılmış / oturum kapalı) yanıtta ayrılmıyor | Kodun var olup olmadığı dışarıya sızmasın |
| Ajan sunucu adresini `https` değilse kabul etmiyor (`http` yalnızca localhost) | Kod ağda açık metin giderse yolda okunup kullanılabilir |

**Ajanın yerel ucu:** CORS başlıkları yalnızca tarayıcının yanıtı okumasını engelliyor, isteğin çalışmasını engellemiyor. Bu yüzden ajan izinsiz origin'den gelen isteği 403 ile reddediyor ve kodu sunucuya hiç göndermiyor. Diğer kurallar:

- Origin başlığı olmayan istek reddediliyor; eşleştirme yalnızca tarayıcıdan.
- `Host` başlığı `127.0.0.1` veya `localhost` değilse istek reddediliyor. Kötü niyetli bir site kendi alan adını 127.0.0.1'e çözdürse bile (DNS rebinding) geçemiyor.
- Yalnızca JSON gövde kabul ediliyor. Böylece tarayıcı her istekten önce ön kontrol (preflight) yapıyor.
- Ön kontrol yanıtında `Access-Control-Allow-Private-Network: true` başlığı var (Chrome'un yerel ağ erişimi kuralı). Tarayıcı denemeleri: `docs/tarayici-deneme-notlari.md`.

**Ajan projesi:** Ortak kod `AIGuard.Agent.Core`'da. `AIGuard.Agent.Windows` ve `AIGuard.Agent.MacOS` yalnızca giriş noktası ve kendi `IDeviceInspector` uygulamasını içeriyor; cihaz incelemesi Y3'te.

**E2'ye not:** Kod üreten uç, olay ucu gibi E2 gelene kadar yalnızca Development'ta açık (S-12).

---

## Kartlarda geçmeyen GAB kuralları

Trello kartları güncel GAB'a göre yazıldı, ama aşağıdaki kurallar kart metinlerinde geçmiyor. İlgili kartı uygularken bunlar da kapsama dahil.

### E4 · Sınav oturumu durum makinesi

- **Rıza reddi oturum hakkını tüketmez (FR-10).** Rıza Bekliyor durumunda aday rızayı reddederse oturum `Cancelled` olur ve aday alternatif yönteme yönlendirilir. Hak kullanılmış sayılmaz; aynı aday sonra yeni oturum açabilir. Veritabanı buna hazır: tek oturum indeksi iptal edilen oturumları saymıyor (K-11). `ExamAssignment.AttemptUsedAt` yalnızca oturum gerçekten başladığında doldurulmalı; doluysa yeni oturum açılmamalı.
- **Sınav sırasında biyometrik rıza geri alınırsa oturum iptal olur (FR-05).** `InProgress`, `PermissionPending` veya `Disconnected` durumundaki oturum `Cancelled`'a geçer, izleme durur, işlem denetim kaydına yazılır.
- **Uzun kesinti (FR-26).** 5 dakikadan uzun bağlantı kesintisi yüksek ağırlıklı bir olay üretmeli. Ortak olay listesinde bunun için ayrı bir tür yok (S-13); karar verilene kadar kesinti `CONNECTION_LOST` olarak, süresiyle birlikte yazılmalı.
- **İzin Bekliyor süresi (FR-17).** İzin yeniden verilince izinsiz geçen süre `PERMISSION_REVOKED` olayı olarak, `DurationSec` ile kaydedilmeli. Olay oturuma `PermissionGap` işaretini kendiliğinden koyuyor (K-17).
- **Kesinti sonrası doğrulama (FR-26).** Aday döndüğünde canlılık testi ve yüz eşleştirmesi tekrar yapılmalı; oturum hakkı yenilenmez. Doğrulama `VerificationKind.Reconnect` ile kaydedilir.

### A3 · Kayıt fotoğrafı ve doğrulama

- **Rıza geri alma oturumu da etkiler (FR-05).** Biyometrik rıza geri alınınca yüz vektörü silinir. Aday o sırada sınavdaysa oturum iptal edilir (E4 ile birlikte).
- **Kayıt fotoğrafı yoksa sınav başlamaz (FR-04).** Oturum başlatılırken adayın `ReferenceFaceTemplate` kaydı kontrol edilmeli.

### A4 · Rapor API'si ve inceleme bildirimleri

- **İnceleme bekleyen oturum sayısı (FR-29).** Değerlendirici panelinde, atandığı sınavlarda incelenmeyi bekleyen (riskli veya işaretli, henüz kararı olmayan) oturum sayısı gösterilmeli. Liste ucuyla birlikte bu sayı da dönmeli.
- **İşaretli oturumlar skordan bağımsız listelenir (FR-27).** Listede öne çıkarma `Flags` içinde `Risky`, `NoAgent`, `Indicated`, `PermissionGap` veya `MonitoringGap` olmasına göre yapılmalı, yalnızca skora göre değil.

### S5 · Değerlendirici inceleme paneli

- **İnceleme bekleyen oturum sayısı panelde görünmeli (FR-29).** Sayıyı A4 sağlayacak.
- **İşaretler ayrı ayrı gösterilmeli (FR-27).** Riskli, ajansız, göstergeli, izinsiz süre ve izleme kesintisi işaretleri listede rozet olarak görünmeli.

---

## Açık konular

| # | Konu | İlgili | Öneri |
|---|---|---|---|
| S-1 | A4'teki not ucu için ayrı tablo mu, `ReviewerDecision.Note` mu? | Ayşe, Şeyma | Birden fazla not gerekecekse A4'te `ReviewerNote` tablosu |
| S-2 | Şifre özetleme yöntemi | Esma | Şimdilik PasswordHasher; değişirse yalnızca `IdentityPasswordHasher` etkilenir |
| S-3 | HMAC anahtarının şifreli saklanması (K-08) | Yağmur | Y2'de bu alanın kullanılması |
| S-4 | Docker ortamında göçlerin uygulanması (K-07) | Yağmur | Compose'da ayrı migrate adımı |
| S-5 | A6, E5b'ye bağlı ama tarihleri çakışıyor | Ayşe, Esma | Locust betiği 29 Kasım'da, ölçüm E5b'den sonra |
| S-6 | Cevaptaki sorunun oturumun sınavına ait olması veritabanında zorlanmıyor | Esma (E4) | Serviste kontrol ve test; gerekirse `answers.exam_id` ile bileşik yabancı anahtar |
| S-7 | Uygulama yerelde `postgres` süper kullanıcısıyla bağlanıyor | Ayşe (A5), Yağmur (E1b) | Docker/sunucu için yalnızca gerekli yetkilere sahip ayrı kullanıcı; göçler ayrı kullanıcıyla |
| S-8 | `agent_signals` hızlı büyüyor; kullanıcı silme talebi | Ayşe (A5) | Saklama görevine eklenmesi; kişisel alanların anonimleştirilmesi |
| S-9 | Olay ucu E2 gelene kadar yalnızca Development'ta açık | Esma (E2), Ayşe | E2 birleşince `[DevelopmentOnly]` yerine aday rolü ve oturum sahipliği kontrolü |
| S-10 | `riskconfig.json` ağırlıkları başlangıç tahmini | Tüm ekip | Pilot oturumda ayarlanması, deneylerde sabit tutulması |
| S-11 | Ajan aynı göstergeyi her sinyalde bildirirse her seferinde olay oluşur | Yağmur (Y2–Y4) | Gösterge yalnızca ilk görüldüğünde ve değiştiğinde olay olarak bildirilmeli |
| S-12 | Eşleştirme kodu ucu (`POST /api/sessions/{id}/agent-code`) E2 gelene kadar yalnızca Development'ta açık; kart Candidate rolü ve "yalnızca kendi oturumu" istiyor | Esma (E2), Yağmur | E2 birleşince `[DevelopmentOnly]` yerine aday rolü ve oturum sahipliği kontrolü (S-9 ile aynı) |
| S-13 | GAB FR-26: 5 dakikadan uzun bağlantı kesintisi "yüksek ağırlıklı uzun kesinti olayı" üretmeli; ortak olay listesinde böyle bir tür yok | Esma (E4), Ayşe | Ya `LONG_DISCONNECT` türü eklenir (ortak ad değişikliği, ekip kararı), ya da `CONNECTION_LOST` 300 sn'yi aşınca daha yüksek ağırlıkla puanlanır |
| S-14 | `POST /api/agent/pair` eşleşme kimliği dönüyor ama ajana henüz bir anahtar vermiyor | Yağmur (Y2) | Y2'de eşleşme anında HMAC oturum anahtarı üretilip `EncryptedSessionKey`'e yazılması ve ajana bu yanıtta verilmesi; ajan uçları JWT değil HMAC ile korunur |
