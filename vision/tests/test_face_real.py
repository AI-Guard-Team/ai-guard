"""D2 uçları gerçek modellerle ("models" işaretli; dosyalar yoksa atlanır).

api'deki VisionContractTests'in senaryoları burada da sınanıyor: 64x64 gri, yüzsüz PNG ile kayıt reddedilmeli;
aynı PNG'den 3 kareyle doğrulama 200, passed=false ve reason dolu dönmeli. Yüzlü senaryo AIGUARD_TEST_FACE ile
(repo dışı bir fotoğraf) çalışır, yoksa atlanır.

    pytest -q -m models
"""

import base64
import os
from pathlib import Path

import cv2
import numpy as np
import pytest
from conftest import KEY, REAL_MODEL_DIR
from test_face_service import REASONS, assert_contract, exif_orientation

from app.images import MAX_FRAME_BYTES
from app.models import load_models, model_files

missing = [f for f in model_files() if not (REAL_MODEL_DIR / f).is_file()]
pytestmark = [
    pytest.mark.models,
    pytest.mark.skipif(bool(missing), reason=f"model dosyaları yok ({REAL_MODEL_DIR}): {', '.join(missing)}"),
]

FACE_PATH = os.environ.get("AIGUARD_TEST_FACE")
needs_face = pytest.mark.skipif(not FACE_PATH or not Path(FACE_PATH).is_file(), reason="AIGUARD_TEST_FACE tanımlı değil")


@pytest.fixture(scope="module")
def real_models():
    return load_models(REAL_MODEL_DIR)


@pytest.fixture
def real(client, real_models):
    client.app.state.models = real_models
    return client


def blank_png() -> str:
    """VisionContractTests.BlankImage ile aynı: 64x64, RGB, her piksel 128."""
    ok, data = cv2.imencode(".png", np.full((64, 64, 3), 128, np.uint8))
    assert ok
    return base64.b64encode(data.tobytes()).decode()


def enroll(client, image_b64):
    return client.post("/internal/face/enroll", json={"image": image_b64}, headers=KEY)


def verify(client, frames, reference):
    return client.post("/internal/face/verify", json={"frames": frames, "referenceVector": reference}, headers=KEY)


def test_blank_png_enroll_is_rejected_with_known_code(real):
    r = enroll(real, blank_png())
    assert r.status_code == 422
    assert r.json()["error"]["code"] == "NO_FACE"


def test_blank_png_frames_fail_with_reason(real):
    reference = [1.0] + [0.0] * 511
    r = verify(real, [blank_png()] * 3, reference)
    assert r.status_code == 200
    body = r.json()
    assert_contract(body)
    assert body["passed"] is False and body["reason"] == "NO_FACE" and body["framesUsed"] == 0


@needs_face
def test_real_face_enrolls_and_matches_itself(real):
    photo = Path(FACE_PATH).read_bytes()
    r = enroll(real, base64.b64encode(photo).decode())
    assert r.status_code == 200, r.json()
    body = r.json()
    vector = np.array(body["vector"])
    assert vector.shape == (512,) and np.all(np.isfinite(vector)) and abs(np.linalg.norm(vector) - 1) < 0.01
    assert 0.5 <= body["qualityScore"] <= 1.0

    if len(photo) > MAX_FRAME_BYTES:
        pytest.skip("AIGUARD_TEST_FACE 1 MB'tan büyük; kare sınırını aşıyor (VisionContractTests de bu yüzden kalır)")
    v = verify(real, [base64.b64encode(photo).decode()] * 3, body["vector"])
    assert v.status_code == 200
    result = v.json()
    assert_contract(result)
    # Canlılık burada sınanmıyor: durağan fotoğrafın reddedilmesi beklenen davranış olabilir (D6).
    assert result["similarity"] > 0.9, result
    assert result["passed"] or result["reason"] in REASONS


@needs_face
def test_sideways_phone_photo_is_rotated_by_exif(real):
    # Telefonun yan kaydettiği fotoğraf: pikseller 90° dönük, EXIF 6 dik göstermeyi söylüyor.
    upright = cv2.imread(FACE_PATH)
    ok, jpg = cv2.imencode(".jpg", cv2.rotate(upright, cv2.ROTATE_90_COUNTERCLOCKWISE), [cv2.IMWRITE_JPEG_QUALITY, 95])
    assert ok
    sideways = jpg.tobytes()[:2] + exif_orientation(6) + jpg.tobytes()[2:]
    ok, ref = cv2.imencode(".jpg", upright, [cv2.IMWRITE_JPEG_QUALITY, 95])

    a = enroll(real, base64.b64encode(sideways).decode())
    b = enroll(real, base64.b64encode(ref.tobytes()).decode())
    assert a.status_code == 200, a.json()
    assert b.status_code == 200, b.json()
    assert float(np.dot(a.json()["vector"], b.json()["vector"])) > 0.95
