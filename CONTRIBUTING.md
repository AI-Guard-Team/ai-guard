# Katkı ve çalışma kuralları

## Dallar

| Dal | Amaç | Kural |
|---|---|---|
| `main` | Teslim edilen, kararlı sürüm | Korumalı. Doğrudan push yok. Yalnızca `dev` → `main` PR'ı ile güncellenir. |
| `dev` | Ortak geliştirme dalı | Korumalı. Doğrudan push yok. Özellik dallarından PR ile güncellenir. |
| `feature/<kart-kodu>-kisa-ad` | Bir Trello kartının işi | Her kart için ayrı dal. Örn. `feature/E2-auth-api`, `feature/D2-face-enroll` |
| `fix/<kart-kodu>-kisa-ad` | Hata düzeltmesi | Örn. `fix/A2-risk-decay` |

### Günlük akış

```bash
git checkout dev
git pull                                  # en güncel dev'i al
git checkout -b feature/E2-auth-api       # kartın dalını aç
# ... kod yaz ...
git add .
git commit -m "feat(E2): giriş ve yenileme belirteci uçları"
git push -u origin feature/E2-auth-api
```

Ardından GitHub'da **`dev` dalına** PR aç (main'e değil).

## Commit mesajları

Biçim:

```
<tür>(<kart-kodu>): <kısa açıklama>
```

| Tür | Ne zaman |
|---|---|
| `feat` | Yeni özellik |
| `fix` | Hata düzeltme |
| `test` | Test ekleme/düzeltme |
| `docs` | Doküman, README, rapor |
| `refactor` | Davranışı değiştirmeyen kod düzenlemesi |
| `chore` | Yapılandırma, bağımlılık, CI |

Örnekler:

```
feat(E2): davet bağlantısıyla şifre belirleme ucu
fix(A2): risk skorunda azalma katsayısı hatası
test(E4): durum makinesi geçiş testleri
docs(A1): ER diyagramı güncellendi
chore(E1a): GitHub Actions iş akışı eklendi
```

- Açıklama Türkçe, küçük harfle başlar, sonunda nokta yoktur.
- Bir commit tek bir mantıklı değişiklik içerir.

## Pull request kuralları

- PR her zaman `dev` dalına açılır (`dev` → `main` yalnızca entegrasyon noktalarında, Esma tarafından).
- PR başlığı commit biçimindedir: `feat(E2): kimlik doğrulama API'si`
- PR şablonundaki kontrol listesi doldurulur.
- **En az bir ekip üyesinin onayı** ve **yeşil CI** olmadan birleştirilmez.
- Birleştirme yöntemi: **Squash and merge**. Birleştikten sonra özellik dalı silinir.
- İnceleyen kişi en geç 24 saat içinde yorum yapar veya onaylar.

## Ortak adlar

Uç (endpoint), olay türü ve oturum durumu adları Trello kartlarında tanımlıdır ve herkes için ortaktır. AI aracı farklı ad önerirse karttaki ad kullanılır; değişiklik gerekirse önce ekibe sorulur.

## Gizli bilgi ve veri

- `.env` dosyası, anahtarlar ve parolalar **asla** commit edilmez. Yalnızca `.env.example` örnek değerlerle güncellenir.
- Veri setleri, gönüllü kayıtları ve yüz fotoğrafları repoya eklenmez (kökteki ve vision altındaki `data/`, `datasets/`, `recordings/` klasörleri yok sayılır).
- Yanlışlıkla gizli bilgi push edildiyse hemen Esma'ya haber ver; anahtar değiştirilir.
