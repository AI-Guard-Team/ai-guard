# AI-Guard görüntü/ses servisi imajı (E1b, D1). Yalnızca iç ağda çalışır; port dışarı açılmaz.

# ---------- 1. aşama: model dosyaları (K-28) ----------
# Üç ONNX (MiniFASNetV2, MiniFASNetV1SE, YOLO11n) orijinal ağırlıklardan burada üretilir, diğer üç dosya indirilip sha256
# ile doğrulanır. torch ve ultralytics yalnızca bu aşamada var; son imaja girmez. Container çalışırken internete
# çıkamadığı için (vision-internal) modeller imaj derlenirken hazırlanıyor.
FROM python:3.11-slim AS models

# ultralytics opencv-python getiriyor; Debian slim'de cv2 için libGL ve glib gerekli.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /build
COPY requirements-export.txt .
RUN pip install --no-cache-dir -r requirements-export.txt
COPY scripts/ scripts/
# download_models.py beş dosyanın hepsini kontrol eder; biri eksik ya da bozuksa derleme burada durur.
RUN python scripts/export_models.py --out /models \
    && python scripts/download_models.py --dir /models

# ---------- 2. aşama: servis ----------
FROM python:3.11-slim

# .pyc yazma ve çıktı tamponlama kapalı: container loglarında çıktı gecikmesin.
# VISION_WORKERS: uvicorn işçi sayısı; her işçi modelleri ayrı yükler. Değer D1 benchmark'ında belirlenecek.
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    VISION_MODEL_DIR=/app/models \
    VISION_WORKERS=1

# opencv-contrib-python (mediapipe bu adı istiyor) cv2 için libGL ve glib istiyor.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Önce bağımlılıklar: kod değişse de pip katmanı önbellekten gelir.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .
COPY --from=models /models /app/models

# Root olmayan kullanıcı.
RUN useradd --create-home --uid 10001 vision
USER vision

EXPOSE 8000
# 0.0.0.0: container içindeki diğer servisler (api) erişebilsin; dışarı açılıp
# açılmayacağına docker-compose.yml karar veriyor (açmıyor).
# sh -c: işçi sayısı ortam değişkeninden; exec: uvicorn PID 1 olsun, durdurma sinyalini doğrudan alsın.
CMD ["sh", "-c", "exec uvicorn main:app --host 0.0.0.0 --port 8000 --workers \"${VISION_WORKERS}\""]
