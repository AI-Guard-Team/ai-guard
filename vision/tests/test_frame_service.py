"""D4: /internal/analyze/frame, sahte modellerle (app.state.models).

Sahte modeller sonucu görüntünün kendisinden okur (test_face_service.py'deki yöntem); PNG kayıpsız olduğu için
değerler aynen geri okunur:
    piksel (0, 0) = (yüz sayısı, yaw + 128, pitch + 128)
    piksel (0, 1) = (telefon güveni × 250, benzerlik × 100 + 100, 255 ise kafa yönü yok)
Görüntü çözme gerçek kodla yapılır.
"""

import base64
import json
import logging
from types import SimpleNamespace

import cv2
import numpy as np
import pytest
from conftest import KEY

from app.config import get_settings
from app.frame_service import FrameThresholds
from app.images import MAX_FRAME_B64
from app.models.face_landmarks import Face, FaceBox, FaceLandmarksResult

TH = {"gaze_yaw_deg": 30, "gaze_pitch_deg": 20, "phone_confidence": 0.5}
MATCH = 0.4
E0 = [1.0] + [0.0] * 511
TS = "2026-11-24T10:15:30.250Z"
ALL = ["face", "pose", "phone", "identity"]


# ---------- sahte modeller ----------

class FakeFace:
    def __init__(self):
        self.calls = 0

    def predict(self, image):
        self.calls += 1
        count, yaw, pitch = int(image[0, 0, 0]), int(image[0, 0, 1]) - 128, int(image[0, 0, 2]) - 128
        no_pose = int(image[0, 1, 2]) == 255
        faces = []
        for i in range(count):
            side = 100 - 10 * i  # en büyük yüz başta (gerçek modeldeki sıra)
            angles = (None, None, None) if no_pose else (float(yaw), float(pitch), 0.0)
            faces.append(Face(FaceBox(10, 10, side, side), np.zeros((5, 2), np.float32), *angles))
        return FaceLandmarksResult(tuple(faces))


class FakePhone:
    def __init__(self):
        self.calls = 0

    def predict(self, image):
        self.calls += 1
        return SimpleNamespace(confidence=int(image[0, 1, 0]) / 250, detections=())


class FakeEmbedder:
    """E0 ile benzerliği pikselde yazan birim vektör: [s, √(1 − s²), 0, …]."""

    def __init__(self):
        self.calls = 0

    def predict(self, image, keypoints):
        self.calls += 1
        s = (int(image[0, 1, 1]) - 100) / 100
        v = np.zeros(512, np.float32)
        v[0], v[1] = s, np.sqrt(max(0.0, 1 - s * s))
        return v


def png(faces=1, yaw=0, pitch=0, phone=0.0, sim=0.9, pose=True) -> str:
    img = np.full((40, 40, 3), 128, np.uint8)
    img[0, 0] = (faces, yaw + 128, pitch + 128)
    img[0, 1] = (round(phone * 250), round(sim * 100 + 100), 0 if pose else 255)
    ok, data = cv2.imencode(".png", img)
    assert ok
    return base64.b64encode(data.tobytes()).decode()


@pytest.fixture
def fake(client, monkeypatch):
    settings = get_settings()
    for key, value in TH.items():
        monkeypatch.setitem(settings.thresholds["frame"], key, value)
    monkeypatch.setitem(settings.thresholds["verify"], "match_threshold", MATCH)
    models = SimpleNamespace(face=FakeFace(), phone=FakePhone(), embedder=FakeEmbedder())
    client.app.state.models = models
    return models


def analyze(client, frame, tasks=ALL, reference=E0, **extra):
    body = {"sessionId": "s1", "frame": frame, "ts": TS, "tasks": tasks, **extra}
    if reference is not None:
        body["referenceVector"] = reference
    return client.post("/internal/analyze/frame", json=body, headers=KEY)


def ok(client, frame, tasks=ALL, reference=E0):
    r = analyze(client, frame, tasks, reference)
    assert r.status_code == 200, r.json()
    return r.json()


def error_code(r) -> str:
    return r.json()["error"]["code"]


# ---------- yanıt biçimi ----------

def test_all_tasks_clean_frame(client, fake):
    frame = png(yaw=10, pitch=-5, phone=0.1, sim=0.8)
    body = ok(client, frame)
    assert set(body) == {"sessionId", "ts", "faceCount", "yaw", "pitch", "gazeAway", "phone", "identity",
                         "evidenceFrame", "processingMs"}
    assert body["sessionId"] == "s1" and body["ts"] == TS
    assert body["faceCount"] == 1
    assert body["yaw"] == 10.0 and body["pitch"] == -5.0 and body["gazeAway"] is False
    assert body["phone"] == {"detected": False, "confidence": pytest.approx(0.1)}
    assert body["identity"]["matched"] is True and body["identity"]["similarity"] == pytest.approx(0.8, abs=1e-6)
    assert body["evidenceFrame"] is None
    assert isinstance(body["processingMs"], int) and body["processingMs"] >= 0


@pytest.mark.parametrize(("tasks", "filled"), [
    (["face"], {"faceCount"}),
    (["pose"], {"yaw", "pitch", "gazeAway"}),
    (["phone"], {"phone"}),
    (["identity"], {"identity"}),
    (["face", "pose"], {"faceCount", "yaw", "pitch", "gazeAway"}),
    (["face", "phone"], {"faceCount", "phone"}),
])
def test_unrequested_tasks_are_null(client, fake, tasks, filled):
    body = ok(client, png(), tasks)
    fields = {"faceCount", "yaw", "pitch", "gazeAway", "phone", "identity"}
    assert {k for k in fields if body[k] is not None} == filled
    assert isinstance(body["processingMs"], int)


def test_unrequested_models_do_not_run(client, fake):
    ok(client, png(), ["face", "pose"], reference=None)
    assert fake.face.calls == 1 and fake.phone.calls == 0 and fake.embedder.calls == 0
    ok(client, png(), ["phone"], reference=None)
    assert fake.face.calls == 1 and fake.phone.calls == 1  # yalnızca telefon: yüz modeli çalışmıyor


def test_duplicate_tasks_are_accepted(client, fake):
    assert ok(client, png(), ["face", "face"])["faceCount"] == 1


# ---------- face ----------

@pytest.mark.parametrize("count", [0, 1, 2, 4])
def test_face_count(client, fake, count):
    assert ok(client, png(faces=count))["faceCount"] == count


def test_no_face_nulls_pose_and_identity(client, fake):
    body = ok(client, png(faces=0, yaw=60, phone=0.1))
    assert body["faceCount"] == 0
    assert body["yaw"] is None and body["pitch"] is None and body["gazeAway"] is None
    assert body["identity"] is None
    assert body["phone"] == {"detected": False, "confidence": pytest.approx(0.1)}  # telefon yüzden bağımsız
    assert fake.embedder.calls == 0


def test_missing_pose_matrix_nulls_pose(client, fake):
    body = ok(client, png(pose=False), ["face", "pose"], reference=None)
    assert body["faceCount"] == 1 and body["yaw"] is None and body["pitch"] is None and body["gazeAway"] is None


# ---------- pose ----------

@pytest.mark.parametrize(("yaw", "pitch", "away"), [
    (0, 0, False),
    (30, 0, False),    # sınır değer sapma değil (|yaw| > 30)
    (31, 0, True),
    (-30, 0, False),
    (-31, 0, True),    # mutlak değer: iki yön de
    (0, 20, False),
    (0, 21, True),
    (0, -21, True),
    (29, 19, False),
    (-45, -30, True),
])
def test_gaze_away_thresholds(client, fake, yaw, pitch, away):
    body = ok(client, png(yaw=yaw, pitch=pitch), ["face", "pose"], reference=None)
    assert body["yaw"] == yaw and body["pitch"] == pitch and body["gazeAway"] is away


def test_gaze_thresholds_come_from_config(client, fake, monkeypatch):
    monkeypatch.setitem(get_settings().thresholds["frame"], "gaze_yaw_deg", 40)
    assert ok(client, png(yaw=35), ["pose"], reference=None)["gazeAway"] is False


# ---------- phone ----------

@pytest.mark.parametrize(("confidence", "detected"), [
    (0.0, False),
    (0.3, False),     # Ultralytics'in 0,25 varsayılanının üstü ama eşiğin altı
    (0.496, False),
    (0.5, True),      # sınır değer tespit (≥)
    (0.9, True),
])
def test_phone_threshold(client, fake, confidence, detected):
    body = ok(client, png(phone=confidence), ["phone"], reference=None)
    assert body["phone"]["detected"] is detected
    assert body["phone"]["confidence"] == pytest.approx(confidence)


# ---------- identity ----------

@pytest.mark.parametrize(("sim", "matched"), [
    (0.9, True),
    (MATCH, True),    # sınır değer eşleşir (verify ile aynı)
    (0.39, False),
    (-0.5, False),
])
def test_identity_matching(client, fake, sim, matched):
    body = ok(client, png(sim=sim), ["face", "identity"])
    assert body["identity"]["matched"] is matched
    assert body["identity"]["similarity"] == pytest.approx(sim, abs=1e-6)


def test_identity_uses_verify_match_threshold(client, fake, monkeypatch):
    monkeypatch.setitem(get_settings().thresholds["verify"], "match_threshold", 0.95)
    assert ok(client, png(sim=0.9), ["identity"])["identity"]["matched"] is False


def test_identity_compares_largest_face(client, fake):
    ok(client, png(faces=3), ["identity"])
    assert fake.embedder.calls == 1  # yalnızca en büyük yüz


# ---------- evidenceFrame ----------

@pytest.mark.parametrize(("kwargs", "tasks", "suspicious"), [
    ({}, ALL, False),
    ({"faces": 0}, ALL, True),
    ({"faces": 2}, ALL, True),
    ({"yaw": 31}, ALL, True),
    ({"pitch": -21}, ALL, True),
    ({"phone": 0.6}, ALL, True),
    ({"sim": 0.1}, ALL, True),
    ({"yaw": 30, "pitch": 20, "phone": 0.496, "sim": MATCH}, ALL, False),  # hepsi sınırın temiz tarafında
    # İstenmeyen görevin sonucu şüphe sayılmaz:
    ({"faces": 0}, ["phone"], False),
    ({"yaw": 60}, ["face"], False),
    ({"phone": 0.9}, ["face", "pose"], False),
    ({"sim": 0.1}, ["face", "pose"], False),
    # pose istenip face istenmezse yüz sayısı bilinmez; yaw yine şüphe sebebi:
    ({"yaw": 60}, ["pose"], True),
])
def test_evidence_frame_only_when_suspicious(client, fake, kwargs, tasks, suspicious):
    frame = png(**kwargs)
    reference = E0 if "identity" in tasks else None
    body = ok(client, frame, tasks, reference)
    assert body["evidenceFrame"] == (frame if suspicious else None)


def test_evidence_frame_is_the_incoming_frame_unchanged(client, fake):
    img = np.random.default_rng(4).integers(0, 256, (48, 64, 3), dtype=np.uint8)
    img[:16, :16] = 0  # sahte modelde 0 yüz → şüpheli (JPEG bloğu bütün siyah kalsın diye 16x16)
    ok_, data = cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, 80])
    assert ok_
    frame = base64.b64encode(data.tobytes()).decode()
    body = ok(client, frame, ["face"], reference=None)
    assert body["faceCount"] == 0 and body["evidenceFrame"] == frame  # yeniden sıkıştırılmamış, bayt bayt aynı


# ---------- hatalar ve doğrulama ----------

@pytest.mark.parametrize("frame", [
    "bu base64 değil!",
    base64.b64encode(b"merhaba dunya").decode(),
    base64.b64encode(b"\x89PNG\r\n\x1a\n" + b"\x00" * 30).decode(),
])
def test_invalid_image(client, fake, frame):
    r = analyze(client, frame)
    assert r.status_code == 422 and error_code(r) == "INVALID_IMAGE"
    assert fake.face.calls == fake.phone.calls == fake.embedder.calls == 0


def test_frame_base64_size_limit(client, fake):
    r = analyze(client, "A" * (MAX_FRAME_B64 + 4))
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_identity_requires_reference_vector(client, fake):
    r = analyze(client, png(), ["face", "identity"], reference=None)
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_reference_vector_not_required_without_identity(client, fake):
    assert analyze(client, png(), ["face", "pose", "phone"], reference=None).status_code == 200


@pytest.mark.parametrize("reference", [[0.0] * 512, [1e-9] * 512, [1.0] * 10, [1.0] * 513])
def test_invalid_reference_vector(client, fake, reference):
    r = analyze(client, png(), ALL, reference)
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_non_finite_reference_vector(client, fake):
    body = (f'{{"sessionId": "s1", "frame": {json.dumps(png())}, "ts": "{TS}", "tasks": ["identity"],'
            f' "referenceVector": [NaN{", 0.0" * 511}]}}')
    r = client.post("/internal/analyze/frame", content=body, headers={**KEY, "Content-Type": "application/json"})
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_reference_vector_is_renormalized(client, fake):
    body = ok(client, png(sim=0.6), ["identity"], reference=[3.0] + [0.0] * 511)
    assert body["identity"]["similarity"] == pytest.approx(0.6, abs=1e-6)


@pytest.mark.parametrize("tasks", [[], ["emotion"]])
def test_tasks_must_be_known_and_non_empty(client, fake, tasks):
    r = analyze(client, png(), tasks)
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_models_not_ready(client):
    client.app.state.models = None
    r = analyze(client, png())
    assert r.status_code == 503 and error_code(r) == "MODELS_NOT_READY"


# ---------- eşikler ----------

def test_config_yaml_has_all_d4_thresholds():
    th = FrameThresholds.from_config(get_settings().thresholds)
    assert 0 < th.gaze_yaw_deg < 90 and 0 < th.gaze_pitch_deg < 90
    assert 0.25 < th.phone_confidence < 1  # Ultralytics varsayılanından yüksek (config.yaml gerekçesi)
    assert -1 < th.match_threshold < 1


def test_frame_thresholds_must_be_configured():
    with pytest.raises(ValueError, match="phone_confidence"):
        FrameThresholds.from_config({"frame": {"gaze_yaw_deg": 30, "gaze_pitch_deg": 20, "phone_confidence": None},
                                     "verify": {"match_threshold": 0.2}})


# ---------- günlük (KVKK) ----------

def test_log_has_only_endpoint_code_and_duration(client, fake, caplog):
    with caplog.at_level(logging.INFO, logger="uvicorn.error"):
        analyze(client, png(faces=2, yaw=50, phone=0.9, sim=0.1))
        analyze(client, "bozuk!")
    messages = [r.getMessage() for r in caplog.records if r.getMessage().startswith("/internal/analyze/frame")]
    assert len(messages) == 2
    assert messages[0].startswith("/internal/analyze/frame OK ")
    assert messages[1].startswith("/internal/analyze/frame INVALID_IMAGE ")
    for m in messages:
        assert m.endswith(" ms") and len(m.split()) == 4  # uç, kod, süre, "ms"; ölçüm ya da görüntü yok
