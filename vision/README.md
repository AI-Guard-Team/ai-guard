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

## Test ve lint (CI ile aynı)

```powershell
pip install ruff pytest
ruff check .
pytest -q
```

Eşikler `config.yaml`'da; `null` olanlar ilgili kartta (D2, D4, D5) ayarlanacak.

**Veri setleri, gönüllü kayıtları ve model ağırlıkları bu klasöre konsa bile commit edilmez** (`.gitignore`).
