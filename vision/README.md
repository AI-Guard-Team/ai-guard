# vision — Python FastAPI görüntü/ses servisi

Sorumlu: Dilan Özmen (D1–D8)

Yalnızca iç ağda çalışır; api'den gelen istekler `X-Service-Key` başlığıyla doğrulanır.

Uçlar: `/internal/face/enroll`, `/internal/face/verify`, `/internal/analyze/frame`, `/internal/analyze/audio`

Gereken dosyalar (D1 kartı): `main.py`, `requirements.txt`, `tests/`.
Çalıştırma: `uvicorn main:app --port 8000`

**Veri setleri, gönüllü kayıtları ve model ağırlıkları bu klasöre konsa bile commit edilmez** (`.gitignore`).
