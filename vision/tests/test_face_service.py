"""D2: /internal/face/enroll ve /internal/face/verify kararları, sahte modellerle (app.state.models).

Sahte modeller sonucu görüntünün kendisinden okur, böylece her kare kendi senaryosunu taşır:
    piksel (0, 0) = (yüz sayısı, canlılık × 250, benzerlik × 100 + 100)
    piksel (0, 1) = (yüz kutusunun kenarı, 0, 0)
Kutu (BOX_XY, BOX_XY) köşesinde, 0. satırın dışında; içi gürültü (net) ya da düz (bulanık) doldurulur.
PNG kayıpsız olduğu için değerler aynen geri okunur. Netlik ve görüntü çözme gerçek kodla hesaplanır.
"""

import base64
import json
import logging
import struct
from types import SimpleNamespace

import cv2
import numpy as np
import pytest
from conftest import KEY

from app.config import get_settings
from app.face_service import QUALITY_FULL_AT, Thresholds, quality_score
from app.images import (
    MAX_FRAME_B64,
    MAX_PHOTO_B64,
    MAX_SIDE_PX,
    decode_bytes,
    image_size,
)
from app.models.face_landmarks import Face, FaceBox, FaceLandmarksResult

TH = {"min_face_px": 80, "min_sharpness": 20.0, "match_threshold": 0.4, "liveness_threshold": 0.5}
BOX_XY = 20
REASONS = {"LIVENESS_FAILED", "IDENTITY_MISMATCH", "NO_FACE", "MULTIPLE_FACES"}
E0 = [1.0] + [0.0] * 511  # sahte vektörlerin karşılaştırıldığı referans


# ---------- sahte modeller ----------

class FakeFace:
    def __init__(self):
        self.calls = 0

    def predict(self, image):
        self.calls += 1
        count, side = int(image[0, 0, 0]), int(image[0, 1, 0])
        box = FaceBox(BOX_XY, BOX_XY, side, side)
        face = Face(box, np.zeros((5, 2), np.float32), 0.0, 0.0, 0.0)
        return FaceLandmarksResult(tuple(face for _ in range(count)))


class FakeLiveness:
    def __init__(self):
        self.calls = 0

    def predict(self, image, box):
        self.calls += 1
        return SimpleNamespace(score=int(image[0, 0, 1]) / 250)


class FakeEmbedder:
    """E0 ile benzerliği pikselde yazan birim vektör: [s, √(1 − s²), 0, …]."""

    def __init__(self):
        self.calls = 0

    def predict(self, image, keypoints):
        self.calls += 1
        s = (int(image[0, 0, 2]) - 100) / 100
        v = np.zeros(512, np.float32)
        v[0], v[1] = s, np.sqrt(max(0.0, 1 - s * s))
        return v


def png(faces=1, live=0.9, sim=0.9, side=100, sharp=True) -> str:
    size = BOX_XY + max(side, 1) + 10
    rng = np.random.default_rng(0)
    img = rng.integers(0, 256, (size, size, 3), dtype=np.uint8) if sharp else np.full((size, size, 3), 128, np.uint8)
    img[0, 0] = (faces, round(live * 250), round(sim * 100 + 100))
    img[0, 1] = (side, 0, 0)
    ok, data = cv2.imencode(".png", img)
    assert ok
    return base64.b64encode(data.tobytes()).decode()


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


def encode(ext: str, img: np.ndarray) -> bytes:
    ok, data = cv2.imencode(ext, img)
    assert ok
    return data.tobytes()


@pytest.fixture
def fake(client, monkeypatch):
    settings = get_settings()
    for key in ("min_face_px", "min_sharpness"):
        monkeypatch.setitem(settings.thresholds["enroll"], key, TH[key])
    for key in ("match_threshold", "liveness_threshold"):
        monkeypatch.setitem(settings.thresholds["verify"], key, TH[key])
    models = SimpleNamespace(face=FakeFace(), liveness=FakeLiveness(), embedder=FakeEmbedder())
    client.app.state.models = models
    return models


def enroll(client, image):
    return client.post("/internal/face/enroll", json={"image": image}, headers=KEY)


def verify(client, frames, reference=E0):
    return client.post("/internal/face/verify", json={"frames": frames, "referenceVector": reference}, headers=KEY)


def error_code(r) -> str:
    return r.json()["error"]["code"]


def assert_contract(body):
    """A3 istemcisinin kuralları: skorlar aralıkta sayı, reason sözleşmedeki kodlardan, passed ise null."""
    assert isinstance(body["livenessScore"], float) and 0.0 <= body["livenessScore"] <= 1.0
    assert isinstance(body["similarity"], float) and -1.0 <= body["similarity"] <= 1.0
    assert isinstance(body["passed"], bool) and isinstance(body["framesUsed"], int)
    if body["passed"]:
        assert body["reason"] is None
    else:
        assert body["reason"] in REASONS


# ---------- enroll ----------

@pytest.mark.parametrize("image", [
    "bu base64 değil!",
    b64(b"merhaba dunya"),                                         # base64 ama görüntü değil
    b64(encode(".bmp", np.zeros((50, 50, 3), np.uint8))),          # görüntü ama desteklenen biçim değil
    b64(encode(".jpg", np.zeros((50, 50, 3), np.uint8))[:200]),    # başlık sağlam, veri kesik
    b64(b"\x89PNG\r\n\x1a\n" + b"\x00" * 30),                      # PNG imzası, bozuk başlık
    b64(encode(".png", np.zeros((8, MAX_SIDE_PX + 1, 3), np.uint8))),  # kenar sınırı aşılıyor
])
def test_enroll_invalid_image(client, fake, image):
    r = enroll(client, image)
    assert r.status_code == 422 and error_code(r) == "INVALID_IMAGE"
    assert fake.face.calls == 0


def test_enroll_image_at_side_limit_is_decoded(client, fake):
    img = np.zeros((8, MAX_SIDE_PX, 3), np.uint8)  # sınırda: çözülür, yüz yok
    r = enroll(client, b64(encode(".png", img)))
    assert error_code(r) == "NO_FACE"


@pytest.mark.parametrize(("kwargs", "code"), [
    ({"faces": 0}, "NO_FACE"),
    ({"faces": 2}, "MULTIPLE_FACES"),
    ({"faces": 3, "side": 40}, "MULTIPLE_FACES"),  # sıra: önce yüz sayısı
    ({"side": 79}, "FACE_TOO_SMALL"),
    ({"side": 79, "sharp": False}, "FACE_TOO_SMALL"),  # sıra: önce boyut, sonra netlik
    ({"sharp": False}, "IMAGE_TOO_BLURRY"),
])
def test_enroll_rejections(client, fake, kwargs, code):
    r = enroll(client, png(**kwargs))
    assert r.status_code == 422 and error_code(r) == code
    assert fake.embedder.calls == 0


def test_enroll_success(client, fake):
    r = enroll(client, png(side=100))
    assert r.status_code == 200
    body = r.json()
    vector = np.array(body["vector"])
    assert vector.shape == (512,) and np.all(np.isfinite(vector))
    assert abs(np.linalg.norm(vector) - 1) < 0.01
    assert body["faceBox"] == {"x": BOX_XY, "y": BOX_XY, "width": 100, "height": 100}
    assert 0.5 <= body["qualityScore"] <= 1.0


def test_enroll_at_exact_thresholds_is_accepted(client, fake):
    assert enroll(client, png(side=TH["min_face_px"])).status_code == 200


def test_enroll_accepts_jpeg_and_webp(client, fake):
    img = np.random.default_rng(1).integers(0, 256, (130, 130, 3), dtype=np.uint8)
    img[:16, :16] = 0  # sahte modelde "0 yüz" (JPEG bloğu bütün siyah kalsın diye 16x16)
    for ext in (".jpg", ".webp"):
        r = enroll(client, b64(encode(ext, img)))
        assert r.status_code == 422 and error_code(r) == "NO_FACE", ext  # çözüldü, sahte modele ulaştı


def test_enroll_base64_size_limit(client, fake):
    r = enroll(client, "A" * (MAX_PHOTO_B64 + 4))
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_photo_limits_match_api():
    # api: ImageFile.MaxPhotoBytes = 5 MiB, MaxFrameBytes = 1 MiB; base64 4 * ceil(n / 3)
    assert MAX_PHOTO_B64 == len(base64.b64encode(b"\0" * 5 * 1024 * 1024))
    assert MAX_FRAME_B64 == len(base64.b64encode(b"\0" * 1024 * 1024))


def test_quality_score_definition():
    th = Thresholds(**TH)
    assert quality_score(80, 20.0, th) == 0.5                     # iki ölçüt de tam eşikte
    assert quality_score(160, 40.0, th) == 1.0                    # iki ölçüt de eşiğin 2 katı
    assert quality_score(800, 400.0, th) == 1.0                   # 1'de kesilir
    assert quality_score(160, 30.0, th) == pytest.approx(0.75)    # zayıf olan belirler: 30/20 / 2
    assert QUALITY_FULL_AT == 2.0


def test_thresholds_must_be_configured():
    with pytest.raises(ValueError, match="match_threshold"):
        Thresholds.from_config({"enroll": {"min_face_px": 1, "min_sharpness": 1},
                                "verify": {"match_threshold": None, "liveness_threshold": 0.5}})


def test_config_yaml_has_all_d2_thresholds():
    th = Thresholds.from_config(get_settings().thresholds)
    assert th.min_face_px > 0 and th.min_sharpness > 0
    assert -1 < th.match_threshold < 1 and 0 < th.liveness_threshold < 1


# ---------- görüntü çözme ----------

def exif_orientation(orientation: int) -> bytes:
    """Yalnızca Orientation (0x0112) etiketi olan APP1 Exif bölümü."""
    tiff = (b"II*\x00" + struct.pack("<I", 8) + struct.pack("<H", 1)
            + struct.pack("<HHIHH", 0x0112, 3, 1, orientation, 0) + struct.pack("<I", 0))
    payload = b"Exif\x00\x00" + tiff
    return b"\xff\xe1" + struct.pack(">H", len(payload) + 2) + payload


def test_exif_orientation_is_applied():
    # Telefon fotoğrafı: pikseller yan kaydedilmiş, EXIF 6 = "göstermek için 90° saat yönünde döndür".
    upright = np.zeros((80, 40, 3), np.uint8)
    upright[:20] = 255                                    # üst kısım beyaz
    stored = cv2.rotate(upright, cv2.ROTATE_90_COUNTERCLOCKWISE)  # (40, 80): beyaz sol tarafta
    jpg = encode(".jpg", stored)
    decoded = decode_bytes(jpg[:2] + exif_orientation(6) + jpg[2:])
    assert decoded.shape == (80, 40, 3)
    assert decoded[:15].mean() > 200 and decoded[-40:].mean() < 50


@pytest.mark.parametrize("ext", [".png", ".jpg", ".webp"])
@pytest.mark.parametrize("shape", [(37, 91, 3), (91, 37, 4)])
def test_image_size_reads_header(ext, shape):
    img = np.random.default_rng(2).integers(0, 256, shape, dtype=np.uint8)
    if ext == ".jpg":
        img = img[:, :, :3]
    params = [cv2.IMWRITE_WEBP_QUALITY, 101] if ext == ".webp" and shape[2] == 4 else []  # 101: kayıpsız (VP8L)
    ok, data = cv2.imencode(ext, img, params)
    assert ok
    assert image_size(data.tobytes()) == (shape[1], shape[0])


def test_image_size_reads_extended_webp_header():
    # VP8X: 24 bit (en − 1), 24 bit (boy − 1)
    header = b"RIFF" + b"\0" * 4 + b"WEBPVP8X" + b"\0" * 8 + (6999).to_bytes(3, "little") + (9).to_bytes(3, "little")
    assert image_size(header) == (7000, 10)


def test_image_size_rejects_other_formats():
    assert image_size(b"GIF89a" + b"\0" * 20) is None
    assert image_size(encode(".bmp", np.zeros((4, 4, 3), np.uint8))) is None


# ---------- verify ----------

def test_verify_passes(client, fake):
    r = verify(client, [png(live=0.9, sim=0.8), png(live=0.7, sim=0.6), png(live=0.8, sim=0.7)])
    body = r.json()
    assert r.status_code == 200
    assert_contract(body)
    assert body["passed"] is True and body["reason"] is None and body["framesUsed"] == 3
    assert body["livenessScore"] == pytest.approx(0.8)  # ortalama
    assert body["similarity"] == pytest.approx(0.7, abs=1e-6)


@pytest.mark.parametrize(("live", "sim", "reason"), [
    (0.3, 0.9, "LIVENESS_FAILED"),
    (0.3, 0.1, "LIVENESS_FAILED"),     # ikisi de kalırsa sahte yüz önce gelir
    (0.9, 0.1, "IDENTITY_MISMATCH"),
    (0.9, -0.5, "IDENTITY_MISMATCH"),
])
def test_verify_failure_reasons(client, fake, live, sim, reason):
    body = verify(client, [png(live=live, sim=sim)] * 3).json()
    assert_contract(body)
    assert body["passed"] is False and body["reason"] == reason and body["framesUsed"] == 3


def test_similarity_is_computed_even_when_liveness_fails(client, fake):
    body = verify(client, [png(live=0.2, sim=0.95)] * 3).json()
    assert body["reason"] == "LIVENESS_FAILED"
    assert body["livenessScore"] == pytest.approx(0.2)
    assert body["similarity"] == pytest.approx(0.95, abs=1e-6)
    assert fake.embedder.calls == 3


def test_thresholds_are_inclusive(client, fake):
    body = verify(client, [png(live=TH["liveness_threshold"], sim=TH["match_threshold"])] * 3).json()
    assert body["passed"] is True


def test_multiple_faces_in_any_frame_wins(client, fake):
    frames = [png(), png(faces=2), png()]
    body = verify(client, frames).json()
    assert_contract(body)
    assert body["reason"] == "MULTIPLE_FACES" and body["passed"] is False
    assert body["livenessScore"] == 0.0 and body["similarity"] == 0.0
    assert body["framesUsed"] == 2
    assert fake.liveness.calls == 0 and fake.embedder.calls == 0


def test_multiple_faces_wins_over_no_face(client, fake):
    body = verify(client, [png(faces=0), png(faces=0), png(faces=2)]).json()
    assert body["reason"] == "MULTIPLE_FACES" and body["framesUsed"] == 0


@pytest.mark.parametrize(("usable", "total", "reason"), [
    (0, 3, "NO_FACE"),
    (1, 3, "NO_FACE"),   # 1 < 1,5
    (2, 3, None),
    (2, 4, None),        # tam yarısı yeterli
    (2, 5, "NO_FACE"),   # 2 < 2,5
    (3, 5, None),
])
def test_less_than_half_usable_frames_is_no_face(client, fake, usable, total, reason):
    frames = [png() for _ in range(usable)] + [png(faces=0) for _ in range(total - usable)]
    body = verify(client, frames).json()
    assert_contract(body)
    assert body["reason"] == reason and body["framesUsed"] == usable
    if reason == "NO_FACE":
        assert body["livenessScore"] == 0.0 and body["similarity"] == 0.0


def test_mixed_frames_average_only_usable_ones(client, fake):
    frames = [png(live=0.9, sim=0.9), png(faces=0, live=0.0, sim=-1.0), png(live=0.7, sim=0.5)]
    body = verify(client, frames).json()
    assert body["framesUsed"] == 2 and body["passed"] is True
    assert body["livenessScore"] == pytest.approx(0.8)
    assert body["similarity"] == pytest.approx(0.7, abs=1e-6)
    assert fake.liveness.calls == 2 and fake.embedder.calls == 2


def test_undecodable_frame_rejects_whole_request(client, fake):
    r = verify(client, [png(), b64(b"bozuk"), png()])
    assert r.status_code == 422 and error_code(r) == "INVALID_IMAGE"
    assert fake.face.calls == 0


def test_verify_accepts_png_and_jpeg_frames(client, fake):
    img = np.zeros((60, 60, 3), np.uint8)  # siyah: sahte modelde "0 yüz"
    frames = [b64(encode(".png", img)), b64(encode(".jpg", img)), b64(encode(".webp", img))]
    r = verify(client, frames)
    assert r.status_code == 200 and r.json()["reason"] == "NO_FACE"  # üçü de çözüldü, modele ulaştı


@pytest.mark.parametrize("reference", [
    [0.0] * 512,
    [1e-9] * 512,
    [1.0] * 10,
    [1.0] * 513,
])
def test_invalid_reference_vector(client, fake, reference):
    r = verify(client, [png()] * 3, reference)
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_non_finite_reference_vector(client, fake):
    # JSON standardında NaN yok; Python istemcisi yine de "NaN" yazabiliyor. Her iki durumda VALIDATION_ERROR.
    frames = json.dumps([png()] * 3)
    body = f'{{"frames": {frames}, "referenceVector": [NaN{", 0.0" * 511}]}}'
    r = client.post("/internal/face/verify", content=body, headers={**KEY, "Content-Type": "application/json"})
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_reference_vector_is_renormalized(client, fake):
    scaled = [3.0] + [0.0] * 511
    body = verify(client, [png(sim=0.6)] * 3, scaled).json()
    assert body["similarity"] == pytest.approx(0.6, abs=1e-6)


@pytest.mark.parametrize("count", [2, 6])
def test_frame_count_must_be_3_to_5(client, fake, count):
    r = verify(client, [png()] * count)
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_frame_base64_size_limit(client, fake):
    r = verify(client, [png(), png(), "A" * (MAX_FRAME_B64 + 4)])
    assert r.status_code == 422 and error_code(r) == "VALIDATION_ERROR"


def test_scores_always_in_contract_range(client, fake):
    rng = np.random.default_rng(3)
    for _ in range(25):
        n = int(rng.integers(3, 6))
        frames = [png(faces=int(rng.choice([0, 1, 1, 1, 2])), live=float(rng.uniform(0, 1)),
                      sim=float(rng.uniform(-1, 1))) for _ in range(n)]
        r = verify(client, frames)
        assert r.status_code == 200
        assert_contract(r.json())


# ---------- günlük (KVKK) ----------

def test_log_has_only_endpoint_code_and_duration(client, fake, caplog):
    with caplog.at_level(logging.INFO, logger="uvicorn.error"):
        verify(client, [png(live=0.3, sim=0.95)] * 3)
        enroll(client, png(faces=0))
        enroll(client, png())
    messages = [r.getMessage() for r in caplog.records if r.message.startswith("/internal/face")]
    assert len(messages) == 3
    assert messages[0].startswith("/internal/face/verify LIVENESS_FAILED ")
    assert messages[1].startswith("/internal/face/enroll NO_FACE ")
    assert messages[2].startswith("/internal/face/enroll OK ")
    for m in messages:
        assert m.endswith(" ms") and len(m.split()) == 4  # uç, kod, süre, "ms"; skor ya da vektör yok
