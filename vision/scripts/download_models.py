"""Model dosyalarını sabit adreslerden VISION_MODEL_DIR'e (varsayılan vision/models/) indirir (D1).

    cd vision
    python scripts/download_models.py            # ya da: --dir <klasör>

- face_landmarker.task, w600k_mbf.onnx, silero_vad.onnx: indirilir, sha256 doğrulanır. Dosya zaten varsa ve
  sha256'sı tutuyorsa yeniden indirilmez; tutmuyorsa yeniden indirilir.
- MiniFASNetV2.onnx, MiniFASNetV1SE.onnx, yolo11n.onnx: hazır indirilecek bir kaynakları yok,
  scripts/export_models.py ile üretilir.
  Klasörde varsa sha256'sı gösterilir, yoksa nasıl üretileceği yazdırılır.

Herhangi bir dosya eksik ya da bozuksa çıkış kodu 1 (Docker derlemesi bu adımda dursun diye).
Yalnızca standart kütüphane kullanılır; servisin ortamında ek paket gerekmez. Kaynaklar ve lisanslar: K-28.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import sys
import urllib.request
from pathlib import Path

VISION_DIR = Path(__file__).resolve().parent.parent

DOWNLOADS = {
    # Google MediaPipe, Apache-2.0 (litert-community/MediaPipe-FaceLandmarker model kartına göre).
    "face_landmarker.task": (
        "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task",
        "64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff",
    ),
    # InsightFace buffalo_s içindeki w600k_mbf.onnx (MobileFaceNet @ WebFace600K), yalnızca ticari olmayan
    # araştırma. Tek dosyalık kopya; sha256'sı InsightFace v0.7 buffalo_s.zip içindekiyle aynı.
    "w600k_mbf.onnx": (
        "https://github.com/yakhyo/uniface/releases/download/weights/w600k_mbf.onnx",
        "9cc6e4a75f0e2bf0b1aed94578f144d15175f357bdc05e815e5c4a02b319eb4f",
    ),
    # Silero VAD v6.2.3, MIT. silero-vad pip paketindeki dosyayla aynı.
    "silero_vad.onnx": (
        "https://raw.githubusercontent.com/snakers4/silero-vad/v6.2.3/src/silero_vad/data/silero_vad.onnx",
        "1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3",
    ),
}

# export_models.py'nin requirements-export.txt sürümleriyle ürettiği dosyalar.
EXPORTED = {
    "MiniFASNetV2.onnx": "3a193e9764077a5114e972a1a9c808375885aa4de3f29edb14af161007d3a816",
    "MiniFASNetV1SE.onnx": "f82ef6dc973d3675a645bd4e026e72c546e4069e37bed7123ad89390522d09fd",
    "yolo11n.onnx": "85a9d8f6e497853ca229ee3210548a4f356f83f273104b60f2d87292f60f018e",
}

EXPORT_HELP = """\
  Üretmek için (ayrı bir sanal ortamda; servis ortamına torch kurulmaz):
    cd vision
    uv venv --python 3.11 --seed .venv-export
    .venv-export\\Scripts\\python -m pip install -r requirements-export.txt
    .venv-export\\Scripts\\python scripts/export_models.py --out "{dir}"
"""


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def download(name: str, url: str, expected: str, model_dir: Path) -> bool:
    dest = model_dir / name
    if dest.is_file() and sha256(dest) == expected:
        print(f"  {name}: var, sha256 doğru")
        return True

    part = dest.with_name(dest.name + ".part")
    print(f"  {name}: indiriliyor {url}")
    try:
        urllib.request.urlretrieve(url, part)
    except OSError as e:
        part.unlink(missing_ok=True)
        print(f"  HATA: {name} indirilemedi: {e}")
        return False

    actual = sha256(part)
    if actual != expected:
        part.unlink()
        print(f"  HATA: {name} sha256 uyuşmuyor: {actual} (beklenen {expected}); dosya silindi")
        return False
    part.replace(dest)  # yarım indirilmiş dosya hiçbir zaman asıl adla durmasın
    print(f"  {name}: indirildi, sha256 doğru")
    return True


def check_exported(name: str, expected: str, model_dir: Path) -> bool:
    path = model_dir / name
    if not path.is_file():
        print(f"  {name}: YOK")
        return False
    actual = sha256(path)
    note = "sha256 doğru" if actual == expected else f"UYARI: sha256 {actual}, beklenen {expected} (farklı sürümle üretilmiş)"
    print(f"  {name}: var, {note}")
    return True


def main() -> int:
    default_dir = Path(os.getenv("VISION_MODEL_DIR", VISION_DIR / "models"))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--dir", type=Path, default=default_dir, help=f"model klasörü (varsayılan: {default_dir})")
    args = parser.parse_args()
    model_dir: Path = args.dir
    model_dir.mkdir(parents=True, exist_ok=True)
    print(f"Model klasörü: {model_dir}")

    # Önce hepsi denenir, sonra sonuca bakılır: ilk hatada durulsa diğer dosyaların durumu görünmezdi.
    downloaded = [download(name, url, digest, model_dir) for name, (url, digest) in DOWNLOADS.items()]
    exported = [check_exported(name, digest, model_dir) for name, digest in EXPORTED.items()]
    ok, exported_ok = all(downloaded), all(exported)
    if not exported_ok:
        print(EXPORT_HELP.format(dir=model_dir))
    return 0 if ok and exported_ok else 1


if __name__ == "__main__":
    sys.exit(main())
