"""Elle deneme aracı (D4): kameradan saniyede bir kare, sınav sırası kamera analizi.

    cd vision
    .venv\\Scripts\\python scripts/try_frame.py
    .venv\\Scripts\\python scripts/try_frame.py --preview --photo C:\\yol\\kendi-fotografin.jpg --camera 1

Servisle aynı kod çalışır (app.frame_service, config.yaml eşikleri); uvicorn ve servis anahtarı gerekmez.
Kareler S4'teki önyüz gibi hazırlanır: saniyede bir, en fazla 640 px genişlik, JPEG kalite 0,8. Zamanlama api'deki
gibi: face + pose her kare, phone 3 sn'de bir, identity (yalnızca --photo verilirse) 60 sn'de bir; --every-task
ile her karede bütün görevler çalışır.

Olaylar yalnızca ekranda gösterim için simüle edilir; asıl kurallar api'de (S-20, docs/contracts.md 5. bölüm):
art arda 3 ölçüm yüz yok → FACE_MISSING, art arda 5 ölçüm gazeAway → GAZE_AWAY, 2+ yüz → MULTIPLE_FACES,
telefon → PHONE, kimlik eşleşmezse IDENTITY_MISMATCH.

Yaw/pitch işaretini denemek için: başınızı kendi sağınıza, soluna, yukarı ve aşağı çevirip konsoldaki değerlere
bakın. Sonuçlar her ölçümden hemen sonra konsola yazılır.

--preview: kamera görüntüsü üzerinde en büyük yüzün kutusu, telefon kutusu (son telefon kontrolünde bulunduysa) ve
köşede son ölçüm ile simüle edilen olay. Pencere ESC ya da q ile kapanır (program da biter). Pencere yokken Ctrl+C.
Hiçbir görüntü diske yazılmaz: kareler yalnızca bellekte JPEG'e çevrilir.
"""

from __future__ import annotations

import argparse
import base64
import sys
import time
from pathlib import Path

import cv2
import numpy as np

VISION_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(VISION_DIR))

from app import face_service, frame_service
from app.config import get_settings
from app.errors import ServiceError
from app.models import load_models

MAX_WIDTH = 640    # S4 mediaMonitor: en fazla 640 px genişlik
JPEG_QUALITY = 80  # S4: canvas.toBlob(..., 'image/jpeg', 0.8)
PHONE_EVERY_S = 3
IDENTITY_EVERY_S = 60
FACE_MISSING_AFTER = 3  # art arda ölçüm (contracts.md 5. bölüm)
GAZE_AWAY_AFTER = 5
EVENT_SHOW_S = 2.5      # önizlemede olay adının ekranda kalma süresi
WINDOW = "AI-Guard try_frame (ESC: cik)"

# Önizleme renkleri (BGR)
GREEN, RED, ORANGE, WHITE, BLACK = (0, 200, 0), (0, 0, 255), (0, 140, 255), (255, 255, 255), (0, 0, 0)


def shrink(image: np.ndarray) -> np.ndarray:
    """S4'teki gibi genişlik en fazla 640 px."""
    h, w = image.shape[:2]
    if w <= MAX_WIDTH:
        return image
    return cv2.resize(image, (MAX_WIDTH, round(h * MAX_WIDTH / w)), interpolation=cv2.INTER_AREA)


def to_frame(image: np.ndarray) -> str:
    """JPEG 0,8, base64. Kodlama bellekte (imencode); dosya yok."""
    ok, jpg = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, JPEG_QUALITY])
    if not ok:
        raise RuntimeError("Kare JPEG'e çevrilemedi.")
    return base64.b64encode(jpg.tobytes()).decode()


class _Recording:
    """Bir modeli sarar, son sonucunu saklar. Önizleme kutuları modeli ikinci kez çalıştırmadan buradan alır."""

    def __init__(self, model) -> None:
        self._model = model
        self.last = None

    def predict(self, *args, **kwargs):
        self.last = self._model.predict(*args, **kwargs)
        return self.last


class RecordingModels:
    """frame_service.analyze'ın kullandığı modellerin kaydedici sürümü (yalnızca bu araçta; servis kodu aynı)."""

    def __init__(self, models) -> None:
        self.face = _Recording(models.face)
        self.phone = _Recording(models.phone)
        self.embedder = models.embedder

    def reset(self) -> None:
        self.face.last = self.phone.last = None


class EventSimulator:
    """api'nin uygulayacağı olay kurallarının yalnızca gösterim amaçlı kopyası (api kodu değil)."""

    def __init__(self) -> None:
        self.no_face_run = 0
        self.gaze_run = 0

    def update(self, r: frame_service.FrameResult) -> list[str]:
        events = []
        if r.face_count is not None:
            self.no_face_run = self.no_face_run + 1 if r.face_count == 0 else 0
            if self.no_face_run == FACE_MISSING_AFTER:
                events.append("FACE_MISSING")
            if r.face_count >= 2:
                events.append("MULTIPLE_FACES")
        # Yüz yoksa gazeAway null; seri bozulur (o süre FACE_MISSING'e sayılır). Olay seri başına bir kez yazılır.
        self.gaze_run = self.gaze_run + 1 if r.gaze_away else 0
        if self.gaze_run == GAZE_AWAY_AFTER:
            events.append("GAZE_AWAY")
        if r.phone is not None and r.phone.detected:
            events.append("PHONE")
        if r.identity is not None and not r.identity.matched:
            events.append("IDENTITY_MISMATCH")
        return events


def fmt(value: float | None, digits: int = 1) -> str:
    return "-" if value is None else f"{value:+.{digits}f}"


def describe(r: frame_service.FrameResult, th: frame_service.FrameThresholds) -> str:
    parts = [f"faceCount {r.face_count}",
             f"yaw {fmt(r.yaw)} pitch {fmt(r.pitch)} (eşik ±{th.gaze_yaw_deg:g}/±{th.gaze_pitch_deg:g})",
             f"gazeAway {r.gaze_away}"]
    if r.phone is not None:
        parts.append(f"phone {r.phone.detected} ({r.phone.confidence:.3f}, eşik {th.phone_confidence:g})")
    if r.identity is not None:
        parts.append(f"identity {r.identity.matched} ({r.identity.similarity:.3f}, eşik {th.match_threshold:g})")
    parts.append(f"{r.processing_ms} ms")
    return " | ".join(parts)


class Overlay:
    """Önizlemede gösterilen son durum. Yazılar ASCII: cv2.putText Türkçe karakter çizemiyor."""

    def __init__(self) -> None:
        self.face_box = None       # (x, y, w, h), analiz edilen kare pikseli
        self.phone_box = None      # (x1, y1, x2, y2) ve detected
        self.phone_detected = False
        self.phone_conf = None
        self.result: frame_service.FrameResult | None = None
        self.events: list[str] = []
        self.events_until = 0.0

    def update(self, rec: RecordingModels, r: frame_service.FrameResult, events: list[str], now: float) -> None:
        self.result = r
        faces = rec.face.last
        largest = faces.largest if faces is not None else None
        self.face_box = None if largest is None else (largest.box.x, largest.box.y, largest.box.width,
                                                      largest.box.height)
        if r.phone is not None:  # telefon 3 sn'de bir; arada son kontrolün sonucu gösterilir
            detections = rec.phone.last.detections if rec.phone.last is not None else ()
            self.phone_box = detections[0].box if detections else None
            self.phone_detected, self.phone_conf = r.phone.detected, r.phone.confidence
        if events:
            self.events, self.events_until = events, now + EVENT_SHOW_S

    def draw(self, image: np.ndarray, now: float) -> np.ndarray:
        view = image.copy()
        r = self.result
        if self.face_box is not None:
            x, y, w, h = self.face_box
            color = RED if r is not None and r.gaze_away else GREEN
            cv2.rectangle(view, (x, y), (x + w, y + h), color, 2)
        if self.phone_box is not None:
            x1, y1, x2, y2 = (int(v) for v in self.phone_box)
            color = RED if self.phone_detected else ORANGE
            cv2.rectangle(view, (x1, y1), (x2, y2), color, 2)
            cv2.putText(view, f"phone {self.phone_conf:.2f}", (x1, max(y1 - 6, 12)),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, color, 1, cv2.LINE_AA)

        lines = []
        if r is not None:
            lines = [f"faceCount {r.face_count}",
                     f"yaw {fmt(r.yaw)}  pitch {fmt(r.pitch)}",
                     f"gazeAway {r.gaze_away}",
                     f"phone {'-' if self.phone_conf is None else f'{self.phone_conf:.2f}'}"
                     f" {'DETECTED' if self.phone_detected else ''}".rstrip()]
            if r.identity is not None:
                lines.append(f"identity {r.identity.similarity:.2f} {'ok' if r.identity.matched else 'MISMATCH'}")
            lines.append(f"{r.processing_ms} ms")
        cv2.rectangle(view, (0, 0), (250, 12 + 20 * len(lines)), BLACK, -1)
        for i, line in enumerate(lines):
            cv2.putText(view, line, (8, 22 + 20 * i), cv2.FONT_HERSHEY_SIMPLEX, 0.5, WHITE, 1, cv2.LINE_AA)
        if now < self.events_until:
            text = " ".join(self.events)
            (tw, th), _ = cv2.getTextSize(text, cv2.FONT_HERSHEY_SIMPLEX, 0.9, 2)
            y = view.shape[0] - 16
            cv2.rectangle(view, (0, y - th - 12), (tw + 16, view.shape[0]), RED, -1)
            cv2.putText(view, text, (8, y), cv2.FONT_HERSHEY_SIMPLEX, 0.9, WHITE, 2, cv2.LINE_AA)
        return view


def open_camera(index: int) -> cv2.VideoCapture:
    # Windows'ta DirectShow arka ucu kamerayı varsayılan MSMF'den çok daha hızlı açıyor.
    backend = cv2.CAP_DSHOW if sys.platform == "win32" else cv2.CAP_ANY
    camera = cv2.VideoCapture(index, backend)
    if not camera.isOpened():
        raise SystemExit(f"Kamera {index} açılamadı.")
    return camera


def main() -> int:
    # --help de Türkçe karakter ve ok yazıyor; Windows konsolunun kod sayfasına bırakılmasın.
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", line_buffering=True)  # her ölçüm hemen görünsün
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--photo", type=Path, help="kayıt fotoğrafı; verilirse identity görevi de çalışır")
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--interval-s", type=float, default=1.0, help="ölçüm aralığı (S4: 1 sn)")
    parser.add_argument("--every-task", action="store_true", help="her karede bütün görevler")
    parser.add_argument("--preview", action="store_true", help="kutulu ve yazılı önizleme penceresi aç")
    parser.add_argument("--model-dir", type=Path, default=VISION_DIR / "models")
    args = parser.parse_args()

    config = get_settings().thresholds
    th = frame_service.FrameThresholds.from_config(config)
    models = load_models(args.model_dir)
    rec = RecordingModels(models)

    reference = None
    if args.photo:
        try:
            enrolled = face_service.enroll(models, base64.b64encode(args.photo.read_bytes()).decode(),
                                           face_service.Thresholds.from_config(config))
        except ServiceError as e:
            print(f"Kayıt reddedildi: {e.code} ({e.message})")
            return 1
        reference = enrolled.vector.astype(np.float32)
        print(f"Kayıt: qualityScore {enrolled.quality_score} | yüz {enrolled.face.box.width}x"
              f"{enrolled.face.box.height} px")

    camera = open_camera(args.camera)
    simulator = EventSimulator()
    overlay = Overlay()
    started = time.monotonic()
    last_phone = last_identity = None
    next_at = started
    n = 0
    print("Ölçüm başladı. Yaw/pitch işareti için başınızı sağa, sola, yukarı, aşağı çevirin.")
    try:
        while True:
            ok, image = camera.read()  # kamera tamponu boşalsın, önizleme akıcı olsun
            if not ok:
                print("Kameradan kare alınamadı.")
                return 1
            image = shrink(image)  # önizleme de analiz edilen boyutta: kutular aynı koordinatta
            now = time.monotonic()
            if now >= next_at:
                next_at = now + args.interval_s
                tasks = {"face", "pose"}
                if args.every_task or last_phone is None or now - last_phone >= PHONE_EVERY_S:
                    tasks.add("phone")
                    last_phone = now
                if reference is not None and (args.every_task or last_identity is None
                                              or now - last_identity >= IDENTITY_EVERY_S):
                    tasks.add("identity")
                    last_identity = now

                n += 1
                rec.reset()
                try:
                    r = frame_service.analyze(rec, to_frame(image), tasks, reference, th)
                except ServiceError as e:
                    print(f"[{n:4d}] HTTP {e.status} {e.code}")
                    continue
                line = f"[{n:4d} {now - started:6.1f} sn] {describe(r, th)}"
                if r.evidence_frame is not None:
                    line += " | kanıt karesi"
                print(line)
                events = simulator.update(r)
                for event in events:
                    print(f"       >>> olay (simülasyon): {event}")
                overlay.update(rec, r, events, now)

            if args.preview:
                cv2.imshow(WINDOW, overlay.draw(image, now))
                if (cv2.waitKey(1) & 0xFF) in (27, ord("q")):
                    return 0
    except KeyboardInterrupt:
        return 0
    finally:
        camera.release()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    raise SystemExit(main())
