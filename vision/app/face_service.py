"""Kayıt fotoğrafı (enroll) ve sınav başı doğrulama (verify) kararları (D2, docs/contracts.md 3. ve 4. bölüm).

Uçlar (app/routes.py) yalnızca bu modülü çağırır. Eşikler config.yaml'dan gelir; geçti/kaldı kararı burada
verilir, api eşik tutmaz (MIMARI-KARARLAR K-27, S-19). Görüntü, vektör ve skorlar günlüğe yazılmaz (KVKK).
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from app.errors import ServiceError
from app.images import decode_base64
from app.models import Models
from app.models.face_landmarks import Face, sharpness

# Kalite skorunda bir ölçüt eşiğinin bu katına ulaşınca tam puan alır (bkz. quality_score).
QUALITY_FULL_AT = 2.0


@dataclass(frozen=True)
class Thresholds:
    min_face_px: int
    min_sharpness: float
    match_threshold: float
    liveness_threshold: float

    @classmethod
    def from_config(cls, config: dict) -> Thresholds:
        enroll, verify = config.get("enroll") or {}, config.get("verify") or {}
        values = {
            "min_face_px": enroll.get("min_face_px"),
            "min_sharpness": enroll.get("min_sharpness"),
            "match_threshold": verify.get("match_threshold"),
            "liveness_threshold": verify.get("liveness_threshold"),
        }
        empty = [k for k, v in values.items() if v is None]
        if empty:
            raise ValueError(f"config.yaml'da eşik tanımlı değil: {', '.join(empty)}")
        return cls(int(values["min_face_px"]), float(values["min_sharpness"]),
                   float(values["match_threshold"]), float(values["liveness_threshold"]))


@dataclass(frozen=True)
class EnrollResult:
    vector: np.ndarray
    face: Face
    quality_score: float


@dataclass(frozen=True)
class VerifyResult:
    liveness_score: float
    similarity: float
    passed: bool
    reason: str | None
    frames_used: int


def rejected(code: str, message: str) -> ServiceError:
    return ServiceError(422, code, message)


def quality_score(face_px: int, face_sharpness: float, th: Thresholds) -> float:
    """Kayıt fotoğrafının kalitesi, 0,5–1.

    İki kabul ölçütünün (yüzün kısa kenarı ve yüz bölgesinin Laplacian varyansı) her biri kendi eşiğine
    bölünür; eşiğin QUALITY_FULL_AT (2) katında 1'de kesilir. Skor ikisinin küçüğünün yarısıdır:
    tam eşikteki fotoğraf 0,5, iki ölçütte de eşiğin en az iki katı olan fotoğraf 1 alır.
    Kabul edilen her fotoğraf en az 0,5 alır; altı zaten reddedilmiştir.
    """
    size_ratio = min(face_px / th.min_face_px, QUALITY_FULL_AT)
    sharp_ratio = min(face_sharpness / th.min_sharpness, QUALITY_FULL_AT)
    return round(min(size_ratio, sharp_ratio) / QUALITY_FULL_AT, 4)


def enroll(models: Models, image_b64: str, th: Thresholds) -> EnrollResult:
    """Tek yüz, yeterince büyük ve net olmalı; değilse 422 ve neden kodu. Kontrol sırası sabit."""
    image = decode_base64(image_b64)
    faces = models.face.predict(image)
    if faces.count == 0:
        raise rejected("NO_FACE", "Görüntüde yüz bulunamadı.")
    if faces.count > 1:
        raise rejected("MULTIPLE_FACES", "Görüntüde birden fazla yüz var.")

    face = faces.largest
    if face.box.min_side < th.min_face_px:
        raise rejected("FACE_TOO_SMALL", "Yüz çok küçük; kameraya yaklaşın.")
    face_sharpness = sharpness(image, face.box)
    if face_sharpness < th.min_sharpness:
        raise rejected("IMAGE_TOO_BLURRY", "Fotoğraf bulanık.")

    vector = models.embedder.predict(image, face.keypoints)
    return EnrollResult(vector, face, quality_score(face.box.min_side, face_sharpness, th))


def verify(models: Models, frames_b64: list[str], reference: np.ndarray, th: Thresholds) -> VerifyResult:
    """3–5 kareden canlılık ve kimlik kararı. `reference` birim uzunlukta olmalı (şema normalize eder).

    Kurallar (docs/contracts.md 4. bölüm), sırayla:
    1. Kare çözülemezse tüm istek 422 INVALID_IMAGE.
    2. Herhangi bir karede 2+ yüz → MULTIPLE_FACES (diğer kareler temiz olsa bile).
    3. Tam bir yüz bulunan kareler "kullanılabilir"; sayısı kare sayısının yarısından azsa → NO_FACE.
       2. ve 3. durumda skorlar 0, framesUsed kullanılabilir kare sayısı.
    4. Aksi hâlde skorlar kullanılabilir karelerin ortalaması. Canlılık kalsa bile benzerlik hesaplanır.
       İkisi de kalırsa LIVENESS_FAILED (sahte yüz önce gelir), yalnızca benzerlik kalırsa IDENTITY_MISMATCH.
    """
    images = [decode_base64(f) for f in frames_b64]  # biri bozuksa hiçbir model çalışmadan reddedilir
    detections = [(image, models.face.predict(image)) for image in images]
    usable = [(image, result.largest) for image, result in detections if result.count == 1]

    if any(result.count > 1 for _, result in detections):
        return VerifyResult(0.0, 0.0, False, "MULTIPLE_FACES", len(usable))
    if 2 * len(usable) < len(images):
        return VerifyResult(0.0, 0.0, False, "NO_FACE", len(usable))

    liveness = float(np.mean([models.liveness.predict(image, face.box).score for image, face in usable]))
    similarity = float(np.mean([
        float(np.dot(models.embedder.predict(image, face.keypoints), reference)) for image, face in usable
    ]))
    # Kayan nokta hatası sözleşme aralığını (0–1, −1…1) aşmasın; api aralık dışını "servis bozuk" sayıyor.
    liveness = float(np.clip(liveness, 0.0, 1.0))
    similarity = float(np.clip(similarity, -1.0, 1.0))

    live_ok = liveness >= th.liveness_threshold
    match_ok = similarity >= th.match_threshold
    reason = None if live_ok and match_ok else ("LIVENESS_FAILED" if not live_ok else "IDENTITY_MISMATCH")
    return VerifyResult(liveness, similarity, reason is None, reason, len(usable))
