"""MediaPipe Face Landmarker: yüz sayısı, kafa yönü (yaw/pitch), yüz kutusu ve 5 nokta.

Kutu ve 5 nokta ArcFace hizalaması (face_embedder) ile MiniFASNet kırpması (liveness) için kullanılır;
ayrı bir yüz bulucu (RetinaFace) yok (MIMARI-KARARLAR K-28).
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np

from app.models.base import VisionModel, as_bgr

MAX_FACES = 4  # "birden fazla yüz" sayılabilsin; dördüncüden sonrası fark etmiyor
MIN_SIDE_PX = 32  # bundan küçük görüntüde yüz aranmaz, "yüz yok" döner

# Face Landmarker'ın 478 noktalık modelindeki indeksler (468-477 iris noktaları).
IRIS_CENTERS = (468, 473)
NOSE_TIP = 1
MOUTH_CORNERS = (61, 291)


@dataclass(frozen=True)
class FaceBox:
    """Piksel cinsinden, görüntünün içine kırpılmış kutu."""

    x: int
    y: int
    width: int
    height: int

    @property
    def min_side(self) -> int:
        """Yüzün piksel boyutu; kayıtta `FACE_TOO_SMALL` eşiğiyle karşılaştırılır (D2)."""
        return min(self.width, self.height)

    @property
    def area(self) -> int:
        return self.width * self.height


@dataclass(frozen=True)
class Face:
    box: FaceBox
    # (5, 2) piksel; ArcFace sırası: görüntüde soldaki göz, sağdaki göz, burun ucu, soldaki ağız köşesi, sağdaki.
    keypoints: np.ndarray
    # Derece. Dönüşüm matrisi gelmezse None.
    yaw: float | None
    pitch: float | None
    roll: float | None


@dataclass(frozen=True)
class FaceLandmarksResult:
    faces: tuple[Face, ...]  # en büyük yüz başta

    @property
    def count(self) -> int:
        return len(self.faces)

    @property
    def largest(self) -> Face | None:
        return self.faces[0] if self.faces else None


def face_box(points_px: np.ndarray, width: int, height: int) -> FaceBox:
    """Noktaların sınır kutusu, görüntünün içine kırpılmış."""
    x1, y1 = np.floor(points_px.min(axis=0)).astype(int)
    x2, y2 = np.ceil(points_px.max(axis=0)).astype(int)
    x1, x2 = int(np.clip(x1, 0, width - 1)), int(np.clip(x2, 0, width - 1))
    y1, y2 = int(np.clip(y1, 0, height - 1)), int(np.clip(y2, 0, height - 1))
    return FaceBox(x1, y1, x2 - x1 + 1, y2 - y1 + 1)


def five_points(points_px: np.ndarray) -> np.ndarray:
    """478 noktadan ArcFace'in 5 noktası. Gözler ve ağız köşeleri görüntüdeki x konumuna göre sıralanır;
    böylece MediaPipe'ın sağ/sol adlandırmasına bağlı kalınmıyor."""
    eyes = points_px[list(IRIS_CENTERS)]
    mouth = points_px[list(MOUTH_CORNERS)]
    eyes = eyes[np.argsort(eyes[:, 0])]
    mouth = mouth[np.argsort(mouth[:, 0])]
    return np.stack([eyes[0], eyes[1], points_px[NOSE_TIP], mouth[0], mouth[1]]).astype(np.float32)


def euler_angles(matrix: np.ndarray) -> tuple[float, float, float]:
    """4x4 (ya da 3x3) dönüşüm matrisinden (yaw, pitch, roll), derece.

    R = Rz(roll) · Ry(yaw) · Rx(pitch) ayrıştırması. Matristeki ölçek sütunlar normalize edilerek atılır.
    Kontrolde mutlak değer kullanıldığı için (|yaw| > 30) işaret yönü sonucu değiştirmiyor.
    """
    r = np.asarray(matrix, dtype=np.float64)[:3, :3]
    norms = np.linalg.norm(r, axis=0)
    if np.any(norms < 1e-9):
        raise ValueError("Dönüşüm matrisi tekil")
    r = r / norms
    pitch = math.atan2(r[2, 1], r[2, 2])
    yaw = math.atan2(-r[2, 0], math.hypot(r[2, 1], r[2, 2]))
    roll = math.atan2(r[1, 0], r[0, 0])
    return math.degrees(yaw), math.degrees(pitch), math.degrees(roll)


SHARPNESS_SIDE_PX = 112  # netlik bu ölçekte ölçülür (ArcFace giriş kenarı)


def sharpness(image: np.ndarray, box: FaceBox) -> float:
    """Yüz bölgesinde Laplacian varyansı; kayıtta `IMAGE_TOO_BLURRY` eşiğiyle karşılaştırılır (D2).

    Kesit önce kısa kenarı SHARPNESS_SIDE_PX olacak şekilde ölçeklenir. Ham Laplacian varyansı çözünürlüğe
    çok bağlı: LFW'de aynı net yüz 4 kat büyütülünce medyan 191'den 3,8'e düşüyor; yüksek çözünürlüklü
    telefon fotoğrafları "bulanık" sayılırdı. Sabit ölçekte değer çözünürlükten bağımsız (D2 ölçümü).
    """
    crop = as_bgr(image)[box.y:box.y + box.height, box.x:box.x + box.width]
    if crop.size == 0:
        return 0.0
    scale = SHARPNESS_SIDE_PX / min(crop.shape[:2])
    interpolation = cv2.INTER_AREA if scale < 1 else cv2.INTER_LINEAR
    crop = cv2.resize(crop, None, fx=scale, fy=scale, interpolation=interpolation)
    gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
    return float(cv2.Laplacian(gray, cv2.CV_64F).var())


class FaceLandmarker(VisionModel):
    name = "MediaPipe Face Landmarker"
    files = ("face_landmarker.task",)

    def _load(self, path: Path) -> None:
        # mediapipe ağır bir paket; yalnızca model gerçekten yüklenirken içe aktarılıyor.
        import mediapipe as mp
        from mediapipe.tasks.python import BaseOptions, vision

        self._mp = mp
        options = vision.FaceLandmarkerOptions(
            base_options=BaseOptions(model_asset_path=str(path)),
            running_mode=vision.RunningMode.IMAGE,
            num_faces=MAX_FACES,
            output_facial_transformation_matrixes=True,
        )
        self._landmarker = vision.FaceLandmarker.create_from_options(options)

    def predict(self, image: np.ndarray) -> FaceLandmarksResult:
        bgr = as_bgr(image)
        height, width = bgr.shape[:2]
        if min(height, width) < MIN_SIDE_PX:
            return FaceLandmarksResult(())

        rgb = np.ascontiguousarray(bgr[:, :, ::-1])
        result = self._landmarker.detect(self._mp.Image(image_format=self._mp.ImageFormat.SRGB, data=rgb))
        matrices = result.facial_transformation_matrixes or []

        faces = []
        for i, landmarks in enumerate(result.face_landmarks):
            points = np.array([(p.x * width, p.y * height) for p in landmarks], dtype=np.float32)
            angles = euler_angles(np.asarray(matrices[i])) if i < len(matrices) else (None, None, None)
            faces.append(Face(face_box(points, width, height), five_points(points), *angles))
        faces.sort(key=lambda f: f.box.area, reverse=True)
        return FaceLandmarksResult(tuple(faces))
