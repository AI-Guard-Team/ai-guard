# AI-Guard görüntü/ses servisi imajı (E1b). Yalnızca iç ağda çalışır; port dışarı açılmaz.
FROM python:3.11-slim

# .pyc yazma ve çıktı tamponlama kapalı: container loglarında çıktı gecikmesin.
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

# Önce bağımlılıklar: kod değişse de pip katmanı önbellekten gelir.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Root olmayan kullanıcı.
RUN useradd --create-home --uid 10001 vision
USER vision

EXPOSE 8000
# 0.0.0.0: container içindeki diğer servisler (api) erişebilsin; dışarı açılıp
# açılmayacağına docker-compose.yml karar veriyor (açmıyor).
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
