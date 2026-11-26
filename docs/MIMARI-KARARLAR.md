# Arka Uç Mimari Kararları

Bu belgede `api/` tarafında aldığımız tasarım kararlarını ve gerekçelerini topluyoruz. Amaç, bir karta başlayan herkesin aynı kurallarla ilerlemesi ve "bu neden böyle yapılmış?" sorusunun cevabını tek yerde bulabilmesi.

Bir kararı değiştirmek gerekirse önce ekipte konuşalım, sonra belgeyi ve kodu aynı PR'da güncelleyelim. Yeni karar eklerken sıradaki numarayı kullanın; eski numaraları değiştirmeyin, çünkü kod yorumlarında bu numaralara atıf var.

*Son güncelleme: 26 Kasım 2026 (D4)*

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
| İnceleme ve rapor | Atanmış değerlendirici okur ve karar verir, Admin yalnızca okur; karar yalnızca işaretli tamamlanmış oturumda ve bir kez; yanlış alarm olayı silmez, skordan çıkarır; kanıt karesi 5 dakikalık imzalı bağlantıyla; işaretli oturum bitince SignalR bildirimi (K-31) |
| Şifre sıfırlama | Bağlantı 30 dk, tek kullanımlık, özeti saklanıyor; istek her durumda 202 (e-posta sızmıyor); aynı hesaba 2 dk'da en fazla bir e-posta; sıfırlama kilidi kaldırıp bütün oturumları kapatıyor (K-33) |
| Önyüz tasarımı | Renk, yazı, boşluk tek dosyada (`web/src/styles/theme.css`); bileşen stili bileşenin yanında; yazı tipleri pakette; nginx'te CSP ve güvenlik başlıkları; sayfalar tembel yükleniyor; yetkisiz rol 403 görüyor (K-35) |
| Aday akışı ekranları | Sihirbaz sırası rıza → ajan → hazırlık → kimlik; süre kimlik doğrulanınca başladığı için kurallar ondan önce. Rıza metni ve sürümü sunucudan (K-37) |
| Sınav ekranı | Yan menüsüz; mantık `useExamSession`'da, ekran bileşenleri yalnızca çizer; çevrimdışıyken cevaplama sürer, bekleyen sayısı görünür; "daha sonra dön" yalnızca istemcide (K-38) |
| Aday ekranı verisi | Sınav listesi adaya kendi son oturumunu (`mySession`) da dönüyor; `GET /api/me/biometric` yalnızca şablon var mı, ne zaman, hangi rıza sürümüyle (K-36) |
| Veri koruma | Saklama süresi dolan kanıt, olay ve yüz vektörü günlük görevle siliniyor (sonuç ve karar kalıyor); denetim kaydı ve rıza metinleri veritabanı tetikleyicisiyle değiştirilemez; uçlar GAB erişim matrisinden policy kullanıyor; rıza metinleri sürümlü tabloda; aday raporu Excel ve PDF olarak iniyor (K-32) |
| Kamera olayları | Kurallar API'de, bölüm bitince süresiyle tek kayıt; bakış serisinde 1–2 yüz kaybı seriyi sürdürür ama sayılmaz; telefon 3 sn, kimlik 60 sn; MONITORING_GAP tek bölüm, INVALID_IMAGE boşluk değil (K-45) |
| Arayüz dili | Türkçe ve İngilizce; metinler kullanıldıkları dosyanın yanında iki dilde, tercih tarayıcıda; sunucu metinleri isteğin dilinde (K-46, K-48) |
| Giriş ve koyu tema | Giriş panelinde dönen, parlayan halkalar (Şeyma'nın S1 tasarımı); koyu temada yan menü sayfayla aynı ailede, kontrastlar AA (K-47) |

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
2. **A5:** Veritabanında `audit_logs` için tetikleyici: güncelleme ve `TRUNCATE` hiç, silme yalnızca 1 yıldan eski kayıtta kabul. `ExecuteUpdate`, `ExecuteDelete` ve ham SQL interceptor'a uğramadığı için bu ikinci adım gerekliydi. Yetki geri almak yerine tetikleyici seçildi, çünkü uygulama tabloların sahibiyle bağlanıyor (K-32, S-7).

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
| `POST /api/auth/forgot-password` | açık | `{email}` → her durumda `202` (K-33) |
| `POST /api/auth/reset-password` | açık | `{token, password}` → `204` · 400 (K-33) |
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

**Denetim kaydı (FR-35).** `user.login`, `user.lockout`, `user.create`, `user.invite_accept`, `user.password_change`, `user.password_reset_request`, `user.password_reset` (K-33), `user.role_change`, `user.activate`, `user.deactivate`, `auth.refresh_reuse`. Eski ve yeni değere yalnızca rol ve durum yazılıyor (K-10).

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

**Sınırlılıklar:** OBS sanal kamerası OBS sürecinden anlaşılıyor; kamera aygıtı ayrıca denetlenmiyor. AnyDesk ve TeamViewer servis olarak kuruluysa aday kullanmasa da süreç çalışıyor ve gösterge oluşuyor. Hyper-V konuğu (Hyper-V'de açılmış sanal makine) karta göre `HYPERVISOR_INFO` sayılıyor, `VM_DETECTED` değil.

**macOS (Y5).** Çekirdek değişmeden çalışıyor; macOS modülü yalnızca `IDeviceInspector`'ı uyguluyor. `Platform` `"MacOS"`, gösterge biçimi ve "kurulum izi değil konuk izi" kuralı aynı. Parallels ve VMware için bakılan yerler: `kern.hv_vmm_present`, `system_profiler` model adı/kimliği, `IOPlatformExpertDevice` içindeki manufacturer/product/model. Adlar `Parallels` ve `VMware`. Ürün belli değilse (ör. UTM, `VirtualMac`) yalnızca `HYPERVISOR_INFO`. Bütün `ioreg` ağacı okunmuyor; ana bilgisayarda Parallels veya Fusion kurulu olması gösterge üretmiyor. Sonuç bir kez saklanıyor. Yasaklı yazılım ve VPN S6'da. Deneme: `docs/y5-macos-deneme-notlari.md`.

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

**Açık konu (S-16).** Rıza, eşleştirme veya doğrulama adımında takılı kalıp son başlangıç zamanını kaçıran oturum açık kalıyor (süre başlamadığı için otomatik teslim yok). Aday yeniden başlamaya çalışınca 409 "son başlangıç zamanı geçti" alıyor. Bu oturumların raporda nasıl görüneceğine A4'te karar verilmeli. *A4'te: listede ve raporda `NotStarted` ("başlamadı") olarak görünüyorlar, karar verilemiyor; durum makinesi değişmedi (K-31).*

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

**Kanıt kareleri:** `IEvidenceStore` / `FileEvidenceStore`. Kare şifrelenip `EVIDENCE_STORAGE_PATH` klasörüne yazılıyor (varsayılan: uygulama klasöründe `data/evidence`; Docker'da `evidence` volume'u). Dosya adını sunucu üretiyor (`{guid}.bin`); silmede ad biçimi kontrol ediliyor, `../` ile klasör dışına çıkılamıyor. Kaydı veritabanına yazılamayan dosya geri siliniyor. Okuma A4'te kısa ömürlü imzalı bağlantıyla eklendi (K-31); süresi dolanlar A5'teki günlük saklama görevinde siliniyor (K-32).

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
- Yaw ve pitch MediaPipe dönüşüm matrisinden hesaplanıyor. **İşaret D4'te doğrulandı:** `yaw > 0` aday kendi soluna dönmüş (aynalanmamış karede burun görüntünün sağına kaymış), `pitch > 0` aşağı bakıyor. Yöntem: 600 LFW yüzünde yaw ile burun ucunun göz ortasına göre yatay kayması (göz arası mesafeye bölünmüş) arasındaki korelasyon +0,89; pitch ile burun ucunun gözler–ağız arasındaki dikey konumu arasındaki korelasyon +0,86 (|yaw| > 15° olan yüzlerde kaymanın ortalaması −0,26 / +0,29). Kamerayla da doğrulandı (`scripts/try_frame.py --preview`, ekran kaydıyla eşleştirildi): kendi soluna dönüşte yaw +56…+60, sağına −52…−57, aşağı bakışta pitch +21…+24, yukarı −19…−44. Kareler aynalı değil: try_frame ham kamera görüntüsünü, tarayıcı (`web/src/features/exam/mediaCapture.ts`) gizli bir `<video>`'dan dönüşümsüz `drawImage` ile alıyor; `CameraView.css`'teki `scaleX(-1)` yalnızca adaya gösterilen önizlemede. Kural mutlak değerle çalıştığı için işaret sonucu değiştirmiyor.
- **D4 kamera denemesinden notlar:** (1) |yaw| ≈ 50–60°'de MediaPipe yüzü yer yer kaybediyor (faceCount 1 → 0 → 1); sağa dönüşte art arda üç kayıp `GAZE_AWAY` yerine `FACE_MISSING` üretti. **2. parçada (api)** tekil bir "yüz yok" ölçümü bakış serisini bozmamalı, sürdürmeli; yoksa başını çok çeviren aday iki kuralın arasından kayabilir. (2) Kamera adayın hafif solundayken dinlenmede yaw −3…−6; **D3 protokolünde kamera ortalanmalı.** (3) Kulağa yakın tutulan telefon kaçtı (skor 0,00); arka plandaki siyah bir stand 0,37 aldı ve 0,5 eşiği eledi; elde tutulan cam şişe 0,01–0,03. **Deney 2'de** yüze yakın telefon ayrı bir koşul olarak ölçülmeli ve baş dönüklüğünde `GAZE_AWAY`/`FACE_MISSING` karışması raporlanmalı.
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
- **Sunucu tarafı hız sınırı.** Oturum ve tür başına 250 ms'den sık gelen parça 429 alıyor (`SessionMediaRateLimiter`), yani en fazla saniyede 4. İlk sürümde 500 ms'ydi; tarayıcı denemesinde ağ gecikmesi değişince saniyede bir gönderilen meşru kareler sunucuya 500 ms'den yakın vardı ve düştü. Sınır API belleğinde; API tek örnekle çalışıyor (S-17 ile aynı varsayım).
- **Kare bağlantıyı canlı tutuyor.** Kare ya da ses geldiğinde `LastSeenAt` en geç 5 sn'de bir yazılıyor. Her saniye veritabanına yazılmıyor; kesinti eşiği (30 sn) bunun çok üstünde.
- **Analiz D4/D5'te.** Uçlar veriyi `IMediaAnalyzer`'a veriyor. Şimdiki uygulama (`PendingMediaAnalyzer`) hiçbir şey yapmıyor, çünkü vision'ın analiz uçları henüz 503 dönüyor ve her saniye `MONITORING_GAP` üretirdi. D4/D5 bu arayüzü vision istemcisiyle uygulayıp kaydı değiştirecek. Olay kuralları (S-20) ve `MONITORING_GAP` orada. *D4'te yapıldı (K-45): kare analizi CameraMonitor'da; PendingMediaAnalyzer kaldırıldı. Ses D5'te.*
- **Hiçbir şey saklanmıyor.** Kare ve ses istek bitince bellekten çıkıyor. Tarayıcıda da kuyruğa ya da depolamaya yazılmıyor (kart).

**Önyüz** (`web/src/exam/`):

- `useExamMonitoring`: `InProgress`'te `getUserMedia({ video, audio })`. İzin verilmezse, bir iz "ended" olursa ya da site izni "engelle"ye çekilirse (`navigator.permissions` değişikliği) `granted: false` bildirilip oturum yeniden okunuyor; hangisi önce gelirse bir kez. `PermissionPending`'de "Kamera ve mikrofona izin ver" düğmesi izni yeniden alıp `granted: true` gönderiyor. Sınav dışında kamera ve mikrofon kapatılıyor; bağlantı kesikken açık kalıyor.
- **Sorular yalnızca kamera ve mikrofon açıkken görünüyor.** İzin istemi yanıtlanana ya da izin verilene kadar sorular yerine açıklama gösteriliyor, süre işliyor. Bu önyüzde uygulanıyor; sunucu `InProgress`'te soruları yine döndürüyor (S-29).
- `mediaMonitor`: saniyede bir kare (en fazla 640 px genişlik, JPEG 0,8). Önceki kare gitmeden yenisi gönderilmiyor, yavaş bağlantıda istek birikmiyor. Ses `MediaRecorder` ile ~1 sn parçalar. **Kayıt her parçada yeniden başlatılıyor:** `timeslice` ile yalnızca ilk parçada WebM başlığı olur, vision ise her parçayı ayrı çözüyor.
- `browserEvents`: `visibilitychange` ve `blur`/`focus` → `TAB_BLUR`, süresiyle dönüşte. Aday hiç dönmese de ayrılma 60 sn'de bir o ana kadarki süresiyle bildiriliyor. `fullscreenchange` → `FULLSCREEN_EXIT`, `copy`/`cut`/`paste` → `COPY_PASTE`, `contextmenu` → `RIGHT_CLICK`. Aynı tür saniyede en fazla bir kez. Olaylar engellenmiyor, yalnızca bildiriliyor.
- **Tam ekran düğmesi.** Hiçbir ekran tam ekrana geçmediği için `FULLSCREEN_EXIT` hiç oluşmazdı. Sınav ekranına "Tam ekrana geç" düğmesi eklendi; tarayıcılar tam ekranı yalnızca kullanıcı tıklamasıyla açıyor.
- `PERMISSION_REVOKED` tarayıcıdan `events` ucuna gönderilmiyor; A2'deki katalog bunu hâlâ kabul ediyor (K-16), ama süreyi bu uç taşıdığı için kullanılmıyor.

---

## K-31 · İnceleme paneli, aday raporu ve bildirimler (A4)

Değerlendirici panelinin (S5) ve aday rapor sayfasının (E6) arka ucu (FR-27–FR-33). Kod: `Application/Review`, `Api/Controllers/ReviewController`, `ReportsController`, `EvidenceController`, `Api/Review/ReviewHub`.

**Uçlar**

| Uç | Kim | Ne yapar |
|---|---|---|
| `GET /api/review/exams/{examId}/sessions?risky=&flags=&sort=` | Değerlendirici, Admin | Oturum listesi ve karar bekleyen sayısı (`pendingCount`, FR-29). `flags` birden fazla verilebilir (`flags=NoAgent&flags=Indicated`), en az biri olanlar döner. `sort`: `Priority` (varsayılan), `Score`, `StartedAt` |
| `GET /api/review/sessions/{id}` | Değerlendirici, Admin | Olay zaman çizelgesi, olay katkıları, skor geçmişi, notlar, karar, kanıt karesi bağlantıları |
| `POST /api/review/sessions/{id}/decision` | Değerlendirici | `{ decision: Clean \| Suspicious \| Violation, note }` |
| `POST /api/review/sessions/{id}/notes` | Değerlendirici | `{ text }`; 201 ve eklenen not |
| `POST /api/events/{id}/false-alarm` | Değerlendirici | Olayı yanlış alarm işaretler |
| `GET /api/reports/exams/{examId}/sessions/{sessionId}` | Değerlendirici, Admin | Aday bazında rapor |
| `GET /api/evidence/{id}?exp=&sig=` | İmzalı bağlantıyı taşıyan | Kanıt karesi (aşağıda) |
| SignalR `/hubs/review` | Değerlendirici, Admin | `SessionReadyForReview` mesajı |

**Erişim.** Değerlendirici yalnızca `ExamAssignments`'ta `(ExamId, UserId, Role = Reviewer)` kaydı olan sınavı görür (K-23). Admin bütün sınavları okur ama karar, not ve yanlış alarm uçları ona kapalı (403): karar değerlendiricinin işi, Admin'in okuması E6'daki rapor sayfası ve denetim için. Atanmadığı sınavın oturumu için değerlendiriciye 404 dönüyor; oturumun varlığı da söylenmiyor (K-20'deki aday kuralıyla aynı).

**İnceleme durumu (`status`).** Listede ve detayda her oturumun bir inceleme durumu var. Süren ve iptal edilen oturumlar listede yok.

| Durum | Ne zaman | Karar verilebilir mi |
|---|---|---|
| `AwaitingReview` | `Completed` ve en az bir işaret var (`Risky`, `NoAgent`, `Indicated`, `PermissionGap`, `MonitoringGap`) | Evet |
| `Completed` | `Completed`, hiç işaret yok | Hayır (kart: "yalnızca riskli veya işaretli") |
| `Decided` | Karar verildi | Hayır, karar bir kez verilir |
| `NotStarted` | Rıza, eşleştirme veya doğrulama adımında kalıp son başlangıç zamanını kaçırdı (S-16) | Hayır; not yazılabilir |

**Sıralama.** Varsayılan sırada önce karar bekleyenler, sonra işaretliler (karar verilmiş olanlar dahil), sonra skor. Böylece yalnızca `NoAgent` işaretli, skoru 0 olan bir oturum da üstte kalıyor (FR-27). Satırdaki kısa gerekçe, kaydedildiği andaki katkısı en yüksek olayın gerekçesi (yanlış alarmlar hariç).

**Karar.** Oturum kilitlenip (K-18) `TransitionTo(Decided)` ile geçiyor; `ReviewerDecision` ve denetim kaydı (`review.decision`, kararın kendisi ve not olup olmadığı) aynı işlemde yazılıyor. Oturum başına tek karar indeksi eşzamanlı ikinci kararı da reddediyor (409). Not en fazla 4000 karakter.

**Notlar (S-1).** Ayrı `reviewer_notes` tablosu: bir oturuma birden fazla not, her biri yazanı ve zamanıyla. Karar verilmeden önce ve sonra yazılabiliyor, düzenlenmiyor ve silinmiyor. Karardaki not (`ReviewerDecision.Note`) kararın gerekçesi olarak ayrı kalıyor. Not metni denetim kaydına kopyalanmıyor, yalnızca hangi notun kimin tarafından eklendiği yazılıyor.

**Yanlış alarm.** Olay silinmiyor; `IsFalseAlarm`, işaretleyen ve zaman yazılıyor. Olay skora artık girmiyor (K-17): oturumun skoru bitiş anına göre yeniden hesaplanıp kaydediliyor, eski ve yeni skor denetim kaydında (`event.false_alarm`). `Risky` işareti kalıyor; değerlendirici oturumun neden incelemeye düştüğünü görmeye devam etmeli. Karar verilmiş oturumda yanlış alarm işaretlenemiyor (kararın dayandığı kanıt sonradan değişmesin). İşaret geri alınamıyor; yanlışlıkla işaretlenirse not yazılmalı.

**Detaydaki katkılar ve skor geçmişi.** Katkılar sınav bitiş anına göre risk motorundan yeniden hesaplanıyor (azalma, tekrar ve tür üst sınırı dahil); toplamları kayıtlı skorla aynı. `Event.ScoreContribution` ise olayın kaydedildiği andaki değer, ayrıca gösterilmiyor. Skor geçmişi (grafik verisi) olayların başlangıç ve bitiş anlarında ve düzenli aralıklarla (en az 1 dakika, en fazla ~300 nokta) hesaplanan skorlardan oluşuyor; azalma eğrisi de böylece görünüyor.

**Kanıt karesi bağlantısı.** Panel kareyi `<img src>` ile açıyor ve tarayıcı bu istekte `Authorization` başlığı göndermiyor. Bu yüzden yetki kontrolü bağlantıyı üreten detay ucunda yapılıyor ve bağlantı imzalı:

- `sig = HMAC-SHA256(anahtar, "evidence|{frameId}|{exp}")`, base64url. Bağlantı 5 dakika geçerli.
- İmza kare kimliğine ve son tarihe bağlı: başka bir kareye ya da daha uzun süreye çevrilemiyor. Ömründen uzun son tarih taşıyan bağlantı da reddediliyor.
- Geçersiz bağlantıda 404 dönüyor; karenin var olup olmadığı söylenmiyor. İmza geçersizse veritabanına ve dosyaya hiç dokunulmuyor. Yanıt `Cache-Control: private, no-store` ve `X-Content-Type-Options: nosniff`.
- Şifresi çözülemeyen dosya (bozulmuş ya da başka bir dosyayla değiştirilmiş, GCM etiketi tutmuyor) 500 yerine 404 dönüyor ve bütünlük sorunu olarak loga yazılıyor.
- Anahtar her açılışta rastgele üretilip yalnızca bellekte tutuluyor. API yeniden başlarsa eski bağlantılar geçersizleşiyor; panel detayı yeniden isteyip yeni bağlantı alıyor. Tek API örneğiyle çalıştığımız için yeterli (S-17); birden fazla örnek olursa anahtar ortak bir yerden okunmalı.
- `EvidenceController` bu yüzden `[AllowAnonymous]` (K-20'deki gerekçe kuralı).

**Bildirim (SignalR).** Oturum teslim edildiğinde ya da süresi dolup otomatik teslim edildiğinde (E4), işaretliyse `SessionReadyForReview` gidiyor: `{ sessionId, examId, riskScore, flags, endedAt }`. Aday adı gönderilmiyor; panel mesajı alınca listeyi yeniden çekiyor.

- Değerlendirici bağlanınca atandığı sınavların gruplarına ekleniyor. Mesaj yalnızca o sınavın grubuna ve Admin'lere gidiyor. Bağlıyken yapılan yeni atama bir sonraki bağlantıda geçerli oluyor.
- Mesaj teslim kaydedildikten (commit) sonra gidiyor. Gönderim hatası teslimi bozmuyor, yalnızca loga yazılıyor. API düşerse bildirim kaybolabiliyor ama oturum listede yine görünüyor; bildirim yalnızca kolaylık.
- Bağlantı belirtecin süresi dolunca (15 dk) kapanıyor (`CloseOnAuthenticationExpiration`); istemci yeni belirteçle yeniden bağlanıyor. Böylece pasife alınan ya da rolü değişen kullanıcı açık bağlantıyla bildirim almaya devam edemiyor (E2, FR-02).
- Tarayıcı WebSocket'te başlık gönderemediği için belirteç `access_token` sorgu parametresiyle geliyor. Bu yalnızca `/hubs` altında kabul ediliyor, REST uçlarında belirteç URL'de taşınmıyor. nginx'te `/hubs/` için ayrı `location` var: erişim günlüğü kapalı (belirteç loga düşmesin) ve okuma zaman aşımı 1 saat. Vite geliştirme sunucusunda da `/hubs` yönlendiriliyor.
- Önyüz (S5): `new HubConnectionBuilder().withUrl('/hubs/review', { accessTokenFactory: () => token }).withAutomaticReconnect().build()` ve `connection.on('SessionReadyForReview', …)`.
- `SessionService` ve `SessionTimekeeper` bildirimciyi kurucuda isteğe bağlı alıyor. Testler oturum akışını bildirimsiz kurabiliyor; uygulamada DI her zaman veriyor.

**Diğer korumalar.** Karar ve not uçlarında gövde 32 KB ile sınırlı (`RequestSizeLimit`; TestServer bu sınırı uygulamadığı için testte özniteliğin varlığı kontrol ediliyor). Liste, detay ve rapor yanıtları kişisel veri içerdiği için `Cache-Control: no-store`.

**Aday raporu.** Sınav sonucu (her soruda en yüksek `seq`'li cevap geçerli; doğru, yanlış, boş ve bağlantı kesikken verilen cevap sayısı), risk skoru ve işaretler, olay türü başına sayı, yanlış alarm sayısı ve katkı, en çok katkı yapan üç gerekçe, doğrulama denemesi sayısı, karar ve notlar. Hiç başlamayan oturumda sonuç `null` ("0 doğru" yanıltıcı olurdu). Zaman çizelgesi ve kanıt kareleri raporda yok, detay ucunda.

**Sınırlılıklar ve sonraki kartlara notlar**

- Kanıt karesinin açılması denetim kaydına yazılmıyor; erişim matrisi ve kayıt kuralları A5'te ele alınabilir. *A5'te: detay ve rapor açılışı `review.view` / `report.view` olarak yazılıyor, matris policy'lerle uygulanıyor (K-32).*
- `EvidenceFrame.DeleteAfter` geçmiş ama henüz silinmemiş bir kare detayda bağlantıyla görünüyor; silme A5'teki zamanlanmış görevde. Dosya silinmişse uç 404 dönüyor. *A5'te: görev günde bir çalışıyor (K-32).*
- Değerlendirici değiştirilirse (atama kaldırılırsa) eski değerlendiricinin kararı ve notları kalıyor; yeni değerlendirici görüyor ama aynı oturuma ikinci karar veremiyor.

---

## K-32 · Veri koruma: saklama süreleri, değiştirilemez kayıtlar, erişim matrisi, rıza metinleri (A5)

KVKK ve GAB 3.3.8'deki veri yaşam döngüsünün koda dökülmesi (FR-34, FR-35, FR-36). Kod: `Application/DataProtection`, `Application/ConsentTexts`, `Application/Audit`, `Api/Auth/AccessPolicies`, `Api/DataProtection/DataRetentionWorker`.

**Saklama süresi görevi.** `DataRetentionService` günde bir çalışıyor (`DataRetentionWorker`; ilk tur açılıştan 1 dakika sonra, `Retention:IntervalHours` ile değiştirilebilir, `Retention:Enabled=false` ile kapanıyor). Silinenler:

| Veri | Ne zaman | Not |
|---|---|---|
| Kanıt karesi (dosya + kayıt) | `DeleteAfter` geçince (sınavın saklama süresi, varsayılan 30 gün) | Önce dosya, sonra kayıt: kayıt kalırsa sonraki tur siler, tersi olsa şifreli dosya sahipsiz kalırdı |
| Olaylar, kareleri, ajan sinyalleri, doğrulama denemeleri | Sınavın saklama süresi sonunda | Oturum satırı, skor, cevaplar, notlar ve karar **kalıyor**; sınav sonucu ve karar silinmiyor. Oturumda `MonitoringDataDeletedAt` doluyor |
| Referans yüz vektörü | Adayın son sınavından 30 gün sonra | "Son sınav": adayın oturumlarındaki en son bitiş, son istek ya da açılış. Hiç sınava girmemişse kayıt anından. Aday sonra yeniden fotoğraf yüklemeli |
| Rıza kayıtları | 1 yıl sonra (geri alınmışsa geri alma anından) | Hâlâ duran bir yüz vektörünün dayandığı rıza silinmiyor |
| Denetim kayıtları | 1 yıl sonra | Veritabanı tetikleyicisinin izin verdiği tek silme (aşağıda) |

Saklama süresinin başladığı an: biten oturumda bitiş anı, hiç başlamayan oturumda sınav penceresinin kapanışı (o andan sonra oturum başlayamıyor). Süren oturumlara dokunulmuyor. Her oturum kendi işleminde ve satır kilidiyle (K-18) temizleniyor; aynı anda yanlış alarm işaretleyen istekle yarışmıyor.

Her silme denetim kaydına yazılıyor: `retention.session_purged` (oturum başına, kaç olay/kare/sinyal/doğrulama gittiği), `retention.face_template_deleted` (aday başına) ve turun özeti `retention.cleanup`. Bu kayıtlarda kişisel veri yok, yalnızca kimlik ve sayı.

**Saklama süresinden sonra inceleme ekranı.** Detay ve rapor yanıtlarında `monitoringDataDeletedAt` var. Doluysa olay listesi, skor grafiği ve gerekçeler boş; skor, işaretler, sınav sonucu ve karar kayıtlı hâliyle görünüyor. Grafik bilerek boş: olaylar olmadan yeniden hesaplansa sıfırdan oluşan, kayıtlı skorla çelişen bir eğri çıkardı.

**Değiştirilemez denetim kaydı (K-10'un ikinci adımı).** Uygulama veritabanına tabloların sahibi olan kullanıcıyla bağlandığı için `REVOKE` tek başına işe yaramıyor (sahip yetkiyi geri verebilir). Bu yüzden göçle iki tetikleyici eklendi:

- `audit_logs`: `UPDATE` ve `TRUNCATE` hiç, `DELETE` yalnızca 1 yıldan eski kayıtta kabul. Kod tarafında silmenin tek yolu `IAppDbContext.DeleteAuditLogsOlderThanAsync`; saklama görevi dışında çağrılmıyor.
- `consent_texts`: `UPDATE`, `DELETE`, `TRUNCATE` hiç kabul edilmiyor.

`ExecuteUpdate`, `ExecuteDelete` ve ham SQL interceptor'a uğramıyor; bunları artık tetikleyici durduruyor. Interceptor da genelleşti: `IAppendOnly` işaretli her varlık (`AuditLog`, `ConsentText`) için çalışıyor (`AppendOnlyInterceptor`). Ayrı ve yetkisi kısıtlı bir veritabanı kullanıcısı (S-7) hâlâ önerilir ama Docker ve CI kurulumunu değiştirdiği için bu karta alınmadı.

**Erişim matrisi.** GAB 3.3.8'deki matrisin her satırı bir policy (`AccessPolicies`). Uçlar artık rol adı değil policy adı kullanıyor:

| Policy | Matris satırı | Rol |
|---|---|---|
| `TakeExam` | Atandığı sınavı çözme, kendi rızasını verme ve geri alma | Aday |
| `ReviewRead` | Kanıt karelerini ve olayları inceleme, rapor, bildirim hub'ı | Gözetmen, Yönetici |
| `ReviewDecide` | Yanlış alarm, not ve karar | Gözetmen |
| `Manage` | Kullanıcı, sınav, soru, atama, saklama süresi | Yönetici |
| `ReadAuditLog` | Denetim kaydını görüntüleme | Yönetici |

Policy yalnızca rolü kontrol ediyor; "kendi oturumu" ve "atandığı sınav" kuralı servislerde kalıyor (K-20, K-23, K-31). `AccessMatrixTests` uygulamadaki her ucu yansımayla bulup beklenen tabloyla karşılaştırıyor: **yeni bir uç ekleyen ya da yetkisini değiştiren, bu testteki tabloyu da güncellemeli.** Aynı test, `Roles = ...` kullanan bir uç kalmadığını ve hiçbir yanıt tipinde yüz vektörü, şablon ya da sayı dizisi alanı olmadığını da kontrol ediyor ("referans yüz vektörünü görme: kimse").

**Denetim kaydı ucu.** `GET /api/audit-logs?action=&actorUserId=&targetType=&targetId=&from=&to=&page=&pageSize=` (yalnızca Admin, `ReadAuditLog`). En yeni önde, sayfa en fazla 200. `action` işlem adının başıyla eşleşiyor (`review.`, `retention.`). Yazma ucu yok.

**Kişisel veri ekranlarının kaydı.** İnceleme detayı (`review.view`, verilen kanıt bağlantısı sayısıyla) ve rapor (`report.view`) her açılışta, bakan kişinin adına denetim kaydına yazılıyor. Kanıt karesinin kendisi girişsiz imzalı bağlantıyla açıldığı için (K-31) kayıt, bağlantının verildiği anda tutuluyor. Erişimi olmayanın isteği veri göstermediği için kaydedilmiyor.

**Rıza metinleri.** `consent_texts` tablosu: tür (`Biometric`, `Session`), sürüm, başlık, metin, yayım anı. İlk sürümler (`kvkk-biometric-v1`, `kvkk-session-v1`) göçle geliyor; metinler GAB 3.3.8'deki yaşam döngüsü tablosuna göre yazıldı (`ConsentTextBodies`). Yayımlanan metin değişmiyor; metin değişecekse aynı türde yeni sürüm yeni bir göçle ekleniyor. Güncel sürüm, türün en son yayımlanan kaydı. Rıza kaydeden uçlar (A3) artık sürümü ayarlardan değil bu tablodan kontrol ediyor; eski sürümle gelen rıza A3'teki gibi reddediliyor (`BiometricOptions`'taki iki sürüm ayarı kaldırıldı).

- `GET /api/consent-texts/{type}`: güncel metin. Önyüz rıza ekranında bunu gösterip `version`'ı rıza isteğiyle geri göndermeli.
- `GET /api/consent-texts/{type}/{version}`: adayın geçmişte kabul ettiği metin.

Metinler herkese aynı ve kişisel veri içermediği için giriş yapmış her kullanıcı okuyabiliyor. Gerçek kullanımda metinlerin kurumun hukuk birimince gözden geçirilmesi gerekiyor (GAB 3.3.8).

**Raporun dışa aktarılması (FR-34).** Kartın görev satırı FR-34'ü anıyor ama kontrol listesinde yoktu; ekip kararıyla A5'e eklendi. Aynı rapor ucu `?format=Xlsx` ya da `?format=Pdf` ile dosya olarak iniyor (boşsa JSON). Erişim JSON raporla aynı (`ReviewRead`, atandığı sınav). İçerik JSON raporla aynı: özet, sonuç, işaretler, öne çıkan gerekçeler, olay türü özeti, karar ve notlar; zamanlar Türkiye saatiyle. Her dosyada "sistem kopya kararı vermez, karar desteğidir" uyarısı var (GAB 3.3.8).

- Dosya adı `aday-raporu-{oturum}.xlsx|pdf`; aday adı dosya adına girmiyor.
- Not ve ad gibi kullanıcı metinleri Excel'e düz metin olarak yazılıyor; `=` ile başlayan bir not formül olarak çalışmıyor (formül enjeksiyonu).
- İndirme denetim kaydına `report.export` (biçimiyle) olarak yazılıyor: kişisel veri sistemin dışına çıkıyor.
- Kod: `Application/Abstractions/IReportExporter`, `Infrastructure/Reports` (`ExcelReportExporter`, `PdfReportExporter`, ortak Türkçe etiketler `ReportText`).

| Paket | Sürüm | Lisans | Not |
|---|---|---|---|
| ClosedXML | 0.104.2 | MIT | Excel |
| QuestPDF | 2025.7.4 | QuestPDF Community | Akademik ve küçük ölçekli kullanımda ücretsiz; lisans türü kodda bildiriliyor (`LicenseType.Community`). Yıllık geliri 1 milyon doları aşan bir kurumda ücretli lisans gerekir |

PDF'in yazı tipi kütüphanenin gömülü Lato'su (Türkçe karakterleri içeriyor); Docker imajına (`aspnet:8.0`, Debian) ek paket gerekmiyor. Docker'da PDF üretimi E5b'de elle denenmeli.

**Kanıt karelerinin şifrelenmesi** A3'te yapılmıştı (K-26, AES-256-GCM, dosya adına bağlı bağlam); A5'te yalnızca silme eklendi.

**Sınırlılıklar**

- GAB'daki "itiraz süreci bitmişse" koşulu uygulanmadı: itiraz (FR-32) henüz yok, saklama süresi itirazı beklemiyor (S-26).
- "Silinen veriler 7 gün içinde yedeklerden de çıkar" kuralı kodda değil, yedekleme düzeninde (S-25).
- Reddedilen isteklerin (401/403) denetim kaydına yazılması (GAB 3.3.2) yapılmadı; bu istekler uygulama günlüğünde (S-27).

---

## K-33 · Şifre sıfırlama (UI1)

Kartlarda ve GAB'da yok. Stitch'teki "Şifremi unuttum ve sıfırlama akışı" ekranı için eklendi; ekip kararıyla ayrı PR'da.

| Uç | Yetki | Gövde → Yanıt |
|---|---|---|
| `POST /api/auth/forgot-password` | açık | `{email}` → her durumda `202` |
| `POST /api/auth/reset-password` | açık | `{token, password}` → `204` · 400 |

| Kural | Değer | Gerekçe |
|---|---|---|
| Belirteç | 256 bit rastgele, 30 dk, tek kullanımlık; `users.password_reset_token_hash`'te SHA-256 özeti (K-08) | Davetle aynı yöntem; e-postadaki bağlantı bir kimlik bilgisi olduğu için ömür kısa |
| Yanıt | Hesap yok, pasif ya da daveti kabul edilmemiş olsa da `202` | E-postanın kayıtlı olup olmadığı sızmasın (K-20'deki giriş kuralıyla aynı) |
| Daveti kabul edilmemiş hesap | Sıfırlama gönderilmiyor | Bu kullanıcı davet bağlantısını kullanmalı; iki ayrı kanıt yolu açılmasın |
| Bekleme | Aynı hesaba 2 dk'da en fazla bir e-posta; yeni bağlantı eskisini geçersiz kılıyor | Başkasının adresine e-posta yağdırılamasın |
| Sıfırlama sonrası | Şifre kuralı (K-20), hesap kilidi kalkıyor, bütün yenileme belirteçleri iptal | Kilitlenen gerçek kullanıcı şifresini sıfırlayınca hemen girebilsin; çalınmış oturum kapansın |
| Denetim kaydı | `user.password_reset_request`, `user.password_reset` | FR-35 |

- Kurala uymayan şifre bağlantıyı harcamıyor; kullanıcı aynı bağlantıyla tekrar deneyebiliyor.
- Bağlantı `Web:BaseUrl` + `/reset-password?token=`. Davette olduğu gibi sahte gönderici loga yazıyor (S-15).
- Sınırlılık: hesap varken e-posta gönderildiği için yanıt süresi biraz uzayabiliyor. Sahte gönderici beklemediği için şu an fark yok; gerçek SMTP gelirse gönderim kuyruğa alınmalı.

---

## K-34 · Değerlendirici paneli önyüzü (S5)

K-31'deki uçların ekranı (FR-27–FR-33). Kod: `web/src/pages/Review*Page.tsx`, `web/src/api/review.ts`, `web/src/review/`.

| Adres | Ne gösterir |
|---|---|
| `/review` | Atandığı sınavlar (`GET /api/exams`, değerlendiriciye yalnızca atandıkları döner) |
| `/review/exams/:examId` | Oturum listesi: karar bekleyen sayısı, işaret rozetleri, risk/işaret süzgeci, sıralama, kısa gerekçe |
| `/review/sessions/:sessionId` | Solda risk grafiği ve olay zaman çizelgesi; sağda kanıt kareleri, karar, notlar |

- **Roller.** Sayfalar yalnızca `Reviewer`'a açık. Karar, not ve yanlış alarm düğmeleri zaten yalnızca değerlendiriciye çıkıyor (K-31); Admin okuma ekranı E6'daki rapor sayfası.
- **Karar düğmeleri** yalnızca `status = AwaitingReview` oturumda görünür. Karar ve yanlış alarm geri alınamadığı için önce onay penceresi açılıyor.
- **Bildirim.** Liste sayfası açıkken `/hubs/review`'e bağlanır; kendi sınavının `SessionReadyForReview` mesajında toast gösterir ve listeyi yeniden çeker. Bağlanamazsa liste yine çalışır (bildirim yalnızca kolaylık). Belirteç süresi dolmak üzereyse bağlanmadan önce yenilenir (`accessTokenFactory`).
- **Kanıt bağlantıları 5 dakika geçerli.** Kare açılmazsa (süre dolmuş) detay yeniden çekilir; bu en sık 30 saniyede bir yapılır.
- **Saklama süresi dolmuş oturum** (`monitoringDataDeletedAt`): grafik yerine açıklama gösterilir (K-32).
- Yeni bağımlılıklar (kartta adı geçiyor): `@microsoft/signalr` (MIT), `recharts` (MIT).

---

## K-35 · Önyüz tasarım sistemi (UI1)

Stitch tasarımı ve `DESIGN.md` (Sıcak Minimalizm: fildişi zemin, bordo ana eylem, lacivert menü) web'e taşındı. Ekranlar üç PR'da geliyor: 1–6 (tema, kabuk, giriş, profil, hata sayfaları), 7–13 (aday sınav öncesi akışı, K-37), 14–15 (sınav ve kesinti, K-38).

| Kural | Gerekçe |
|---|---|
| Renk, yazı, boşluk, gölge ve hareket değişkenleri yalnızca `web/src/styles/theme.css`'te; bileşenler anlamsal adları (`--color-primary`, `--color-text-muted`…) kullanır, renk kodu yazmaz | Renk değişince tek dosya değişsin |
| Her bileşenin stili yanındaki `.css` dosyasında (`components/Button.css`), sayfa düzenleri `layouts/` altında | Modüler; kullanılmayan sayfanın stili inmez |
| İkincil metin `#4B5B72` (DESIGN.md'deki `#798DA6` yalnızca çizgide); kehribar uyarı metni koyulaştırıldı | `#798DA6` fildişi zeminde 3:1, okunur metin için 4,5:1 gerekiyor (WCAG AA) |
| Manrope + Inter `@fontsource-variable` paketinden, CDN yok; Vite yazı tipini `data:` olarak gömmüyor | CSP `font-src 'self'`; kullanıcının IP'si üçüncü tarafa gitmiyor |
| Göz, kamera, kilit ikonu yok; şifre "Göster/Gizle" metniyle | DESIGN.md gözetim çağrıştıran metaforları yasaklıyor |
| Ekranda gerçek olmayan iddia yok (Stitch'teki "ISO 27001 sertifikalı", "256-bit SSL", sahte grafikler çıkarıldı); şifre kuralı ekranda sunucunun kuralı (8–128 karakter) | Kullanıcıyı yanıltmamak |
| Giriş dışındaki sayfalar `React.lazy` ile ayrı parça; kabuğun içinde ayrı `Suspense` | Adayın giriş sayfası sınav/kamera kodunu indirmiyor; sayfa değişince yan menü yerinde kalıyor |
| Giriş ekranlarında form dikeyde ortalanmıyor, sabit üst boşluktan başlıyor | Ekranlar arası geçişte başlık kaymıyor (ölçülen CLS 0) |
| Rolün açamayacağı sayfada 403 ekranı (eskiden sessizce ana sayfaya atılıyordu) | Kullanıcı nedenini görsün (Stitch ekran 6) |
| Yenileme belirteci reddedilince "Oturum süresi doldu" ekranı; kullanıcının kendi çıkışında gösterilmiyor (`tokenStore.endReason`) | Kullanıcı neden çıkarıldığını bilsin; giriş sonrası kaldığı sayfaya dönsün |
| Profilde şifre değişince aynı sekmede yeni şifreyle sessizce yeniden giriş | Sunucu bütün yenileme belirteçlerini iptal ediyor (K-20); aksi hâlde sekme 15 dk sonra düşerdi |
| "Beni hatırla (30 gün)" eklenmedi | Belirteçler bilerek yalnızca bellekte; 30 günlük açık oturum sınav güvenliğiyle çelişiyor |

**nginx güvenlik başlıkları** (`web/docker/nginx.conf`): `Content-Security-Policy` (yalnızca kendi kaynakları; `connect-src`'de ajanın yerel ucu `http://127.0.0.1:47321`, K-19), `Referrer-Policy: no-referrer` (davet ve sıfırlama bağlantısındaki belirteç başka siteye gitmesin), `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Permissions-Policy` (kamera ve mikrofon yalnızca bu site), `Cross-Origin-Opener-Policy`. `/assets/` bir yıl önbellekte, `index.html` önbelleğe alınmıyor. Derlenmiş uygulama bu CSP ile denendi; ihlal yok.

**Testlerde:** `renderApp` artık `async`; sayfa parçalarını önceden yükleyip `act` içinde çiziyor. Testte `await renderApp(...)` yazın. Windows'ta testleri `C:\` (büyük harf) yolundan çalıştırın; `c:\` ile vitest modülleri iki kez yükleyip "failed to find the current suite" hatası veriyor.

---

## K-36 · Aday listesi ve kayıt fotoğrafı durumu (UI1)

Stitch "Sınavlarım" (ekran 7) ve "Kayıt fotoğrafı ve biyometrik rıza" (ekran 8) için. Kartlarda yok; ekip kararıyla ayrı API PR'ında.

| Uç | Yetki | Yanıt |
|---|---|---|
| `GET /api/exams`, `GET /api/exams/{id}` | değişmedi | Mevcut alanlara ek: `mySession: { id, state, startedAt, endedAt } \| null` |
| `GET /api/me/biometric` | `TakeExam` (aday) | `{ enrolled, enrolledAt, consentTextVersion }`; `Cache-Control: no-store` |

- `mySession` yalnızca adaya dolu, değerlendirici ve yöneticide `null`; başka adayın oturumu dönmüyor.
- En son oluşturulan oturum dönüyor, iptal edilmiş olsa da. Rızayı reddeden aday yeni oturum açabildiğini görsün (FR-10).
- Liste tek ek sorguyla dolduruluyor (N+1 yok).
- Biyometrik durum ucu vektörü ve fotoğrafı döndürmüyor. Rıza geri alınınca şablon silindiği için `enrolled: false` oluyor (K-27).

---

## K-37 · Aday akışı ekranları (UI1, Stitch 7–13)

| Karar | Gerekçe |
|---|---|
| Sihirbaz adımları: Aydınlatma ve rıza → Güvenlik ajanı → **Hazırlık** → Kimlik doğrulama. Stitch'te "Başla" en sondaydı | Sunucuda süre ve hak kimlik doğrulaması başarılı olunca başlıyor (K-24). Kurallar doğrulamadan sonra gösterilseydi aday onları okurken süresi işlerdi |
| Rıza metni ve sürümü `GET /api/consent-texts/{type}`'tan; rıza isteği bu sürümle gidiyor. Metin düz yazı olarak çiziliyor (HTML yorumlanmıyor) | Önyüz `kvkk-*-v1` sürümünü koda gömmüştü; A5'ten sonra metin değişirse her rıza 400 alırdı. Metni HTML olarak çizmek XSS'e kapı açardı |
| Kayıt fotoğrafı formu `consent` ve `consentTextVersion` alanlarıyla gidiyor | Önyüz `biometricConsent` ve `textVersion` gönderiyordu; sunucu (`PhotoUploadForm`) bunları okumadığı için her yükleme "açık rıza gerekli" ile reddediliyordu |
| `ApiError.title` Türkçe ProblemDetails başlığını veriyor; mesaj önce `detail`, yoksa Türkçe `title` | Fotoğraf, rıza ve doğrulama uçları nedeni `title`'da veriyor; önyüz bunu gösteremiyor, genel hata yazıyordu. İngilizce varsayılan başlıklar gösterilmiyor |
| Sınav kartının durumu saf fonksiyonda (`features/exams/examStatus.ts`), kart yalnızca çiziyor | Kurallar (pencere, son başlangıç, hak, fotoğraf, iptal) tek yerde ve ayrı test ediliyor |
| Tamamlanan sınavda yalnızca "Teslim edildi" görünüyor; karar ve risk adaya gösterilmiyor | Kararı değerlendirici veriyor; adaya bildirim ayrı bir iş (itiraz FR-32 yok) |
| Rıza reddi onay penceresiyle; reddedilince "hakkınız kullanılmadı" bilgisi | FR-10; yanlışlıkla tıklamada oturum iptal olmasın |
| Kamera kutusu açılmadan da 4:3 yer kaplıyor; yükleme yer tutucuları gerçek içerikle aynı yükseklikte | Sayfa kayması (ölçülen CLS 0) |
| Ajan durumu sekme arka plandayken yoklanmıyor | Gereksiz istek ve pil tüketimi olmasın |

Ortak parçalar: `hooks/useResource` (yükleme, iptal, yeniden deneme; geç gelen yanıt yenisinin üstüne yazmaz), `components/{Skeleton,EmptyState,Checkbox,Stepper}`, `features/camera/CameraView`, `features/consent/ConsentTextPanel`.

---

## K-38 · Sınav ekranı ve kesinti durumları (UI1, Stitch 14–15)

| Karar | Gerekçe |
|---|---|
| Sınav ekranı uygulama kabuğunun dışında (`/exams/:sessionId`), yan menü yok | Aday yalnızca sınava odaklansın; yanlışlıkla başka sayfaya gidilmesin |
| Oturum mantığı `features/exam/useExamSession.ts`'te (yenileme, IndexedDB kuyruğu, sıralı gönderim, kesintiden dönüş, teslim); ekran bileşenleri (`ExamTimer`, `QuestionCard`, `QuestionNavigator`, `ExamInterruptions`, `SubmitDialog`) yalnızca çizer | S3/S4'teki davranış aynen korundu; görünüm değişince mantık etkilenmesin |
| "Seçimi temizle" `selectedOption: null` gönderiyor | Sunucu boş cevabı destekliyor (K-24); aday yanlış seçimi geri alabilsin |
| "Daha sonra dön" işareti yalnızca istemcide, sunucuya gitmiyor | Sınav sonucuna etkisi yok; sunucu modeline alan eklemeye değmez. Sayfa yenilenince işaretler kaybolur |
| Çevrimdışıyken aday cevaplamaya devam ediyor; üstte bant ve bekleyen cevap sayısı görünüyor | Stitch 15. Cevaplar IndexedDB'de bekliyor, bağlantı gelince sırayla gidiyor (S3) |
| Teslim penceresi cevaplanan, boş ve işaretli soru sayısını söylüyor; bekleyen cevap varsa uyarıyor | Teslim geri alınamıyor; kuyruk gönderilemezse teslim tamamlanmıyor |
| Süre son 5 dakikada kehribar, son dakikada kırmızı; süre dolunca sorular kapanıyor, "otomatik teslim ediliyor" yazıyor | Sunucu teslimi kendisi yapıyor (K-24 `SessionTimekeeper`) |
| Teslimden sonra yalnızca "teslim edildi" ve cevaplanan sayısı; sonuç ve karar gösterilmiyor | Kararı değerlendirici veriyor |
| Üst çubuğun yüksekliği sabit; teslim düğmesi durum belli olana kadar görünmez ama yer kaplıyor | Ölçülen CLS: masaüstü 0,005, telefon 0,0004 |

---

## K-39 · Modern kurumsal İK tasarım yönü (UI1)

İlk denenen "resmî sınav evrakı" yönü (Fraunces, daktilo yazısı, kâğıt dokusu, mühür) ekipte eski nesil bulundu; yerine kullanıcının verdiği referansla (hiringcycle.ai) modern, kurumsal bir İK platformu yönü geçti. Ürün bilgisi ve tasarım ilkeleri `PRODUCT.md`'de.

| Kural | Gerekçe |
|---|---|
| Yazı Plus Jakarta Sans (başlık 750–800, gövde), `@fontsource` paketinden; IBM Plex Mono yalnızca kod/kimlik için (`.mono`). Inter, Manrope, Fraunces kaldırıldı | Referanstaki geometrik ve dostane karakter; Türkçe karakter desteği; CSP `font-src 'self'` |
| Renk: lacivert yan menü ve tanıtım panelleri (`--gradient-navy`), açık gök mavisi zemin, indigo `#4361EE` ana eylem; marka geçişi (gök mavisi → lila → gül) yalnızca giriş başlığında ve logoda | Kurumsal İK havası; geçişli yazı tek bir marka anında |
| Kartlar beyaz, 18px köşe, yumuşak lacivert gölge; düğmeler hap biçimli; rozetler yumuşak renkli hap | Modern ve okunaklı |
| Başlık üstüne küçük etiket konmaz; kartın kenarına renkli şerit konmaz | impeccable craft floor yasakları; vurgu tam çerçeve ya da zemin rengiyle |
| Yön bulma: her sayfada yol çubuğu (`PageHeader.breadcrumbs`), başlığın yanında "i" bilgi düğmesi (`InfoTip`), başlığın altında tek cümle açıklama. Uzun açıklama sayfada değil, bilgi kutusunda | Kullanıcı nerede olduğunu ve ne yapacağını bilsin; sayfa metinle dolmasın |
| İlk girişte rol bazlı 4 adımlık tanıtım (`OnboardingDialog`, `layouts/onboardingSteps.ts`), yan menüde "Tanıtımı yeniden göster". Görülme bilgisi tarayıcıda (`localStorage`); testlerde açılmaz | Kullanıcı isteği; metinler platformun gerçek davranışını anlatır. Lottie animasyonları sonra sol görsel alana gelecek |
| Hareket: `motion` kütüphanesi; sayfa geçişi, tanıtım adımları, bilgi kutusu. `MotionConfig reducedMotion="user"` ile hareket azaltma tercihine uyuluyor | Tek tek dağınık efekt yerine birkaç anlamlı an |
| Karar rozeti (`Seal`): yumuşak renkli daire, ikon ve yazı; eğim ve kenar yazısı yok. `ring`/`tilt` alanları geriye uyumluluk için duruyor | Eski mühür görünümü kaldırıldı, bileşen adı aynı |
| Belge numarası uydurma sayaç değil: `docNumber(id)` = `AG-<yıl>-<kimliğin ilk 4 karakteri>` | Ekranda gerçek olmayan bilgi yok (K-35) |
| Risk düzeyi sunucunun `Risky` işaretinden ("Eşik üstü/altı"); önyüz eşik tahmin etmiyor | Eşik `riskconfig.json`'da, tek kaynak sunucu |
| Verisi olmayan öğe eklenmedi: inceleme listesinde satır başına sinyal şeridi ve "% yanlış alarm" (liste uçları dönmüyor), J/K kısayolları (kartta yok), risk eğrisinde eşik çizgisi (eşik API'den dönmüyor), karar seçeneklerinin tanım metinleri (kartta/GAB'da yok). Kanıt görüntülemesi tek tek denetime yazılmadığı için ekranda "dosyayı açmanız denetim kaydına yazıldı" (`review.view`) deniyor | Ekranda gerçek olmayan iddia yok; kapsam dışı özellik yok |

---

## K-40 · Ekip PR'larındaki dosyalarda yapılan değişiklikler (UI1, `feature/UI1-teslim-ve-degerlendirici`)

Bu dal Yağmur'un son dalı `feature/UI1-sinav-ekrani`'nin (#33, altında #30, #31, #32) üstüne kurulu ve `dev`'i (Şeyma'nın #26'sı dahil) içine alıyor. Açık PR'lara (#30–#33) hiçbir commit atılmadı; aşağıdakiler yalnızca bu dalda. #30 ve #31 önce birleşirse bu dalın farkında yalnızca aşağıdaki değişiklikler ve yeni ekranlar kalır; #32 ve #33'ün içeriği bu dalda olduğu için o iki PR kapatılabilir.

### Birleştirme (`dev` → bu dal)

| Kimin | Dosya | Ne değişti | Neden |
|---|---|---|---|
| Şeyma (#26, S4) | `web/src/pages/ExamPage.tsx` → Yağmur'un `web/src/features/exam/ExamPage.tsx` | Eski sayfadaki `monitoring.ready` kontrolü yeni sınav ekranına taşındı: kamera ve mikrofon açılmadan sorular çizilmiyor, yerine "Sorular kamera ve mikrofon açılınca görünür…" bilgisi | #33 eski sayfayı siliyordu; birleşince bu S4 kuralı sessizce kaybolurdu (aday izin vermeden soruları görürdü) |
| Şeyma (#26, S4) | `web/src/features/exam/examPage.test.tsx` | Şeyma'nın 3 yeni testi yeni ekrana uyarlandı: `renderExam` artık `await` ile (K-35), kalan süre `timer` rolünden okunuyor. Test silinmedi, beklenen davranış değişmedi | Yağmur `renderApp`'i `async` yaptı ve süreyi ayrı bileşene aldı |
| Şeyma (S2 düzeltmesi) | `web/src/api/profile.ts` | Çakışmada Yağmur'un sürümü alındı | Aynı alan adı düzeltmesini (`consent`, `consentTextVersion`) Yağmur #32'de zaten yapmıştı; ayrıca rıza sürümünü sunucudan alıyor |
| Şeyma (S2 düzeltmesi) | `web/src/pages/profilePhoto.test.tsx` | Silindi | Yağmur #32'de sayfayı `features/biometric/ReferencePhotoPage`'e taşıdı; aynı kontroller (alan adları dahil) `referencePhoto.test.tsx`'te |

### Tasarım yönü (K-39)

| Kimin | Dosya | Ne değişti | Eski kullanım bozuldu mu? |
|---|---|---|---|
| Yağmur (#30) | `styles/theme.css` | Değerler: Manrope → Fraunces, kart beyaz, gölge yok, köşe 8px. Yeni adlar eklendi (`--font-mono`, `--color-seal`, `--texture-paper`…); eski adların hepsi duruyor | Hayır; hiçbir ad silinmedi |
| Yağmur (#30) | `styles/global.css`, `package.json` | Fraunces ve IBM Plex Mono paketleri eklendi, `@fontsource-variable/manrope` kaldırıldı | Hayır |
| Yağmur (#30) | `styles/base.css` | Zemin dokusu, başlık ağırlığı; yeni yardımcı sınıflar `.mono`, `.kicker`, `.perforation` | Hayır |
| Yağmur (#30) | `components/Panel.css`, `Button.css`, `Table.css`, `StatusBadge.css`, `PageHeader.css` | Yalnızca görünüm (gölge, köşe, tablo başlığı mono) | Hayır; sınıf adları aynı |
| Yağmur (#30) | `components/StatusBadge.tsx`, `PageHeader.tsx` | İsteğe bağlı yeni alanlar: `stamp`, `kicker`; `PageHeader.title` artık `ReactNode` | Hayır; eski çağrılar aynen çalışıyor |
| Yağmur (#30) | `auth/roles.ts`, `layouts/AppShell.tsx` | `MenuItem.end` alanı ve yeni sayfaların ikonları. Menü içeriği değişmedi; yeni bağlantı sayfası yazıldıkça ekleniyor | Hayır |
| Yağmur (#30) | `layouts/AuthLayout.tsx/.css` | Sol paneldeki üç ilke listesi yerine "Adil sınav, şeffaf karar." ve mühür (Yağmur'un Prompt B'si); form kartı ve alt çizgili alanlar. **`kicker` alanı zorunlu oldu**; dört giriş sayfasına eklendi | Evet, `AuthLayout` kullanan yeni sayfa `kicker` vermeli |
| Yağmur (#30) | `pages/auth/LoginPage.tsx` ve diğer üç giriş sayfası | Girişte açıklama satırı kaldırıldı, alt not kısaltıldı (Prompt B: "yazı çok az") | Hayır; testler aynı |
| Yağmur (#33) | `features/exam/QuestionCard.tsx`, `QuestionNavigator.tsx`, `ExamTimer.tsx`, `ExamStatusChips.tsx`, `ExamPage.tsx/.css` | Yalnızca görünüm (Prompt D, optik form): kenarda büyük soru numarası, harfli baloncuk seçenekler, altta minyatür cevap kâğıdı (yan sütun yerine), üstte 3px süre çizgisi (`ExamTimeLine`), durum noktaları, "Kaydedildi" bilgisi sunucu onayından sonra 1,2 sn görünüp sönüyor (eski sürekli "kaydedildi · saat" satırı yerine; bekleyen cevap sayısı yine sürekli görünüyor). "Tam ekrana geç" yalnızca ikon (adı ekran okuyucuda ve ipucunda). İzin kaldırılınca bulanık boş kâğıt üstünde izin kartı; **soru metni çizilmiyor** (S4 kuralı korunuyor). `useExamSession`, `useExamMonitoring` ve cevap kuyruğu değişmedi | Hayır; Yağmur'un ve Şeyma'nın sınav testlerinin hepsi değişmeden geçiyor |
| Yağmur (#30) | `layouts/AppShell.tsx/.css`, `components/BrandMark.css` | Yeni görünüm (K-39): lacivert geçişli yan menü, "Menü" başlığı, beyaz etkin öğe, "Tanıtımı yeniden göster", ilk girişte tanıtım penceresi, sayfa geçiş hareketi. Menü içeriği ve rol mantığı değişmedi | Hayır; kabuk testleri geçiyor |
| Yağmur (#30) | `components/PageHeader.tsx` | Yeni alanlar `breadcrumbs`, `info`; `kicker` artık tek öğeli yol çubuğu olarak gösteriliyor | Hayır |
| Yağmur (#30) | `layouts/AuthLayout.tsx` | `kicker` yeniden isteğe bağlı ve gösterilmiyor; sol panel yeniden üç ilke + başlık (Yağmur'un ilk sürümündeki ilkeler geri geldi) | Hayır |
| Yağmur (#30) | `pages/RoleHomePage.tsx` | Silindi; yerine gerçek ana sayfalar (yönetici: Sınavlar, değerlendirici: İncelemeler). `auth/roles.ts`'te yöneticinin ana sayfası `/admin/exams`, menüye beş yönetici sayfası eklendi | Hayır; yer tutucuydu |
| Yağmur (#30) | `App.test.tsx` | İki beklenti güncellendi: yöneticinin kökten düştüğü başlık "Yönetim" yerine "Sınavlar"; çıkış testinde `/api/auth/logout` ilk istek değil (ana sayfa önce sınavları istiyor), istekler arasında aranıyor | Davranış aynı, yalnızca ana sayfa gerçek |
| Yağmur (#32) | `features/preflight/PreflightPage.tsx`, `preflight.css` | Yol çubuğu ve "Neden bu adımlar?" bilgi kutusu; sağda "Sınava giriş kartı" (Prompt C'nin modern hâli): sınav adı, aday, süre ve adımlar. Adım göstergesi (`Stepper`) kartın içine dikey taşındı; adlar ve sıra aynı. Adım bileşenleri ve akış mantığı değişmedi | Hayır; sihirbaz testlerinin hepsi geçiyor |
| Yağmur (#33) | `features/exam/QuestionCard.tsx`, `ExamPage.css` | Kart içindeki "Soru 01 / 03" üst çubuktakini tekrarladığı için "3 sorudan 1." oldu; kenardaki soru numarası kalın | Hayır |
| Yağmur (#30) | `styles/base.css`, `layouts/AppShell.*`, `layouts/AuthLayout.*`, `main.tsx` | Koyu tema (K-42): `color-scheme`, mobil üst çubuk zemini tokenla, tema düğmesi, açılışta tema uygulanması | Hayır |
| Yağmur (#30) | `auth/roles.ts` | Bütün rollerin ana sayfası `/overview`; menülere "Genel bakış" ve "Platform hakkında", adaya "Kayıt fotoğrafı" eklendi | Hayır |
| Yağmur (#30) | `layouts/AppShell.*`, `components/PageHeader.*` | Renkli simge kutuları, daraltılabilir menü, sayfa başlığı bandı, hata sınırı (K-43) | Hayır |
| Yağmur (#30) | `App.test.tsx`, `pages/authPages.test.tsx` | Kökten ve girişten sonra beklenen başlık artık genel bakışın karşılaması ("Merhaba, Ayşe 👋", adı olmayan kullanıcıda "Hoş geldiniz 👋"); 403 ekranındaki "Ana sayfaya dön" `/overview`'e gidiyor | Davranış aynı, yalnızca ana sayfa değişti |
| Yağmur (#30) | `auth/RequireAuth.tsx`, `auth/tokenStore.ts` | K-44: rol uyuşmazlığında yönlendirme, belirteçler `sessionStorage`'da | Evet: K-35'teki iki karar değişti (gerekçe K-44'te) |
| Yağmur (#30) | `pages/profile/ProfilePage.*`, yeni `ProfileHero.tsx` | Profil kartı (avatar, ad, rol), simgeli hesap satırları; şifre formu aynı. Ekran okuyucu başlığı "Profil" | Hayır |
| Yağmur (#32) | `features/biometric/ReferencePhotoPage.*`, `referencePhoto.test.tsx` | Rıza açılır pencerede, sonra fotoğraf alanı; testler önce "Metni oku ve onayla"yı tıklıyor, beklenen gönderim aynı | Akış değişti, kurallar aynı |
| Yağmur (#32) | `features/exams/MyExamsPage.tsx`, `ExamCard.*`, `myExams.test.tsx` | Uyarı yerine eylem kartı ("Kayıt fotoğrafınızı yükleyin"); sınav bilgileri çip ve pencere kutusu | Hayır |
| Yağmur (#33) / Şeyma (S4) | `features/exam/examPage.test.tsx` | "Tarayıcıda kare saklanmaz" kontrolü: `sessionStorage`'da yalnızca oturum belirteci | Hayır; kontrolün amacı aynı |
| Şeyma (#34, S5) | `web/src/pages/ReviewExamsPage.tsx`, `ReviewSessionsPage.tsx`, `ReviewSessionPage.tsx`, `web/src/review/*`, `web/src/pages/review.test.tsx`, S5'in `web/src/api/review.ts`'i ve `global.css` ekleri | **Kaldırıldı** (Ayşe'nin kararı, 27 Kasım): S5 ile bu daldaki değerlendirici ekranları (`features/review/*`, Stitch 18–22) aynı adresleri ve aynı `api/review.ts` dosyasını kullanıyordu; birleşmede ikisi bir arada duramıyordu. Kalan ekranlar yeni tasarımda ve kullanıcı geri bildirimiyle düzeltilmiş olanlar. S5'in K-34'teki kuralları korunuyor: yalnızca `Reviewer`, karar yalnızca `AwaitingReview`'da, **yanlış alarm ve karar geri alınamadığı için önce onay** (yanlış alarm için onay penceresi bu birleşmede eklendi; karar basılı tutarak onaylanıyor), bildirimde liste yenileme ve toast, bağlanamazsa sessiz, saklama süresi dolmuşsa açıklama. `recharts` bağımlılığı kaldırıldı (grafikler SVG/CSS) | **Evet, Şeyma'nın kodu kaldırıldı.** Testlerindeki 20 davranışın hepsi `features/review/review.test.tsx`'e taşındı (aynı SignalR taklidiyle): liste ve rozetler, süzgeç/sıralama sorgusu, kendi sınavının bildirimi, başka sınavın bildirimini yok sayma, çıkınca bağlantıyı kapatma, atanmadığı sınavda sunucu mesajı, kanıt ve grafik, onaylı karar, vazgeçince istek yok, karar gerekmeyen/verilmiş oturum, onaylı yanlış alarm, not ekleme, çakışma mesajı, saklama süresi açıklaması, sınav listesi, boş liste, aday erişemez |
| Esma (#35, E5a) | `CandidateFlowE2ETests.cs`, `docs/entegrasyon-E5a.md` | Değişmedi; bu dalda da geçiyor. B-4 (rıza isteğinde `accepted`) bu dalda doğru | Hayır |
| Yağmur (K-35 kuralı) | `docs/MIMARI-KARARLAR.md` | K-35'teki "başlık üstüne küçük etiket konmaz" kuralı yeniden geçerli (K-39, modern yön) | Hayır |

---

## K-41 · Yönetici ekranları ve risk ayarları ucu (UI1, Stitch 23–32)

| Ekran | Adres | Bağlandığı uç |
|---|---|---|
| 25 Sınavlar | `/admin/exams` | `GET /api/exams`; durum erişim penceresinden (planlandı / erişime açık / son başlangıç geçti / sona erdi) |
| 26–29 Sınav düzenleyici | `/admin/exams/new`, `/admin/exams/{id}[/questions\|assignments\|risk]` | `POST/PUT/DELETE /api/exams`, soru ve atama uçları (E3), `GET /api/risk-settings` |
| 23–24 Kullanıcılar ve davet | `/admin/users` | `GET/POST /api/users`, `PATCH /api/users/{id}` (rol, pasife alma) |
| 30 Raporlar | `/admin/reports` | `GET /api/review/exams/{id}/sessions` (yönetici okur, K-31), rapor görünümü `/reports/exams/{examId}/sessions/{sessionId}`, PDF/Excel indirme |
| 31 Ayarlar | `/admin/settings` | Rıza metinleri (`GET /api/consent-texts/{type}`), sınav başına saklama süresi, risk değerleri özeti |
| 32 Denetim kaydı | `/admin/audit` | `GET /api/audit-logs` (işlem grubu, tarih aralığı, sayfa) |

- **Yeni uç `GET /api/risk-settings`** (`Manage`): `riskconfig.json`'daki oturum eşiği, tekrar ve süre ayarları ile her olay türünün ağırlığı, azalma katsayısı ve tek başına eşiği; ağırlığa göre sıralı. Salt okunur, değerler sunucunun skor hesabındakiyle aynı. Sınav başına düzenleme açık konu (S-30); ekranda "salt okunur, sınav başına ayar henüz yok" yazıyor.
- Ekranda uydurma ayar yok: "alternatif sınav yöntemi metni" ve "destek talebi" için uç olmadığından Ayarlar sayfasında gösterilmiyor.
- Yeni sınav yalnızca "Genel bilgiler" adımıyla açılıyor; kaydedilince sınavın adresine ve "Sorular" adımına geçiliyor. Adımların yanında ilerleme (soru sayısı, aday/değerlendirici sayısı).
- Kullanıcı kendi rolünü değiştiremez ve kendini pasife alamaz (ekranda "Siz"). Pasife alma yenileme belirteçlerini iptal ediyor; ekranda "oturumu en geç 15 dakika içinde kapanır" deniyor (erişim belirteci ömrü, K-20).
- Ortak desenler (filtre çipleri, arama, veri tablosu, seçim kutusu, form ızgarası) `web/src/styles/patterns.css`'te.

---

## K-42 · Koyu tema (UI1)

- Aynı token adları, koyu değerler: `theme.css`'te `:root[data-theme='dark']`. Bileşenler renk kodu yazmadığı için ek iş yok; lacivert yan menü ve tanıtım panelleri iki temada aynı.
- Tercih `lib/theme.ts`'te: açık, koyu ya da sistem (varsayılan). Tarayıcıda `aiguard.theme` anahtarıyla saklanıyor; "sistem"de işletim sistemi teması değişince sayfa da değişiyor.
- Tema `main.tsx`'te ilk çizimden önce uygulanıyor (CSP `script-src 'self'` satır içi betiğe izin vermediği için `index.html`'de değil); sayfa yanlış renkte açılıp sonra değişmiyor.
- Geçiş düğmesi (`ThemeToggle`) yan menünün altında ve giriş ekranının sağ üst köşesinde.

---

## K-43 · Genel bakış panelleri, platform hakkında ve yön bulma (UI1)

Kullanıcı geri bildirimi: "sitenin amacı belli değil", "nerede olduğumu anlamıyorum", "sayfalar şablon havasında ve dashboard olmalı".

| Karar | Ayrıntı |
|---|---|
| Her rolün ana sayfası `/overview` (genel bakış) | Aday: karşılama, sıradaki sınav ve açılışa kalan süre, hazırlık listesi (kayıt fotoğrafı gerçek durumdan), "sınav nasıl ilerler" adımları. Değerlendirici: bekleyen/karara bağlanan/tamamlanan sayıları, sınav bazında kuyruk, "incelemeye başla" (en çok bekleyen sınavın ilk dosyası). Yönetici: erişime açık ve planlanan sınav, aktif kullanıcı, karar bekleyen dosya, hızlı eylemler, sorusu olmayan sınav uyarısı, son 6 denetim kaydı. Bütün sayılar mevcut uçlardan; yeni uç yok |
| `/about` "Platform hakkında" (bütün roller) | Amaç, sinyal → açıklanabilir skor → insan kararı akışı, ilkeler (kararı insan verir, sürekli kayıt yok, açık rıza, saklama süresi, denetlenebilirlik, sıfır güven), roller, araştırma projesi. Metinler K-24…K-32 kararlarına dayanıyor; iddia yok |
| Sayfa başlığı şablonu (`PageHeader`) | Yumuşak geçişli bant, solda bölümün renkli simgesi (`IconTile`, `layouts/sections.ts`), yol çubuğu, başlık + "i", tek cümle açıklama, eylemler. Bölüm simgesi menüyle aynı; adres verilmezse yoldan bulunuyor. Değerlendirici paneli, dosya ve sihirbaz başlıkları da aynı bantta |
| Yan menü | Her bölüm kendi renginde simge kutusuyla; masaüstünde "Menüyü daralt" ile yalnızca simgeler kalır (adlar ipucunda), tercih `aiguard.sidebar.collapsed`. Telefonda çekmece |
| Tanıtım | Her rolde önce "AI-Guard nedir?" ve "Kararı insan verir", sonra role özel dört adım, sonunda "i" düğmesi ve Platform hakkında. Anahtar `aiguard.onboarding.<rol>.v2` (önceki tanıtımı görmüş kullanıcı yenisini bir kez görür) |
| Hata sınırı (`RouteErrorBoundary`) | Sayfa çizilirken ya da sayfa parçası yüklenemezken boş ekran yerine "Sayfa yüklenemedi / Sayfayı yenile". Yeni sürüm yayımlanınca eski parçanın bulunamaması da buraya düşer (yerelde, çalışırken paket kurulunca görülen boş sayfa hatası) |

---

## K-44 · Kullanıcı geri bildirimi turu: yetki, oturum, sadeleştirme (UI1)

| Karar | Ayrıntı | Önceki karar |
|---|---|---|
| Rolün açamadığı sayfa gösterilmez | `RequireAuth` rol uyuşmazlığında 403 ekranı yerine rolün ana sayfasına (`/overview`) yönlendiriyor. Başka rolün adresiyle giriş yapan da kendi ana sayfasına düşüyor (üç rolün "yanlış yerden başlaması" buydu) | K-35'teki "403 ekranı" kaldırıldı (`ForbiddenPage` kullanılmıyor) |
| Sayfa yenilenince oturum düşmez | Belirteçler sekmeye özel `sessionStorage`'da (`aiguard.session`): yenilemede kalır, sekme kapanınca silinir; `localStorage` kullanılmıyor. XSS'e karşı CSP `script-src 'self'` | K-35'teki "yalnızca bellek" |
| Site anlatımı tek yerde: tanıtım | "Platform hakkında" sayfası, panellerdeki "nasıl ilerler" bölümleri ve girişteki YM 491 satırı kaldırıldı. Tanıtıma "Bir daha gösterme" eklendi (seçilmezse bir sonraki girişte yeniden açılır, aynı oturumda açılmaz) | K-43 |
| "i" kutusu kesilmez | Kutu `document.body`'ye (portal) sabit konumla açılıyor; kart ya da başlık bandı onu kesemiyor | — |
| Form alanları yazarken bozulmaz | Sınav formunda tarih ve sayı alanlarının ham metni ayrı tutuluyor; değer yalnızca geçerliyken forma yansıyor (erişim penceresi ve saklama süresindeki "donma") | — |
| Aramalar takılmaz | Liste süzme `useDeferredValue` ile; kullanıcı tablosunda satır animasyonu yok | — |
| Kayıt fotoğrafı akışı | Önce "Önce açık rızanız gerekiyor" kartı; metin ve onay açılır pencerede; onaydan sonra sürükle-bırak fotoğraf alanı. Her yeni yükleme için rıza yeniden veriliyor | Yağmur'un satır içi rıza kutusu |
| Skor gerekçesi | Şelale yerine olay türüne göre sıralı yatay çubuk (tek renk, değer çubuk ucunda, üzerine gelince ayrıntı). "Seçilen olay" yerine "Olay ayrıntısı": açılışta en etkili olay seçili, yanlış alarm düğmesi bu kartta | — |
| Davet | Davet penceresi bağlantının 72 saat geçerli olduğunu ve e-posta gönderiminin henüz kurulmadığını (S-15) yazıyor | — |
| Aday raporu kaymaz | Rapor ızgarası ve bölümleri `minmax(0, 1fr)`; uzun sözcük (ör. boşluksuz not) satır kırıyor. Önce sayfa 18 bin piksele genişliyordu. PDF ve Excel indirme tarayıcıda denendi | — |
| Erişim penceresi | Başlangıç bitişi geçerse bitiş, pencerenin uzunluğunu koruyarak birlikte kayar | — |
| Boşluklar | Sayfa başlığı bandı, karşılama, kartlar sıkılaştırıldı; ana ekranlar 1440×900'de kaydırmadan sığıyor | — |
| Deneme verisi | `DevDataSeeder` aday1–aday12 (10 aday daha, hepsi örnek sınava atanmış); `DevDataSeederTests` sayıları `ExtraCandidates` ile | 2 aday |

Uçtan uca deneme (tarayıcı, gerçek API): giriş, yenilemede oturumun kalması, yazarak tarih/sayı girme, soru ekleme, aday ve değerlendirici atama, kaydedilen değerlerin doğruluğu (saklama 60, süre 45), raporlarda sınav seçme ve rapor açma, sınav listesinde arama: hepsi geçti.

Yerelde görülen yavaşlık: fotoğraf yükleme görüntü servisine gidiyor; servis çalışmıyorsa API 15 sn zaman aşımını bekliyor (`VisionFaceService`). Rıza geri alma ucu ölçüldü: 26 ms. Ekranda yükleme sırasında "birkaç saniye sürebilir" yazıyor.


---

## K-45 · Sınav sırası kamera analizi ve olay kuralları (D4)

S-20 kararı gereği kurallar API'de; vision durumsuz, her kare için yalnızca ölçüm dönüyor (`docs/contracts.md` 5. bölüm, v0.5). K-30'daki `PendingMediaAnalyzer` kaldırıldı; `IMediaAnalyzer`'ın uygulaması artık `CameraMonitor`. Kod: `Application/Monitoring/` (`CameraEpisodeTracker` kurallar, `CameraMonitor` kare akışı, `CameraEpisodeCloser` oturum bitişi ve temizlik, `CameraMonitorStorage` veritabanı ve kanıt), `Infrastructure/Vision/VisionFrameAnalysisService`, `Api/Monitoring/CameraMonitorSweepService`. Ses (`AnalyzeAudioAsync`) D5'e kadar hiçbir şey yapmıyor.

**Zamanlama** (oturum başına, sunucu saatiyle; ölçümün anı karenin alındığı an):

| Görev | Ne zaman | "Son kontrol" ne zaman ilerliyor |
|---|---|---|
| `face` + `pose` | Her kare | — |
| `phone` | Son telefon ölçümünden en az 3 sn sonra | Yalnızca `phone` sonucu geldiğinde |
| `identity` | Oturumun ilk karesinde, sonra son kimlik ölçümünden en az 60 sn sonra | Yalnızca `matched` true/false geldiğinde. `identity = null` (yüz yok), `INVALID_IMAGE` ya da servis hatasında ilerlemiyor; sonraki karede yeniden deneniyor |

Aynı anda gelen iki kare aynı kontrolü iki kez istemesin diye "istek yolda" işareti ayrı tutuluyor. Referans vektör oturumun ilk kimlik kontrolünde bir kez çözülüyor (`IFieldEncryptor`, `face-template:{userId}`), yalnızca bellekte duruyor ve oturum bitince ya da durum silinince sıfırlanıyor; diske ve günlüğe yazılmıyor. Şablon yoksa ya da çözülemiyorsa o oturumda kimlik kontrolü yapılmıyor.

**Olay kuralları ve bölümler**

| Olay | Başlar | Biter | confidence |
|---|---|---|---|
| `FACE_MISSING` | Art arda 3. `faceCount = 0` (başlangıç ilk kayıp) | `faceCount ≥ 1` | — |
| `GAZE_AWAY` | Seride 5. `gazeAway = true` | `gazeAway = false`, ya da 3. ardışık "yüz yok" | — |
| `MULTIPLE_FACES` | `faceCount ≥ 2` | `faceCount < 2` | — |
| `PHONE` | `phone.detected` | `detected = false`; telefon kontrolü olmayan kare bölüme dokunmuyor | Bölümdeki en yüksek telefon güveni |
| `IDENTITY_MISMATCH` | `matched = false` | `matched = true`; kontrolsüz kare ve `identity = null` bölüme dokunmuyor | `1 − en yüksek benzerlik` ([0, 1]) |

- **Bölüm bitince tek kayıt.** `CONNECTION_LOST` ve `PERMISSION_REVOKED`'daki kalıp (K-24, K-30): başlangıç anı tutuluyor, bitişte süresiyle bir kez `RecordSystemEventAsync`. EventService'te olay güncelleme yok; eşikte yazsaydık süre bilinmezdi. K-17'de de süren olay bitmeden azalmıyor. Bedeli: uzun bir bölüm panelde bitince görünüyor.
- `startedAt` = koşulun ilk ölçümü; `durationSec` = ilk ölçümden koşulun sağlandığı **son** ölçüme. Tek ölçüm 0 sn. Üç kapanış yolunda (koşul bitti, kareler kesildi, oturum bitti) aynı tanım geçerli ve süre şişmiyor. Üst sınır 3600 sn (EventService).
- **Bakış serisinde tekil yüz kaybı.** Serinin içindeki 1–2 ardışık `faceCount = 0` seriyi sıfırlamıyor, sürdürüyor, ama 5'e sayılmıyor: `G G 0 G G` olay değil, `G G 0 G G G` `GAZE_AWAY`. Bölümün süresine boşluk dahil. Seriden önce gelen kayıplar köprü değil. Gerekçe: D4 kamera denemesi (K-28), |yaw| ≈ 50–60°'de MediaPipe yüzü ara ara kaybediyor (1 → 0 → 1); sayaç sıfırlansaydı başını çok çeviren aday iki kuralın arasından kayabilirdi. Kayıp sırasında kafa yönü bilinmediği için sayılmıyor (tutucu). Yüz var ama kafa yönü yoksa (dönüşüm matrisi gelmedi; `yaw`, `pitch`, `gazeAway` üçü `null`) ölçüm aynı muameleyi görür: bakış serisinde sayılmaz, seriyi sıfırlamaz; yüz sayısı kuralları normal işler. Bakışı bilinmeyen ardışık ölçümler (yüz yok ya da kafa yönü yok) toplamda 3'e ulaşınca seri son G'de kapanır.
- Eşiğe ulaşmamış seri olay olmadan atılıyor.
- **payload** (görüntü ve vektör yok, birkaç yüz bayt; skorlar 4 basamak): hepsinde `measurements` ve `closedBy` (`condition` / `stale` / `sessionEnd`); `GAZE_AWAY` `faceLostMeasurements`, `poseMissingMeasurements`, `maxAbsYaw`, `maxAbsPitch`; `MULTIPLE_FACES` `maxFaces`; `PHONE` `maxConfidence`; `IDENTITY_MISMATCH` `minSimilarity`, `maxSimilarity`.
- **Kanıt karesi.** Vision'ın `evidenceFrame`'i çözülmüyor, yalnızca dolu olup olmadığına bakılıyor; saklanan kare API'nin aldığı ve biçimini doğruladığı baytlar. Bölümün ilk `evidenceFrame`'i olay yazılana kadar bellekte; yazılınca `IEvidenceStore` ile şifreli saklanıp `EvidenceFrame` olarak olaya bağlanıyor (saklama süresi sınavın `RetentionDays`'i). Bölüm başına en fazla bir kare; olay oluşmazsa kare saklanmıyor; kaydı yazılamayan dosya geri siliniyor (K-26).

**`MONITORING_GAP`.** Hata ayrımı VisionFaceService ile aynı (K-27): bağlantı hatası, zaman aşımı, 401, 503, 500, `VALIDATION_ERROR`, tanınmayan kod, sözleşmeye uymayan yanıt ya da ayarlanmamış servis anahtarı (`NOT_CONFIGURED`; kare anahtarsız gönderilmiyor) boşluk açıyor. Boşluk ilk başarılı yanıtta süresiyle **tek kayıt** olarak yazılıyor; her kare için olay yok. Payload: `failedFrames`, ilk hatanın `status` ve `code`'u, `closedBy` (`recovered` / `stale` / `sessionEnd`). Kareler arada 10 sn'den uzun kesildiyse boşluk son başarısız karede bitmiş sayılıyor; kesinti `CONNECTION_LOST`'un işi. **422 `INVALID_IMAGE` boşluk değil**: adayın karesi, kare atlanıyor, bölümleri ne kesiyor ne sürdürüyor. Tarayıcı isteği iptal ederse boşluk açılmıyor.

**Senkron çağrı, 3 sn zaman aşımı.** Tarayıcı bir önceki kare gitmeden yenisini göndermiyor (K-30). Bu yüzden:
- vision'ın gecikmesi (p95 ≤ 150 ms) yalnızca kare aralığına ekleniyor;
- vision yavaşlarsa kare hızı kendiliğinden düşüyor; kuyrukta biriken iş, bellek sınırı ve sıra sorunu yok;
- H3'te `/frames` yanıt süresi gerçek uçtan uca gecikmeyi gösteriyor (kuyruk bekleme süresini gizlerdi).

Zaman aşımı, kesilme (10 sn) ve bağlantı kesintisi (30 sn) eşiklerinin altında. `LastSeenAt` analizden önce yazıldığı için yavaş vision oturumu `Disconnected` yapmıyor. İstemci yüz uçlarından ayrı; oradaki 15 sn değişmedi. Olaylar `CancellationToken.None` ile yazılıyor; ölçülmüş bölüm tarayıcı iptaliyle kaybolmuyor.

**Bölümün kapandığı yerler**
1. Koşul bitti: sıradaki ölçümde, istek içinde.
2. Kareler kesildi: 10 sn başarılı ölçüm yoksa (izin bekliyor, bağlantı kesik, kamera durdu ya da vision uzun süredir yanıt vermiyor) temizlik turu (5 sn'de bir) açık bölümleri son ölçüme göre yazıyor. Uzun aradan sonra gelen ölçüm eski seriyi sürdürmüyor.
3. Teslim ve otomatik teslim: kapalı oturuma olay yazılamadığı için `SessionService.CompleteAsync` `ISessionMonitoringCloser.CloseSessionAsync`'i oturum satırı kilitliyken, aynı işlemde ve durum `Completed`'a geçmeden çağırıyor (izinsiz sürenin yazıldığı yer). `SessionService` ve `SessionTimekeeper`'a isteğe bağlı parametre olarak eklendi (Esma'nın onayıyla); verilmezse davranış aynı. İşlem geri alınırsa o anda saklanmış kanıt dosyası öksüz kalabilir; A5'teki saklama görevi kaydı olmayan dosyayı görmüyor, bilinen sınırlılık.
4. İptal (biyometrik rıza geri alındı): olay yazılmıyor. Temizlik turu kapanmış ve iptal edilmiş oturumların durumunu, vektör dahil, en geç bir tur içinde siliyor (AgentSignalMonitor'daki kalıp); BiometricEnrollmentService değişmedi.

**Durum ve temizlik.** Oturum başına durum API belleğinde, tek nesnede (`CameraMonitorStore`; API tek örnek, S-17); yeniden başlatmada açık bölümler kayboluyor. Erişim oturum başına kısa bir kilitle. Kilit vision çağrısı ve veritabanı yazımı sırasında **tutulmuyor**; tutulsaydı teslim (satır kilidi → bellek kilidi) ile kare (bellek kilidi → satır kilidi) kilitlenirdi. Oturum kapanınca durum "kapalı" işaretleniyor; yolda olan karenin geç gelen sonucu atılıyor. 2 dk kare gelmeyen ve açık bölümü olmayan oturumun durumu siliniyor. Kare akışı (`CameraMonitor`) ve kapatıcı depolamayı (şifreleyici) yalnızca kimlik vektörü ya da yazılacak bölüm gerektiğinde çözümlüyor; şifreleme anahtarı olmayan bilgisayarda kare, ses ve izin uçları ile teslim çalışıyor, kimlik kontrolü o oturumda kapanıyor (K-26).

**Eşikler** tek yerde: `CameraMonitorOptions` ("CameraMonitoring" bölümü; varsayılanlar contracts.md ile aynı). Değerler: 3 ve 5 ölçüm, telefon 3 sn, kimlik 60 sn, kesilme 10 sn, boşta silme 120 sn, zaman aşımı 3 sn, tur 5 sn. Tutarsız değerle (ör. kesilme ≤ zaman aşımı) API açılmıyor.

**Günlük.** Yalnızca oturum kimliği, olay türü, HTTP durumu ve hata kodu (K-27). Boşluk başına bir uyarı (açılışta) ve bir bilgi (dönüşte); her kare için yazılmıyor. Görüntü, vektör ve skor yazılmıyor.

**Sınırlılıklar.** Uzun bir bölüm değerlendirici panelinde ancak bitince görünüyor. API yeniden başlarsa açık bölümler yazılmıyor. Deney 2'de baş dönüklüğünde `GAZE_AWAY`/`FACE_MISSING` karışması bu kuralla yeniden ölçülmeli (K-28). Teslim işlemi geri alınırsa (ör. kayıt hatası) açık bölümler kaybolur ve oturumun kamera durumu en geç ~2 dk kapalı kalır; bu sürede kareler işlenmez. Teslimle aynı anda kapanan bir bölüm de kaybolabilir (milisaniyeler mertebesinde pencere). Temizlik turunda ya da kare isteğinde yazma hatası olursa o bölüm kaybolur; temizlik turu her oturumu kendi DI kapsamında (kendi DbContext'iyle) yazar, hata veren oturumu atlayıp günlüğe oturum kimliğiyle uyarı yazar, turun geri kalanı devam eder. Kimliğe özgü kalıcı bir doğrulama hatası olursa (ör. bozuk vektörle `VALIDATION_ERROR`) kimlik her karede yeniden istenir ve izleme boşluğa düşer; kayıtta vektör doğrulandığı için olasılığı düşük. Vision anahtarı olup şifreleme anahtarı olmayan yerel ortamda, bölüm yazılacağı zaman o kare isteği 500 alır (yalnızca yerel geliştirme; Docker ve CI anahtarı zorunlu tutuyor).

## K-46 · Türkçe / İngilizce arayüz (UI1)

Kullanıcı isteği: "uygulamanın her yerine İngilizce–Türkçe özelliği gelsin".

| Karar | Ayrıntı | Gerekçe |
|---|---|---|
| Kütüphane yok | `web/src/i18n/`: `lang.ts` (geçerli dil, `useLang`, `setLang`), `messages.ts` (`defineMessages`, `useMessages`, `pick`, `live`) | Yaklaşık 1.300 metin için i18next gibi bir paket gerekmiyor; ana paket büyümesin |
| Metinler dosyanın yanında | Her bileşen kendi `messages` sözlüğünü iki dilde tutar; sık tekrar edenler `i18n/common.ts` ve `pages/auth/authCopy.ts` | Modüler: ekranı değiştiren kişi metnini de aynı yerde görür |
| Eksik çeviri derlenmez | `defineMessages({ tr, en })`'de İngilizce sözlük Türkçeyle aynı tipte olmak zorunda (`NoInfer`) | Unutulan anahtar CI'da yakalanır |
| Etiket eşlemeleri canlı | `ROLE_LABEL`, `EVENT_LABEL`, `ACTION_LABEL` gibi eşlemeler `live()` ile; okunduğu anda geçerli dili verir | Bu eşlemeleri okuyan onlarca dosya değişmeden kaldı |
| Dil değişince ağaç yeniden çizilir | `Root` (`src/Root.tsx`) `useLang` ile abone; durum korunuyor (giriş formuna yazılan e-posta kaybolmuyor; giriş kartının anahtarı başlık değil adres) | Düz işlevlerden okunan metinler de güncel dile geçsin |
| Tercih | `localStorage` `aiguard.lang`; yoksa tarayıcı dili Türkçe değilse İngilizce. `<html lang>` güncelleniyor (ekran okuyucu doğru telaffuz etsin) | Tema tercihiyle aynı yöntem (K-42) |
| Düğme | Yan menüde, giriş ekranının ve kabuk dışı sayfaların (404, oturum süresi doldu) sağ üstünde; öbür dilin kendi adıyla ("English" / "Türkçe") | Dili bilmeyen kişi de tanısın |
| Tarih ve sayı | `lib/format.ts` geçerli dile göre `tr-TR` / `en-GB` | — |
| Sunucu metinleri | API'nin ProblemDetails metinleri Türkçe. İngilizcede istemci, duruma göre İngilizce genel metin gösteriyor (`statusMessage`); bilinen durumlar (hatalı giriş, kilit, aynı e-posta, geçersiz bağlantı) ekranın kendi metniyle söyleniyor | Sunucuya çeviri eklemek ayrı bir işti (S-31); K-48 ile sunucu metinleri de isteğin dilinde geliyor |
| Hukuki metin | Aydınlatma ve rıza metinleri sunucudan Türkçe; İngilizcede altında "bağlayıcı olan Türkçe metin" notu ve `lang="tr"` | Rıza metninin resmî hâli tek |

Sınav ekranına dil düğmesi konmadı (aday yalnızca sınava odaklanır, K-37); sınav öncesinde seçilen dil sınavda da geçerli.

## K-47 · Giriş ekranı, koyu tema ve kurumsal hareket (UI1)

Kullanıcı isteği: Şeyma'nın "yanar dönerli" arka planı, soldan tanıtım ve sağdan giriş, ikisi de animasyonlu; koyu tema renklerinin düzeltilmesi.

| Karar | Ayrıntı | Gerekçe |
|---|---|---|
| Halkalı arka plan | `layouts/AuthOrbit.tsx`: Şeyma'nın S1'deki parlayan halkaları; iki köşede iç içe halkalar, üzerlerinde farklı hız ve yönde dönen ışık yayları ve ışıması, nefes alan yumuşak parıltı | Tek "an": göz alıcı ama kurumsal |
| Takılma yok | Dönüş ve nefes yalnızca `transform` / `opacity` (kompozitörde); bulanıklık katmanda bir kez çiziliyor. Hareket azaltma tercihinde durur | Giriş ekranı zayıf cihazda da akıcı |
| Giriş hareketi | Tanıtım soldan (başlık bulanıklıktan netleşerek), ilkeler sırayla; form kartı sağdan, üzerinden bir kez ince ışık geçer. Tanıtım uygulama açılışında bir kez oynar; giriş ekranları arasında yalnızca kart yenilenir | Her geçişte sol panelin yeniden oynaması yorucu |
| Telefonda | Sol panel yok; halkaların soluk sürümü kartın arkasında, dil ve tema düğmeleri yalnızca simge | — |
| Koyu tema | Zeminler düşük doygunlukta mürekkep tonu, üç katman (zemin < yüzey < vurgulu yüzey); yan menü ve karşılama bantları sayfayla aynı aileden; seçili menü öğesi beyaz hap yerine marka renginde hafif zemin | Önceki koyu temada parlak lacivert menü ile siyaha yakın sayfa iki ayrı uygulama gibi duruyordu |
| Kontrast | Koyu temada gövde metni 14.7:1, ikincil 8.0:1, soluk 5.8:1, düğme yazısı 4.7:1 (WCAG AA) | Ölçüldü |
| Menü göstergesi | Seçili öğenin zemini sayfa değişince bir öğeden ötekine kayar (motion `layoutId`, yay) | Kurumsal, kısa, tek bir yerde |
| Düzeltme | Genel bakıştaki "Hızlı eylemler" simgeleri uzun çubuk gibi esniyordu (simge de `span`'dı); iki temada da düzeldi | — |
| YM 491 satırı | Giriş panelinde yok (K-44 kararı korunuyor) | — |

Doğrulama: giriş ve şifremi unuttum ekranları 1440 ve 390 genişlikte, açık/koyu ve TR/EN (16 görünüm) için CLS 0, yatay taşma yok. Üretim paketi nginx'teki CSP başlığıyla açıldı; ihlal yok.

### Doğrulama kuralları (bu turda sıkılaştırılan)

- E-posta: zorunlu, en fazla 254 karakter, biçim; alandan çıkınca denetleniyor, düzeltildikçe hata kalkıyor. Giriş, şifremi unuttum ve davet aynı kuralı kullanıyor (`passwordRules.validateEmail`).
- Şifre alanları: en fazla 128 karakter; Caps Lock açıkken uyarı.
- Davet ve sıfırlama bağlantısı: kod base64url biçiminde değilse sunucuya gönderilmiyor; okunduktan sonra adres çubuğundan siliniyor (tarayıcı geçmişinde ve ekran paylaşımında görünmesin).
- Davette ad en az 2 karakter; aynı e-posta (409) alanın altında söyleniyor.
- Sınav formunda süre 1–600 dk, saklama 1–3650 gün; soruda aynı seçenek iki kez yazılamıyor.
- Ajansız / göstergeli devam gerekçesi en az 10 karakter.
- Denetim kaydında başlangıç tarihi bitişten sonraysa istek gönderilmiyor.
- Bütün gönderim düğmeleri istek sürerken ikinci kez çalışmıyor.

## K-48 · Sunucu metinlerinin dili (UI1)

Önyüz TR/EN olduğu hâlde hata başlıkları, ayrıntılar, ajan göstergesi uyarıları ve olay gerekçeleri yalnızca Türkçe geliyordu.

- **Dil başlıktan.** İstemci her istekte `Accept-Language: tr` ya da `en` gönderiyor. `UiLanguageMiddleware` en yüksek öncelikli `tr`/`en` etiketini seçip `ApiLanguage`'a istek boyunca (AsyncLocal) yazıyor. Başlık yoksa, tanınmıyorsa ya da istek dışındaysa (arka plan görevi, SignalR bildirimi, test) **Türkçe**. `CultureInfo` değişmiyor: sayı, tarih ve ayrıştırma bugünkü gibi; işletim sisteminin dili de etkilemiyor.
- **Metinler kodda Türkçe kalıyor.** İngilizce karşılıklar tek yerde: `Api/Localization/ServerTexts.cs` (birebir sözlük + değişkenli cümleler için kalıp). `ProblemDetails` oluşturulurken (`CustomizeProblemDetails`) istek İngilizceyse `title` ve `detail` çevriliyor; karşılığı olmayan metin Türkçe kalıyor, boşalmıyor.
- **Unutulmaz.** `ServerLocalizationTests` kaynak koddaki her sabit Türkçe hata metnini (`title:`, `detail:`, `Fail(...)`, `Invalid(...)`, `Error = ...`) tarıyor; İngilizcesi yoksa test kırılıyor. Yeni bir hata metni eklendiğinde `ServerTexts`'e de eklenmeli.
- **Ajan uyarıları** (`IndicatorAdvice`) iki dilde yazıldı. **Olay gerekçeleri** veritabanında Türkçe saklanıyor (kayıtlı değer değişmiyor); inceleme ve rapor yanıtında `ReasonFormatter.Localize` İngilizceye çeviriyor ("3. dakikada 8 sn telefon görüldü" → "In minute 3, for 8 s, phone seen").
- Sınav düzeyi CSV başlıkları da isteğin diline göre. Rıza metinleri sürümlü ve değiştirilemez olduğu için (K-32) bu kapsamda çevrilmedi; İngilizce rıza metni gerekirse ayrı bir sürüm olarak eklenmeli.

## K-49 · Yönetim özellikleri: e-posta, platform ayarları, davet, sınava özel risk, toplu rapor, bildirimler (UI1)

| Konu | Karar |
|---|---|
| Gerçek e-posta | `SMTP_HOST` tanımlıysa `SmtpEmailSender` (MailKit 4.18.2), değilse eskisi gibi `LoggingEmailSender`. Ayarlar ortam değişkeninden: `SMTP_HOST`, `SMTP_PORT` (587), `SMTP_SECURITY` (`StartTls`/`Ssl`/`None`), `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM` (host varken zorunlu, yoksa açılışta hata). 15 sn zaman aşımı; gönderim hatası loglanıyor, isteği bozmuyor (davet/sıfırlama yine 2xx, hesap varlığı sızmasın). Bağlantı loga yazılmıyor. Şablonlar (`EmailTemplates`) iki dilli, düz metin + HTML, ad ve bağlantı HTML kaçışlı |
| Platform ayarları | Tek satırlık `platform_settings` tablosu: alternatif sınav yöntemi metni (≤4000), destek e-postası ve telefonu, ajan indirme adresleri (Windows, macOS). `GET /api/platform-settings` her giriş yapmış kullanıcıya (aday alternatif yöntem ekranında okuyor), `PUT` yalnızca Admin (`Manage`). Adresler yalnızca `https://`, ≤500; telefon `^[0-9 +()\-]{3,40}$`; boş metin "ayarlanmadı" (null). Her değişiklik denetim kaydına (`settings.update`) |
| Son giriş | `users.last_login_at` başarılı girişte yazılıyor; `GET /api/users` dönüyor |
| Daveti yeniden gönder | `POST /api/users/{id}/invite` (Admin): yeni bağlantı üretir, eskisini geçersiz kılar, süresi yeniden 72 saat. Davet kabul edilmişse ya da kullanıcı pasifse 409; aynı kullanıcıya dakikada bir (`users.invite_sent_at`), erkense 429. Denetim: `user.invite_resend` |
| Sınava özel risk ayarı | `GET/PUT/DELETE /api/exams/{examId}/risk-settings` (Admin). Varsayılan `riskconfig.json`; sınav yalnızca farklı olan olay türlerini (`risk_settings.exam_id` dolu satırlar) ve isterse oturum eşiğini (`exams.risk_session_threshold`) saklıyor. Ağırlık 0–100, azalma 0–5/dk, tek başına eşik boş ya da 0–1000, oturum eşiği 1–1000. **Sınavda ilk oturum açılınca kilitli (409):** aynı sınavdaki adaylar aynı ayarla puanlanır, geçmiş skorların hangi ayarla hesaplandığını ayrıca saklamak gerekmez. Olay kaydı, inceleme ve rapor `ExamRiskService.EngineForExamAsync` ile oturumun sınavının motorunu kullanıyor. Denetim: `exam.risk_settings` |
| Sınav düzeyi rapor | `GET /api/reports/exams/{examId}` (`ReviewRead`, değerlendirici yalnızca atandığı sınav): atanan/başlayan/süren/tamamlanan/inceleme bekleyen/karar verilen sayıları, riskli oturum, ortalama ve en yüksek skor, ortalama doğru, karar dağılımı, olay türü sayıları, dakika başına olay yoğunluğu. `?format=Csv` bütün oturumlar tek dosya: UTF-8 BOM, `;` ayraç, `= + - @` ile başlayan hücreler `'` ile metne çevriliyor (CSV enjeksiyonu). Denetim: `report.exam_view` / `report.exam_export` |
| Bildirim zili | `GET /api/notifications`: ayrı tablo yok, her istekte güncel veriden türetiliyor, en fazla 20. Değerlendirici: atandığı sınavlarda inceleme bekleyen dosyalar. Admin: inceleme bekleyen sınavlar, yaklaşan ama sorusu ya da adayı olmayan sınavlar, süresi dolan davetler. Aday: açık ya da yakında açılacak sınavları, kayıt fotoğrafı yoksa uyarı. Okundu bilgisi istemcide (bildirim kimliği kararlı, ör. `review:{sessionId}`) |

Göç: `UI1_YonetimAyarlari` (`users.invite_sent_at`, `users.last_login_at`, `exams.risk_session_threshold`, `platform_settings`). Erişim matrisi testine yeni uçlar eklendi.

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

*A4'te uygulandı (K-31): liste ucu `pendingCount` dönüyor, varsayılan sırada işaretli oturumlar skordan bağımsız önde.*

- **İnceleme bekleyen oturum sayısı (FR-29).** Değerlendirici panelinde, atandığı sınavlarda incelenmeyi bekleyen (riskli veya işaretli, henüz kararı olmayan) oturum sayısı gösterilmeli. Liste ucuyla birlikte bu sayı da dönmeli.
- **İşaretli oturumlar skordan bağımsız listelenir (FR-27).** Listede öne çıkarma `Flags` içinde `Risky`, `NoAgent`, `Indicated`, `PermissionGap` veya `MonitoringGap` olmasına göre yapılmalı, yalnızca skora göre değil.

### S5 · Değerlendirici inceleme paneli

- **İnceleme bekleyen oturum sayısı panelde görünmeli (FR-29).** Sayıyı A4 sağlayacak.
- **İşaretler ayrı ayrı gösterilmeli (FR-27).** Riskli, ajansız, göstergeli, izinsiz süre ve izleme kesintisi işaretleri listede rozet olarak görünmeli.

---

## Açık konular

| # | Konu | İlgili | Öneri |
|---|---|---|---|
| S-1 | A4'teki not ucu için ayrı tablo mu, `ReviewerDecision.Note` mu? | Ayşe, Şeyma | **Çözüldü (A4):** ayrı `reviewer_notes` tablosu, oturuma birden fazla not; karar notu `ReviewerDecision.Note`'ta (K-31) |
| S-2 | Şifre özetleme yöntemi | Esma | **Karar (E2):** ASP.NET Core Identity PasswordHasher (PBKDF2) kalıyor; değişirse yalnızca `IdentityPasswordHasher` etkilenir |
| S-3 | HMAC anahtarının şifreli saklanması (K-08) | Yağmur | Y2'de bu alanın kullanılması |
| S-4 | Docker ortamında göçlerin uygulanması (K-07) | Yağmur | **Çözüldü (E1b):** compose'daki tek seferlik `migrate` servisi EF göç paketini (`efbundle`) çalıştırıyor; `api` bunun başarıyla bitmesini bekliyor |
| S-5 | A6, E5b'ye bağlı ama tarihleri çakışıyor | Ayşe, Esma | Locust betiği 29 Kasım'da, ölçüm E5b'den sonra |
| S-6 | Cevaptaki sorunun oturumun sınavına ait olması veritabanında zorlanmıyor | Esma (E4) | **Çözüldü (E4):** serviste kontrol ve test (K-24) |
| S-7 | Uygulama yerelde `postgres` süper kullanıcısıyla bağlanıyor | Ayşe (A5), Yağmur (E1b) | **Kısmen (A5):** denetim kaydı ve rıza metinleri tetikleyiciyle korunuyor (K-32). Docker/sunucu için yalnızca gerekli yetkilere sahip ayrı kullanıcı (göçler ayrı kullanıcıyla) hâlâ öneri |
| S-8 | `agent_signals` hızlı büyüyor; kullanıcı silme talebi | Ayşe (A5) | **Kısmen (A5):** sinyaller sınavın saklama süresi sonunda oturumun izleme verisiyle siliniyor (K-32). Kullanıcı silme talebi (KVKK m. 11) için uç yok |
| S-9 | Olay ucu E2 gelene kadar yalnızca Development'ta açıktı | Esma (E2), Ayşe | **Çözüldü (E2):** aday rolü ve oturum sahipliği kontrolü (K-20) |
| S-10 | `riskconfig.json` ağırlıkları başlangıç tahmini | Tüm ekip | Pilot oturumda ayarlanması, deneylerde sabit tutulması |
| S-11 | Ajan aynı göstergeyi her sinyalde bildirirse her seferinde olay oluşur | Yağmur (Y2–Y4) | **Çözüldü (Y3):** gösterge yalnızca ilk görüldüğünde ve kaybolup yeniden göründüğünde olay oluyor (K-22) |
| S-12 | Eşleştirme kodu ucu (`POST /api/sessions/{id}/agent-code`) E2 gelene kadar yalnızca Development'ta açıktı | Esma (E2), Yağmur | **Çözüldü (E2):** aday rolü ve oturum sahipliği kontrolü (K-20) |
| S-13 | GAB FR-26: 5 dakikadan uzun kesinti "uzun kesinti olayı" üretmeli; ortak listede tür yok | Esma (E4), Ayşe | **Karar (E4):** ayrı tür eklenmedi; `CONNECTION_LOST` süresiyle yazılıyor, süre çarpanı ağırlaştırıyor (K-24). Gerekirse pilot oturumda ağırlık ayarı |
| S-14 | `POST /api/agent/pair` eşleşme kimliği dönüyor ama ajana henüz bir anahtar vermiyor | Yağmur (Y2) | **Çözüldü (Y2):** anahtar eşleşmede üretilip yanıtta veriliyor; veritabanında özeti (K-21) |
| S-15 | Davet e-postası sahte gönderici ile loga yazılıyor; davet yeniden gönderme ucu yok | Esma | **Çözüldü (UI1):** `SMTP_HOST` tanımlıysa SMTP ile gönderim; `POST /api/users/{id}/invite` (K-49) |
| S-16 | `AgentPairing.EncryptedSessionKey` artık kullanılmıyor (K-21: veritabanında yalnızca özet) | Ayşe, Yağmur | Bir sonraki göçle kaldırılması; K-13 gereği önce ekipte konuşulacak |
| S-17 | Ajan oturum anahtarları tek API sürecinin belleğinde; API yeniden başlarsa ya da birden fazla örnekle çalışırsa doğrulanamıyor | Yağmur, Esma | Proje tek örnekle çalışıyor; gerekirse paylaşılan önbellek (ör. Redis) ya da ajanın yeniden eşleşmesi |
| S-18 | `vpn-ip-blocks.json` yalnızca örnek bloklar içeriyor; gerçek VPN/veri merkezi listesinin kaynağı belirlenmedi (FR-23, İstenen) | Yağmur, Ayşe | Pilot oturum öncesi bir kaynak seçilip liste doldurulması; seçilen kaynak ve tarih dosyaya yazılmalı |
| S-16 | Rıza/eşleştirme/doğrulama adımında kalıp son başlangıç zamanını kaçıran oturum açık kalıyor (süre başlamadığı için otomatik teslim yok) | Esma, Ayşe (A4) | **Karar (A4):** listede ve raporda `NotStarted` olarak gösteriliyor, karar verilemiyor (K-31). Zamanlanmış görevle iptal istenirse durum makinesine geçiş gerekiyor (S-23) |
| S-19 | Yüz doğrulamada `passed` kararını kim veriyor: vision mı, API mi? `Verification.Passed` yorumu "sonuç sunucuda eşiklere göre belirleniyor" diyor | Ayşe (A3), Dilan (D2) | **Çözüldü (A3):** kararı vision veriyor (`config.yaml`), API skorları, sonucu ve `reason`'ı kaydediyor; API'de eşik yok (K-27) |
| S-20 | Kamera olay kuralları ("art arda 3 ölçümde yüz yok", "art arda 5 ölçümde bakış sapması") nerede çalışacak? | Ayşe, Dilan (D4) | **Çözüldü (D4):** kurallar API'de (CameraEpisodeTracker), bölüm bitince süresiyle tek kayıt (K-45) |
| S-21 | `POST /internal/analyze/frame` isteğinde `referenceVector` D4 kartında yok, ama vision vektör saklamadığı için 60 sn'lik kimlik kontrolünde gerekli | Ayşe, Dilan (D4) | Yalnızca `tasks` içinde `identity` varken gönderilsin; vision bu durumda alanı zorunlu tutuyor |
| S-22 | Ses parçasının uzunluğu ve biçimi belirlenmedi | Yağmur (S4), Dilan (D5) | **Çözüldü (S4):** ~1 sn WebM/Opus, her parça kendi başına tam dosya; API WebM ve WAV kabul ediyor, en fazla 256 KiB (K-30). Vision 16 kHz mono'ya kendisi dönüştürüyor |
| S-23 | Biyometrik rıza geri alınınca `AgentPairing` ve `IdentityVerification` adımındaki oturumlar iptal edilemiyor: durum makinesinde bu adımlardan `Cancelled` geçişi yok (GAB 3.4.4) | Ayşe (A3), Esma (E4) | Şimdilik oturum olduğu adımda kalıyor, vektör silindiği için doğrulamayı geçemiyor ve son başlangıç zamanını kaçıran takılı oturumlarla (K-24'teki S-16) aynı şekilde ele alınıyor. Tabloya bu iki geçişin eklenmesi önerilir |
| S-24 | Tarayıcı ses parçasını WebM/Opus olarak kaydedemiyorsa (`MediaRecorder` yok ya da `audio/webm` desteklenmiyor) ses gönderilmiyor; kareler gidiyor | Yağmur (S4), Dilan (D5) | D5'te kare gelirken ses gelmeyen oturum için `MONITORING_GAP` üretilmesi; gerekirse tarayıcıda WAV'a dönüştürme. Desteklenen tarayıcılar `docs/tarayici-deneme-notlari.md`'deki S4 denemesiyle belirlenecek |
| S-25 | GAB 3.3.8: "Silinen veriler en geç 7 gün içinde yedeklerden de çıkar" | Yağmur (E1b), Ayşe | Kodla değil yedekleme düzeniyle: yedekler en fazla 7 gün tutulmalı. Veritabanı yedeği alınıyorsa saklama süresi buna göre ayarlanmalı |
| S-26 | GAB'da olaylar ve kararlar "saklama süresi sonunda (itiraz süreci bitmişse)" siliniyor; itiraz (FR-32) henüz yok | Ayşe, Esma | Şimdilik saklama süresi itirazı beklemiyor (K-32). FR-32 eklenirse açık itirazı olan oturum saklama görevinde atlanmalı |
| S-27 | GAB 3.3.2: reddedilen istekler denetim kaydına yazılmalı | Ayşe, Esma | A5'te yapılmadı (K-32): her 401/403'ü kayda yazmak tabloyu hızla büyütür. Uygulama günlüğünde var; gerekirse yalnızca tekrarlayan reddedilmeler (ör. aynı kullanıcıdan art arda 403) kayda alınabilir |
| S-28 | Web kayıt fotoğrafı formu `biometricConsent` ve `textVersion` alanlarını gönderiyor, API `consent` ve `consentTextVersion` bekliyor; tarayıcıdan yükleme rıza hatası alır | Şeyma (S2), Ayşe (A3) | Önyüzde alan adlarının düzeltilmesi; sürümün sabit yerine `GET /api/consent-texts/Biometric` yanıtından alınması (K-32) |
| S-29 | Sorular kamera/mikrofon açılmadan önyüzde gizleniyor, ama sunucu `InProgress`'te soruları yine döndürüyor; değiştirilmiş bir istemci kamera açmadan soruları okuyabilir | Yağmur (S4), Esma (E4) | Sunucuda da kontrol: oturum uç `GET /api/sessions/{id}` soruları ancak son birkaç saniyede kare gelmişse döndürsün. K-24'ü değiştirdiği için ekip kararı gerekiyor |
| S-30 | Risk ağırlıkları şu an platform geneli ve salt okunur (`riskconfig.json`, K-41); Stitch 29 sınav başına düzenleme gösteriyordu | Ayşe | **Çözüldü (UI1):** `GET/PUT/DELETE /api/exams/{id}/risk-settings`; ilk oturumdan sonra kilitli, böylece bir sınavın bütün skorları aynı ayarla (K-49) |
| S-31 | API'nin hata metinleri (ProblemDetails) yalnızca Türkçe; İngilizce arayüzde istemci genel metin gösteriyor | Ekip | **Çözüldü (UI1):** istemci `Accept-Language` gönderiyor, sunucu `title`/`detail`'ı çeviriyor (K-48) |
