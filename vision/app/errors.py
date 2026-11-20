"""Tüm uçlar için ortak hata biçimi: {"error": {"code", "message"}} (contracts.md, Bölüm 1)."""
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse


def error_response(status: int, code: str, message: str) -> JSONResponse:
    return JSONResponse(status_code=status, content={"error": {"code": code, "message": message}})


class ServiceError(Exception):
    """Uç içinden atılan, sözleşmede tanımlı hata (ör. NO_FACE)."""

    def __init__(self, status: int, code: str, message: str) -> None:
        self.status, self.code, self.message = status, code, message


def register_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(ServiceError)
    async def _service_error(_: Request, exc: ServiceError):
        return error_response(exc.status, exc.code, exc.message)

    # FastAPI'nin varsayılan 422 biçimi yerine ortak biçim; arka uç tek tip hata ayrıştırsın diye.
    @app.exception_handler(RequestValidationError)
    async def _validation_error(_: Request, exc: RequestValidationError):
        first = exc.errors()[0] if exc.errors() else {}
        field = ".".join(str(p) for p in first.get("loc", []) if p != "body")
        return error_response(422, "VALIDATION_ERROR", f"{field}: {first.get('msg', 'geçersiz istek')}")

    @app.exception_handler(Exception)
    async def _unexpected(_: Request, __: Exception):
        # Ayrıntı yanıta konmaz; iç hata bilgisi dışarı sızmasın.
        return error_response(500, "INTERNAL_ERROR", "Beklenmeyen bir hata oluştu.")
