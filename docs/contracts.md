# Görüntü/Ses Servisi (vision) – JSON Sözleşmesi

> **Durum:** TASLAK v0.2 · 20 Kasım 2026 · Hazırlayan: Dilan Özmen
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
| Görüntü | **Base64 kodlu** görüntü (`data:` öneki olmadan). Kamera kareleri JPEG; kayıt fotoğrafı JPEG, PNG veya WebP olabilir (S2, en fazla 5 MB). |
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
| 422 | `INVALID_IMAGE` / `INVALID_AUDIO` | Base64 veya dosya çözülemedi |
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

**Hatalar (422):** `NO_FACE`, `MULTIPLE_FACES` (yüz sayısı tam 1 olmalı), `FACE_TOO_SMALL`, `IMAGE_TOO_BLURRY`. En küçük yüz boyutu ve netlik eşiği `config.yaml`'dadır.

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
- `referenceVector`: arka ucun şifresini çözdüğü kayıtlı vektör.

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
- `livenessScore`: kullanılabilir karelerin canlılık skorlarının ortalaması (0–1).
- `similarity`: kosinüs benzerliği (−1…1).
- `passed` = `livenessScore ≥ liveness_threshold` **ve** `similarity ≥ match_threshold` (eşikler `config.yaml`'da). Arka uç skorları `Verification.LivenessScore` / `Similarity`'ye yazar (bkz. Bölüm 7, S-19).
- `reason` (başarısızsa): `LIVENESS_FAILED` | `IDENTITY_MISMATCH` | `NO_FACE` | `MULTIPLE_FACES`.
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
- `tasks`: arka uç zamanlamayı bu alanla belirler (D4):
  - `face` + `pose`: her kare (saniyede 1)
  - `phone`: 3 saniyede bir
  - `identity`: 60 saniyede bir; **yalnızca bu durumda `referenceVector` zorunludur**.

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
- İstenmeyen görevlerin alanları `null` döner. `faceCount = 0` ise `yaw`/`pitch`/`gazeAway` `null`.
- `yaw` (sağ-sol) ve `pitch` (yukarı-aşağı) derece cinsinden, birden fazla yüz varsa en büyük yüz için.
- `gazeAway`: tek ölçüm için `|yaw| > 30` veya `|pitch| > 20` (eşikler `config.yaml`'da).
- `evidenceFrame`: **yalnızca bu karede şüpheli bir durum varsa** dolu, yoksa `null`. Şüpheli durumlar: `faceCount ≠ 1`, `gazeAway`, `phone.detected` veya `identity.matched = false`. Arka uç, olay oluşursa kareyi şifreleyip saklar, oluşmazsa atar.

### Olay kuralları (arka uçta uygulanır — S-20)

| Olay | Kural |
|---|---|
| `FACE_MISSING` | Art arda en az **3** ölçümde `faceCount = 0` |
| `MULTIPLE_FACES` | `faceCount ≥ 2` |
| `GAZE_AWAY` | Art arda en az **5** ölçümde `gazeAway = true` |
| `PHONE` | `phone.detected = true` |
| `IDENTITY_MISMATCH` | `identity.matched = false` |

Arka uç olayı `EventService.RecordSystemEventAsync` ile `SystemEvent(Type, StartedAt, DurationSec, Confidence, Payload)` olarak kaydeder (K-16). Kaynak `EventCatalog`'dan belirlenir. `payload` en fazla 4 KB JSON olabilir; kanıt karesi payload'a konmaz, `EvidenceFrame` olarak ayrıca saklanır (A5). Servis yanıt vermezse veya `503` dönerse arka uç `MONITORING_GAP` üretir (K-16, FR-26).

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
