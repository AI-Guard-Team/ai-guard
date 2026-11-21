"""YOLO11n (COCO, ONNX): yalnızca 67. sınıf "cell phone".

Ön ve son işleme Ultralytics'in tahmin yoluyla aynı: 640'a oranı koruyarak küçültme ve 114 gri dolgu
(LetterBox), BGR → RGB, /255; çıktı (1, 84, 8400) = 4 kutu (cx, cy, w, h; piksel) + 80 sınıf skoru
(sigmoid uygulanmış). Güven eşiği 0,25 ve NMS IoU 0,7 Ultralytics'in varsayılanları. Telefon var/yok kararı
`config.yaml`'daki `phone_confidence` eşiğiyle uçta verilir (D4); model yalnızca skor ve kutuları döner.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np

from app.models.base import VisionModel, as_bgr, check_input_shape, onnx_session

IMAGE_SIZE = 640
PAD_VALUE = 114
CELL_PHONE = 67
MIN_SCORE = 0.25
NMS_IOU = 0.7


@dataclass(frozen=True)
class Detection:
    box: tuple[float, float, float, float]  # x1, y1, x2, y2; özgün görüntü pikseli
    score: float


@dataclass(frozen=True)
class PhoneResult:
    confidence: float  # bütün adaylar içinde en yüksek telefon skoru (eşik altında olsa da)
    detections: tuple[Detection, ...]  # MIN_SCORE üstü, NMS sonrası, skora göre azalan


@dataclass(frozen=True)
class Letterbox:
    scale: float
    pad_x: int  # soldaki dolgu
    pad_y: int  # üstteki dolgu


def letterbox(image: np.ndarray, size: int = IMAGE_SIZE) -> tuple[np.ndarray, Letterbox]:
    """Ultralytics LetterBox(auto=False, center=True) karşılığı."""
    h, w = image.shape[:2]
    scale = min(size / h, size / w)
    new_w, new_h = round(w * scale), round(h * scale)
    dw, dh = (size - new_w) / 2, (size - new_h) / 2
    resized = image if (new_w, new_h) == (w, h) else cv2.resize(image, (new_w, new_h), interpolation=cv2.INTER_LINEAR)
    top, bottom = round(dh - 0.1), round(dh + 0.1)
    left, right = round(dw - 0.1), round(dw + 0.1)
    padded = cv2.copyMakeBorder(resized, top, bottom, left, right, cv2.BORDER_CONSTANT,
                                value=(PAD_VALUE, PAD_VALUE, PAD_VALUE))
    return padded, Letterbox(scale, left, top)


def iou(box: np.ndarray, boxes: np.ndarray) -> np.ndarray:
    x1 = np.maximum(box[0], boxes[:, 0])
    y1 = np.maximum(box[1], boxes[:, 1])
    x2 = np.minimum(box[2], boxes[:, 2])
    y2 = np.minimum(box[3], boxes[:, 3])
    inter = np.clip(x2 - x1, 0, None) * np.clip(y2 - y1, 0, None)
    area = (box[2] - box[0]) * (box[3] - box[1])
    areas = (boxes[:, 2] - boxes[:, 0]) * (boxes[:, 3] - boxes[:, 1])
    return inter / np.maximum(area + areas - inter, 1e-9)


def nms(boxes: np.ndarray, scores: np.ndarray, iou_threshold: float = NMS_IOU) -> list[int]:
    """Açgözlü NMS; skora göre azalan sırada tutulan indeksler."""
    order = np.argsort(-scores, kind="stable")
    keep: list[int] = []
    while order.size:
        best = int(order[0])
        keep.append(best)
        rest = order[1:]
        order = rest[iou(boxes[best], boxes[rest]) <= iou_threshold]
    return keep


def decode(output: np.ndarray, lb: Letterbox, image_shape: tuple[int, int], class_id: int = CELL_PHONE,
           min_score: float = MIN_SCORE, iou_threshold: float = NMS_IOU) -> PhoneResult:
    """YOLO çıktısından tek sınıfın kutuları, özgün görüntü koordinatlarında."""
    pred = np.asarray(output, dtype=np.float32).reshape(-1, output.shape[-1])  # (84, N)
    scores = pred[4 + class_id]
    confidence = float(scores.max()) if scores.size else 0.0

    mask = scores >= min_score
    if not mask.any():
        return PhoneResult(confidence, ())
    cx, cy, bw, bh = pred[:4, mask]
    boxes = np.stack([cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2], axis=1)
    kept_scores = scores[mask]
    keep = nms(boxes, kept_scores, iou_threshold)

    # Dolguyu çıkar, ölçeği geri al, görüntünün içine kırp.
    h, w = image_shape
    boxes = boxes[keep]
    boxes[:, [0, 2]] = np.clip((boxes[:, [0, 2]] - lb.pad_x) / lb.scale, 0, w)
    boxes[:, [1, 3]] = np.clip((boxes[:, [1, 3]] - lb.pad_y) / lb.scale, 0, h)
    detections = tuple(Detection(tuple(float(v) for v in b), float(s)) for b, s in zip(boxes, kept_scores[keep]))
    return PhoneResult(confidence, detections)


class PhoneDetector(VisionModel):
    name = "YOLO11n (COCO cell phone)"
    files = ("yolo11n.onnx",)

    def _load(self, path: Path) -> None:
        self._session = onnx_session(path)
        self._input = check_input_shape(self._session, (1, 3, IMAGE_SIZE, IMAGE_SIZE), self.name)

    def predict(self, image: np.ndarray, min_score: float = MIN_SCORE) -> PhoneResult:
        bgr = as_bgr(image)
        padded, lb = letterbox(bgr)
        blob = np.ascontiguousarray(padded[:, :, ::-1].transpose(2, 0, 1)[None], dtype=np.float32) / 255.0
        output = self._session.run(None, {self._input: blob})[0]
        return decode(output, lb, bgr.shape[:2], min_score=min_score)
