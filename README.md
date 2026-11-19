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

1. https://www.postgresql.org/download/windows/ adresinden PostgreSQL 16 kurulum dosyasını indir ve kur (port: `5432`).
2. Kurulumda belirlediğin şifreyi `.env` içindeki `POSTGRES_PASSWORD` alanına yaz.
3. pgAdmin veya `psql` ile `.env` içindeki `POSTGRES_DB` adında boş bir veritabanı oluştur:
   ```sql
   CREATE DATABASE aiguard;
   ```

### 3. API (.NET)

```bash
cd api
dotnet restore
dotnet ef database update --project src/AIGuard.Infrastructure --startup-project src/AIGuard.Api
dotnet run --project src/AIGuard.Api
```

Bağlantı dizesi ortam değişkeninden (`ConnectionStrings__Default`) veya `appsettings.Development.json` dosyasından okunur. Swagger: http://localhost:5000/swagger

> Proje adları A1 / E2 kartlarıyla netleşince bu bölüm güncellenecek.

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

Bkz. E1b kartı. `docker compose up -d` ile api, vision ve postgres birlikte ayağa kalkar.

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
