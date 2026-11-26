"""D4 ucu gerçek modellerle ("models" işaretli; dosyalar yoksa atlanır).

    pytest -q -m models
"""

import pytest
from conftest import KEY, REAL_MODEL_DIR
from test_face_real import blank_png, missing

from app.models import load_models

pytestmark = [
    pytest.mark.models,
    pytest.mark.skipif(bool(missing), reason=f"model dosyaları yok ({REAL_MODEL_DIR}): {', '.join(missing)}"),
]


@pytest.fixture(scope="module")
def real_models():
    return load_models(REAL_MODEL_DIR)


@pytest.fixture
def real(client, real_models):
    client.app.state.models = real_models
    return client


def test_blank_png_all_tasks(real):
    frame = blank_png()  # 64x64, her piksel 128 (VisionContractTests.BlankImage)
    body = {"sessionId": "s1", "frame": frame, "ts": "2026-11-24T10:15:30.250Z",
            "tasks": ["face", "pose", "phone", "identity"], "referenceVector": [1.0] + [0.0] * 511}
    r = real.post("/internal/analyze/frame", json=body, headers=KEY)
    assert r.status_code == 200, r.json()
    result = r.json()
    assert result["faceCount"] == 0
    assert result["yaw"] is None and result["pitch"] is None and result["gazeAway"] is None
    assert result["identity"] is None  # yüz yok: kimlik ölçülmüyor
    assert result["phone"]["detected"] is False and 0.0 <= result["phone"]["confidence"] < 0.25
    assert result["evidenceFrame"] == frame  # faceCount ≠ 1 şüpheli
    assert isinstance(result["processingMs"], int) and result["processingMs"] >= 0
