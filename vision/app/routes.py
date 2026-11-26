"""Dört iç uç. Yüz uçlarının kararları app/face_service.py'de, kamera analizi app/frame_service.py'de (D4);
ses analizi D5'te bağlanacak.

Günlüğe yalnızca uç, sonuç kodu ve süre yazılır; görüntü, vektör ve skor yazılmaz (MIMARI-KARARLAR K-27, KVKK).
"""
import logging
import time
from contextlib import contextmanager

import numpy as np
from fastapi import APIRouter, Request

from app import face_service, frame_service
from app.config import get_settings
from app.errors import ServiceError
from app.models import Models
from app.schemas import (
    AnalyzeAudioRequest,
    AnalyzeAudioResponse,
    AnalyzeFrameRequest,
    AnalyzeFrameResponse,
    EnrollRequest,
    EnrollResponse,
    FaceBox,
    IdentityResult,
    PhoneResult,
    VerifyRequest,
    VerifyResponse,
)

router = APIRouter(prefix="/internal")
logger = logging.getLogger("uvicorn.error")


def _not_ready() -> ServiceError:
    # Modeller yüklenene kadar dürüstçe 503 dön; arka uç sahte (mock) verilerle çalışmaya devam edebilir (A2).
    return ServiceError(503, "MODELS_NOT_READY", "Modeller henüz yüklenmedi.")


def _models(request: Request) -> Models:
    models = getattr(request.app.state, "models", None)
    if models is None:
        raise _not_ready()
    return models


@contextmanager
def _audit(path: str):
    """Uç, sonuç kodu ve süre. Sonuç kodu uç içinde `outcome["code"]` ile verilir; hata kodu yakalanır."""
    outcome = {"code": "INTERNAL_ERROR"}
    start = time.perf_counter()
    try:
        yield outcome
    except ServiceError as e:
        outcome["code"] = e.code
        raise
    finally:
        logger.info("%s %s %d ms", path, outcome["code"], round((time.perf_counter() - start) * 1000))


# Model çağrıları bilerek olay döngüsünde ve sırayla çalışıyor: MediaPipe nesnesi iş parçacıkları arasında
# paylaşılmaya uygun değil, eşzamanlılık uvicorn işçi sayısıyla sağlanıyor (docs/benchmark-D1.md).
@router.post("/face/enroll", response_model=EnrollResponse)
async def face_enroll(req: EnrollRequest, request: Request):
    models = _models(request)
    with _audit("/internal/face/enroll") as outcome:
        th = face_service.Thresholds.from_config(get_settings().thresholds)
        result = face_service.enroll(models, req.image, th)
        outcome["code"] = "OK"
    box = result.face.box
    return EnrollResponse(
        vector=result.vector.tolist(),
        faceBox=FaceBox(x=box.x, y=box.y, width=box.width, height=box.height),
        qualityScore=result.quality_score,
    )


@router.post("/face/verify", response_model=VerifyResponse)
async def face_verify(req: VerifyRequest, request: Request):
    models = _models(request)
    with _audit("/internal/face/verify") as outcome:
        th = face_service.Thresholds.from_config(get_settings().thresholds)
        reference = np.asarray(req.referenceVector, dtype=np.float32)
        result = face_service.verify(models, req.frames, reference, th)
        outcome["code"] = "PASSED" if result.passed else result.reason
    return VerifyResponse(
        livenessScore=result.liveness_score,
        similarity=result.similarity,
        passed=result.passed,
        reason=result.reason,
        framesUsed=result.frames_used,
    )


@router.post("/analyze/frame", response_model=AnalyzeFrameResponse)
async def analyze_frame(req: AnalyzeFrameRequest, request: Request):
    models = _models(request)
    with _audit("/internal/analyze/frame") as outcome:
        th = frame_service.FrameThresholds.from_config(get_settings().thresholds)
        reference = None if req.referenceVector is None else np.asarray(req.referenceVector, dtype=np.float32)
        r = frame_service.analyze(models, req.frame, set(req.tasks), reference, th)
        outcome["code"] = "OK"
    return AnalyzeFrameResponse(
        sessionId=req.sessionId,
        ts=req.ts,
        faceCount=r.face_count,
        yaw=r.yaw,
        pitch=r.pitch,
        gazeAway=r.gaze_away,
        phone=None if r.phone is None else PhoneResult(detected=r.phone.detected, confidence=r.phone.confidence),
        identity=None if r.identity is None else IdentityResult(similarity=r.identity.similarity,
                                                                matched=r.identity.matched),
        evidenceFrame=r.evidence_frame,
        processingMs=r.processing_ms,
    )


@router.post("/analyze/audio", response_model=AnalyzeAudioResponse)
async def analyze_audio(req: AnalyzeAudioRequest):
    raise _not_ready()  # D5
