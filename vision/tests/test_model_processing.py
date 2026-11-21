"""Model katmanının ön/son işleme testleri; model dosyası gerekmez (CI'da çalışır).

Modellerin kendisi yerine sahte bir ONNX oturumu kullanılıyor: girdinin biçimi (renk sırası, ölçek, şekil)
ve çıktının işlenmesi sınanıyor.
"""

import math

import numpy as np
import pytest

from app.models import ModelFilesMissing, load_models, model_files
from app.models.base import as_bgr, check_input_shape
from app.models.face_embedder import (
    ARCFACE_TEMPLATE,
    FaceEmbedder,
    align_face,
    estimate_similarity,
    l2_normalize,
)
from app.models.face_landmarks import (
    IRIS_CENTERS,
    MOUTH_CORNERS,
    NOSE_TIP,
    FaceBox,
    euler_angles,
    face_box,
    five_points,
    sharpness,
)
from app.models.liveness import SCALES, LivenessModel, crop_box, preprocess, softmax
from app.models.phone_detector import (
    CELL_PHONE,
    Letterbox,
    PhoneDetector,
    decode,
    letterbox,
    nms,
)
from app.models.speech_detector import CONTEXT, WINDOW, SpeechDetector, windows


class FakeInput:
    def __init__(self, name, shape):
        self.name, self.shape = name, shape


class FakeSession:
    """onnxruntime.InferenceSession yerine: girdileri kaydeder, sırayla hazır çıktıları döner."""

    def __init__(self, outputs, inputs=None):
        self.outputs = outputs
        self.inputs = list(inputs) if inputs is not None else [FakeInput("x", [1, 3, 112, 112])]
        self.calls = []

    def get_inputs(self):
        return self.inputs

    def run(self, _, feeds):
        self.calls.append({k: np.array(v, copy=True) for k, v in feeds.items()})
        out = self.outputs(feeds) if callable(self.outputs) else self.outputs
        return out


def bare(cls, session, input_name="x"):
    """Dosya yüklemeden model nesnesi."""
    model = cls.__new__(cls)
    model._session = session
    model._input = input_name
    return model


def rotation(yaw, pitch, roll):
    y, p, r = map(math.radians, (yaw, pitch, roll))
    rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    rz = np.array([[math.cos(r), -math.sin(r), 0], [math.sin(r), math.cos(r), 0], [0, 0, 1]])
    return rz @ ry @ rx


# ---------- ortak ----------

def test_missing_files_are_reported_together(tmp_path):
    with pytest.raises(ModelFilesMissing) as e:
        load_models(tmp_path)
    assert sorted(p.name for p in e.value.missing) == sorted(model_files())
    assert set(model_files()) == {"face_landmarker.task", "w600k_mbf.onnx", "MiniFASNetV2.onnx",
                                  "MiniFASNetV1SE.onnx", "yolo11n.onnx", "silero_vad.onnx"}


def test_single_model_checks_its_file(tmp_path):
    with pytest.raises(ModelFilesMissing):
        FaceEmbedder(tmp_path)


def test_input_shape_check_accepts_dynamic_batch_and_rejects_wrong_size():
    assert check_input_shape(FakeSession(None, [FakeInput("in", ["None", 3, 112, 112])]), (1, 3, 112, 112), "m") == "in"
    with pytest.raises(ValueError):
        check_input_shape(FakeSession(None, [FakeInput("in", [1, 3, 80, 80])]), (1, 3, 112, 112), "m")


def test_as_bgr_accepts_gray_and_bgra_rejects_float():
    assert as_bgr(np.zeros((4, 5), np.uint8)).shape == (4, 5, 3)
    assert as_bgr(np.zeros((4, 5, 4), np.uint8)).shape == (4, 5, 3)
    with pytest.raises(ValueError):
        as_bgr(np.zeros((4, 5, 3), np.float32))


# ---------- yüz noktaları ve kafa yönü ----------

def landmarks_478(eye_a, eye_b, nose, mouth_a, mouth_b):
    pts = np.full((478, 2), 50.0, np.float32)
    pts[IRIS_CENTERS[0]], pts[IRIS_CENTERS[1]] = eye_a, eye_b
    pts[NOSE_TIP] = nose
    pts[MOUTH_CORNERS[0]], pts[MOUTH_CORNERS[1]] = mouth_a, mouth_b
    return pts


def test_five_points_are_in_arcface_order_whatever_the_landmark_order():
    # MediaPipe'ın indeks sırası görüntüdeki sağ/sol ile ters olsa da sonuç x'e göre sıralı.
    pts = landmarks_478((70, 40), (30, 40), (50, 60), (65, 80), (35, 80))
    np.testing.assert_allclose(five_points(pts), [[30, 40], [70, 40], [50, 60], [35, 80], [65, 80]])


def test_face_box_is_clipped_to_image():
    pts = np.array([[-5, 10], [50, -3], [120, 70]], np.float32)
    assert face_box(pts, width=100, height=60) == FaceBox(0, 0, 100, 60)
    assert FaceBox(10, 10, 30, 50).min_side == 30


@pytest.mark.parametrize("yaw,pitch,roll", [(0, 0, 0), (35, 0, 0), (-20, 15, 0), (10, -25, 5), (-45, 10, -8)])
def test_euler_angles_recover_rotation(yaw, pitch, roll):
    m = np.eye(4)
    m[:3, :3] = rotation(yaw, pitch, roll) * 7.5  # MediaPipe matrisinde ölçek de olabilir
    np.testing.assert_allclose(euler_angles(m), (yaw, pitch, roll), atol=1e-6)


def test_euler_angles_reject_singular_matrix():
    with pytest.raises(ValueError):
        euler_angles(np.zeros((4, 4)))


def test_sharpness_flat_is_zero_and_texture_is_positive():
    flat = np.full((100, 100, 3), 128, np.uint8)
    board = (np.indices((100, 100)).sum(axis=0) % 2 * 255).astype(np.uint8)
    box = FaceBox(10, 10, 50, 50)
    assert sharpness(flat, box) == 0.0
    assert sharpness(board, box) > 1000
    assert sharpness(flat, FaceBox(200, 200, 10, 10)) == 0.0  # görüntü dışı kutu çökmez


# ---------- ArcFace ----------

def test_similarity_transform_recovers_known_transform():
    angle, scale, t = math.radians(20), 1.3, np.array([12.0, -7.0])
    r = np.array([[math.cos(angle), -math.sin(angle)], [math.sin(angle), math.cos(angle)]])
    src = ARCFACE_TEMPLATE.astype(np.float64)
    dst = scale * src @ r.T + t
    expected = np.hstack([scale * r, t[:, None]])
    np.testing.assert_allclose(estimate_similarity(src, dst), expected, atol=1e-4)


def test_similarity_transform_rejects_degenerate_points():
    with pytest.raises(ValueError):
        estimate_similarity(np.ones((5, 2)), ARCFACE_TEMPLATE)


def test_alignment_of_already_aligned_face_is_identity():
    rng = np.random.default_rng(1)
    img = rng.integers(0, 256, (112, 112, 3), dtype=np.uint8)
    np.testing.assert_array_equal(align_face(img, ARCFACE_TEMPLATE), img)


def test_l2_normalize():
    v = l2_normalize(np.array([3.0, 4.0]))
    np.testing.assert_allclose(v, [0.6, 0.8])
    for bad in (np.zeros(512), np.array([np.nan, 1.0])):
        with pytest.raises(ValueError):
            l2_normalize(bad)


def test_embedder_feeds_rgb_scaled_blob_and_returns_unit_vector():
    session = FakeSession(lambda feeds: [np.arange(1, 513, dtype=np.float32)[None] * 3.0])
    img = np.zeros((112, 112, 3), np.uint8)
    img[:, :] = (0, 128, 255)  # BGR
    vec = bare(FaceEmbedder, session).predict(img, ARCFACE_TEMPLATE)

    blob = session.calls[0]["x"]
    assert blob.shape == (1, 3, 112, 112) and blob.dtype == np.float32
    center = blob[0, :, 56, 56]
    np.testing.assert_allclose(center, [(255 - 127.5) / 127.5, (128 - 127.5) / 127.5, -1.0])  # R, G, B
    assert vec.shape == (512,) and abs(float(np.linalg.norm(vec)) - 1) < 1e-6


# ---------- MiniFASNet ----------

def test_softmax():
    p = softmax(np.array([1.0, 2.0, 3.0]))
    np.testing.assert_allclose(p.sum(), 1.0)
    np.testing.assert_allclose(softmax(np.array([1001.0, 1002.0, 1003.0])), p)  # taşma yok


def test_crop_box_matches_silent_face():
    # Ortada: ölçek kadar büyütülmüş, merkezi aynı (V2 2.7, V1SE 4.0).
    assert crop_box(640, 480, FaceBox(270, 190, 100, 100), 2.7) == (185, 105, 455, 375)
    assert crop_box(640, 480, FaceBox(270, 190, 100, 100), 4.0) == (120, 40, 520, 440)
    # Köşede: görüntünün içine kaydırılır.
    assert crop_box(640, 480, FaceBox(0, 0, 100, 100), 2.7) == (0, 0, 270, 270)
    # Büyük yüz: ölçek görüntüye sığacak kadar küçülür.
    x1, y1, x2, y2 = crop_box(200, 200, FaceBox(50, 50, 100, 100), 4.0)
    assert (x1, y1) == (0, 0) and (x2, y2) == (199, 199)
    with pytest.raises(ValueError):
        crop_box(640, 480, FaceBox(0, 0, 0, 10), 2.7)


def test_liveness_input_is_bgr_0_255_without_normalization():
    img = np.zeros((480, 640, 3), np.uint8)
    img[:, :] = (10, 20, 30)
    blob = preprocess(img, FaceBox(270, 190, 100, 100), 2.7)
    assert blob.shape == (1, 3, 80, 80) and blob.dtype == np.float32
    np.testing.assert_allclose(blob[0, :, 40, 40], [10, 20, 30])


def test_liveness_uses_both_models_with_their_own_scale_and_sums_predictions():
    rng = np.random.default_rng(3)
    img = rng.integers(0, 256, (480, 640, 3), dtype=np.uint8)
    box = FaceBox(270, 190, 100, 100)
    v2 = FakeSession([np.array([[0.0, 2.0, 0.0]], np.float32)])
    v1se = FakeSession([np.array([[1.0, 0.0, 0.0]], np.float32)])
    model = LivenessModel.__new__(LivenessModel)
    model._models = [(v2, "x", 2.7), (v1se, "x", 4.0)]

    result = model.predict(img, box)

    np.testing.assert_array_equal(v2.calls[0]["x"], preprocess(img, box, 2.7))
    np.testing.assert_array_equal(v1se.calls[0]["x"], preprocess(img, box, 4.0))
    p_v2 = math.exp(2) / (math.exp(2) + 2)  # softmax([0, 2, 0])[1]
    p_v1se = 1 / (math.e + 2)               # softmax([1, 0, 0])[1]
    assert result.score == pytest.approx((p_v2 + p_v1se) / 2)
    assert sum(result.probabilities) == pytest.approx(1.0)


def test_liveness_file_names_and_scales():
    assert LivenessModel.files == ("MiniFASNetV2.onnx", "MiniFASNetV1SE.onnx")
    assert SCALES == {"MiniFASNetV2.onnx": 2.7, "MiniFASNetV1SE.onnx": 4.0}


# ---------- YOLO ----------

def test_letterbox_keeps_ratio_and_pads_with_114():
    out, lb = letterbox(np.zeros((480, 640, 3), np.uint8))
    assert out.shape == (640, 640, 3)
    assert (lb.scale, lb.pad_x, lb.pad_y) == (1.0, 0, 80)
    assert (out[0, 0] == 114).all() and (out[320, 320] == 0).all()

    out, lb = letterbox(np.zeros((100, 200, 3), np.uint8))
    assert out.shape == (640, 640, 3) and lb.scale == 3.2 and lb.pad_y == 160


def test_nms_keeps_best_of_overlapping_boxes():
    boxes = np.array([[0, 0, 10, 10], [1, 1, 11, 11], [50, 50, 60, 60]], np.float32)
    scores = np.array([0.6, 0.9, 0.5], np.float32)
    assert nms(boxes, scores, iou_threshold=0.5) == [1, 2]
    assert nms(boxes, scores, iou_threshold=0.9) == [1, 0, 2]


def yolo_output(anchors):
    """anchors: [(cx, cy, w, h, sınıf, skor)] → (1, 84, N)."""
    out = np.zeros((1, 84, len(anchors)), np.float32)
    for i, (cx, cy, w, h, cls, score) in enumerate(anchors):
        out[0, :4, i] = (cx, cy, w, h)
        out[0, 4 + cls, i] = score
    return out


def test_decode_selects_cell_phone_and_maps_back_to_image():
    out = yolo_output([
        (320, 320, 100, 100, CELL_PHONE, 0.80),
        (322, 321, 100, 100, CELL_PHONE, 0.70),  # aynı telefonun ikinci adayı: NMS atar
        (100, 200, 40, 40, 0, 0.95),             # kişi: dikkate alınmaz
        (500, 500, 20, 20, CELL_PHONE, 0.10),    # eşik altı
    ])
    result = decode(out, Letterbox(scale=1.0, pad_x=0, pad_y=80), image_shape=(480, 640))
    assert result.confidence == pytest.approx(0.80)
    assert len(result.detections) == 1
    det = result.detections[0]
    assert det.score == pytest.approx(0.80)
    np.testing.assert_allclose(det.box, (270, 190, 370, 290))  # dolgu (80 piksel) çıkarıldı


def test_decode_reports_low_confidence_without_detections():
    result = decode(yolo_output([(320, 320, 50, 50, CELL_PHONE, 0.08)]), Letterbox(1.0, 0, 80), (480, 640))
    assert result.confidence == pytest.approx(0.08) and result.detections == ()


def test_phone_detector_feeds_rgb_divided_by_255():
    session = FakeSession([yolo_output([(320, 320, 50, 50, CELL_PHONE, 0.5)])])
    img = np.zeros((640, 640, 3), np.uint8)
    img[:, :] = (0, 0, 255)  # BGR kırmızı
    bare(PhoneDetector, session).predict(img)
    blob = session.calls[0]["x"]
    assert blob.shape == (1, 3, 640, 640)
    np.testing.assert_allclose(blob[0, :, 0, 0], [1.0, 0.0, 0.0])  # R, G, B


# ---------- Silero VAD ----------

def test_windows_pad_last_window_and_validate_input():
    w = windows(np.ones(16000, np.float32))
    assert w.shape == (32, WINDOW)
    assert w[-1, 16000 - 31 * WINDOW:].sum() == 0 and w[-1, :16000 - 31 * WINDOW].sum() > 0
    assert windows(np.zeros(0, np.float32)).shape == (0, WINDOW)
    with pytest.raises(ValueError):
        windows(np.zeros(100, np.int16))
    with pytest.raises(ValueError):
        windows(np.zeros((2, 100), np.float32))


def test_speech_detector_passes_context_and_state_between_windows():
    def respond(feeds):
        state = feeds["state"] + 1  # her çağrıda değişen durum
        return [np.array([[0.1 * len(session.calls)]], np.float32), state]

    session = FakeSession(respond)
    audio = np.arange(3 * WINDOW, dtype=np.float32) / 10000
    result = bare(SpeechDetector, session).predict(audio)

    assert len(session.calls) == 3
    first, second = session.calls[0], session.calls[1]
    assert first["input"].shape == (1, CONTEXT + WINDOW)
    assert not first["input"][0, :CONTEXT].any()                      # ilk pencerede bağlam sıfır
    np.testing.assert_array_equal(second["input"][0, :CONTEXT], audio[WINDOW - CONTEXT:WINDOW])
    assert first["state"].shape == (2, 1, 128) and not first["state"].any()
    assert (second["state"] == 1).all()                               # durum bir sonraki pencereye geçiyor
    assert first["sr"].dtype == np.int64 and int(first["sr"]) == 16000
    np.testing.assert_allclose(result.probabilities, [0.1, 0.2, 0.3], rtol=1e-6)
    assert result.duration_sec == pytest.approx(3 * WINDOW / 16000)
    assert result.max_probability == pytest.approx(0.3)
