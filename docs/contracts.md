# Görüntü/Ses Servisi (vision) – JSON Sözleşmesi

> **Durum:** TASLAK v0.5 · 26 Kasım 2026 · Hazırlayan: Dilan Özmen
> v0.5 (D4, api): olay bölümleri (süreyle, bitişte tek kayıt), `startedAt`/`durationSec` tanımı, bakış serisinde tekil yüz kaybı kuralı, `MONITORING_GAP`'in tek bölüm olması (MIMARI-KARARLAR K-45)
> v0.4 (D4): `analyze/frame` uygulandı: görev başına alanlar, `null` kuralları, telefon eşiği, kimlik eşiği, kanıt karesi, yaw/pitch işareti, süre
> v0.3 (D2): boyut sınırları, `qualityScore` tanımı, `verify` karar kuralları, `referenceVector` doğrulaması
> Onaylayacaklar: Ayşe Nur Kendirci (A3, D4/D5 entegrasyonu), Esma İrem Öztürk
> Kaynaklar: D1, D2, D4, D5, A2, A3, E1b, S2 kartları; MIMARI-KARARLAR K-13, K-16, K-17

Bu belge, ana arka uç (`api`, .NET) ile görüntü/ses servisi (`vision`, FastAPI) arasındaki tüm istek ve yanıtları tanımlar. Sözleşme onaylandıktan sonra değişiklik yalnızca ekip onayıyla yapılır.

---

## 1. Genel kurallar

| Konu | Karar |
|---|---|
| Adres | `http://vision:8000` (Docker Compose iç ağı). Dışarıya port açılmaz; yalnızca `api` çağırır. |
| Kimlik doğrulama | `/internal/*` isteklerinde `X-Service-Key: <VISION_SERVICE_KEY>` başlığı (CLAUDE.md, `.env.example`). Eksik veya yanlışsa `401`. Anahtar tanımlı değilse servis açılmaz. |
| Gövde | `Content-Type: application/json`, UTF-8. |
| Görüntü | **Base64 kodlu** görüntü (`data:` öneki olmadan). Kamera kareleri JPEG; kayıt fotoğrafı JPEG, PNG veya WebP olabilir (S2). Biçim dosyanın ilk baytlarından anlaşılır; her iki uçta da üç biçim kabul edilir. EXIF yönü uygulanır (yan kaydedilmiş telefon fotoğrafı dik okunur). |
| Boyut sınırları | api'deki sınırlarla aynı (`ImageFile`): fotoğraf 5 MiB, kare 1 MiB. Base64 karşılıkları şemada üst sınırdır: `image` en fazla 6 990 508, her `frames` öğesi en fazla 1 398 104 karakter; aşan istek `VALIDATION_ERROR`. Çözülmüş görüntünün uzun kenarı en fazla **6000 piksel**; kenar başlıktan, çözmeden önce okunur, aşan görüntü `INVALID_IMAGE`. |
| Ses | **Base64 kodlu** WAV veya WebM/Opus parçası. Servis 16 kHz mono'ya kendisi dönüştürür. |
| Zaman | `ts`: ISO 8601, UTC, milisaniyeli (ör. `2026-11-24T10:15:30.250Z`). Servis değeri aynen geri döner. |
| Yüz vektörü | 512 adet `float`, L2 ile normalize (birim uzunlukta). Servis vektörü **saklamaz**; şifreleme ve saklama arka ucun işidir (A3). |
| Saklama | Servis gelen görüntü ve sesi diske yazmaz, işlendikten sonra bellekten atar. |
| Durum | Servis **durumsuzdur (stateless)**; oturum bilgisi tutmaz (bkz. Bölüm 7, S-20). |

### Hata biçimi (tanımlı uçlar)

```json
{ "error": { "code": "NO_FACE", "message": "Görüntüde yüz bulunamadı." } }
```

Tanımsız yol veya yanlış HTTP yöntemi FastAPI'nin varsayılan yanıtını döner; arka uç yalnızca tanımlı uçları doğru yöntemle çağırır.

| HTTP | `code` | Ne zaman |
|---|---|---|
| 401 | `UNAUTHORIZED` | `X-Service-Key` eksik veya yanlış |
| 422 | `VALIDATION_ERROR` | Gövde sözleşmeye uymuyor (eksik alan, yanlış tür) |
| 422 | `INVALID_IMAGE` / `INVALID_AUDIO` | Base64 veya dosya çözülemedi; görüntü JPEG/PNG/WebP değil ya da kenarı 6000 pikseli aşıyor |
| 422 | `NO_FACE` / `MULTIPLE_FACES` / `FACE_TOO_SMALL` / `IMAGE_TOO_BLURRY` | Yalnızca `enroll` ucunda (Bölüm 3) |
| 500 | `INTERNAL_ERROR` | Beklenmeyen hata |
| 503 | `MODELS_NOT_READY` | Modeller henüz yüklenmedi |

---

## 2. `GET /health`

Sağlık kontrolü. E1b'den geliyor ve değişmiyor: docker-compose.yml'deki healthcheck ve CI bu ucu çağırıyor. **Servis anahtarı istemez**; yanıtta kişisel veri yoktur.

```json
{ "status": "Healthy" }
```

---

## 3. `POST /internal/face/enroll` — kayıt fotoğrafından yüz vektörü (D2 · A3)

**İstek**
```json
{ "image": "<base64 JPEG/PNG/WebP>" }
```
Arka uç, `POST /api/me/photo` ile gelen fotoğrafı (multipart, S2) base64'e çevirip gönderir; vektörü AES-GCM ile şifreleyip `ReferenceFaceTemplate.EncryptedVector`'a yazar (A3).

**Yanıt 200**
```json
{
  "vector": [0.0123, -0.0456, "... toplam 512 değer"],
  "faceBox": { "x": 210, "y": 120, "width": 180, "height": 180 },
  "qualityScore": 0.91
}
```

- `vector`: 512 değer, L2 ile normalize (birim uzunlukta).
- `faceBox`: yüzün piksel kutusu (MediaPipe yüz ağının sınır kutusu, görüntünün içine kırpılmış; EXIF yönü uygulanmış görüntüde).
- `qualityScore` (0,5–1): iki kabul ölçütünün zayıf olanı. Yüzün kısa kenarı `min_face_px`'e, yüz netliği `min_sharpness`'a bölünür, her oran 2'de kesilir; skor küçük oranın yarısıdır. Tam eşikteki fotoğraf 0,5, iki ölçütte de eşiğin en az iki katı olan fotoğraf 1 alır. Karar için kullanılmaz, yalnızca bilgi (kod: `app/face_service.py` `quality_score`).

**Hatalar (422)**, bu sırayla kontrol edilir:

| Sıra | `code` | Ne zaman |
|---|---|---|
| 1 | `INVALID_IMAGE` | Görüntü çözülemedi (Bölüm 1) |
| 2 | `NO_FACE` | Yüz yok |
| 3 | `MULTIPLE_FACES` | 2 ya da daha fazla yüz (yüz sayısı tam 1 olmalı) |
| 4 | `FACE_TOO_SMALL` | Yüz kutusunun kısa kenarı < `enroll.min_face_px` |
| 5 | `IMAGE_TOO_BLURRY` | Yüz bölgesinin Laplacian varyansı < `enroll.min_sharpness`. Kesit önce kısa kenarı 112 px olacak şekilde ölçeklenir; böylece değer fotoğrafın çözünürlüğünden bağımsızdır |

Eşikler ve gerekçeleri `vision/config.yaml`'dadır.

---

## 4. `POST /internal/face/verify` — sınav başı doğrulama (D2 · A3)

**İstek**
```json
{
  "frames": ["<base64 JPEG>", "<base64 JPEG>", "<base64 JPEG>"],
  "referenceVector": [0.0123, -0.0456, "... 512 değer"]
}
```
- `frames`: **3–5 kare**. S2'deki önyüz şu an 3 kareyi ~150 ms arayla, JPEG (kalite 0,92) olarak gönderiyor.
- `referenceVector`: arka ucun şifresini çözdüğü kayıtlı vektör. 512 değer olmalı, sonlu olmayan değer ya da sıfır vektör `VALIDATION_ERROR`; birim uzunlukta değilse servis yeniden normalize eder.
- Karelerden biri çözülemezse tüm istek **422 `INVALID_IMAGE`** (hiçbir model çalışmaz). Diğer bütün durumlarda 200.

**Yanıt 200** (doğrulama başarısız olsa da HTTP 200 döner; sonucu `passed` belirler)
```json
{
  "livenessScore": 0.94,
  "similarity": 0.62,
  "passed": true,
  "reason": null,
  "framesUsed": 3
}
```
- `livenessScore` (0–1): kullanılabilir karelerin Silent-Face skorlarının ortalaması.
- `similarity` (−1…1): kullanılabilir karelerin vektörleriyle `referenceVector` arasındaki kosinüs benzerliklerinin ortalaması.
- Skorlar her zaman aralıkta bir **sayıdır**, `null` olmaz; yüz bulunmayan durumda 0.
- `framesUsed`: kullanılabilir kare sayısı.
- `reason`: `passed=true` ise `null`; değilse `LIVENESS_FAILED` | `IDENTITY_MISMATCH` | `NO_FACE` | `MULTIPLE_FACES`.
- Arka uç skorları `Verification.LivenessScore` / `Similarity`'ye yazar (S-19).

**Karar kuralları** (sırayla; kod: `vision/app/face_service.py` `verify`):

1. Herhangi bir karede 2 ya da daha fazla yüz varsa → `MULTIPLE_FACES`, diğer kareler temiz olsa bile (kameraya ikinci kişi girmesi uyarı işaretidir). Skorlar 0.
2. Tam bir yüz bulunan kareler **kullanılabilir** karelerdir. Kullanılabilir kare sayısı kare sayısının yarısından azsa → `NO_FACE` (3 karede en az 2, 4 karede en az 2, 5 karede en az 3 gerekir). Skorlar 0.
3. Aksi hâlde iki skor da yalnızca kullanılabilir karelerden, ortalama alınarak hesaplanır. Canlılık kalsa bile benzerlik hesaplanır ve döner.
4. `passed` = `livenessScore ≥ verify.liveness_threshold` **ve** `similarity ≥ verify.match_threshold` (eşikler `config.yaml`'da, sınır değer geçer).
5. Neden önceliği: canlılık kaldıysa (benzerlik ne olursa olsun) `LIVENESS_FAILED`, sahte yüz önce gelir; yalnızca benzerlik kaldıysa `IDENTITY_MISMATCH`.

Eşikler: canlılık 0,5 (Silent-Face `test.py` kararının tek sayı karşılığı, gerekçe `config.yaml`'da); benzerlik LFW'de FAR ≈ %1 için ölçüldü (`docs/threshold-D2.md`).
- Başarısız denemede `VERIFICATION_FAILED` olayını ve kanıt karesini **arka uç** kaydeder; kareler zaten arka uçtadır.

---

## 5. `POST /internal/analyze/frame` — sınav sırası kamera analizi (D4)

**İstek**
```json
{
  "sessionId": "8f3c...-uuid",
  "frame": "<base64 JPEG>",
  "ts": "2026-11-24T10:15:30.250Z",
  "tasks": ["face", "pose", "phone", "identity"],
  "referenceVector": [0.0123, "... 512 değer"]
}
```
- `frame`: base64 kare, en fazla 1 MiB (1 398 104 karakter; aşan istek `VALIDATION_ERROR`). S4'teki tarayıcı saniyede bir, en fazla 640 px genişlikte, JPEG 0,8 gönderiyor. Çözülemezse **422 `INVALID_IMAGE`** (hiçbir model çalışmaz).
- `tasks`: en az bir görev; `face`, `pose`, `phone`, `identity` dışındaki değer `VALIDATION_ERROR`. Arka uç zamanlamayı bu alanla belirler (D4):
  - `face` + `pose`: her kare (saniyede 1)
  - `phone`: 3 saniyede bir
  - `identity`: 60 saniyede bir; **yalnızca bu durumda `referenceVector` zorunludur** (S-21), yoksa `VALIDATION_ERROR`.
- `referenceVector`: `verify` ile aynı kural (Bölüm 4): 512 değer, sonlu olmayan değer ya da sıfır vektör `VALIDATION_ERROR`, birim uzunlukta değilse yeniden normalize edilir. `identity` yokken gönderilirse yine doğrulanır, kullanılmaz.
- `sessionId` ve `ts` yalnızca yanıtta aynen geri döner; servis durumsuz (S-20).

**Yanıt 200**
```json
{
  "sessionId": "8f3c...-uuid",
  "ts": "2026-11-24T10:15:30.250Z",
  "faceCount": 1,
  "yaw": 34.2,
  "pitch": -5.1,
  "gazeAway": true,
  "phone": { "detected": false, "confidence": 0.08 },
  "identity": { "similarity": 0.58, "matched": true },
  "evidenceFrame": "<base64 JPEG>",
  "processingMs": 142
}
```
Görevler ve alanlar (kod: `vision/app/frame_service.py` `analyze`; eşikler ve gerekçeleri `vision/config.yaml`):

| Görev | Alanlar | Kural |
|---|---|---|
| `face` | `faceCount` | MediaPipe Face Landmarker'ın bulduğu yüz sayısı; en fazla 4 yüz aranır (4 "4 ya da daha fazla" demek) |
| `pose` | `yaw`, `pitch`, `gazeAway` | En büyük yüzün kafa yönü, derece. `gazeAway` = `\|yaw\| > frame.gaze_yaw_deg` (30) veya `\|pitch\| > frame.gaze_pitch_deg` (20); sınır değer sapma değil. Yüz yoksa üçü de `null` |
| `phone` | `phone.detected`, `phone.confidence` | YOLO11n, yalnızca COCO 67. sınıf (cell phone). `confidence` (0–1) karedeki en yüksek telefon skoru; `detected` = `confidence ≥ frame.phone_confidence` (0,5). Yüzden bağımsız |
| `identity` | `identity.similarity`, `identity.matched` | En büyük yüzün ArcFace vektörüyle `referenceVector` arasındaki kosinüs benzerliği (−1…1); `matched` = `similarity ≥ verify.match_threshold` (D2 ile aynı eşik, tek yerde; sınır değer eşleşir). Yüz yoksa `identity` `null`. **Canlılık bu uçta yok** |

- İstenmeyen görevlerin alanları `null` döner; o görevlerin modelleri çalışmaz. `processingMs` her zaman doludur (karenin çözülmesi dahil, ağ hariç).
- Sayılar yuvarlanmaz; dönen değer ile karar sınırda çelişmesin.
- **Yaw/pitch işareti** (D4'te LFW geometrisiyle ve kamerayla doğrulandı, K-28): `yaw > 0` aday kendi soluna dönmüş (aynalanmamış karede burun görüntünün sağına kaymış), `yaw < 0` kendi sağına; `pitch > 0` aşağı, `pitch < 0` yukarı bakıyor. Tarayıcının gönderdiği kare aynalı değil. Kural mutlak değerle çalıştığı için işaret yalnızca bilgi.
- `evidenceFrame`: **yalnızca bu karede şüpheli bir durum varsa** dolu, yoksa `null`. Şüpheli durumlar: `faceCount ≠ 1`, `gazeAway = true`, `phone.detected = true` veya `identity.matched = false`; istenmeyen görev (alanı `null`) şüphe sayılmaz. Değer **gelen karenin kendisi**, yeniden sıkıştırılmadan (`frame` ile aynı base64). Arka uç, olay oluşursa kareyi şifreleyip saklar, oluşmazsa atar.
- Günlüğe yalnızca uç, sonuç kodu (`OK` ya da hata kodu) ve süre yazılır; görüntü, vektör ve ölçümler yazılmaz (K-27).
- Hatalar: 401 `UNAUTHORIZED`, 422 `VALIDATION_ERROR` / `INVALID_IMAGE`, 503 `MODELS_NOT_READY` (Bölüm 1).

**Süre** (`processingMs`; 640×480 JPEG 0,8 kare, tek yüz, `VISION_ORT_THREADS=1`, i5-12450H, prizde, iki tur × 100 ölçüm):

| Görevler | Ortalama | p95 |
|---|---:|---:|
| `face` + `pose` (her kare) | 11 ms | 12–14 ms |
| `face` + `pose` + `identity` | 23–24 ms | 26–27 ms |
| `face` + `pose` + `phone` | 102–107 ms | 116–139 ms |
| Dördü birlikte | 112–126 ms | 123–149 ms |

D1 benchmark'ına göre (yüz 9 + ArcFace 11 + YOLO 79 ms) ~100 ms bekleniyordu; fark YOLO'da (~90–95 ms). Pilde dördü birlikte ortalama 125–155 ms ölçüldü. Saniyede bir kare için kart hedefleri (yüz 150, eşleştirme 200, telefon 300 ms) karşılanıyor. Telefon 3 sn'de bir çalıştığı için aday başına ortalama yük ~50 ms/sn (13 + ~100 / 3 + ~13 / 60).

### Olay kuralları (arka uçta uygulanır — S-20)

| Olay | Kural |
|---|---|
| `FACE_MISSING` | Art arda en az **3** ölçümde `faceCount = 0` |
| `MULTIPLE_FACES` | `faceCount ≥ 2` |
| `GAZE_AWAY` | Art arda en az **5** ölçümde `gazeAway = true` |
| `PHONE` | `phone.detected = true` |
| `IDENTITY_MISMATCH` | `identity.matched = false` |

**Kamera denemesinden notlar (D4, K-28):** |yaw| ≈ 50–60°'de yüz yer yer kayboluyor (`faceCount` 1 → 0 → 1). Arka uç tekil bir `faceCount = 0` ölçümünü `GAZE_AWAY` serisini bozan değil **sürdüren** saymalı; aksi hâlde başını çok çeviren aday iki kuralın arasından kayabilir (denemede sağa dönüş `GAZE_AWAY` yerine `FACE_MISSING` üretti). Kulağa yakın tutulan telefon bulunamıyor (Deney 2'de ayrı koşul). Kamera ortalanmamışsa dinlenmede yaw birkaç derece kayık (D3 protokolü).

Arka uç olayı `EventService.RecordSystemEventAsync` ile `SystemEvent(Type, StartedAt, DurationSec, Confidence, Payload)` olarak kaydeder (K-16). Kaynak `EventCatalog`'dan belirlenir. `payload` en fazla 4 KB JSON olabilir; kanıt karesi payload'a konmaz, `EvidenceFrame` olarak ayrıca saklanır (A5). Servis yanıt vermezse veya `503` dönerse arka uç `MONITORING_GAP` üretir (K-16, FR-26).

### Olay bölümleri ve `MONITORING_GAP` (arka uç, K-45)

- **Bölümün bitişinde tek kayıt.** Art arda gelen tespitler aynı bölüme katılır; bölüm koşul bitince süresiyle **bir kez** yazılır (`CONNECTION_LOST` ve `PERMISSION_REVOKED` ile aynı kalıp). Telefon ve kimlik kontrolü yapılmayan kareler o bölümü ne uzatır ne keser. 10 sn başarılı ölçüm gelmezse ya da oturum biterse (teslim, otomatik teslim) açık bölümler o ana kadarki hâliyle yazılır; iptal edilen oturumda yazılmaz.
- **`startedAt` / `durationSec`.** Zamanlar sunucunun karenin alındığı an. `startedAt` = koşulun sağlandığı **ilk** ölçüm (`FACE_MISSING`'de üç kaybın ilki); `durationSec` = ilk ölçümden koşulun sağlandığı **son** ölçüme. Tek ölçümlük olay 0 sn'dir.
- **Bakış serisinde tekil yüz kaybı.** `GAZE_AWAY` serisinin içine giren 1–2 ardışık `faceCount = 0` ölçümü seriyi sıfırlamaz, sürdürür, ama 5'e sayılmaz: `G G 0 G G` olay değildir, `G G 0 G G G` `GAZE_AWAY`'dir. 3. ardışık kayıpta bakış serisi kapanır, `FACE_MISSING` başlar.
- **Yüz var ama kafa yönü yok.** Dönüşüm matrisi gelmediği için `faceCount ≥ 1` iken `yaw`, `pitch` ve `gazeAway` üçü birden `null` dönerse arka uç bunu sözleşme hatası saymaz: yüz sayısı kuralları (`FACE_MISSING`, `MULTIPLE_FACES`) normal işler, bakış serisinde ise ölçüm tekil yüz kaybı gibi sayılmaz ve seriyi sıfırlamaz.
- **`MONITORING_GAP` tek bölüm.** Bağlantı hatası, zaman aşımı (3 sn), 401, 503, 500, `VALIDATION_ERROR`, tanınmayan kod ya da sözleşmeye uymayan yanıtta boşluk açılır; ilk başarılı yanıtta süresiyle **tek kayıt** olarak yazılır, her kare için ayrı olay yazılmaz. **422 `INVALID_IMAGE` boşluk değildir**: adayın karesidir, o kare atlanır ve bölümlere dokunmaz.
- **Kanıt karesi.** Bölümün ilk `evidenceFrame`'i olay yazılırsa şifreli saklanıp olaya bağlanır (bölüm başına en fazla bir kare); olay oluşmazsa kare saklanmaz.

---

## 6. `POST /internal/analyze/audio` — konuşma tespiti (D5)

**İstek**
```json
{
  "sessionId": "8f3c...-uuid",
  "chunk": "<base64 WAV veya WebM>",
  "format": "webm",
  "ts": "2026-11-24T10:15:30.000Z"
}
```

**Yanıt 200**
```json
{
  "sessionId": "8f3c...-uuid",
  "ts": "2026-11-24T10:15:30.000Z",
  "speech": true,
  "prob": 0.87,
  "speechSec": 0.7,
  "durationSec": 1.0,
  "processingMs": 35
}
```
- `speech` = `prob ≥ vad_threshold` (`config.yaml`). Konuşmacı tanıma yapılmaz, ses saklanmaz.
- Arka uç `speech = true` olan parçalardan `SPEECH` olayı üretir; kaynak `EventCatalog`'dan gelir (K-16).

---

## 7. Açık konular (MIMARI-KARARLAR.md'ye eklenecek)

Mevcut son numara S-18 (not: tabloda S-16 iki kez kullanılmış). Aşağıdakiler S-19'dan başlıyor.

| # | Konu | İlgili | Öneri |
|---|---|---|---|
| S-19 | Doğrulamada `passed` kararını kim veriyor? `Verification` varlığındaki yorum "sonuç sunucuda eşiklere göre belirleniyor" diyor. | Ayşe (A3), Dilan (D2) | Eşikler tek yerde dursun diye vision `config.yaml`'a göre `passed` dönsün; arka uç skorları ve sonucu kaydetsin. Arka uç eşikleri kendisi tutmak isterse vision yalnızca skor döner. |
| S-20 | Kamera olay kuralları ("art arda 3 kare yüz yok", "art arda 5 ölçüm bakış sapması") nerede çalışacak? | Ayşe, Dilan (D4) | **Arka uçta.** K-16'ya uygun ("kamera ve ses olayları: API, görüntü servisinin sonucuna göre") ve vision çok işçili, durumsuz çalışacağı için oturum sayacını tutamaz. Vision her kare için ölçüm döner. |
| S-21 | `analyze/frame` isteğinde `referenceVector`: D4 kartındaki istekte yok, ama vision vektör saklamadığı için 60 sn'lik kimlik kontrolünde gerekli. | Ayşe, Dilan (D4) | Yalnızca `tasks` içinde `identity` varken gönderilsin. |
| S-22 | Ses parçası uzunluğu ve biçimi | Yağmur (S4), Dilan (D5) | S4'e göre ~1 sn WebM/Opus; vision 16 kHz mono'ya kendisi dönüştürür. |
