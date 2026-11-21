"""Model katmanı (D1). Uçlar modellere yalnızca bu paketten erişir.

Model dosyaları repoda değil; `scripts/download_models.py` ve `scripts/export_models.py` ile
VISION_MODEL_DIR'e (varsayılan vision/models/) konur. Lisanslar ve kaynaklar: MIMARI-KARARLAR K-28.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from app.models.base import ModelFilesMissing, VisionModel
from app.models.face_embedder import FaceEmbedder
from app.models.face_landmarks import FaceLandmarker
from app.models.liveness import LivenessModel
from app.models.phone_detector import PhoneDetector
from app.models.speech_detector import SpeechDetector

MODEL_CLASSES: tuple[type[VisionModel], ...] = (
    FaceLandmarker, FaceEmbedder, LivenessModel, PhoneDetector, SpeechDetector,
)


@dataclass(frozen=True)
class Models:
    face: FaceLandmarker
    embedder: FaceEmbedder
    liveness: LivenessModel
    phone: PhoneDetector
    speech: SpeechDetector


def model_files() -> list[str]:
    return [f for cls in MODEL_CLASSES for f in cls.files]


def load_models(model_dir: Path) -> Models:
    """Beş modeli yükler. Eksik dosya varsa hiçbirini yüklemeden hepsini bir arada bildirir."""
    missing = [Path(model_dir) / f for f in model_files() if not (Path(model_dir) / f).is_file()]
    if missing:
        raise ModelFilesMissing(missing)
    return Models(*(cls(model_dir) for cls in MODEL_CLASSES))


__all__ = ["ModelFilesMissing", "Models", "load_models", "model_files"]
