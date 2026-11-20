# AI-Guard

Çevrim içi Genel Yetenek ve İngilizce Yeterlilik sınavları için **sıfır güven (zero-trust)** yaklaşımlı, görüntü işleme destekli sınav ve kopya tespit platformu.
Doğuş Üniversitesi · YM 491 Araştırma Projesi · 2026–2027 Güz

Adayın bilgisayarına bir güvenlik ajanı (Windows 10/11, macOS) kurulur. Sınav başında aday web kamerasıyla kayıt fotoğrafına göre doğrulanır. Sınav boyunca kamera, mikrofon, tarayıcı, cihaz ve ağ olayları açıklanabilir bir risk skorunda birleştirilir. Canlı gözetmen yoktur; değerlendirici riskli oturumları sınav bitiminde inceler.

## Ekip

| Kişi | Rol |
|---|---|
| Esma İrem Öztürk | Proje yöneticisi, .NET arka uç, DevOps |
| Ayşe Nur Kendirci | .NET arka uç, veritabanı, risk motoru |
| Yağmur Erol | Güvenlik ajanı, güvenlik testleri, Docker ortamı |
| Şeyma Mavideniz | React önyüz, macOS ajanı |
| Dilan Özmen | Görüntü ve ses işleme servisi, deneyler |

Danışman: Dr. Öğr. Üyesi Zeynep Behrin Güven Aydın

## Klasör yapısı

```
ai-guard/
├── web/       React + TypeScript + Vite önyüz
├── api/       ASP.NET Core Web API (Api, Application, Domain, Infrastructure) + EF Core + SignalR
├── vision/    Python FastAPI görüntü/ses servisi (yalnızca iç ağ)
├── agent/     .NET güvenlik ajanı (ortak çekirdek + Windows/macOS modülleri)
├── docs/      Mimari, ER diyagramı, tehdit modeli, raporlar
├── .github/   CI iş akışları ve PR şablonu
└── .env.example
```

## Teknolojiler

- **Önyüz:** React, TypeScript, Vite
- **Ana arka uç:** .NET (LTS), ASP.NET Core Web API, Entity Framework Core, SignalR
- **Görüntü/ses servisi:** Python, FastAPI, OpenCV, MediaPipe, YOLO, Silent-Face-Anti-Spoofing, ArcFace, Silero VAD
- **Veritabanı:** PostgreSQL 16
- **Güvenlik ajanı:** .NET
- **Altyapı:** Docker Compose, GitHub Actions

## Gereksinimler

| Araç | Sürüm | Kimler için |
|---|---|---|
| Git | güncel | herkes |
| .NET SDK | 8.0 (LTS) | api, agent |
| Node.js | 20 LTS | web |
| Python | 3.11 | vision |
| PostgreSQL | 16 | api (Docker yoksa) |

## Yerel kurulum (Docker olmadan)

Ekipte Docker yalnızca Yağmur'un bilgisayarında var. Diğer herkes aşağıdaki adımlarla çalışır.

### 1. Repoyu indir ve ortam dosyasını oluştur

```bash
git clone https://github.com/AI-Guard-Team/ai-guard.git
cd ai-guard
git checkout dev
cp .env.example .env        # Windows PowerShell: Copy-Item .env.example .env
```

`.env` dosyasındaki değerleri kendi bilgisayarına göre düzenle. **`.env` asla commit edilmez.**

### 2. PostgreSQL 16

1. https://www.postgresql.org/download/windows/ → "Download the installer" → **16.x, Windows x86-64** kurulum dosyasını indir.
2. Bileşenlerden **PostgreSQL Server** ve **Command Line Tools** yeterli; pgAdmin ve Stack Builder isteğe bağlı.
3. Port `5432` kalsın. `postgres` şifresinde **Türkçe karakter kullanma** (ş, ğ, ı, ö, ü, ç).
4. **Locale** ekranında `Default locale` yerine **`C`** seç. Türkçe Windows'ta varsayılan ayarın adı (`Turkish_Türkiye.1254`) "ü" içerdiği için veritabanı oluşturulamıyor ve kurulum "database cluster initialisation failed" uyarısıyla yarım kalıyor. Kodlama yine UTF-8 olduğundan Türkçe metinler sorunsuz saklanıyor.

`aiguard` veritabanını elle oluşturmana gerek yok; aşağıdaki `dotnet ef database update` komutu oluşturuyor.

<details>
<summary>Kurulum "database cluster initialisation failed" uyarısıyla bittiyse</summary>

Program dosyaları kurulmuş ama veritabanı oluşmamış demektir. Yeniden kurmak yerine **yönetici olarak açılmış** PowerShell'de:

```powershell
$pg = "C:\Program Files\PostgreSQL\16"; $data = "$pg\data"
& "$pg\bin\initdb.exe" -D $data -U postgres -E UTF8 --locale=C -A scram-sha-256 -W
icacls $data /grant "NT AUTHORITY\NetworkService:(OI)(CI)F" /T | Out-Null
& "$pg\bin\pg_ctl.exe" register -N postgresql-x64-16 -U "NT AUTHORITY\NetworkService" -D $data -S auto
Start-Service postgresql-x64-16
```

`data` klasörü boş değilse önce içini boşalt. Servis zaten kayıtlıysa `pg_ctl register` satırı hata verir; atlayabilirsin.
</details>

### 3. API (.NET)

Bağlantı dizesi ve geliştirme şifresi **repoya yazılmaz**; bir kez `user-secrets` ile ayarlanır (bilgisayarında, repo dışında saklanır):

```powershell
cd api
dotnet tool restore                      # repodaki EF aracı (dotnet-ef 8.0.11)
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=aiguard;Username=postgres;Password=<şifren>" --project src/AIGuard.Api
dotnet user-secrets set "Seed:DefaultPassword" "<en-az-12-karakter>" --project src/AIGuard.Api

dotnet ef database update --project src/AIGuard.Infrastructure --startup-project src/AIGuard.Api
dotnet run --project src/AIGuard.Api     # http://localhost:5080/health -> Healthy
```

Alternatif olarak `ConnectionStrings__Default` ortam değişkeni de okunur (CI ve Docker böyle çalışır).
İlk açılışta Development ortamında örnek veri yüklenir: `admin@aiguard.local`, `reviewer@aiguard.local`, `aday1@aiguard.local`, `aday2@aiguard.local` (şifre: `Seed:DefaultPassword`) ve bir örnek sınav.

Testler:

```powershell
dotnet test                                                    # birim testleri (veritabanı gerekmez)
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<şifren>"
dotnet test                                                    # + entegrasyon testleri (geçici DB açıp siler)
```

Veritabanı modeli: [docs/er-diagram.md](docs/er-diagram.md) · Mimari kararlar ve nedenleri: [docs/MIMARI-KARARLAR.md](docs/MIMARI-KARARLAR.md)

### 4. Vision (Python)

```bash
cd vision
python -m venv .venv
.venv\Scripts\activate          # macOS/Linux: source .venv/bin/activate
pip install -r requirements.txt
uvicorn main:app --port 8000
```

### 5. Web (React)

```bash
cd web
npm ci
npm run dev
```

## Docker ile çalıştırma (Yağmur'un bilgisayarı)

Tüm sistemi birlikte çalıştırmak ve entegrasyon günleri (E5a, E5b) için. Günlük geliştirme için Docker gerekmez; yukarıdaki yerel kurulum yeterli.

| Servis | Adres | Not |
|---|---|---|
| `web` | http://localhost:5173 | Vite build çıktısını nginx sunar; `/api/...` istekleri `api`'ye gider. `web/package.json` gelene kadar geçici bir sayfa gösterir |
| `api` | http://localhost:8080 (`/health` → `Healthy`) | Development ortamında örnek veriyle açılır |
| `vision` | yalnızca iç ağda, `http://vision:8000` | Bilgisayardan erişilemez, ona yalnızca `api` erişir |
| `postgres` | `localhost:5432` | PostgreSQL 16, veri `aiguard_pgdata` volume'unda kalıcı |
| `migrate` | — | Göçleri uygulayıp kapanır; `api` bunu bekler |

- **Yapılandırma dosyaları:** `api/src/AIGuard.Api/riskconfig.json` container'a salt okunur bağlanıyor. Ağırlıkları değiştirdikten sonra `docker compose restart api` yeterli, yeniden derleme gerekmiyor. `forbidden-apps.json` oluşturulunca (Y3) aynı şekilde eklenecek.
- **Önyüz API'yi `/api` üzerinden çağırmalı** (ör. `fetch('/api/...')`). Böylece tarayıcı için aynı adres oluyor ve API'de CORS gerekmiyor.
- **İstemci IP'si:** API, `X-Forwarded-For` başlığını yalnızca `web` container'ından (172.30.47.10) kabul ediyor (K-16). Bilgisayarda 172.30.47.0/24 ağı başka bir şeyle çakışırsa `docker-compose.yml` içinde ikisi birlikte değiştirilmeli.

### Tek komutla (örnek değerlerle)

```bash
docker compose --env-file .env.example up -d --build
```

### Kendi değerlerinle

```bash
cp .env.example .env        # Windows PowerShell: Copy-Item .env.example .env
# .env içindeki change_me değerlerini değiştir
docker compose up -d --build
```

- Şifrelerde `;` kullanma; bağlantı dizesini bozar.
- Bilgisayarında ayrıca PostgreSQL kuruluysa 5432 portu çakışır. `.env` içinde `POSTGRES_PORT=5433` yap (yalnızca dışarıya açılan port değişir).
- Örnek hesapların şifresi `Seed__DefaultPassword` değeridir (en az 12 karakter).

### Sık kullanılan komutlar

```bash
docker compose ps                    # durum (web, api, vision, postgres "healthy"/"running"; migrate "exited (0)")
docker compose logs -f api           # api logları
docker compose up -d --build api     # yalnızca api'yi yeniden derle (ör. dev'i çektikten sonra)
docker compose up -d --build web     # web projesi değişince önyüzü yeniden derle
docker compose run --rm migrate      # yeni göç geldiyse tekrar uygula
docker compose down                  # durdur (veri kalır)
docker compose down -v               # durdur ve veritabanını sil (temiz başlangıç)
```

Compose kural testleri (Docker gerekir): `python -m pytest tests/compose -q`

**Podman Desktop:** aynı dosya `podman compose up -d --build` ile çalışır (`--env-file` seçeneği de aynı).

## Bir karta başlamadan önce

Arka uçta çalışacaksanız [docs/MIMARI-KARARLAR.md](docs/MIMARI-KARARLAR.md) dosyasına göz atın. Katmanlar, ortak adlar, gizli değerlerin saklanması ve göç ekleme adımları orada anlatılıyor.

## Çalışma kuralları

Dal stratejisi, commit mesajı biçimi ve PR süreci için **[CONTRIBUTING.md](CONTRIBUTING.md)** dosyasını oku. Kısaca:

- `main` korumalıdır; doğrudan push yapılmaz.
- Tüm geliştirme `dev` üzerinden yürür; her kart için `feature/<kart-kodu>-kisa-ad` dalı açılır.
- Her PR en az bir ekip üyesince onaylanır ve CI yeşil olmadan birleştirilmez.

## Veri ve gizlilik

Bu repoya **hiçbir zaman** şunlar eklenmez:

- Gerçek gizli anahtarlar (`.env`, sertifikalar, parolalar)
- Lisanslı veri setleri (Replay-Attack, CASIA-FASD, CelebA-Spoof vb.)
- Gönüllü kayıtları, yüz fotoğrafları, ses kayıtları (KVKK kapsamında kişisel veri)
- Büyük model ağırlık dosyaları

Bunlar `.gitignore` ile engellenmiştir. Veriler yalnızca ekibin paylaşılan sürücüsünde tutulur.
