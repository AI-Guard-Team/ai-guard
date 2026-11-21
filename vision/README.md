# vision — Python FastAPI görüntü/ses servisi

Sorumlu: Dilan Özmen (D1–D8)

Yalnızca iç ağda çalışır; api'den gelen istekler `X-Service-Key` başlığıyla doğrulanır.
İstek ve yanıt biçimleri: [`docs/contracts.md`](../docs/contracts.md).

Uçlar: `/internal/face/enroll`, `/internal/face/verify`, `/internal/analyze/frame`, `/internal/analyze/audio`
`/health` anahtarsız açıktır; docker-compose.yml'deki healthcheck onu çağırıyor (E1b).

## Yerel çalıştırma (Docker olmadan)

Python 3.11 gerekli (CI ve Docker imajı 3.11; daha yeni sürümlerde pydantic-core kurulamıyor). Bilgisayarında yoksa: `uv python install 3.11`

```powershell
cd vision
py -3.11 -m venv .venv            # python.org'dan 3.11 kurduysan
# veya uv ile: uv venv --python 3.11 --seed .venv   (--seed: ortama pip de kurulsun)
.venv\Scripts\activate            # macOS/Linux: source .venv/bin/activate
pip install -r requirements.txt
$env:VISION_SERVICE_KEY="dev-key" # macOS/Linux: export VISION_SERVICE_KEY=dev-key
uvicorn main:app --port 8000 --reload
```

- `VISION_SERVICE_KEY` tanımlı değilse servis açılmaz.
- Swagger yalnızca `VISION_DEBUG=true` iken açılır: http://localhost:8000/docs
- Model dosyaları yoksa servis yine açılır, `/internal/*` uçları `503 MODELS_NOT_READY` döner.
- `VISION_ORT_THREADS`: onnxruntime modellerinin iş parçacığı sayısı (tanımsız ya da 0: onnxruntime varsayılanı). MediaPipe'ı etkilemez.

## Benchmark (D1)

```powershell
python scripts/benchmark.py --threads 1,2,0   # kamera açılır: kameraya bakın; kareler diske yazılmaz
```

Sonuçlar ve önerilen işçi/iş parçacığı sayısı: [`docs/benchmark-D1.md`](../docs/benchmark-D1.md).

## Model dosyaları

Repoda değiller; `VISION_MODEL_DIR` klasörüne (varsayılan `vision/models/`, `.gitignore`'da) konur. Docker imajında
bu adımlar derleme sırasında kendiliğinden çalışır (`Dockerfile`, `models` aşaması); aşağıdakiler Docker'sız çalışma için.

Docker imajı (Debian slim) dört sistem paketi kurar: `libgl1` ve `libglib2.0-0` (opencv), `libegl1` ve `libgles2`
(mediapipe). Derlemenin sonunda modeller bir kez yüklenir; bir kütüphane eksikse derleme orada hatayla durur.

**1. ONNX üretme ortamı (bir kez).** Üç model orijinal PyTorch ağırlıklarından ONNX'e çevrilir. Bunun için torch ve
ultralytics gerekir; servis ortamına (`.venv`) kurulmazlar, ayrı bir sanal ortam kullanılır (`.venv-export`, `.gitignore`'da):

```powershell
cd vision
uv venv --python 3.11 --seed .venv-export
.venv-export\Scripts\python -m pip install -r requirements-export.txt   # ~1,5 GB, torch dahil
```

**2. Üret ve indir.**

```powershell
.venv-export\Scripts\python scripts/export_models.py   # MiniFASNetV2, MiniFASNetV1SE, yolo11n (.onnx)
.venv\Scripts\python scripts/download_models.py        # face_landmarker.task, w600k_mbf.onnx, silero_vad.onnx
```

- `export_models.py` orijinal ağırlıkları sabit adreslerden indirir, sha256'larını kontrol eder, ONNX'e çevirir ve
  PyTorch ile ONNX çıktısını karşılaştırır; fark varsa dosyayı yazmaz.
- `download_models.py` üç dosyayı indirip sha256'larını doğrular, sonra altı dosyanın hepsini kontrol eder.
  Hepsi hazırsa çıkış kodu 0; eksik olanı ve nasıl üretileceğini yazar.

| Dosya | Model | Lisans |
|---|---|---|
| `face_landmarker.task` | MediaPipe Face Landmarker | Apache-2.0 |
| `w600k_mbf.onnx` | ArcFace (InsightFace, MobileFaceNet) | Yalnızca ticari olmayan araştırma |
| `MiniFASNetV2.onnx`, `MiniFASNetV1SE.onnx` | Silent-Face-Anti-Spoofing | Apache-2.0 |
| `yolo11n.onnx` | Ultralytics YOLO11n | AGPL-3.0 |
| `silero_vad.onnx` | Silero VAD | MIT |

Kaynaklar, sürümler ve lisans ayrıntıları: [MIMARI-KARARLAR K-28](../docs/MIMARI-KARARLAR.md#k-28--görüntü-servisinin-model-ve-paket-seçimi-d1).

## Test ve lint (CI ile aynı)

```powershell
pip install ruff pytest
ruff check .
pytest -q            # model dosyaları yoksa "models" işaretli testler atlanır
pytest -q -m models  # yalnızca gerçek modelli testler
```

Yüzlü test isteğe bağlı: `AIGUARD_TEST_FACE` = tek yüzlü bir fotoğrafın yolu (repoya eklenmez).

Eşikler `config.yaml`'da; `null` olanlar ilgili kartta (D2, D4, D5) ayarlanacak.

**Veri setleri, gönüllü kayıtları ve model ağırlıkları bu klasöre konsa bile commit edilmez** (`.gitignore`).
