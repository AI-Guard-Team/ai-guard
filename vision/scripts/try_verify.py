"""Elle deneme aracı (D2 kabul kriteri): fotoğraftan kayıt, kameradan sınav başı doğrulama.

    cd vision
    python scripts/try_verify.py --photo C:\\yol\\kendi-fotografin.jpg
    python scripts/try_verify.py --photo foto.jpg --camera 1 --no-preview

Servisle aynı kod çalışır (app.face_service, config.yaml eşikleri); uvicorn ve servis anahtarı gerekmez.
Kareler S2'deki önyüz gibi alınır: kameranın varsayılan çözünürlüğü, 3 kare, ~150 ms arayla, JPEG kalite 0,92.
Hiçbir görüntü diske yazılmaz: fotoğraf okunur, kareler yalnızca bellekte JPEG'e çevrilir.

Önizleme penceresinde BOŞLUK kareleri alıp doğrular (tekrar tekrar denenebilir: önce kendiniz, sonra telefon
ekranındaki fotoğrafınız), ESC ya da q çıkar. --no-preview ile kamera ısındıktan sonra bir kez dener.
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

from app import face_service
from app.config import get_settings
from app.errors import ServiceError
from app.models import load_models

JPEG_QUALITY = 92  # S2: canvas.toBlob(..., 'image/jpeg', 0.92)
WINDOW = "AI-Guard try_verify (BOSLUK: dogrula, ESC: cik)"


def capture_frames(camera: cv2.VideoCapture, count: int, interval_ms: int) -> list[str]:
    """Kameradan `count` kare, base64 JPEG. Kodlama bellekte (imencode); dosya yok."""
    frames = []
    for i in range(count):
        ok, frame = camera.read()
        if not ok:
            raise RuntimeError("Kameradan kare alınamadı.")
        ok, jpg = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, JPEG_QUALITY])
        if not ok:
            raise RuntimeError("Kare JPEG'e çevrilemedi.")
        frames.append(base64.b64encode(jpg.tobytes()).decode())
        if i < count - 1:
            time.sleep(interval_ms / 1000)
    return frames


def run_verify(models, camera, reference, th, args) -> None:
    frames = capture_frames(camera, args.frames, args.interval_ms)
    start = time.perf_counter()
    try:
        r = face_service.verify(models, frames, reference, th)
    except ServiceError as e:
        print(f"  doğrulama: HTTP {e.status} {e.code}")
        return
    ms = (time.perf_counter() - start) * 1000
    print(f"  livenessScore {r.liveness_score:.3f} (eşik {th.liveness_threshold})"
          f" | similarity {r.similarity:.3f} (eşik {th.match_threshold})"
          f" | passed {r.passed} | reason {r.reason} | framesUsed {r.frames_used}/{len(frames)} | {ms:.0f} ms")


def open_camera(index: int) -> cv2.VideoCapture:
    # Windows'ta DirectShow arka ucu kamerayı varsayılan MSMF'den çok daha hızlı açıyor.
    backend = cv2.CAP_DSHOW if sys.platform == "win32" else cv2.CAP_ANY
    camera = cv2.VideoCapture(index, backend)
    if not camera.isOpened():
        raise SystemExit(f"Kamera {index} açılamadı.")
    return camera


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--photo", type=Path, required=True, help="kayıt fotoğrafı (JPEG/PNG/WebP)")
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--frames", type=int, default=3, choices=range(3, 6), help="3–5 (contracts.md)")
    parser.add_argument("--interval-ms", type=int, default=150)
    parser.add_argument("--no-preview", action="store_true", help="pencere açmadan bir kez dene")
    parser.add_argument("--model-dir", type=Path, default=VISION_DIR / "models")
    args = parser.parse_args()
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    th = face_service.Thresholds.from_config(get_settings().thresholds)
    models = load_models(args.model_dir)

    photo = base64.b64encode(args.photo.read_bytes()).decode()
    try:
        enrolled = face_service.enroll(models, photo, th)
    except ServiceError as e:
        print(f"Kayıt reddedildi: {e.code} ({e.message})")
        return 1
    reference = enrolled.vector.astype(np.float32)
    box = enrolled.face.box
    print(f"Kayıt: qualityScore {enrolled.quality_score} | yüz {box.width}x{box.height} px")

    camera = open_camera(args.camera)
    try:
        if args.no_preview:
            for _ in range(15):  # pozlama ve beyaz dengesi otursun
                camera.read()
            run_verify(models, camera, reference, th, args)
            return 0

        attempt = 0
        while True:
            ok, frame = camera.read()
            if not ok:
                print("Kameradan kare alınamadı.")
                return 1
            cv2.imshow(WINDOW, frame)
            key = cv2.waitKey(1) & 0xFF
            if key in (27, ord("q")):
                return 0
            if key == ord(" "):
                attempt += 1
                print(f"Deneme {attempt}:")
                run_verify(models, camera, reference, th, args)
    finally:
        camera.release()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    raise SystemExit(main())
