"""Dört iç uç. Şimdilik yalnızca şema doğrulaması yapar; model bağlantıları D1–D5'te eklenecek."""
from fastapi import APIRouter

from app.errors import ServiceError
from app.schemas import (
    AnalyzeAudioRequest,
    AnalyzeAudioResponse,
    AnalyzeFrameRequest,
    AnalyzeFrameResponse,
    EnrollRequest,
    EnrollResponse,
    VerifyRequest,
    VerifyResponse,
)

router = APIRouter(prefix="/internal")


def _not_ready() -> ServiceError:
    # Modeller yüklenene kadar dürüstçe 503 dön; arka uç sahte (mock) verilerle çalışmaya devam edebilir (A2).
    return ServiceError(503, "MODELS_NOT_READY", "Modeller henüz yüklenmedi.")


@router.post("/face/enroll", response_model=EnrollResponse)
async def face_enroll(req: EnrollRequest):
    raise _not_ready()  # D2


@router.post("/face/verify", response_model=VerifyResponse)
async def face_verify(req: VerifyRequest):
    raise _not_ready()  # D2


@router.post("/analyze/frame", response_model=AnalyzeFrameResponse)
async def analyze_frame(req: AnalyzeFrameRequest):
    raise _not_ready()  # D4


@router.post("/analyze/audio", response_model=AnalyzeAudioResponse)
async def analyze_audio(req: AnalyzeAudioRequest):
    raise _not_ready()  # D5
