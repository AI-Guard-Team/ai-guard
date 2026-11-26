"""contracts.md'deki istek/yanıt şemaları. Sözleşme değişirse önce burası değişir."""
import math
from typing import Annotated, Literal

from pydantic import BaseModel, Field, field_validator, model_validator

from app.images import MAX_FRAME_B64, MAX_PHOTO_B64

VECTOR_DIM = 512  # ArcFace çıktı boyutu
Vector = list[float]


def unit_vector(v: Vector) -> Vector:
    """referenceVector kuralı (verify ve analyze/frame'de aynı, K-29 8. kural).

    Sıfır ya da sonlu olmayan vektörle benzerlik tanımsız; bu bizim isteğimizin hatası (VALIDATION_ERROR).
    Birim uzunlukta değilse yeniden normalize edilir: kosinüs benzerliği yalnızca yöne bakar.
    """
    if not all(math.isfinite(x) for x in v):
        raise ValueError("referenceVector sonlu olmayan değer içeriyor")
    norm = math.sqrt(math.fsum(x * x for x in v))
    if norm < 1e-6:
        raise ValueError("referenceVector sıfır vektör")
    return [x / norm for x in v]


# ---------- /internal/face/enroll ----------
class EnrollRequest(BaseModel):
    # Üst sınır api'deki 5 MB'ın base64 karşılığı; aşan istek VALIDATION_ERROR (contracts.md 1. bölüm).
    image: str = Field(min_length=1, max_length=MAX_PHOTO_B64, description="Base64 JPEG, PNG veya WebP")


class FaceBox(BaseModel):
    x: int
    y: int
    width: int
    height: int


class EnrollResponse(BaseModel):
    vector: Vector = Field(min_length=VECTOR_DIM, max_length=VECTOR_DIM)
    faceBox: FaceBox
    qualityScore: float


# ---------- /internal/face/verify ----------
Frame = Annotated[str, Field(min_length=1, max_length=MAX_FRAME_B64)]  # api'deki 1 MB'ın base64 karşılığı


class VerifyRequest(BaseModel):
    frames: list[Frame] = Field(min_length=3, max_length=5)
    referenceVector: Vector = Field(min_length=VECTOR_DIM, max_length=VECTOR_DIM)

    @field_validator("referenceVector")
    @classmethod
    def unit_reference(cls, v: Vector) -> Vector:
        return unit_vector(v)


class VerifyResponse(BaseModel):
    livenessScore: float
    similarity: float
    passed: bool
    reason: Literal["LIVENESS_FAILED", "IDENTITY_MISMATCH", "NO_FACE", "MULTIPLE_FACES"] | None = None
    framesUsed: int


# ---------- /internal/analyze/frame ----------
Task = Literal["face", "pose", "phone", "identity"]


class AnalyzeFrameRequest(BaseModel):
    sessionId: str = Field(min_length=1)
    frame: Frame  # verify'daki karelerle aynı sınır (api'deki 1 MiB)
    ts: str
    tasks: list[Task] = Field(min_length=1)
    # Yalnızca identity görevinde zorunlu (S-21); gelirse verify'daki kuralla doğrulanıp normalize edilir.
    referenceVector: Vector | None = Field(default=None, min_length=VECTOR_DIM, max_length=VECTOR_DIM)

    @field_validator("referenceVector")
    @classmethod
    def unit_reference(cls, v: Vector | None) -> Vector | None:
        return None if v is None else unit_vector(v)

    @model_validator(mode="after")
    def identity_needs_vector(self):
        # Servis vektör saklamadığı için kimlik kontrolünde vektör istekle gelmek zorunda (contracts.md S-21).
        if "identity" in self.tasks and self.referenceVector is None:
            raise ValueError("'identity' görevi için referenceVector zorunludur")
        return self


class PhoneResult(BaseModel):
    detected: bool
    confidence: float


class IdentityResult(BaseModel):
    similarity: float
    matched: bool


class AnalyzeFrameResponse(BaseModel):
    sessionId: str
    ts: str
    faceCount: int | None = None
    yaw: float | None = None
    pitch: float | None = None
    gazeAway: bool | None = None
    phone: PhoneResult | None = None
    identity: IdentityResult | None = None
    evidenceFrame: str | None = None
    processingMs: int


# ---------- /internal/analyze/audio ----------
class AnalyzeAudioRequest(BaseModel):
    sessionId: str = Field(min_length=1)
    chunk: str = Field(min_length=1)
    format: Literal["wav", "webm"]
    ts: str


class AnalyzeAudioResponse(BaseModel):
    sessionId: str
    ts: str
    speech: bool
    prob: float
    speechSec: float
    durationSec: float
    processingMs: int
