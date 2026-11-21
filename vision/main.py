"""AI-Guard vision servisi (D1).

/health ucu E1b'den geliyor ve anahtarsız açık kalmalı: docker-compose.yml'deki
healthcheck onu çağırıyor. /internal/* uçları X-Service-Key ister (app/security.py);
istek ve yanıt biçimleri docs/contracts.md'de. Modeller açılışta, işçi başına bir kez yükleniyor (app/models).
"""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.config import get_settings
from app.errors import register_error_handlers
from app.models import ModelFilesMissing, load_models
from app.routes import router
from app.security import register_service_key_middleware

# Uvicorn yalnızca kendi logger'larına işleyici bağlıyor; açılış mesajları container günlüğünde görünsün diye.
logger = logging.getLogger("uvicorn.error")


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    # Anahtar yoksa hiç açılma: anahtarsız çalışan bir servis herkese açık demektir.
    if not settings.service_key:
        raise RuntimeError("VISION_SERVICE_KEY ortam değişkeni tanımlı değil.")

    # Uvicorn her işçi sürecinde lifespan'i ayrı çalıştırır; modeller süreç başına bir kez yükleniyor.
    # Dosyalar yoksa servis yine açılır (CI'da model yok, /health çalışmalı); uçlar 503 MODELS_NOT_READY döner.
    # Dosya var ama bozuksa hata yutulmaz, servis açılmaz.
    app.state.models = None
    try:
        app.state.models = load_models(settings.model_dir)
        logger.info("Modeller yüklendi: %s", settings.model_dir)
    except ModelFilesMissing as e:
        logger.warning("%s (%s). Uçlar 503 dönecek; bkz. scripts/download_models.py", e, settings.model_dir)
    yield


# Swagger yalnızca VISION_DEBUG=true iken açık; iç servisin şemasını göstermeye gerek yok.
_debug = get_settings().debug
app = FastAPI(
    title="AI-Guard vision",
    lifespan=lifespan,
    docs_url="/docs" if _debug else None,
    redoc_url=None,
    openapi_url="/openapi.json" if _debug else None,
)
register_error_handlers(app)
register_service_key_middleware(app)
app.include_router(router)


@app.get("/health")
def health() -> dict[str, str]:
    # API'nin /health yanıtıyla aynı kelime, kontroller tek biçimde yazılsın diye.
    return {"status": "Healthy"}
