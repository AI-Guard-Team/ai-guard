"""contracts.md'deki istek/yanıt şemaları. Sözleşme değişirse önce burası değişir."""
from typing import Literal

from pydantic import BaseModel, Field, model_validator

VECTOR_DIM = 512  # ArcFace çıktı boyutu
Vector = list[float]


# ---------- /internal/face/enroll ----------
class EnrollRequest(BaseModel):
    image: str = Field(min_length=1, description="Base64 JPEG, PNG veya WebP")


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
class VerifyRequest(BaseModel):
    frames: list[str] = Field(min_length=3, max_length=5)
    referenceVector: Vector = Field(min_length=VECTOR_DIM, max_length=VECTOR_DIM)


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
    frame: str = Field(min_length=1)
    ts: str
    tasks: list[Task] = Field(min_length=1)
    referenceVector: Vector | None = Field(default=None, min_length=VECTOR_DIM, max_length=VECTOR_DIM)

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
