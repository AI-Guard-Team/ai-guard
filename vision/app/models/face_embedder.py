"""ArcFace (InsightFace w600k_mbf, MobileFaceNet @ WebFace600K): 112x112 hizalanmış yüzden 512 boyutlu vektör.

Ön işleme InsightFace'teki gibi: 5 noktayla benzerlik dönüşümü (Umeyama) → 112x112, BGR → RGB,
(x − 127,5) / 127,5. Modelin grafiğinde gömülü normalizasyon yok (ilk düğüm Conv).
Çıktı her zaman L2 ile normalize edilir (docs/contracts.md: birim uzunlukta vektör; A3 ±0,01 kontrol ediyor).
"""

from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np

from app.models.base import VisionModel, as_bgr, check_input_shape, onnx_session

INPUT_SIZE = 112
EMBEDDING_DIM = 512

# InsightFace face_align.arcface_dst: 112x112 görüntüde 5 noktanın hedef konumları.
ARCFACE_TEMPLATE = np.array(
    [[38.2946, 51.6963], [73.5318, 51.5014], [56.0252, 71.7366], [41.5493, 92.3655], [70.7299, 92.2041]],
    dtype=np.float32,
)


def estimate_similarity(src: np.ndarray, dst: np.ndarray) -> np.ndarray:
    """src noktalarını dst'ye taşıyan benzerlik dönüşümü (dönme + eşit ölçek + öteleme), 2x3.

    Umeyama (1991), en küçük kareler; scikit-image SimilarityTransform.estimate ile aynı yöntem.
    """
    src = np.asarray(src, dtype=np.float64)
    dst = np.asarray(dst, dtype=np.float64)
    if src.shape != dst.shape or src.ndim != 2 or src.shape[1] != 2 or len(src) < 2:
        raise ValueError("Nokta dizileri (N, 2) biçiminde ve eşit uzunlukta olmalı")
    src_mean, dst_mean = src.mean(axis=0), dst.mean(axis=0)
    src_c, dst_c = src - src_mean, dst - dst_mean
    src_var = (src_c ** 2).sum() / len(src)
    if src_var < 1e-12:
        raise ValueError("Noktalar tek bir konumda; dönüşüm hesaplanamıyor")

    cov = dst_c.T @ src_c / len(src)
    u, s, vt = np.linalg.svd(cov)
    d = np.ones(2)
    if np.linalg.det(cov) < 0:
        d[1] = -1  # yansımayı engelle
    rotation = u @ np.diag(d) @ vt
    scale = (s * d).sum() / src_var
    translation = dst_mean - scale * rotation @ src_mean
    return np.hstack([scale * rotation, translation[:, None]]).astype(np.float32)


def align_face(image: np.ndarray, keypoints: np.ndarray) -> np.ndarray:
    """5 noktayı ArcFace şablonuna oturtup 112x112 BGR yüz kesiti döner."""
    matrix = estimate_similarity(keypoints, ARCFACE_TEMPLATE)
    return cv2.warpAffine(as_bgr(image), matrix, (INPUT_SIZE, INPUT_SIZE), borderValue=0.0)


def l2_normalize(vector: np.ndarray) -> np.ndarray:
    """Birim uzunlukta vektör. Sonlu olmayan ya da sıfıra yakın vektör model hatasıdır, yutulmaz."""
    v = np.asarray(vector, dtype=np.float32).reshape(-1)
    norm = float(np.linalg.norm(v))
    if not np.all(np.isfinite(v)) or norm < 1e-6:
        raise ValueError("Yüz vektörü normalize edilemiyor (sıfır ya da sonlu olmayan değer)")
    return v / norm


class FaceEmbedder(VisionModel):
    name = "ArcFace (w600k_mbf)"
    files = ("w600k_mbf.onnx",)

    def _load(self, path: Path) -> None:
        self._session = onnx_session(path)
        self._input = check_input_shape(self._session, (1, 3, INPUT_SIZE, INPUT_SIZE), self.name)

    def predict(self, image: np.ndarray, keypoints: np.ndarray) -> np.ndarray:
        """Görüntü ve o yüzün 5 noktası (FaceLandmarker) → (512,) float32, L2 normalize."""
        aligned = align_face(image, keypoints)
        blob = (aligned[:, :, ::-1].astype(np.float32) - 127.5) / 127.5
        blob = np.ascontiguousarray(blob.transpose(2, 0, 1)[None])
        embedding = self._session.run(None, {self._input: blob})[0]
        if embedding.size != EMBEDDING_DIM:
            raise ValueError(f"{self.name}: {embedding.size} boyutlu çıktı, beklenen {EMBEDDING_DIM}")
        return l2_normalize(embedding)
