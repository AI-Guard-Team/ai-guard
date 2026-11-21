"""Gerçek model dosyalarıyla testler ("models" işaretli). Dosyalar yoksa (CI) atlanır.

    cd vision
    python scripts/download_models.py          # + export_models.py (MiniFASNetV2, MiniFASNetV1SE, yolo11n)
    pytest -q -m models

Yüz fotoğrafı repoya eklenmediği için yüzlü test isteğe bağlı: AIGUARD_TEST_FACE = tek bir yüzün bulunduğu
JPEG/PNG'nin yolu (api'deki VisionContractTests ile aynı değişken).
"""

import os
from pathlib import Path

import cv2
import numpy as np
import pytest
from conftest import REAL_MODEL_DIR

from app.models import load_models, model_files
from app.models.face_embedder import ARCFACE_TEMPLATE
from app.models.face_landmarks import MAX_FACES, FaceBox, sharpness

missing = [f for f in model_files() if not (REAL_MODEL_DIR / f).is_file()]
pytestmark = [
    pytest.mark.models,
    pytest.mark.skipif(bool(missing), reason=f"model dosyaları yok ({REAL_MODEL_DIR}): {', '.join(missing)}"),
]

FACE_PATH = os.environ.get("AIGUARD_TEST_FACE")


@pytest.fixture(scope="module")
def models():
    return load_models(REAL_MODEL_DIR)


@pytest.mark.parametrize("shape", [(480, 640, 3), (64, 64, 3), (64, 64), (10, 10, 3), (1080, 1920, 3)])
def test_no_face_in_blank_images(models, shape):
    # A3'ün sözleşme testi 64x64 gri PNG gönderiyor: çökme değil "yüz yok".
    assert models.face.predict(np.full(shape, 128, np.uint8)).count == 0


def test_noise_image_does_not_crash(models):
    noise = np.random.default_rng(0).integers(0, 256, (480, 640, 3), dtype=np.uint8)
    assert 0 <= models.face.predict(noise).count <= MAX_FACES


def test_embedding_is_512_unit_vector(models):
    img = np.random.default_rng(0).integers(0, 256, (112, 112, 3), dtype=np.uint8)
    vec = models.embedder.predict(img, ARCFACE_TEMPLATE)
    assert vec.shape == (512,) and np.all(np.isfinite(vec))
    assert abs(float(np.linalg.norm(vec)) - 1.0) < 1e-5


def test_liveness_score_is_probability(models):
    result = models.liveness.predict(np.full((480, 640, 3), 128, np.uint8), FaceBox(270, 190, 100, 100))
    assert 0.0 <= result.score <= 1.0
    assert sum(result.probabilities) == pytest.approx(1.0, abs=1e-6)


def test_no_phone_in_blank_frame(models):
    result = models.phone.predict(np.full((480, 640, 3), 128, np.uint8))
    assert result.detections == () and 0.0 <= result.confidence < 0.25


def test_silence_has_low_speech_probability(models):
    result = models.speech.predict(np.zeros(16000, np.float32))
    assert result.probabilities.shape == (32,)
    assert result.duration_sec == pytest.approx(1.0)
    assert result.max_probability < 0.1


@pytest.mark.skipif(not FACE_PATH or not Path(FACE_PATH).is_file(), reason="AIGUARD_TEST_FACE tanımlı değil")
def test_real_face(models):
    img = cv2.imread(FACE_PATH)
    faces = models.face.predict(img)
    assert faces.count == 1, f"{faces.count} yüz bulundu"
    face = faces.largest
    assert face.box.min_side > 0 and sharpness(img, face.box) > 0
    assert face.yaw is not None and abs(face.yaw) < 90

    vec = models.embedder.predict(img, face.keypoints)
    assert abs(float(np.linalg.norm(vec)) - 1.0) < 1e-5
    # Aynı yüzün biraz kaydırılmış hâli kendisine çok benzemeli.
    shifted = np.roll(img, 5, axis=1)
    vec2 = models.embedder.predict(shifted, models.face.predict(shifted).largest.keypoints)
    assert float(vec @ vec2) > 0.9

    assert 0.0 <= models.liveness.predict(img, face.box).score <= 1.0
