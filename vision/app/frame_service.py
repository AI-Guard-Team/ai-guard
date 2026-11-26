"""Sınav sırası kamera analizi (D4, docs/contracts.md 5. bölüm): kare başına ölçüm, olay kararı yok.

Servis durumsuzdur: "art arda 3 ölçüm yüz yok" gibi olay kuralları api'de uygulanır (S-20). Burada yalnızca
istenen görevlerin (`tasks`) ölçümü yapılır; istenmeyen görevlerin alanları None döner. Zamanlamayı api belirler
(face + pose her kare, phone 3 sn'de bir, identity 60 sn'de bir). Görüntü, vektör ve skorlar günlüğe yazılmaz.
"""

from __future__ import annotations

import time
from dataclasses import dataclass

import numpy as np

from app.images import decode_base64
from app.models import Models

FACE_TASKS = frozenset({"face", "pose", "identity"})  # bu görevlerden biri varsa yüz noktaları çalışır


@dataclass(frozen=True)
class FrameThresholds:
    gaze_yaw_deg: float
    gaze_pitch_deg: float
    phone_confidence: float
    match_threshold: float  # verify ile aynı eşik; tek yerde (config.yaml verify.match_threshold)

    @classmethod
    def from_config(cls, config: dict) -> FrameThresholds:
        frame, verify = config.get("frame") or {}, config.get("verify") or {}
        values = {
            "gaze_yaw_deg": frame.get("gaze_yaw_deg"),
            "gaze_pitch_deg": frame.get("gaze_pitch_deg"),
            "phone_confidence": frame.get("phone_confidence"),
            "match_threshold": verify.get("match_threshold"),
        }
        empty = [k for k, v in values.items() if v is None]
        if empty:
            raise ValueError(f"config.yaml'da eşik tanımlı değil: {', '.join(empty)}")
        return cls(**{k: float(v) for k, v in values.items()})


@dataclass(frozen=True)
class Phone:
    detected: bool
    confidence: float


@dataclass(frozen=True)
class Identity:
    similarity: float
    matched: bool


@dataclass(frozen=True)
class FrameResult:
    face_count: int | None
    yaw: float | None
    pitch: float | None
    gaze_away: bool | None
    phone: Phone | None
    identity: Identity | None
    evidence_frame: str | None
    processing_ms: int


def gaze_away(yaw: float, pitch: float, th: FrameThresholds) -> bool:
    """Tek ölçüm için bakış sapması; sınır değer sapma sayılmaz (|yaw| > eşik)."""
    return abs(yaw) > th.gaze_yaw_deg or abs(pitch) > th.gaze_pitch_deg


def is_suspicious(face_count: int | None, away: bool | None, phone: Phone | None, identity: Identity | None) -> bool:
    """Kanıt karesi döndürülecek durumlar (contracts.md 5. bölüm). İstenmeyen görev (None) şüphe sayılmaz."""
    return (
        (face_count is not None and face_count != 1)
        or away is True
        or (phone is not None and phone.detected)
        or (identity is not None and not identity.matched)
    )


def analyze(models: Models, frame_b64: str, tasks: set[str], reference: np.ndarray | None,
            th: FrameThresholds) -> FrameResult:
    """Tek karenin ölçümü. `reference` yalnızca identity görevinde gerekir ve birim uzunlukta olmalı (şema
    normalize eder). Kare çözülemezse 422 INVALID_IMAGE (hiçbir model çalışmaz).

    - face: yüz sayısı (MediaPipe, en fazla 4 yüz aranır).
    - pose: en büyük yüzün yaw/pitch'i ve gazeAway; yüz yoksa üçü de None.
    - phone: YOLO11n "cell phone" sınıfının en yüksek güveni; detected = güven ≥ phone_confidence.
    - identity: en büyük yüzün vektörüyle referansın kosinüs benzerliği; matched = benzerlik ≥ match_threshold.
      Yüz yoksa None. Canlılık bu uçta yok (kart).
    """
    start = time.perf_counter()
    image = decode_base64(frame_b64)

    face_count = yaw = pitch = away = phone = identity = None
    largest = None
    if tasks & FACE_TASKS:
        faces = models.face.predict(image)
        largest = faces.largest
        if "face" in tasks:
            face_count = faces.count

    if "pose" in tasks and largest is not None and largest.yaw is not None and largest.pitch is not None:
        # Yuvarlanmıyor: dönen değer ile karar sınırda çelişmesin (verify'daki skorlar gibi).
        yaw, pitch = float(largest.yaw), float(largest.pitch)
        away = gaze_away(yaw, pitch, th)

    if "phone" in tasks:
        confidence = float(np.clip(models.phone.predict(image).confidence, 0.0, 1.0))
        phone = Phone(confidence >= th.phone_confidence, confidence)

    if "identity" in tasks and largest is not None:
        if reference is None:
            raise ValueError("identity görevi için referans vektör gerekli")  # şema bunu zaten engeller
        similarity = float(np.clip(np.dot(models.embedder.predict(image, largest.keypoints), reference), -1.0, 1.0))
        identity = Identity(similarity, similarity >= th.match_threshold)

    # Kanıt karesi gelen karenin kendisi, yeniden sıkıştırılmadan; api olay oluşursa şifreleyip saklar.
    evidence = frame_b64 if is_suspicious(face_count, away, phone, identity) else None
    processing_ms = round((time.perf_counter() - start) * 1000)
    return FrameResult(face_count, yaw, pitch, away, phone, identity, evidence, processing_ms)
