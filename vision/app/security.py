"""X-Service-Key ara katmanı: yalnızca ana arka uç (api) servisi çağırabilsin (sıfır güven)."""
import hmac

from fastapi import FastAPI, Request

from app.config import get_settings
from app.errors import error_response

PUBLIC_PATHS = {"/health"}
DOCS_PATHS = {"/docs", "/openapi.json", "/redoc"}


def register_service_key_middleware(app: FastAPI) -> None:
    @app.middleware("http")
    async def check_service_key(request: Request, call_next):
        settings = get_settings()
        path = request.url.path
        if path in PUBLIC_PATHS or (settings.debug and path in DOCS_PATHS):
            return await call_next(request)

        provided = request.headers.get("X-Service-Key", "")
        # compare_digest: karşılaştırma süresinden anahtar tahmin edilemesin (zamanlama saldırısı).
        if not settings.service_key or not hmac.compare_digest(provided, settings.service_key):
            return error_response(401, "UNAUTHORIZED", "Geçersiz veya eksik servis anahtarı.")
        return await call_next(request)
