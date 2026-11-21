"""Silent-Face-Anti-Spoofing: iki MiniFASNet modeliyle canlılık skoru.

Orijinal depodaki varsayılan çalışma biçimi (test.py, commit b6d5f04): iki model, her biri kendi ölçeğiyle
kırpılmış kareden softmax üretir, iki tahmin toplanır; gerçek yüz sınıfının payı (toplam / 2) skor olur.

| Model | Ölçek | Orijinal ağırlık |
|---|---|---|
| MiniFASNetV2 | 2.7 | 2.7_80x80_MiniFASNetV2.pth |
| MiniFASNetV1SE | 4.0 | 4_0_0_80x80_MiniFASNetV1SE.pth |

ONNX dosyaları orijinal ağırlıklardan scripts/export_models.py ile üretilir. Ön işleme orijinaldekiyle aynı:
- Kırpma: yüz kutusu modelin ölçeğiyle büyütülür, görüntünün içine kaydırılır (src/generate_patches.py CropImage).
- 80x80'e cv2.resize (varsayılan doğrusal ara değerleme).
- Renk BGR kalır (cv2.imread), değerler 0–255; normalizasyon ve /255 yok (src/data_io/functional.py to_tensor).
- Çıktı 3 sınıf; 1. sınıf gerçek yüz (test.py: label == 1 → Real Face).
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np

from app.models.base import VisionModel, as_bgr, check_input_shape, onnx_session
from app.models.face_landmarks import FaceBox

INPUT_SIZE = 80
REAL_CLASS = 1
# Dosya → kırpma ölçeği (orijinal dosya adlarının başındaki değer: "2.7_…", "4_0_0_…").
SCALES = {"MiniFASNetV2.onnx": 2.7, "MiniFASNetV1SE.onnx": 4.0}


@dataclass(frozen=True)
class LivenessResult:
    score: float  # gerçek yüz sınıfının payı, 0–1
    probabilities: tuple[float, float, float]  # iki modelin softmax ortalaması


def softmax(logits: np.ndarray, axis: int = -1) -> np.ndarray:
    z = np.asarray(logits, dtype=np.float64)
    z = z - z.max(axis=axis, keepdims=True)  # taşmayı önle
    e = np.exp(z)
    return e / e.sum(axis=axis, keepdims=True)


def crop_box(src_w: int, src_h: int, box: FaceBox, scale: float) -> tuple[int, int, int, int]:
    """CropImage._get_new_box'ın birebir karşılığı: (x1, y1, x2, y2), uçlar dahil."""
    if box.width <= 0 or box.height <= 0:
        raise ValueError("Yüz kutusu boş")
    scale = min((src_h - 1) / box.height, min((src_w - 1) / box.width, scale))
    new_w, new_h = box.width * scale, box.height * scale
    cx, cy = box.width / 2 + box.x, box.height / 2 + box.y

    x1, y1 = cx - new_w / 2, cy - new_h / 2
    x2, y2 = cx + new_w / 2, cy + new_h / 2
    if x1 < 0:
        x2 -= x1
        x1 = 0
    if y1 < 0:
        y2 -= y1
        y1 = 0
    if x2 > src_w - 1:
        x1 -= x2 - src_w + 1
        x2 = src_w - 1
    if y2 > src_h - 1:
        y1 -= y2 - src_h + 1
        y2 = src_h - 1
    return int(x1), int(y1), int(x2), int(y2)


def preprocess(image: np.ndarray, box: FaceBox, scale: float) -> np.ndarray:
    """(1, 3, 80, 80) float32, BGR, 0–255."""
    bgr = as_bgr(image)
    x1, y1, x2, y2 = crop_box(bgr.shape[1], bgr.shape[0], box, scale)
    patch = cv2.resize(bgr[y1:y2 + 1, x1:x2 + 1], (INPUT_SIZE, INPUT_SIZE))
    return np.ascontiguousarray(patch.transpose(2, 0, 1)[None].astype(np.float32))


def combine(probabilities: list[np.ndarray]) -> LivenessResult:
    """test.py: tahminler toplanır, gerçek sınıfın toplamdaki payı skor olur."""
    mean = np.sum(probabilities, axis=0) / len(probabilities)
    return LivenessResult(float(mean[REAL_CLASS]), tuple(float(p) for p in mean))


class LivenessModel(VisionModel):
    name = "Silent-Face-Anti-Spoofing (MiniFASNetV2 + MiniFASNetV1SE)"
    files = tuple(SCALES)

    def _load(self, *paths: Path) -> None:
        self._models = []
        for path in paths:
            session = onnx_session(path)
            input_name = check_input_shape(session, (1, 3, INPUT_SIZE, INPUT_SIZE), path.name)
            self._models.append((session, input_name, SCALES[path.name]))

    def predict(self, image: np.ndarray, box: FaceBox) -> LivenessResult:
        probs = []
        for session, input_name, scale in self._models:
            logits = session.run(None, {input_name: preprocess(image, box, scale)})[0].reshape(-1)
            probs.append(softmax(logits))
        return combine(probs)
