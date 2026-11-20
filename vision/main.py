"""AI-Guard vision servisi (D1).

/health ucu E1b'den geliyor ve anahtarsız açık kalmalı: docker-compose.yml'deki
healthcheck onu çağırıyor. /internal/* uçları X-Service-Key ister (app/security.py);
istek ve yanıt biçimleri docs/contracts.md'de. Modeller D1'in ikinci adımında yüklenecek.
"""

from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.config import get_settings
from app.errors import register_error_handlers
from app.routes import router
from app.security import register_service_key_middleware


@asynccontextmanager
async def lifespan(_: FastAPI):
    # Anahtar yoksa hiç açılma: anahtarsız çalışan bir servis herkese açık demektir.
    if not get_settings().service_key:
        raise RuntimeError("VISION_SERVICE_KEY ortam değişkeni tanımlı değil.")
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
