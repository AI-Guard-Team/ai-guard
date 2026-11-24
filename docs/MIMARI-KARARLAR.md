# Arka Uç Mimari Kararları

Bu belgede `api/` tarafında aldığımız tasarım kararlarını ve gerekçelerini topluyoruz. Amaç, bir karta başlayan herkesin aynı kurallarla ilerlemesi ve "bu neden böyle yapılmış?" sorusunun cevabını tek yerde bulabilmesi.

Bir kararı değiştirmek gerekirse önce ekipte konuşalım, sonra belgeyi ve kodu aynı PR'da güncelleyelim. Yeni karar eklerken sıradaki numarayı kullanın; eski numaraları değiştirmeyin, çünkü kod yorumlarında bu numaralara atıf var.

*Son güncelleme: 21 Kasım 2026 (D2)*

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
| Kimlik doğrulama | 15 dk JWT + tek kullanımlık yenileme belirteci; **her uç varsayılan olarak giriş istiyor**, açık uçlar `[AllowAnonymous]` (K-20) |
| Oturum uçları | Aday yalnızca kendi oturumuna erişir (`SessionAccessService.IsCandidateOfAsync`); başkasının oturumuna 404 (K-20) |
| Ajan sinyali | HMAC-SHA256 imzalı, ±30 sn, tek kullanımlık nonce; anahtar API belleğinde, veritabanında özeti (K-21) |
| Cihaz göstergeleri | `{ type, name, detail }`, yalnızca eşleşenler; ilk görüldüğünde olay; hipervizör yalnızca bilgi (K-22) |
| Ajansız / göstergeli devam | Gerekçe oturuma, işlem denetim kaydına; `Flags += NoAgent` / `Indicated`; ajan çalışırken ajansız devam yok (K-25) |
| Sınav yönetimi | Doğru seçenek yalnızca Admin'e; oturum başlamış sınav kilitli (409); başlatma kuralı `ExamRules.CanStart` (K-23) |
| Oturum durumu | Yalnızca `session.TransitionTo(...)`; tablo dışı geçiş 409. Süre ve hak `InProgress`'e geçişte başlar (K-24) |
| Şifreleme | Tek sınıf (`IFieldEncryptor`), AES-256-GCM, kayda bağlı bağlam; kanıt kareleri şifreli dosyada (K-26) |
| Biyometrik akış | Fotoğraf saklanmıyor; geçti/kaldı kararı görüntü servisinde, durum geçişleri E4'ten; başarısız deneme olay ve kanıt karesiyle (K-27) |
| Görüntü/ses modelleri | Model listesi GAB 3.3.5 ile aynı; ArcFace, MiniFASNet (V2 + V1SE), YOLO11n ve Silero VAD ONNX olarak onnxruntime ile, yüz noktaları MediaPipe'tan; torch ve ultralytics serviste yok (K-28) |
| Yüz doğrulama kararı | Herhangi bir karede 2+ yüz `MULTIPLE_FACES`, kullanılabilir kare yarıdan azsa `NO_FACE`, skorlar kullanılabilir karelerin ortalaması; eşikler `config.yaml`'da: benzerlik 0,164 (LFW, FAR %1), canlılık 0,5; görüntü 5 MiB / kare 1 MiB, kenar en fazla 6000 px (K-29) |

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
| Ajan oturum anahtarı | Veritabanında SHA-256 özeti (`AgentPairing.SessionKeyHash`); anahtarın kendisi API belleğinde (K-21) | Y2 kartı GAB'a göre veritabanında yalnızca özet istiyor; HMAC doğrulaması bellekteki anahtarla yapılıyor |
| Yüz vektörü | `ReferenceFaceTemplate.EncryptedVector`, AES-GCM | KVKK kapsamında özel nitelikli veri |
| Kanıt karesi | Dosya deposunda şifreli; tabloda yalnızca `StorageKey` | Büyük dosyaları veritabanı yedeklerinin dışında tutmak için |

`KeyVersion` alanları anahtar değiştirildiğinde eski kayıtların hangi anahtarla açılacağını belirtiyor. Anahtarlar ortam değişkenlerinden okunuyor.

GAB'da ajan için "oturum anahtarı özeti" yazıyor. A1'de özetten HMAC imzası doğrulanamadığı için anahtarı şifreli tutmayı planlamıştık; Y2 kartının revize hâli GAB'ı esas aldı: veritabanında yalnızca özet, anahtarın kendisi API belleğinde (K-21). `EncryptedSessionKey` alanı bu yüzden kullanılmıyor (S-16). Diğer şifreli alanlar için ortak sınıf A3'te yazıldı: `IFieldEncryptor` (K-26).

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
| `ExamSession.ContinueJustification` | Y4 (kullanılıyor, K-25) |
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

**Yetki (E2'de yapıldı):** Uç yalnızca `Candidate` rolüne açık ve aday yalnızca kendi oturumuna olay yazabiliyor (K-20). `[DevelopmentOnly]` kaldırıldı.

**IP kontrolü (FR-22):** `SessionIpGuard.CheckAsync` oturumla ilgili her istekte çağrılmalı (E4'ün uçları dahil). İlk IP oturuma yazılıyor; farklı bir IP gelince bir kez `IP_CHANGE` üretiliyor. `::ffff:1.2.3.4` ile `1.2.3.4` aynı sayılıyor. API proxy arkasındaysa `X-Forwarded-For` yalnızca bilinen proxy'den kabul edilmeli (E1b). **E1b'de yapıldı:** Compose'da `web` (nginx) API'nin önünde. `Api/TrustedProxySetup` başlığı yalnızca `ForwardedHeaders:KnownNetworks` ayarındaki adresten (web container'ı, 172.30.47.10) kabul ediyor. Ayar yoksa (Docker'sız çalışma) hiçbir şey değişmiyor.

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

1. Oturum `AgentPairing` durumundayken tarayıcı `POST /api/sessions/{id}/agent-code` ile kod alıyor (UC-08 adım 1). Kod adaya gösterilmiyor.
2. Tarayıcı kodu `POST http://127.0.0.1:47321/pair { code }` ile ajana gönderiyor.
3. Ajan kodu `POST /api/agent/pair { code, os }` ile TLS üzerinden sunucuya iletiyor. Sunucu kodu doğrulayıp kullanılmış sayıyor, işletim sistemini (`AgentPairing.Platform`) ve eşleşme zamanını kaydediyor.

`/api/agent/pair` JWT ile değil eşleştirme koduyla korunuyor; ajanın JWT'si yok. Y2'deki sinyal uçları da HMAC ile korunacak (S-14).

**"Aynı bilgisayar" kanıtı neye dayanıyor:** Ajan yalnızca `127.0.0.1`'i dinliyor; ağdaki başka bir bilgisayar bu porta bağlanamıyor. Kod tarayıcıdan yalnızca bu yolla çıktığı için kodu sunucuya getirebilen ajan, tarayıcıyla aynı bilgisayardadır. Başka bilgisayardaki bir ajanın tek şansı kodu tahmin etmek. Kod 256 bit rastgele olduğu için bu pratikte imkânsız, bu yüzden deneme sayısı sınırı koymadık.

| Kural | Gerekçe |
|---|---|
| Kod 5 dakika geçerli, tek kullanımlık (kartta önerilen süre) | Tarayıcı kodu hemen gönderiyor; sızan bir kodun işe yarayacağı süre kısa kalıyor |
| Kod yalnızca oturum `AgentPairing` durumundayken üretiliyor | Sınav başladıktan sonra yeni kod alınıp oturuma başka bir ajan bağlanamasın |
| İşletim sistemi (`os`) zorunlu; bildirilmezse eşleşme reddediliyor, kod tüketilmiyor | Ajan değerlendirmesi (Y7a, Y7b) sonuçları işletim sistemine göre ayırıyor. Değeri tarayıcı değil ajanın platform modülü veriyor |
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

**Ajan projesi:** Ortak kod `AIGuard.Agent.Core`'da. `AIGuard.Agent.Windows` ve `AIGuard.Agent.MacOS` yalnızca giriş noktası ve kendi `IDeviceInspector` uygulamasını içeriyor. Windows cihaz incelemesi Y3'te yapıldı (K-22).

**Yetki (E2'de yapıldı):** Kod üreten uç yalnızca `Candidate` rolüne ve oturumun sahibine açık; `/api/agent/pair` `[AllowAnonymous]`, kanıt kodun kendisi (K-20).

---

## K-20 · Kimlik doğrulama ve yetkilendirme (E2)

FR-01–FR-03. Önyüzle sözleşme `web/src/api/auth.ts` ve `client.ts` ile birebir aynı; bir alanı değiştirirseniz ikisini birlikte değiştirin.

| Uç | Yetki | Gövde → Yanıt |
|---|---|---|
| `POST /api/auth/login` | açık | `{email, password}` → `200 {accessToken, accessTokenExpiresAt, refreshToken}` · 401 · 429 (kilit) |
| `POST /api/auth/refresh` | açık | `{refreshToken}` → `200` yeni çift · 401 |
| `POST /api/auth/logout` | açık | `{refreshToken}` → `204` (belirteç yoksa da 204) |
| `POST /api/auth/set-password` | açık | `{token, password}` → `204` · 400 |
| `POST /api/auth/change-password` | giriş yapmış | `{currentPassword, newPassword}` → `204` · 400 |
| `POST /api/users` | Admin | `{fullName, email, role}` → `201 {id}` · 400 · 409; davet e-postası gider |
| `GET /api/users?role=` | Admin | → `[{id, fullName, email, role, isActive, inviteStatus, createdAt}]` |
| `PATCH /api/users/{id}` | Admin | `{role?, isActive?}` → `204` · 400 · 404 |

- `change-password` kartta yok; GAB 3.4.5'teki "Profil → Şifre Değiştir" ve S1'deki ekran için eklendi (adı Şeyma'nın önyüzünden).
- `GET` ve `PATCH /api/users` kartta yok; FR-03 (Zorunlu) ve E6'daki yönetici ekranı için eklendi. Yönetici kendi rolünü veya durumunu değiştiremiyor (sistemde yönetici kalmayabilir).
- Açık kayıt ucu yok.

**Belirteçler**

| Kural | Değer | Gerekçe |
|---|---|---|
| Erişim belirteci | HMAC-SHA256 JWT, 15 dk; talepler `sub`, `role`, `email`, `name`, `jti` | Önyüz kısa adları okuyor (`web/src/auth/jwt.ts`); `MapInboundClaims = false` |
| Anahtar | `JWT_SECRET` (ortam değişkeni / user-secrets), en az 32 bayt; yoksa uygulama açılmıyor | Koda gömülü varsayılan yok |
| Yenileme belirteci | 256 bit rastgele, 7 gün, tek kullanımlık; veritabanında SHA-256 özeti (K-08) | Her kullanımda yenisi veriliyor (rotation); kartta süre yok |
| Yenilenmiş belirteç tekrar gelirse | Kullanıcının bütün yenileme belirteçleri iptal | Çalınma belirtisi; saldırgan mı kullanıcı mı bilinemiyor |
| Eşzamanlı yenileme | `refresh_tokens.xmin` iyimser kilidi; ikinci istek 401 | Tek kullanım iki eşzamanlı istekle aşılmasın |
| Davet | 72 saat, tek kullanımlık; kullanılınca özet siliniyor | Kart |
| Hatalı giriş | 5 denemede 15 dk kilit (429); kilitliyken şifre denenmiyor | Kart; 15 dk bizim seçimimiz |
| Şifre kuralı | 8–128 karakter, yalnızca boşluk olamaz | NIST SP 800-63B; üst sınır PBKDF2'yi yormamak için |
| Şifre değişince | Bütün yenileme belirteçleri iptal | Çalınmış oturum kapanmalı; mevcut sekme de en geç 15 dk sonra yeniden giriş ister |

**Her istekte kullanıcı kontrolü (FR-02).** JWT'nin imzası ve süresi geçerli olsa da `OnTokenValidated` kullanıcıyı veritabanından okuyor: pasif ya da rolü belirteçtekinden farklıysa istek 401 alıyor. Pasife alınan kullanıcının yenileme belirteçleri de iptal ediliyor.

**Varsayılan olarak giriş gerekli.** `FallbackPolicy = RequireAuthenticatedUser`. Yeni bir uç yazarken rolü belirtin (`[Authorize(Roles = nameof(UserRole.Admin))]`). Herkese açık uçlar yalnızca şunlar: `/health`, `api/auth/*` (change-password hariç), `/api/agent/pair` (kanıt eşleştirme kodu). Yeni bir açık uç gerekirse `[AllowAnonymous]` ve gerekçesi yazılmalı. Y2'deki ajan sinyal ucu da JWT değil HMAC ile korunacağı için `[AllowAnonymous]` olacak.

**Oturum sahipliği.** Oturum kimliği alan aday uçları önce `SessionAccessService.IsCandidateOfAsync(sessionId, User.GetUserId())` çağırıyor; başkasının oturumuna 403 değil 404 dönüyor (oturumun var olduğu bilgisi sızmasın). E4, A3 ve S4'teki yeni oturum uçları da aynı kontrolü yapmalı. Değerlendiricinin "yalnızca atandığı sınav" kontrolü E3/A4'te benzer bir yöntemle eklenmeli.

**Hata yanıtları.** ProblemDetails; kullanıcıya gösterilecek Türkçe metin `detail` alanında (önyüz bunu gösteriyor). Giriş hatasında e-postanın var olup olmadığı veya hesabın pasif olup olmadığı ayrılmıyor; var olmayan e-postada da sahte bir özet doğrulanıyor ki yanıt süresinden anlaşılmasın. `change-password`'da yanlış mevcut şifre 401 değil 400: 401 önyüzde belirteç yenilemeyi tetikleyip kullanıcıyı çıkarırdı.

**Denetim kaydı (FR-35).** `user.login`, `user.lockout`, `user.create`, `user.invite_accept`, `user.password_change`, `user.role_change`, `user.activate`, `user.deactivate`, `auth.refresh_reuse`. Eski ve yeni değere yalnızca rol ve durum yazılıyor (K-10).

**E-posta.** Kartta istendiği gibi sahte gönderici (`Infrastructure/Email/LoggingEmailSender`) davet bağlantısını loga yazıyor. Bağlantı `Web:BaseUrl` ayarı (varsayılan `http://localhost:5173`) + `/set-password?token=`. Bağlantı bir kimlik bilgisi olduğu için gerçek kullanımda SMTP gibi bir gönderici gerekir (S-15).

**Testlerde.** HTTP testleri `IntegrationTests/TestApi` ile yazılıyor: `Api.As(user)` kayıtlı kullanıcı adına belirteçli istemci, `Api.Anonymous()` girişsiz istemci, `Api.Emails.InviteTokenFor(email)` yakalanan davet belirteci. Testlerdeki JWT anahtarı sabit (`TestApi.JwtSecret`).

**Yerel kurulum.** `dotnet user-secrets set "JWT_SECRET" "<en-az-32-karakter-rastgele>" --project src/AIGuard.Api` (README).

---

## K-21 · Ajan sinyalleri ve oturum anahtarı (Y2)

FR-21: ajan sinyalleri sahte üretilememeli ve tekrar oynatılamamalı. Akış (UC-08 adım 3–5):

1. Eşleşmede (`POST /api/agent/pair`) sunucu 32 baytlık rastgele oturum anahtarı üretiyor ve yanıtta (`sessionKey`, Base64) TLS üzerinden ajana veriyor. Yeniden eşleşmede yeni anahtar üretiliyor, eskisi geçersiz.
2. Ajan anahtarı yalnızca bellekte tutuyor ve `Agent:SignalIntervalSeconds` (varsayılan 5 sn) aralıkla imzalı sinyal gönderiyor: `POST /api/agent/signal { sessionId, ts, nonce, indicators[], sig }`.
3. `sig = Base64(HMAC-SHA256(anahtar, "{sessionId}|{ts}|{nonce}|{indicatorsJson}"))`. `sessionId` küçük harf tireli Guid, `ts` Unix saniye, `indicatorsJson` gösterge nesnelerinin System.Text.Json varsayılanıyla sıkışık dizisi (`[]`, `[{"type":"VM_DETECTED","name":"VirtualBox","detail":"wmi"}]`; Y3, K-22). Biçim iki projede ayrı yazıldığı için (`Application/Agents/AgentSignature`, `Agent.Core/Signals/SignalSigner`) iki taraftaki `Known_answer` testi aynı sabit örnekte aynı imzayı bekliyor.

| Kural | Gerekçe |
|---|---|
| Veritabanında yalnızca anahtarın SHA-256 özeti (`SessionKeyHash`); anahtar `IAgentKeyStore` ile API belleğinde | GAB nesne modeli; HMAC doğrulamak için anahtarın kendisi gerekiyor. Bellekteki anahtar özetle eşleşmezse (eski anahtar, başka oturum) kullanılmıyor |
| İmza `CryptographicOperations.FixedTimeEquals` ile karşılaştırılıyor | Erken dönen karşılaştırma yanıt süresinden imzanın doğru baytlarını sızdırır |
| Kontrol sırası: imza → nonce tekrarı → zaman (±30 sn) | İmzası bozuk istekteki zaman ve nonce'a güvenilemez |
| Nonce tekrarı veritabanından kontrol ediliyor | Her sinyal zaten `AgentSignal` olarak saklanıyor ve `(eşleşme, nonce)` benzersiz (A1); kart "son N nonce" diyor, veritabanı bütün nonce'ları kapsıyor ve API yeniden başlasa da kayboluyor |
| Reddedilen sinyal `AGENT_SIGNAL_INVALID` olayı; ayrıntıda `{"reason":"signature"|"replay"|"stale"}` | Kart. Ajana yanıtta neden ayrılmıyor (401) |
| Her sinyal `AgentSignal` olarak saklanıyor, geçersizler dahil; `SignatureValid` yalnızca kabul edilenlerde true | Gecikmiş sinyalin imzası doğru olsa da kabul edilmiyor. Tekrar gönderilen sinyal ikinci kez saklanamıyor (benzersiz indeks), yalnızca olay yazılıyor |
| Anahtar bellekte yoksa (API yeniden başladı) sinyal 401, olay yazılmıyor | Ajan suçsuz; sinyaller doğrulanamadığı için 30 sn sonra `AGENT_SIGNAL_LOST` yine üretiliyor |
| Geçerli sinyal `LastSignalAt`'i sunucu saatiyle güncelliyor | 30 sn kuralı ajanın saatine bağlı olmasın |
| İlk geçerli ve göstergesiz sinyalde `AgentPairing → IdentityVerification` (Y3: yalnızca `HYPERVISOR_INFO` varsa da) | UC-08 adım 5. Durum makinesi (E4) gelince bu geçiş oraya taşınmalı. Gösterge varsa geçiş yok; gerekçeyle devam Y4'te |
| Oturum bitince (`Completed`, `Cancelled`, `Decided`) anahtar bellekten siliniyor, özet boşaltılıyor; sinyal 410 alıyor ve ajan anahtarı unutup göndermeyi bırakıyor | FR-21 |
| 30 sn sinyal yoksa (eşleşmeden ya da son geçerli sinyalden beri) `AGENT_SIGNAL_LOST`; her kesinti için tek olay, başlangıcı son sinyalin anı | Kart. Her denetimde yeni olay yazılsaydı skor kesinti süresince durmadan artardı |
| `/api/agent/signal` `[AllowAnonymous]`; kanıt HMAC imzası, gövde en fazla 8 KB (Y3'te 4 KB'tan çıktı), en fazla 32 gösterge | Ajanın JWT'si yok (K-20) |
| Anahtar, imza ve kod loglara yazılmıyor | Kart |

**Zamanlayıcı:** `Api/Agents/AgentSignalWatchdog` her 5 sn'de `AgentSignalMonitor.RunOnceAsync`'i çalıştırıyor: biten oturumların anahtarlarını siliyor, sessiz oturumlara `AGENT_SIGNAL_LOST` yazıyor. Mantık `Application`'da; testler saati ileri alarak bekleme yapmadan sınıyor.

**E4'e not:** Oturumu bitiren kod `AgentSignalService.RevokeAsync(sessionId)` çağırırsa anahtar hemen geçersiz oluyor; çağrılmazsa zamanlayıcı en geç 5 sn içinde yapıyor. `AgentPairing → IdentityVerification` geçişi durum makinesi gelince oraya taşınmalı.

**Sınırlılıklar:** API yeniden başlarsa bellekteki anahtarlar kaybolur, o oturumlar `AGENT_SIGNAL_LOST` alır (kart). Anahtarlar tek süreçte tutulduğu için API birden fazla örnekle çalıştırılırsa anahtar paylaşılmaz (S-17).

---

## K-22 · Cihaz göstergeleri (Y3)

FR-18, FR-19, UC-08 4a/4b. Ajan sanal makine, genel hipervizör ve yasaklı yazılım göstergelerini imzalı sinyalin `indicators` alanında gönderiyor; olayları API üretiyor (K-16).

**Biçim.** `indicators: [{ "type": "...", "name": "...", "detail": "..." }]`. `type` yalnızca `VM_DETECTED`, `HYPERVISOR_INFO` veya `FORBIDDEN_APP` olabilir; `name` en fazla 64, `detail` en fazla 128 karakter. Başka bir tür ya da eksik ad gelirse sinyal bozuk sayılıyor (400). İmzalanan metindeki `indicatorsJson` bu nesnelerin JSON'u (K-21 madde 3). Sunucuda `Application/Agents/AgentIndicator`, ajanda `Agent.Core/Platform/DeviceIndicator`; alan adları `JsonPropertyName` ile sabit.

| Kural | Gerekçe |
|---|---|
| Ajan yalnızca eşleşen göstergeleri gönderiyor; süreç adları ajanın dışına çıkmıyor, loga da yazılmıyor | KVKK, FR-19 |
| Gösterge yalnızca ilk görüldüğünde ve kaybolup yeniden göründüğünde olay oluyor; karşılaştırma önceki geçerli sinyalle, tür + ad üzerinden | S-11. Ajan 5 sn'de bir sinyal gönderdiği için her sinyalde olay yazılsaydı skor durmadan artardı. Uygulama kapatılıp yeniden açılırsa yeni olay |
| Olay ayrıntısı `{"name": "...", "detail": "..."}` | Değerlendirici neyin, nereden bulunduğunu görsün |
| `HYPERVISOR_INFO` tek başına oturumu bekletmiyor: başka gösterge yoksa `AgentPairing → IdentityVerification` | UC-08 4b. `riskconfig.json`'da ağırlık 0,5 ve `MaxTotal: 1`; oturumu riskli yapamıyor |
| VirtualBox ve VMware için yalnızca **konuk** izleri: WMI (`Win32_ComputerSystem`), BIOS (`Win32_BIOS`), konuk eklentisi kayıt anahtarı, konuk sürücüleri/servisleri | Bilgisayarda VirtualBox ya da VMware Workstation **kurulu olması** sanal makinenin içinde olunduğunu göstermez; ana bilgisayar kurulumunun anahtarları (`SOFTWARE\Oracle\VirtualBox`, `VBoxSup`, `vmx86` vb.) aranmıyor |
| Sanal makine bulunduysa `HYPERVISOR_INFO` ayrıca gönderilmiyor | Sanal makinenin içinde hipervizör zaten var; aynı şeyin iki kez bildirilmesi olur |
| Genel hipervizör `Win32_ComputerSystem.HypervisorPresent` ile; ayrıntıda bulunan bileşenler (`vmcompute` → Hyper-V, `WslService`/`LxssManager` → WSL, `com.docker.service` → Docker Desktop), hiçbiri yoksa "VBS ya da başka bir Windows bileşeni" | Kart: Hyper-V, WSL2, VBS, Docker |
| Uzak Masaüstü iki ayrı gösterge: `mstsc.exe` listede ("Uzak Masaüstü bağlantısı", aday başka bilgisayara bağlanıyor); dışarıdan bağlanılması `GetSystemMetrics(SM_REMOTESESSION)` ile ("Uzak Masaüstü oturumu") | Kart: mstsc adayın bilgisayarına bağlanılmasını göstermez. Ortak olay listesinde ayrı tür olmadığı için ikisi de `FORBIDDEN_APP` |
| Yasaklı yazılım listesi `forbidden-apps.json`, ajanın yanında (`Agent:ForbiddenAppsPath`); dosya yoksa ya da bozuksa ajan açılmıyor | Kart: liste yapılandırma dosyasında. Liste okunamadığında tespitin sessizce kapanması yerine ajanın açılmaması |
| `forbidden-apps.json` API container'ına bağlanmıyor | API bu dosyayı kullanmıyor; E1b'de bırakılan "compose'a eklenecek" notu kaldırıldı |
| Denetim sıklığı: sanal makine ve hipervizör bir kez, süreç listesi `Agent:ProcessScanIntervalSeconds` aralıkla (varsayılan 15 sn), uzak masaüstü oturumu her sinyalde | GAB 3.3.3: ortalama işlemci kullanımı %5'in altında. Ölçüm: tek çekirdeğe göre %1,1 (`docs/y3-windows-deneme-notlari.md`) |
| Bütün denetimler kullanıcı modunda | Kart: yönetici izni istenmiyor |
| Gövde sınırı 4 KB'tan 8 KB'a çıktı | Gösterge artık nesne; 32 gösterge 4 KB'a sığmıyor |

**Y4'e not:** Göstergeli oturumda (`VM_DETECTED` veya `FORBIDDEN_APP`) oturum `AgentPairing`'de bekliyor. Gerekçeyle devam ve `SessionFlags.Indicated` Y4'te.

**Sınırlılıklar:** OBS sanal kamerası OBS sürecinden anlaşılıyor; kamera aygıtı ayrıca denetlenmiyor. AnyDesk ve TeamViewer servis olarak kuruluysa aday kullanmasa da süreç çalışıyor ve gösterge oluşuyor. Hyper-V konuğu (Hyper-V'de açılmış sanal makine) karta göre `HYPERVISOR_INFO` sayılıyor, `VM_DETECTED` değil. macOS göstergeleri bu kartta yok.

**macOS (Y7a).** Çekirdek macOS'ta değişmeden çalışıyor; macOS modülü yalnızca `IDeviceInspector`'ı uyguluyor. `Platform`, gösterge biçimi ve yukarıdaki "kurulum izi değil konuk izi" kuralı aynı kalıyor. Ayrıntılar ve doğrulananlar `agent/README.md` "macOS modülü için sözleşme" bölümünde.

---

## K-23 · Sınav, soru ve atama yönetimi (E3)

FR-06–FR-08. Kod: `Application/Exams/` (`ExamService`, `AssignmentService`, `ExamRules`), `Api/Controllers/ExamsController.cs`.

| Uç | Yetki | Not |
|---|---|---|
| `GET /api/exams` | giriş yapmış | Admin hepsini; Reviewer ve Candidate yalnızca atandığı sınavları görür |
| `GET /api/exams/{id}` | giriş yapmış | Atanmadığı sınav 404 (var olduğu bilgisi verilmiyor) |
| `POST /api/exams` · `PUT/DELETE /api/exams/{id}` | Admin | `{name, type, durationMinutes, windowStartAt, windowEndAt, retentionDays?}` |
| `GET/POST /api/exams/{id}/questions` | Admin | `{text, options[], correctOptionIndex, order?}`; `order` yoksa sona eklenir |
| `PUT/DELETE /api/questions/{id}` | Admin | Sıralama `PUT` ile `order` alanından |
| `GET/POST /api/exams/{id}/assignments` | Admin | `{userId, role: Candidate/Reviewer}` |
| `DELETE /api/assignments/{id}` | Admin | |

`GET /api/exams/{id}/assignments` kartta yok; E6'daki atama ekranı için eklendi.

**Sınav yanıtı (`ExamSummary`):** `latestStartAt` (= pencere bitişi − süre), `questionCount` ve yalnızca adaya dönen `attemptUsed` (GAB 3.3.1: aday listede pencereyi, son başlangıç zamanını ve hakkını görür). Sorular ve doğru seçenek bu yanıtta hiç yok.

**Doğru seçenek adaya gitmiyor.** Doğru seçenekli soru listesi yalnızca Admin'e açık. Adaya sorular E4'teki oturum ucundan, doğru seçeneği olmayan ayrı bir DTO ile gitmeli.

**Kurallar `ExamRules` içinde (veritabanısız, birim testli):**

| Kural | Değer |
|---|---|
| Son başlangıç zamanı | `WindowEndAt − DurationMinutes`; pencere sınav süresinden kısa olamaz |
| Başlatma izni | `ExamRules.CanStart(exam, assignment, now)` → `null` ya da `NotAssigned` / `AttemptUsed` / `WindowNotOpen` / `TooLate`. **E4'teki oturum başlatma ucu bu fonksiyonu kullanmalı**, kuralı ikinci kez yazmamalı |
| Süre | 1–600 dk |
| Saklama süresi | 1–3650 gün, verilmezse 30 (GAB veri yaşam döngüsü) |
| Soru | Metin 1–2000 karakter; 2–10 seçenek, her biri 1–500 karakter; doğru seçenek aralıkta |
| Zamanlar | UTC'ye çevrilerek saklanıyor (K-15) |

**Oturum başlamış sınav kilitli.** Sınavda tek bir oturum bile varsa sınav düzenlenemiyor, silinemiyor, soru eklenemiyor, düzenlenemiyor ve silinemiyor (409). Aksi hâlde adayların bir kısmı eski sorularla, bir kısmı yenileriyle sınava girmiş olur ve verilen cevaplar anlamsızlaşır. Kartta yok; veri bütünlüğü için eklendi.

**Atama kuralları.** Atama rolü kullanıcının rolüyle aynı olmalı (aday hesabı değerlendirici olarak atanıp başka adayların kanıtlarını göremesin). Pasif kullanıcı atanamıyor. Aynı kullanıcı aynı sınava ikinci kez atanınca 409 (benzersiz indeks, `IAppDbContext.IsUniqueViolation`). Sınava girmiş adayın ataması kaldırılamıyor (409).

**Swagger.** Yalnızca Development'ta: `http://localhost:5080/swagger`. Sağ üstteki **Authorize** düğmesine `/api/auth/login` yanıtındaki `accessToken` yapıştırılınca uçlar denenebiliyor. Controller'lardaki `///` açıklamaları Swagger'da görünüyor (`GenerateDocumentationFile`).

**Örnek veri.** Kartın istediği örnek sınav A1'deki `DevDataSeeder`'da zaten var: 3 soruluk Genel Yetenek sınavı, iki aday ve değerlendirici atanmış.

**Değerlendirici için sonraki kartlar.** "Değerlendirici yalnızca atandığı sınavı görür" kuralı burada sınav listesi için uygulandı. A4'teki inceleme uçları da aynı kontrolü yapmalı: `ExamAssignments`'ta `(ExamId, UserId, Role = Reviewer)` kaydı.

---

## K-24 · Oturum durum makinesi ve sınav akışı (E4)

FR-09, FR-26, GAB 3.4.4. Kod: `Domain/Sessions/SessionStateMachine.cs`, `Application/Sessions/` (`SessionService`, `SessionTimekeeper`), `Api/Controllers/SessionsController.cs`.

**Durum yalnızca durum makinesinden değişir.** `session.TransitionTo(SessionState.X)`; tabloda olmayan geçiş `InvalidSessionTransitionException` fırlatır, API bunu 409 ProblemDetails'e çevirir. `session.State = ...` diye doğrudan atama yapmayın (testlerde kurulum hariç). Tablo GAB 3.4.4 ile birebir; `SessionStateMachineTests` her durum çiftini sınıyor. Y2'deki `AgentPairing → IdentityVerification` geçişi de artık buradan.

| Uç | Yanıt |
|---|---|
| `POST /api/exams/{examId}/sessions` | 201 `{sessionId, state}` yeni oturum (ConsentPending) · 200 adayın açık oturumu varsa aynısı · 404 atanmamış · 409 pencere dışında / hak kullanılmış (Türkçe `detail`) |
| `GET /api/sessions/{id}` | `{id, examId, examName, state, durationMinutes, startedAt, endsAt, serverTime, questions[], answers[], lastSeq}` |
| `POST /api/sessions/{id}/answers` | `{questionId, selectedOption, seq, clientTime}` → 201 kaydedildi · 200 aynı seq tekrar geldi · 400 · 409 süre doldu / durum uygun değil |
| `POST /api/sessions/{id}/submit` | 204 · 409 zaten teslim edildi / sınav başlamadı |

Hepsi yalnızca `Candidate` ve yalnızca kendi oturumu (K-20); her istekte IP kontrolü (`SessionIpGuard`, FR-22).

**Süre ve oturum hakkı `InProgress`'e geçişte başlar.** `StartedAt = şimdi`, `EndsAt = şimdi + süre` (sunucu saati), `ExamAssignment.AttemptUsedAt = şimdi`. Böylece rızayı reddeden adayın hakkı tüketilmiyor (FR-10) ve risk gerekçelerindeki "12. dakikada" sayımı (K-17) aynı andan başlıyor. Rıza, eşleştirme ve doğrulama adımları süreye dahil değil. Başlatma kuralları E3'teki `ExamRules.CanStart`'tan (K-23).

**Cevaplar.** Her gönderim ayrı satır; bir soru için en yüksek `seq` geçerli (geç gelen eski cevap yenisinin üzerine yazamıyor). Aynı `seq` aynı cevapla tekrar gelirse 200 ve yeni kayıt yok; farklı cevapla gelirse 409. `selectedOption: null` cevabı temizler. Soru oturumun sınavına ait olmalı (S-6). Süre dolduktan sonra gelen cevap, otomatik teslim görevi henüz çalışmamış olsa da reddediliyor. `clientTime` yalnızca kayıt için.

**Sorular** yalnızca `InProgress`'te dönüyor, doğru seçenek olmadan. `PermissionPending`'de sorular gizli ve cevap kabul edilmiyor (FR-17).

**Otomatik teslim ve bağlantı kesintisi (`SessionTimekeeper`, 5 sn'de bir).**
- `EndsAt` geçen ve süren (`InProgress`, `PermissionPending`, `Disconnected`) oturum `Completed` olur; `EndedAt = EndsAt` (FR-26: sunucudaki cevaplarla teslim).
- `InProgress` oturumdan **30 sn** istek gelmezse (`LastSeenAt`) oturum `Disconnected` olur; `DisconnectedAt = LastSeenAt`. Süre işlemeye devam eder.
- `Disconnected` iken gelen cevaplar `Unmonitored = true` ("izlenmeyen sürede verildi", FR-09).
- Ayar: `Sessions:TimekeeperEnabled` (testlerde kapalı), `Sessions:TimekeeperIntervalSeconds`.

**Eşzamanlılık.** Teslim, otomatik teslim, cevap ve olay aynı oturum satırını kilitliyor (K-18); aynı anda gelseler de sırayla işleniyor.

**Diğer kartların çağıracağı geçişler:**

| Kart | Ne zaman | Çağrı |
|---|---|---|
| A3 | Rıza verildi / reddedildi | `session.TransitionTo(AgentPairing)` / `TransitionTo(Cancelled)` (iptalde hak tüketilmiyor) |
| A3 | İlk doğrulama başarılı | `SessionService.BeginExamAsync(sessionId)`: süreyi ve hakkı başlatır |
| A3 | Kesinti sonrası doğrulama başarılı | `SessionService.ResumeAfterReconnectAsync(sessionId)`: `Disconnected → InProgress` ve kesinti süresiyle `CONNECTION_LOST` olayı |
| A3 | Kayıt fotoğrafı yoksa sınav başlamaz (FR-04) | A3'te oturum rızası adımında yapıldı (K-27); `StartAsync` değişmedi. E5a'da aday önce fotoğraf yüklemeli |
| A3/A5 | Biyometrik rıza sınav sırasında geri alındı | `TransitionTo(Cancelled)` + denetim kaydı (FR-05) |
| S4 | Kamera/mikrofon izni kaldırıldı / verildi | `SessionService.RevokePermissionAsync` / `RestorePermissionAsync`; uç `POST /api/sessions/{id}/permission` (K-30) |
| Y4 | Ajansız / göstergeli devam | `TransitionTo(IdentityVerification)` + `Flags` |
| A4 | Değerlendirici kararı | `TransitionTo(Decided)` |

**Önyüz (S3) için.**
- Kalan süre `endsAt − (serverTime + yerel geçen süre)` ile hesaplanmalı, istemci saatiyle değil.
- Sınav sürerken `GET /api/sessions/{id}` **en geç 10 sn'de bir** çağrılmalı (S4'teki kamera kareleri gelince bu doğal olarak sağlanır). 30 sn istek gelmezse oturum `Disconnected` sayılıyor.
- Sayfa yenilenince `answers` ve `lastSeq` ile kaldığı yerden devam edilir; yeni `seq` = `max(yerel seq, lastSeq) + 1`.
- Bağlantı geri gelince önce IndexedDB kuyruğu gönderilmeli (`Disconnected` iken; cevaplar işaretlenir), sonra yeniden doğrulama (A3).
- 409 yanıtlarındaki `detail` adaya gösterilebilir.

**Uzun kesinti (S-13).** Ayrı olay türü eklenmedi; kesinti `CONNECTION_LOST` olarak süresiyle yazılıyor ve süre çarpanı (K-17) uzun kesintiyi ağırlaştırıyor. 5 dakika üstü için ayrıca ağırlık gerekirse `riskconfig.json`'da pilot oturumda ayarlanır.

**Açık konu (S-16).** Rıza, eşleştirme veya doğrulama adımında takılı kalıp son başlangıç zamanını kaçıran oturum açık kalıyor (süre başlamadığı için otomatik teslim yok). Aday yeniden başlamaya çalışınca 409 "son başlangıç zamanı geçti" alıyor. Bu oturumların raporda nasıl görüneceğine A4'te karar verilmeli.

---

## K-25 · Ağ göstergeleri, ajansız ve göstergeli devam, kurulum paketi (Y4)

FR-12, FR-20, FR-23 (İstenen), FR-35, FR-36, GAB 3.3.1, UC-08 4a/4b.

### Ağ göstergeleri (ajan)

| Kural | Gerekçe |
|---|---|
| Etkin (Up) VPN/TUN/TAP bağdaştırıcıları `VPN_PROXY` "VPN bağdaştırıcısı"; ad ya da açıklamada `TAP`, `TUN`, `VPN`, `Tunnel` (tam kelime) veya bilinen marka (Wintun, WireGuard, OpenVPN, NordLynx, AnyConnect, GlobalProtect, Fortinet, Cloudflare WARP …); Windows'un kendi VPN bağlantıları (PPP) | Kart: NetworkInterface API. Hyper-V vEthernet, WSL, Docker, VirtualBox Host-Only ve VMware VMnet bağdaştırıcıları eşleşmiyor (denendi) |
| Sistem vekil sunucu: kullanıcı ayarı (WinINet), PAC betiği ve bilgisayar geneli WinHTTP vekili ayrı ayrı `VPN_PROXY` | Kart. "Ayarları otomatik algıla" (WPAD) Windows'ta varsayılan açık; gösterge sayılmıyor |
| Bilinen VPN uygulamaları `forbidden-apps.json`'da `"type": "VPN_PROXY"` ile (OpenVPN, WireGuard, NordVPN, ProtonVPN, ExpressVPN, Cloudflare WARP, Windscribe, Psiphon, Tor) | Ayrı bir liste dosyası yerine aynı dosya; yazılım eklemek için yeniden derleme gerekmiyor |
| Ağ denetimi süreç taramasıyla aynı aralıkta (`ProcessScanIntervalSeconds`, 15 sn) | GAB 3.3.3; ölçüm tek çekirdeğe göre %0,5 |
| `VPN_PROXY` kaynağı `Network` (EventCatalog) | Ortak olay listesi; ajandan ya da IP bloğundan gelmesi fark etmiyor |

### Durum ve devam uçları (API)

- `GET /api/sessions/{id}/agent`: `{ state, paired, agentActive, lastSignalAt, indicators[{ type, name, message, fix }], canContinueIndicated, canContinueWithoutAgent }`. Göstergeler son geçerli sinyalden; hipervizör bilgisi listede yok (UC-08 4b). Neden ve çözüm metni sunucuda üretiliyor (`IndicatorAdvice`).
- `POST /api/sessions/{id}/agent/continue { mode: "noAgent" | "indicated", reason }`: 200 `{ state: "IdentityVerification" }` · 400 geçersiz mod ya da boş/2000 karakterden uzun gerekçe · 404 · 409.
- İki uç da yalnızca aday ve yalnızca kendi oturumu (K-20); her istekte IP kontrolü (FR-22).

| Kural | Gerekçe |
|---|---|
| Devam yalnızca `AgentPairing` durumunda; geçiş `TransitionTo(IdentityVerification)` | K-24 |
| `noAgent`: ajan çalışıyorsa 409. Ajan eşleşmiş ve son 30 sn içinde sinyal göndermişse (ilk sinyal gelene kadar eşleşme anı sayılıyor) "çalışıyor" | Ajansız devam, cihaz denetimini kapatmanın yolu olmasın |
| `indicated`: son geçerli sinyalde engelleyici gösterge yoksa 409 | Göstergesiz oturum zaten kendiliğinden geçiyor (K-21) |
| Gerekçe `ExamSession.ContinueJustification`'a; `Flags += NoAgent` ya da `Indicated` | Kart. İşaretli oturum risk skorundan bağımsız incelemeye düşüyor (K-24, FR-12) |
| `noAgent` sonrası ajanın anahtarı geçersiz; sinyal kabul edilmiyor, `AGENT_SIGNAL_LOST` yazılmıyor | FR-12: ajansız oturumda cihaz ve ağ denetimi yapılmıyor |
| `indicated` sonrası ajan çalışmaya devam ediyor; yeni göstergeler olay oluyor | FR-20 |
| Denetim kaydı: `session.continue_no_agent` / `session.continue_indicated`; aktör aday, hedef oturum; `NewValue` = `{ mode, reason, indicators[{type,name}] }` | FR-35 (K-10) |
| Gösterge giderilince (ör. uygulama kapatılınca) sonraki temiz sinyalde oturum kendiliğinden geçiyor | Çözüm adımının işe yaraması için; aday gerekçe yazmak zorunda değil |

### IP blok listesi (FR-23, İstenen)

- `api/src/AIGuard.Api/vpn-ip-blocks.json` (`VpnIpBlocks:Blocks[{ Cidr, Label }]`), compose'da container'a salt okunur bağlı. Bozuk CIDR varsa API açılmıyor.
- `SessionIpGuard` oturumun ilk IP'sini ve her yeni IP'yi bloklarla karşılaştırıyor; eşleşirse `VPN_PROXY` `{ name: label, detail: "IP bloğu <cidr>", ip }`. Ajansız oturumda da çalışıyor.
- Repodaki liste yalnızca belgelendirme bloklarını içeriyor (RFC 5737, RFC 3849). Gerçek listenin kaynağı ekip kararı (S-18).

### Kurulum paketi

- `agent/scripts/publish-windows.ps1 -ApiBaseUrl … -AllowedOrigins …` → `agent/dist/windows/aiguard-agent.exe`: self-contained, tek dosya (yaklaşık 45 MB). `appsettings.json`, `forbidden-apps.json` ve kurumun ayarları (`deployment.json`) exe'nin içinde.
- Ajan ayarları exe'nin klasöründen okuyor (`ContentRootPath = AppContext.BaseDirectory`); İndirilenler'den çift tıklanınca da çalışıyor.
- Aday için iki adım: indir, çalıştır (SmartScreen onayı hariç; GAB 3.3.1 en fazla üç). Eşleştirme sınav sayfasından. Kılavuz: `docs/ajan-kurulum-windows.md`.
- Exe imzasız; SmartScreen "Windows bilgisayarınızı korudu" uyarısı veriyor. Kod imzalama sertifikası kapsam dışı.

---

## K-26 · Şifreleme ve kanıt deposu

Şifreli alanların hepsi tek sınıftan geçiyor: `IFieldEncryptor` (Application), uygulaması `AesGcmFieldEncryptor` (Infrastructure). Y2'deki ajan oturum anahtarı ve A5'teki işlemler de bunu kullanmalı; başka bir şifreleme kodu yazılmamalı.

| Kural | Gerekçe |
|---|---|
| AES-256-GCM; çıktı nonce (12 bayt) + etiket (16 bayt) + şifreli veri | GCM hem gizlilik hem bütünlük sağlıyor; değiştirilen veri açılmıyor |
| Her şifrelemede yeni rastgele nonce | Aynı anahtarla nonce tekrarı GCM'nin güvenliğini bozuyor |
| Her çağrıda bir bağlam (associated data) veriliyor: `face-template:{userId}`, `evidence:{storageKey}` | Bir adayın şifreli vektörü başka bir adayın satırına kopyalansa bile açılmıyor. Yeni şifreli alan eklenirken kaydın sahibini içeren bir bağlam seçilmeli |
| Anahtar `EVIDENCE_ENCRYPTION_KEY` (Base64, 32 bayt); ortam değişkeni veya user-secrets | Koda gömülü yedek anahtar yok. Değişken adı E1b'de kanıt kareleri için konmuştu; aynı anahtar yüz vektörleri için de kullanılıyor |
| Anahtar ilk kullanımda okunuyor, açılışta değil | Anahtarı olmayan bir geliştiricinin bilgisayarında API yine açılıyor, yalnızca biyometrik uçlar hata veriyor. Docker Compose ve CI değişkeni zaten zorunlu tutuyor |
| `KeyVersion` şimdilik hep 1; bilinmeyen sürüm reddediliyor | Anahtar değiştirmek gerekirse eski anahtar sürüm numarasıyla eklenecek |

**Kanıt kareleri:** `IEvidenceStore` / `FileEvidenceStore`. Kare şifrelenip `EVIDENCE_STORAGE_PATH` klasörüne yazılıyor (varsayılan: uygulama klasöründe `data/evidence`; Docker'da `evidence` volume'u). Dosya adını sunucu üretiyor (`{guid}.bin`); silmede ad biçimi kontrol ediliyor, `../` ile klasör dışına çıkılamıyor. Kaydı veritabanına yazılamayan dosya geri siliniyor. Okuma (kısa ömürlü imzalı bağlantı) A4'te, süresi dolanların silinmesi A5'te eklenecek.

---

## K-27 · Kayıt fotoğrafı ve sınav başı doğrulama

A3'ün akışı (FR-04, FR-05, FR-10, UC-12):

1. `POST /api/me/photo` (multipart: `photo`, `consent`, `consentTextVersion`). Ayrı açık rıza ve güncel metin sürümü zorunlu. Fotoğraf görüntü servisine (`/internal/face/enroll`) gidiyor, dönen 512 boyutlu vektör şifrelenip `ReferenceFaceTemplate`'e yazılıyor. Fotoğraf hiçbir yere kaydedilmiyor.
2. `POST /api/sessions/{id}/consent` (`accepted`, `textVersion`). Kabulde kayıt fotoğrafı yoksa 409 (FR-04), varsa oturum `AgentPairing`'e geçiyor. Rette oturum `Cancelled` oluyor ve hak tüketilmiyor (FR-10).
3. `POST /api/sessions/{id}/verify` (multipart: 3–5 kare, `frames`). Oturum `IdentityVerification` veya `Disconnected` durumunda olmalı. Görüntü servisi (`/internal/face/verify`, `docs/contracts.md` 4. bölüm) canlılık ve benzerlik skoruyla birlikte `passed` ve başarısızsa `reason` dönüyor.
   - Başarılıysa oturum E4'e devrediliyor: ilk doğrulamada `SessionService.BeginExamAsync` (süre ve hak başlar), kesintiden dönüşte `ResumeAfterReconnectAsync` (süre baştan başlamaz, kesinti `CONNECTION_LOST` olarak yazılır). Süre dolmuşsa devam edilmiyor (409).
   - Başarısızsa oturum olduğu adımda kalıyor. `VERIFICATION_FAILED` olayı (ayrıntıda `reason` ve skorlar) ve ilk kare kanıt olarak kaydediliyor; yanıtta "Canlı yüz algılanamadı" ya da "Kimlik doğrulanamadı" ve "yeniden deneyin / destek talep edin" mesajı var.
4. `DELETE /api/me/biometric-consent`: rıza kayıtları kapatılıyor, vektör siliniyor, durum makinesinin izin verdiği açık oturumlar (rıza adımı ve sınav sırası) iptal ediliyor (FR-05, S-23).

| Kural | Gerekçe |
|---|---|
| Geçti/kaldı kararı görüntü servisinin (`passed`); API skorları ve sonucu `Verification`'a yazıyor | S-19 kararı: eşikler tek yerde, vision'ın `config.yaml` dosyasında dursun; D2'deki eşik betiği ve D6 sonucu yalnızca orayı değiştirsin |
| `reason`: `LIVENESS_FAILED`, `NO_FACE` ve çözülemeyen kare (`INVALID_IMAGE`) → "Canlı yüz algılanamadı"; `IDENTITY_MISMATCH`, `MULTIPLE_FACES` ve tanınmayan neden → "Kimlik doğrulanamadı" | UC-12 4a/4b'deki iki mesaj; ayrıntı yalnızca olay kaydında. Tanınmayan neden de ret sayılıyor, günlüğe uyarı düşüyor |
| Kayıtta reddedilen fotoğrafın nedeni adaya çözümüyle dönüyor (`NO_FACE`, `MULTIPLE_FACES`, `FACE_TOO_SMALL`, `IMAGE_TOO_BLURRY`, `INVALID_IMAGE`) | S2: her hatada neden ve çözüm. Kodlar `FaceIssue` sınıfında tek yerde |
| Vision'dan gelen yanıt iki gruba ayrılıyor: adayla ilgili sonuç (yukarıdaki kodlar, `passed=false`) ve servis/sözleşme sorunu (bağlantı hatası, 401, 503, `VALIDATION_ERROR`, tanınmayan hata kodu, eksik ya da aralık dışı alan). İkincisi 503; deneme, olay ve kanıt karesi yazılmıyor | Bizim isteğimizdeki ya da servisteki bir hata adayın hanesine geçmesin |
| Kayıtta dönen vektör 512 boyutlu, sonlu ve birim uzunlukta (±0,01) olmalı; değilse saklanmıyor | Sözleşme L2 ile normalize vektör istiyor; normalize olmayan vektörle sonraki benzerlikler sessizce bozulur |
| Vision sorunları günlüğe yalnızca uç, HTTP durumu ve hata koduyla yazılıyor; görüntü, vektör ve skor yazılmıyor | Entegrasyonda sorun ayırt edilebilsin (anahtar uyuşmazlığı, modeller yüklenmedi, sözleşme kayması); KVKK |
| Görüntü servisi çağrısı oturum kilidi dışında yapılıyor, sonuç kilit altında durum yeniden kontrol edilerek yazılıyor | Çağrı birkaç saniye sürebilir; bu sırada oturuma gelen olaylar beklemesin. Eşzamanlı iki denemede ikincisi birincinin sonucunu görüyor |
| İki deneme arasında en az 5 sn (`VerificationCooldownSeconds`); deneme sayısı sınırı yok | GAB adayın tekrar deneyebilmesini istiyor; bekleme süresi görüntü servisini yormayı ve hızlı fotoğraf denemelerini engelliyor |
| Doğrulama aşamasından sonra kayıt fotoğrafı değiştirilemiyor | Aday sınavın ortasında başka birinin fotoğrafını yükleyip yeniden girişte onunla doğrulanamasın |
| Rıza geri alınınca rıza adımındaki ve süren oturumlar iptal ediliyor; geçiş `TransitionTo(Cancelled)` | FR-05, K-24. Rıza adımında hak kullanılmadığı için aday yeniden başlayabiliyor. Eşleştirme ve doğrulama adımı için tabloda geçiş yok (S-23) |
| Görüntüler JPEG, PNG ve WebP imzasına bakılarak kabul ediliyor (fotoğraf 5 MB, kare 1 MB, 3–5 kare) | İstemcinin bildirdiği Content-Type'a güvenilmiyor. Çok parçalı gövde diske tamponlanmıyor, ham görüntü geçici dosyaya da yazılmıyor |
| Rızada `accepted` alanı zorunlu | Eksik alan varsayılan `false`'a düşüp oturumu iptal etmesin |

**Gerçek servisle sözleşme testi.** `VisionContractTests`, A3'ün istemcisini çalışan vision'a karşı sınıyor: yanlış anahtar, yüzsüz fotoğraf, yüzsüz kareler ve isteğe bağlı olarak gerçek bir yüz (birim vektör, kendisiyle yüksek benzerlik). `VISION_CONTRACT_URL` ve `VISION_SERVICE_KEY` verilmezse atlanıyor; çalıştırma adımı README'de. D2 birleşmeden ve eşik değiştirildikten sonra çalıştırılmalı.

**Durum geçişleri E4'ten (K-24).** Rızada `TransitionTo(AgentPairing / Cancelled)`, başarılı doğrulamada `BeginExamAsync` / `ResumeAfterReconnectAsync`. Deneme kaydı kendi işleminde yazılıyor, devir ayrı işlemde (`ResumeAfterReconnectAsync` kendi işlemini açıyor); arada ikinci bir deneme bekleme süresine takılıyor. Oturum hakkı kontrolü E4'teki oturum başlatmada (`ExamRules.CanStart`), burada tekrarlanmıyor.

**Kayıt fotoğrafı kontrolü (FR-04)** oturum rızası adımında yapılıyor: fotoğraf yoksa rıza 409 alıyor ve oturum `ConsentPending`'de kalıyor. Bu yüzden E4'teki `StartAsync`'e ayrıca kontrol eklenmedi.

**Yetki:** Uçlar yalnızca `Candidate` rolüne açık (K-20). Oturum uçlarında aday yalnızca kendi oturumunu görüyor; başkasının oturumuna 404. Diğer oturum uçları gibi her istekte IP kontrolü var (`SessionIpGuard`, FR-22). Fotoğraf ve rıza uçları kimliği JWT'den alıyor, istekte kullanıcı kimliği yok.

---

## K-28 · Görüntü servisinin model ve paket seçimi (D1)

GAB 3.3.5'teki model listesi aynen korunuyor: OpenCV, MediaPipe, YOLO nano, Silent-Face-Anti-Spoofing, ArcFace, Silero VAD. Değişen yalnızca modellerin nasıl çalıştırıldığı. Kod: `vision/app/models/` (her model ortak bir arayüzün arkasında, GAB 3.3.4).

| Model | Dosya | Kaynak | Çalıştırma | Lisans |
|---|---|---|---|---|
| MediaPipe Face Landmarker | `face_landmarker.task` (float16) | Google, `storage.googleapis.com/mediapipe-models` | mediapipe | Apache-2.0 |
| ArcFace, MobileFaceNet @ WebFace600K | `w600k_mbf.onnx` | InsightFace `buffalo_s` (v0.7); indirilen kopyanın sha256'sı resmî zip'tekiyle aynı | onnxruntime | Yalnızca ticari olmayan araştırma (InsightFace) |
| Silent-Face-Anti-Spoofing: MiniFASNetV2 (ölçek 2.7) ve MiniFASNetV1SE (ölçek 4.0) | `MiniFASNetV2.onnx`, `MiniFASNetV1SE.onnx` | Orijinal `.pth` dosyalarından dönüştürüldü (`2.7_80x80_MiniFASNetV2.pth`, `4_0_0_80x80_MiniFASNetV1SE.pth`, commit `b6d5f04`) | onnxruntime | Apache-2.0 (depo); ağırlıklar için ayrı lisans belirtilmemiş |
| YOLO11n (COCO) | `yolo11n.onnx` | Ultralytics `yolo11n.pt` (v8.4.0), 640, opset 17 | onnxruntime | AGPL-3.0 (ağırlıklar dahil) |
| Silero VAD | `silero_vad.onnx` (v6.2.3) | snakers4/silero-vad | onnxruntime | MIT |

Çalışma zamanı paketleri: `numpy`, `onnxruntime`, `mediapipe`, `opencv-contrib-python` (tek opencv paketi; mediapipe bu adı istiyor). `torch`, `torchvision`, `ultralytics`, `uniface` ve `silero-vad` servise girmiyor.

**D1 kartından sapmalar**

| Kartta | Uygulamada | Neden |
|---|---|---|
| insightface | ArcFace ONNX + onnxruntime | insightface PyPI'da yalnızca kaynak dağıtımı; Windows'ta C++ derleyicisi istiyor. Aynı ağırlık ve aynı ön işleme (uniface ile kosinüs ≥ 0,999999) |
| ultralytics | YOLO11n ONNX + onnxruntime | torch ve polars yaklaşık 1 GB ekliyordu; ultralytics `opencv-python`, mediapipe `opencv-contrib-python` istediği için iki opencv aynı `cv2`'yi yazıyordu; AGPL'li kütüphane kodu servise girmiyor. Ağırlıklar yine AGPL-3.0 |
| Yüz bulucu | Yüz kutusu ve 5 nokta MediaPipe'tan; RetinaFace yok | MediaPipe zaten çalışıyor; ikinci bir bulucu boş karede bile ~150 ms ekliyordu |
| Silent-Face | İki model (MiniFASNetV2 + MiniFASNetV1SE), orijinal ağırlıklardan bizim ürettiğimiz ONNX; skor orijinal `test.py`'deki gibi | Başkasının dönüştürdüğü dosyanın kaynağı belirsizdi. Her modelde PyTorch ile ONNX çıktısı 7 girdide aynı (atol 1e-4, rtol 0); skor 24 kutuda `test.py` hesabıyla aynı (fark < 1e-6) |
| ArcFace | MobileFaceNet (`w600k_mbf`, 13 MB) | Hız. D2'deki LFW ölçümü yetersiz çıkarsa ResNet50'ye (`w600k_r50`, 166 MB) geçilir |

**Ön işleme (orijinallerle karşılaştırıldı)**

- Görüntüler BGR uint8 olarak geliyor; her model kendi dönüşümünü yapıyor.
- ArcFace: 5 noktayla Umeyama benzerlik dönüşümü, 112×112, RGB, (x − 127,5) / 127,5, L2 normalize.
- Silent-Face: her model yüz kutusunu kendi ölçeğiyle (V2 2.7, V1SE 4.0) büyütüp görüntünün içine kaydırıyor (`CropImage`), 80×80, BGR, 0–255, normalizasyon yok. İki modelin softmax'ı toplanıyor; skor gerçek yüz sınıfının (1. sınıf) toplamdaki payı (`test.py`: `prediction[0][1] / 2`).
- YOLO11n: 640 letterbox (dolgu 114), RGB, /255; yalnızca 67. sınıf (cell phone); güven 0,25, NMS IoU 0,7 (Ultralytics varsayılanları).
- Silero: 512 örneklik pencereler, önüne önceki pencerenin son 64 örneği ekleniyor, durum her istekte sıfırlanıyor (S-20: servis durumsuz).

**Model dosyaları**

- Repoya girmiyor (`/vision/models/`, `.gitignore`).
- `scripts/download_models.py` üç dosyayı indirip sha256'larını doğruluyor.
- `scripts/export_models.py` üç ONNX'i ayrı bir ortamda (`requirements-export.txt`) üretiyor; aynı sürümlerle her seferinde aynı dosya çıkıyor.
- Docker imajında iki betik derleme aşamasında çalışıyor (`vision/Dockerfile`, `models` aşaması); torch son imaja girmiyor. Container iç ağda olduğu için çalışırken model indirilemez.
- Dosyalar yoksa servis açılıyor ve uçlar 503 `MODELS_NOT_READY` dönüyor (CI'da durum bu). Modeller açılışta, işçi başına bir kez yükleniyor.

**Sınırlılıklar ve sonraki kartlara notlar**

- Silent-Face modelleri orijinalde başka bir yüz bulucunun kutusuyla eğitildi; biz MediaPipe noktalarının kutusunu kullanıyoruz (alından daha dar). Etkisi D6'daki canlılık ölçümünde görülmeli.
- Yaw ve pitch MediaPipe dönüşüm matrisinden hesaplanıyor; işaret yönü D4'te gerçek kareyle doğrulanmalı (kural mutlak değerle çalıştığı için sonucu değiştirmiyor).
- InsightFace ağırlıkları ticari kullanıma kapalı; proje araştırma amaçlı olduğu için uygun, raporda belirtilmeli.
- `VISION_WORKERS` ve onnxruntime iş parçacığı sayısı D1 benchmark'ında belirlenecek. Şimdilik bir işçi; onnxruntime her işçide varsayılan olarak bütün çekirdekleri kullanıyor. **Sonuç (`docs/benchmark-D1.md`):** üç kart hedefi de tek iş parçacığıyla geniş payla karşılanıyor (p95: yüz/kafa yönü 10 ms, eşleştirme 22 ms, telefon 82 ms); varsayılan iş parçacığı ayarı oturumlar arası çekişme yüzünden doğrulama zincirinde p95'i 26 ms'den 114 ms'ye çıkardığı için `VISION_ORT_THREADS=1` ve teorik hesaba göre 25 adaya kadar 2, 50 adaya kadar 4 işçi öneriliyor (H3'te doğrulanacak). Varsayılan `VISION_WORKERS=2`, `VISION_ORT_THREADS=1` (her işçi ~600 MB). H3'ün 50 adaylık senaryosunda .env'de VISION_WORKERS=4 yapılmalı (A6).
- Docker imajı Debian slim'de dört sistem paketi istiyor: `libgl1` ve `libglib2.0-0` (opencv-contrib-python), `libegl1` ve `libgles2` (mediapipe'ın `libmediapipe.so` dosyası; GPU kullanılmasa da yüklenirken bağlanıyor). İmaj derlenirken modeller bir kez yükleniyor (duman testi); kütüphane eksikse derleme orada duruyor.

---

## K-29 · Yüz doğrulama kuralları ve eşikleri (D2)

Görüntü servisinin `/internal/face/enroll` ve `/internal/face/verify` uçlarının kararları. Kod: `vision/app/face_service.py`, `vision/app/images.py`; istek ve yanıtlar `docs/contracts.md` (v0.3) 3. ve 4. bölüm. Geçti/kaldı kararının vision'da, eşiklerin yalnızca `vision/config.yaml`'da olması K-27 ve S-19'dan geliyor.

**Doğrulama (verify) karar kuralları**, sırayla:

| # | Kural | Gerekçe |
|---|---|---|
| 1 | Karelerden biri çözülemezse tüm istek 422 `INVALID_IMAGE`; hiçbir model çalışmıyor | Bozuk kamera görüntüsü; api bunu adayın denemesi sayıyor (K-27) |
| 2 | Herhangi bir karede 2 ya da daha fazla yüz varsa `MULTIPLE_FACES`, diğer kareler temiz olsa bile | Kameraya ikinci kişinin girmesi bir uyarı işareti; ortalamada kaybolmamalı |
| 3 | Tam bir yüz bulunan kareler "kullanılabilir". Sayısı kare sayısının yarısından azsa `NO_FACE` (3 karede en az 2, 4'te 2, 5'te 3) | Tek bir iyi kareye dayanarak karar verilmesin |
| 4 | 2. ve 3. durumda skorlar 0, `framesUsed` kullanılabilir kare sayısı | A3 istemcisi skorların her zaman aralıkta bir sayı olmasını istiyor; `null` "servis bozuk" sayılıyor |
| 5 | Aksi hâlde `livenessScore` ve `similarity`, kullanılabilir karelerin skorlarının ortalaması. Canlılık kalsa bile benzerlik hesaplanıp dönüyor | Olay kaydında iki skor da görünsün; D6 ve H1 ölçümünde iki skor ayrı ayrı lazım |
| 6 | `passed` = `livenessScore ≥ liveness_threshold` **ve** `similarity ≥ match_threshold` (sınır değer geçer) | — |
| 7 | İkisi de kalırsa `LIVENESS_FAILED`, yalnızca benzerlik kalırsa `IDENTITY_MISMATCH`; `passed=true` ise `reason` `null` | Sahte yüz önce gelir: sahte bir yüzün benzerliği anlamlı değil |
| 8 | `referenceVector` 512 değer olmalı; sonlu olmayan değer veya sıfır vektör 422 `VALIDATION_ERROR`; birim uzunlukta değilse servis yeniden normalize ediyor | Bu bizim isteğimizin hatası, adayın değil; kosinüs benzerliği yalnızca yöne bakıyor |

**Kayıt (enroll)** sırasıyla `INVALID_IMAGE`, `NO_FACE`, `MULTIPLE_FACES`, `FACE_TOO_SMALL`, `IMAGE_TOO_BLURRY` kontrol ediyor. `qualityScore` (0,5–1) karar için kullanılmıyor, yalnızca bilgi: yüz boyu / `min_face_px` ve netlik / `min_sharpness` oranlarının küçüğü, 2'de kesilip 2'ye bölünüyor.

**Eşikler ve nasıl belirlendi**

| Eşik | Değer | Nasıl belirlendi | Ne zaman yeniden bakılacak |
|---|---|---|---|
| `verify.match_threshold` | 0,164 | LFW'de (resmî `pairs.txt`, 6000 çift) bizim hattımızla (MediaPipe hizalama + `w600k_mbf`) FAR ≈ %1 eşiği: FAR %0,97, FRR %0,93, doğruluk %99,05 (`docs/threshold-D2.md`, `scripts/calibrate_threshold.py`). Literatürden alınan başlangıç değeri 0,32'ydi; bu hat için fazla katı çıktı | **D6**, gerçek kayıt fotoğrafı + kamera kareleriyle |
| `verify.liveness_threshold` | 0,5 | Silent-Face `test.py` kararının tek sayıya indirgenmiş karşılığı (aşağıda) | H1 / D6 |
| `enroll.min_face_px` | 84 | Yüz ağı kutusu ArcFace'in 112×112 hizalı kesitinde medyan 84 px (LFW, p5–p95 78–91); daha küçük yüz büyütülerek hizalanıyor | Kayıt reddi şikâyetine göre |
| `enroll.min_sharpness` | 20 | LFW'de net yüzlerin p5'i 28,9; σ = 1,5 px bulanıklığın medyanı 14,8, p95'i 26,8 | Kayıt reddi şikâyetine göre |

- **LFW eşiğinin anlamı ve sınırı.** LFW'nin farklı kişi çiftleri rastgele eşleştirilmiş yabancılar. %1 FAR, rastgele 100 yabancıdan birinin eşleşmesi demek; adaya benzeyen birinin yerine girmesini ölçmüyor. %1 yalnızca 29 çifte dayanıyor ve LFW web fotoğrafı. Bu yüzden 0,164 bir başlangıç değeri: eşik **D6'da gerçek kayıt fotoğrafı ve kamera kareleriyle yeniden ölçülecek**, değişirse yalnızca `config.yaml` değişecek.
- **Canlılık eşiği ve `test.py`.** Skor, iki modelin softmax toplamında gerçek yüz sınıfının payı (toplam / 2). `test.py` bir görüntüyü toplam tahminin argmax'ı 1. sınıfsa "Real Face" sayıyor. Skor 0,5'in üstündeyse argmax kesin 1; 1/3'ün altındaysa kesin değil; 1/3–0,5 arasında sonuç diğer iki sınıfın (baskı ve ekran) dağılımına bağlı. Servis tek sayıyla karar verdiği için bu ara bölge "sahte" sayılıyor, yani 0,5 tutucu taraf. **H1'de ikisi de raporlanacak:** varsayılan eşikte (0,5) sonuç ve `test.py`'nin argmax kararı (`LivenessResult.probabilities` ile hesaplanıyor). İki sonuç arasındaki fark ara bölgedeki kare sayısını gösterecek.

**Boyut sınırları**

| Sınır | Değer | Gerekçe |
|---|---|---|
| Kayıt fotoğrafı | 5 MiB (base64 en fazla 6 990 508 karakter) | api'deki `ImageFile.MaxPhotoBytes` ile aynı; aşan istek `VALIDATION_ERROR` |
| Kare | 1 MiB (base64 en fazla 1 398 104 karakter), 3–5 kare | api'deki `ImageFile.MaxFrameBytes` ile aynı |
| Uzun kenar | 6000 px; aşan görüntü `INVALID_IMAGE` | Kenar, çözmeden önce dosya başlığından (JPEG SOF, PNG IHDR, WebP VP8/VP8L/VP8X) okunuyor: 5 MiB'lık sıkıştırılmış bir PNG çözülünce gigabaytlarca bellek isteyebilir. 4096, bazı telefonların varsayılan 24 MP fotoğraflarını (5712×4284) reddederdi; 6000 px'te en kötü durum ≈108 MB bellek |
| Biçim | JPEG, PNG, WebP; ilk baytlara bakılıyor; EXIF yönü uygulanıyor | api ile aynı kural (K-27); yan kaydedilmiş telefon fotoğrafında yüz bulunabilsin |

**Netlik ölçeklemesi.** Laplacian varyansı, yüz kesitinin kısa kenarı 112 px'e (ArcFace giriş kenarı) ölçeklendikten sonra hesaplanıyor (`face_landmarks.sharpness`). Ham değer çözünürlüğe çok bağlı: LFW'de aynı net yüz 4 kat büyütülünce medyan 191'den 3,8'e düşüyordu, yani yüksek çözünürlüklü telefon fotoğrafları "bulanık" sayılırdı. Ölçeklemeden sonra büyütülmüş net yüz (medyan 101,7) orijinaliyle (89,8) aynı aralıkta.

**Günlük.** Uç başına yalnızca uç, sonuç kodu (`OK`, `PASSED` ya da hata/neden kodu) ve süre; görüntü, vektör ve skor yazılmıyor (K-27, KVKK).

**Sözleşme testi.** `VisionContractTests`'in 4 testi bu kuralla gerçek vision'a karşı geçiyor. `AIGUARD_TEST_FACE` en fazla 1 MB olmalı: test, kayıt fotoğrafını doğrulamaya kare olarak da gönderiyor.

---

## K-30 · Sınav sırası kamera, ses, izin ve tarayıcı olayları (S4)

S4 kartı tarayıcının `frames`, `audio` ve `events` uçlarına göndermesini istiyor. `frames` ve `audio` API'de yoktu; Dilan'la (D4/D5) konuşulup S4'te eklendi. İzin geçişlerinin ucu da K-24 tablosu gereği S4'te.

**Uçlar** (`SessionMonitoringController`; yalnızca aday ve yalnızca kendi oturumu, başkasının oturumu 404, her istekte IP kontrolü):

| Uç | Gövde | Yanıt |
|---|---|---|
| `POST /api/sessions/{id}/frames` | multipart, alan `frame`; JPEG/PNG/WebP, en fazla 1 MiB | 202 boş; 400 biçim/boyut; 409 sınav sürmüyor; 429 hız sınırı |
| `POST /api/sessions/{id}/audio` | multipart, alan `audio`; WebM ya da WAV, en fazla 256 KiB | 202 boş; 400; 409; 429 |
| `POST /api/sessions/{id}/permission` | `{ "granted": false \| true }` | 200 `{ state }`; tekrar bildirim de 200; 409 sınav sürmüyor ya da süre doldu |

**Kararlar**

- **İzinsiz süreyi sunucu ölçüyor, tarayıcı göndermiyor.** Kart "izin geri gelince izinsiz süre (`durationSec`) ile olay kapatılır" diyor. Süreyi tarayıcı bildirseydi aday `durationSec: 0` gönderip boşluğu gizleyebilirdi. İzin kalkınca oturuma `PermissionRevokedAt` yazılıyor (yeni göç `S4_PermissionRevokedAt`). İzin gelince süre sunucu saatiyle hesaplanıp `PERMISSION_REVOKED` olarak `RecordSystemEventAsync` ile yazılıyor. `CONNECTION_LOST`'taki `DisconnectedAt` ile aynı yöntem (K-24).
- **Sınav izin beklerken biterse süre yine yazılıyor.** Teslimde ya da otomatik teslimde (`SessionTimekeeper`) izinsiz süre bitiş anına kadar, süre sonunu geçmeden yazılıyor ve `PermissionGap` işareti konuyor. Aday hiç dönmese de oturum incelemeye düşüyor (FR-17).
- **Tekrarlanan bildirim süreyi kısaltmıyor.** Kamera ve mikrofon ayrı ayrı "ended" verebilir; ikinci `granted: false` ilk anı değiştirmiyor.
- **Kare ve ses yalnızca `InProgress`'te kabul ediliyor.** İzin beklerken, bağlantı kesikken, sınav başlamadan ya da bittikten sonra 409.
- **Biçim dosyanın ilk baytlarından.** Content-Type'a güvenilmiyor: görüntüde K-27'deki `ImageFile` kuralı, seste WebM (EBML `1A 45 DF A3`) ya da WAV (`RIFF….WAVE`).
- **Sunucu tarafı hız sınırı.** Oturum ve tür başına 500 ms'den sık gelen parça 429 alıyor (`SessionMediaRateLimiter`). Saniyede bir gönderimin ağda sıkışmasına pay bırakılıyor. Sınır API belleğinde; API tek örnekle çalışıyor (S-17 ile aynı varsayım).
- **Kare bağlantıyı canlı tutuyor.** Kare ya da ses geldiğinde `LastSeenAt` en geç 5 sn'de bir yazılıyor. Her saniye veritabanına yazılmıyor; kesinti eşiği (30 sn) bunun çok üstünde.
- **Analiz D4/D5'te.** Uçlar veriyi `IMediaAnalyzer`'a veriyor. Şimdiki uygulama (`PendingMediaAnalyzer`) hiçbir şey yapmıyor, çünkü vision'ın analiz uçları henüz 503 dönüyor ve her saniye `MONITORING_GAP` üretirdi. D4/D5 bu arayüzü vision istemcisiyle uygulayıp kaydı değiştirecek. Olay kuralları (S-20) ve `MONITORING_GAP` orada.
- **Hiçbir şey saklanmıyor.** Kare ve ses istek bitince bellekten çıkıyor. Tarayıcıda da kuyruğa ya da depolamaya yazılmıyor (kart).

**Önyüz** (`web/src/exam/`):

- `useExamMonitoring`: `InProgress`'te `getUserMedia({ video, audio })`. İzin verilmezse ya da bir iz "ended" olursa `granted: false` bildirilip oturum yeniden okunuyor. `PermissionPending`'de "Kamera ve mikrofona izin ver" düğmesi izni yeniden alıp `granted: true` gönderiyor. Sınav dışında kamera ve mikrofon kapatılıyor.
- `mediaMonitor`: saniyede bir kare (en fazla 640 px genişlik, JPEG 0,8). Önceki kare gitmeden yenisi gönderilmiyor, yavaş bağlantıda istek birikmiyor. Ses `MediaRecorder` ile ~1 sn parçalar. **Kayıt her parçada yeniden başlatılıyor:** `timeslice` ile yalnızca ilk parçada WebM başlığı olur, vision ise her parçayı ayrı çözüyor.
- `browserEvents`: `visibilitychange` ve `blur`/`focus` → `TAB_BLUR`, süresiyle dönüşte. Aday hiç dönmese de ayrılma 60 sn'de bir o ana kadarki süresiyle bildiriliyor. `fullscreenchange` → `FULLSCREEN_EXIT`, `copy`/`cut`/`paste` → `COPY_PASTE`, `contextmenu` → `RIGHT_CLICK`. Aynı tür saniyede en fazla bir kez. Olaylar engellenmiyor, yalnızca bildiriliyor.
- **Tam ekran düğmesi.** Hiçbir ekran tam ekrana geçmediği için `FULLSCREEN_EXIT` hiç oluşmazdı. Sınav ekranına "Tam ekrana geç" düğmesi eklendi; tarayıcılar tam ekranı yalnızca kullanıcı tıklamasıyla açıyor.
- `PERMISSION_REVOKED` tarayıcıdan `events` ucuna gönderilmiyor; A2'deki katalog bunu hâlâ kabul ediyor (K-16), ama süreyi bu uç taşıdığı için kullanılmıyor.

---

## Kartlarda geçmeyen GAB kuralları

Trello kartları güncel GAB'a göre yazıldı, ama aşağıdaki kurallar kart metinlerinde geçmiyor. İlgili kartı uygularken bunlar da kapsama dahil.

### E4 · Sınav oturumu durum makinesi

*E4'te uygulandı (K-24): rıza reddi hakkı tüketmiyor, iptal geçişleri tabloda, uzun kesinti `CONNECTION_LOST` süresiyle, kesinti sonrası dönüş `ResumeAfterReconnectAsync` ile. Rıza, doğrulama ve izin uçları A3/S4'te.*

- **Rıza reddi oturum hakkını tüketmez (FR-10).** Rıza Bekliyor durumunda aday rızayı reddederse oturum `Cancelled` olur ve aday alternatif yönteme yönlendirilir. Hak kullanılmış sayılmaz; aynı aday sonra yeni oturum açabilir. Veritabanı buna hazır: tek oturum indeksi iptal edilen oturumları saymıyor (K-11). `ExamAssignment.AttemptUsedAt` yalnızca oturum gerçekten başladığında doldurulmalı; doluysa yeni oturum açılmamalı.
- **Sınav sırasında biyometrik rıza geri alınırsa oturum iptal olur (FR-05).** `InProgress`, `PermissionPending` veya `Disconnected` durumundaki oturum `Cancelled`'a geçer, izleme durur, işlem denetim kaydına yazılır.
- **Uzun kesinti (FR-26).** 5 dakikadan uzun bağlantı kesintisi yüksek ağırlıklı bir olay üretmeli. Ortak olay listesinde bunun için ayrı bir tür yok (S-13); karar verilene kadar kesinti `CONNECTION_LOST` olarak, süresiyle birlikte yazılmalı.
- **İzin Bekliyor süresi (FR-17).** İzin yeniden verilince izinsiz geçen süre `PERMISSION_REVOKED` olayı olarak, `DurationSec` ile kaydedilmeli. Olay oturuma `PermissionGap` işaretini kendiliğinden koyuyor (K-17). *S4'te uygulandı (K-30): süreyi sunucu ölçüyor.*
- **Kesinti sonrası doğrulama (FR-26).** Aday döndüğünde canlılık testi ve yüz eşleştirmesi tekrar yapılmalı; oturum hakkı yenilenmez. Doğrulama `VerificationKind.Reconnect` ile kaydedilir.

### A3 · Kayıt fotoğrafı ve doğrulama

- **Rıza geri alma oturumu da etkiler (FR-05).** Biyometrik rıza geri alınınca yüz vektörü silinir. Aday o sırada sınavdaysa oturum iptal edilir. *A3'te yapıldı (K-27).*
- **Kayıt fotoğrafı yoksa sınav başlamaz (FR-04).** Oturum başlatılırken adayın `ReferenceFaceTemplate` kaydı kontrol edilmeli. *A3'te oturum rızası adımında kontrol ediliyor (K-27).*

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
| S-2 | Şifre özetleme yöntemi | Esma | **Karar (E2):** ASP.NET Core Identity PasswordHasher (PBKDF2) kalıyor; değişirse yalnızca `IdentityPasswordHasher` etkilenir |
| S-3 | HMAC anahtarının şifreli saklanması (K-08) | Yağmur | Y2'de bu alanın kullanılması |
| S-4 | Docker ortamında göçlerin uygulanması (K-07) | Yağmur | **Çözüldü (E1b):** compose'daki tek seferlik `migrate` servisi EF göç paketini (`efbundle`) çalıştırıyor; `api` bunun başarıyla bitmesini bekliyor |
| S-5 | A6, E5b'ye bağlı ama tarihleri çakışıyor | Ayşe, Esma | Locust betiği 29 Kasım'da, ölçüm E5b'den sonra |
| S-6 | Cevaptaki sorunun oturumun sınavına ait olması veritabanında zorlanmıyor | Esma (E4) | **Çözüldü (E4):** serviste kontrol ve test (K-24) |
| S-7 | Uygulama yerelde `postgres` süper kullanıcısıyla bağlanıyor | Ayşe (A5), Yağmur (E1b) | Docker/sunucu için yalnızca gerekli yetkilere sahip ayrı kullanıcı; göçler ayrı kullanıcıyla |
| S-8 | `agent_signals` hızlı büyüyor; kullanıcı silme talebi | Ayşe (A5) | Saklama görevine eklenmesi; kişisel alanların anonimleştirilmesi |
| S-9 | Olay ucu E2 gelene kadar yalnızca Development'ta açıktı | Esma (E2), Ayşe | **Çözüldü (E2):** aday rolü ve oturum sahipliği kontrolü (K-20) |
| S-10 | `riskconfig.json` ağırlıkları başlangıç tahmini | Tüm ekip | Pilot oturumda ayarlanması, deneylerde sabit tutulması |
| S-11 | Ajan aynı göstergeyi her sinyalde bildirirse her seferinde olay oluşur | Yağmur (Y2–Y4) | **Çözüldü (Y3):** gösterge yalnızca ilk görüldüğünde ve kaybolup yeniden göründüğünde olay oluyor (K-22) |
| S-12 | Eşleştirme kodu ucu (`POST /api/sessions/{id}/agent-code`) E2 gelene kadar yalnızca Development'ta açıktı | Esma (E2), Yağmur | **Çözüldü (E2):** aday rolü ve oturum sahipliği kontrolü (K-20) |
| S-13 | GAB FR-26: 5 dakikadan uzun kesinti "uzun kesinti olayı" üretmeli; ortak listede tür yok | Esma (E4), Ayşe | **Karar (E4):** ayrı tür eklenmedi; `CONNECTION_LOST` süresiyle yazılıyor, süre çarpanı ağırlaştırıyor (K-24). Gerekirse pilot oturumda ağırlık ayarı |
| S-14 | `POST /api/agent/pair` eşleşme kimliği dönüyor ama ajana henüz bir anahtar vermiyor | Yağmur (Y2) | **Çözüldü (Y2):** anahtar eşleşmede üretilip yanıtta veriliyor; veritabanında özeti (K-21) |
| S-15 | Davet e-postası sahte gönderici ile loga yazılıyor; davet yeniden gönderme ucu yok | Esma | Gerçek gönderim gerekirse SMTP göndericisi `IEmailSender` arkasına; süresi dolan davet için `POST /api/users/{id}/invite` eklenebilir (ekip kararı) |
| S-16 | `AgentPairing.EncryptedSessionKey` artık kullanılmıyor (K-21: veritabanında yalnızca özet) | Ayşe, Yağmur | Bir sonraki göçle kaldırılması; K-13 gereği önce ekipte konuşulacak |
| S-17 | Ajan oturum anahtarları tek API sürecinin belleğinde; API yeniden başlarsa ya da birden fazla örnekle çalışırsa doğrulanamıyor | Yağmur, Esma | Proje tek örnekle çalışıyor; gerekirse paylaşılan önbellek (ör. Redis) ya da ajanın yeniden eşleşmesi |
| S-18 | `vpn-ip-blocks.json` yalnızca örnek bloklar içeriyor; gerçek VPN/veri merkezi listesinin kaynağı belirlenmedi (FR-23, İstenen) | Yağmur, Ayşe | Pilot oturum öncesi bir kaynak seçilip liste doldurulması; seçilen kaynak ve tarih dosyaya yazılmalı |
| S-16 | Rıza/eşleştirme/doğrulama adımında kalıp son başlangıç zamanını kaçıran oturum açık kalıyor (süre başlamadığı için otomatik teslim yok) | Esma, Ayşe (A4) | Raporda "başlamadı" olarak gösterilmesi ya da zamanlanmış görevle iptal edilmesi; ekip kararı |
| S-19 | Yüz doğrulamada `passed` kararını kim veriyor: vision mı, API mi? `Verification.Passed` yorumu "sonuç sunucuda eşiklere göre belirleniyor" diyor | Ayşe (A3), Dilan (D2) | **Çözüldü (A3):** kararı vision veriyor (`config.yaml`), API skorları, sonucu ve `reason`'ı kaydediyor; API'de eşik yok (K-27) |
| S-20 | Kamera olay kuralları ("art arda 3 ölçümde yüz yok", "art arda 5 ölçümde bakış sapması") nerede çalışacak? | Ayşe, Dilan (D4) | API'de (K-16). Vision çok işçili ve durumsuz çalışacağı için oturum sayacı tutamaz; her kare için yalnızca ölçüm döner (`docs/contracts.md` 5. bölüm) |
| S-21 | `POST /internal/analyze/frame` isteğinde `referenceVector` D4 kartında yok, ama vision vektör saklamadığı için 60 sn'lik kimlik kontrolünde gerekli | Ayşe, Dilan (D4) | Yalnızca `tasks` içinde `identity` varken gönderilsin; vision bu durumda alanı zorunlu tutuyor |
| S-22 | Ses parçasının uzunluğu ve biçimi belirlenmedi | Yağmur (S4), Dilan (D5) | **Çözüldü (S4):** ~1 sn WebM/Opus, her parça kendi başına tam dosya; API WebM ve WAV kabul ediyor, en fazla 256 KiB (K-30). Vision 16 kHz mono'ya kendisi dönüştürüyor |
| S-23 | Biyometrik rıza geri alınınca `AgentPairing` ve `IdentityVerification` adımındaki oturumlar iptal edilemiyor: durum makinesinde bu adımlardan `Cancelled` geçişi yok (GAB 3.4.4) | Ayşe (A3), Esma (E4) | Şimdilik oturum olduğu adımda kalıyor, vektör silindiği için doğrulamayı geçemiyor ve son başlangıç zamanını kaçıran takılı oturumlarla (K-24'teki S-16) aynı şekilde ele alınıyor. Tabloya bu iki geçişin eklenmesi önerilir |
| S-24 | Tarayıcı ses parçasını WebM/Opus olarak kaydedemiyorsa (`MediaRecorder` yok ya da `audio/webm` desteklenmiyor) ses gönderilmiyor; kareler gidiyor | Yağmur (S4), Dilan (D5) | D5'te kare gelirken ses gelmeyen oturum için `MONITORING_GAP` üretilmesi; gerekirse tarayıcıda WAV'a dönüştürme. Desteklenen tarayıcılar `docs/tarayici-deneme-notlari.md`'deki S4 denemesiyle belirlenecek |
