"""Silent-Face MiniFASNet (iki model) ve YOLO11n ağırlıklarını ONNX'e çevirir (D1).

Servis bu modelleri ONNX olarak onnxruntime ile çalıştırıyor; torch ve ultralytics servise girmiyor
(MIMARI-KARARLAR K-28). Bu betik yalnızca dosyaları üretmek için, ayrı bir sanal ortamda çalıştırılır:

    cd vision
    uv venv --python 3.11 --seed .venv-export
    .venv-export\\Scripts\\python -m pip install -r requirements-export.txt
    .venv-export\\Scripts\\python scripts/export_models.py            # çıktı: VISION_MODEL_DIR ya da vision/models

- MiniFASNetV2 ve MiniFASNetV1SE: minivision-ai/Silent-Face-Anti-Spoofing deposunun sabit bir commit'indeki
  model tanımı ve orijinal ağırlıklar (2.7_80x80_MiniFASNetV2.pth, 4_0_0_80x80_MiniFASNetV1SE.pth). Orijinal
  test.py ikisini birlikte kullanıyor. Başka birinin dönüştürdüğü ONNX kullanılmıyor.
- YOLO11n: Ultralytics'in yayımladığı yolo11n.pt; imgsz=640, sabit opset.

İndirilen her dosyanın sha256'sı kontrol edilir. Dönüştürmeden sonra PyTorch ve ONNX çıktıları aynı girdilerle
karşılaştırılır; fark toleransı aşarsa dosya yazılmaz.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import os
import shutil
import sys
import tempfile
import urllib.request
from collections import OrderedDict
from pathlib import Path

import numpy as np

VISION_DIR = Path(__file__).resolve().parent.parent
OPSET = 17

# Silent-Face-Anti-Spoofing, 21 Kasım 2026'daki master (Apache-2.0).
SILENT_FACE_COMMIT = "b6d5f04ad78778917853b25c778acef6d5626d15"
SILENT_FACE_RAW = f"https://raw.githubusercontent.com/minivision-ai/Silent-Face-Anti-Spoofing/{SILENT_FACE_COMMIT}"
SILENT_FACE_MODEL_DEF = ("src/model_lib/MiniFASNet.py",
                         "e498c4ec5e1ddfaba62b941a126c19d65aa564999f3309661fe43ee8bf38acd7")
# Çıktı dosyası → (depodaki ağırlık, sha256, model sınıfı). Ölçek dosya adının başında (2.7 / 4_0_0 = 4.0) ve
# yalnızca kırpmada kullanılıyor (app/models/liveness.py); ağın kendisi iki modelde de 80x80 girdi alıyor.
MINIFASNET_MODELS = {
    "MiniFASNetV2.onnx": ("resources/anti_spoof_models/2.7_80x80_MiniFASNetV2.pth",
                          "a5eb02e1843f19b5386b953cc4c9f011c3f985d0ee2bb9819eea9a142099bec0", "MiniFASNetV2"),
    "MiniFASNetV1SE.onnx": ("resources/anti_spoof_models/4_0_0_80x80_MiniFASNetV1SE.pth",
                            "84ee1d37d96894d5e82de5a57df044ef80a58be2b218b5ed7cdfd875ec2f5990", "MiniFASNetV1SE"),
}
MINIFASNET_INPUT = 80  # 80x80 girdi, conv6 çekirdeği (80 + 15) // 16 = 5 (src/utility.py get_kernel)

YOLO_URL = "https://github.com/ultralytics/assets/releases/download/v8.4.0/yolo11n.pt"
YOLO_SHA256 = "0ebbc80d4a7680d14987a577cd21342b65ecfd94632bd9a8da63ae6417644ee1"
YOLO_IMGSZ = 640

# requirements-export.txt'teki sürümlerle üretilen dosyaların sha256'sı. Farklı sürümle üretilen dosya da
# çalışabilir; yalnızca uyarı verilir (download_models.py aynı değerleri gösterir).
EXPECTED_OUTPUT_SHA256 = {
    "MiniFASNetV2.onnx": "3a193e9764077a5114e972a1a9c808375885aa4de3f29edb14af161007d3a816",
    "MiniFASNetV1SE.onnx": "f82ef6dc973d3675a645bd4e026e72c546e4069e37bed7123ad89390522d09fd",
    "yolo11n.onnx": "85a9d8f6e497853ca229ee3210548a4f356f83f273104b60f2d87292f60f018e",
}


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def fetch(url: str, dest: Path, expected_sha256: str) -> Path:
    dest.parent.mkdir(parents=True, exist_ok=True)
    print(f"  indiriliyor: {url}")
    urllib.request.urlretrieve(url, dest)
    actual = sha256(dest)
    if actual != expected_sha256:
        raise SystemExit(f"HATA: {dest.name} sha256 uyuşmuyor: {actual} (beklenen {expected_sha256})")
    return dest


def check_parity(name: str, pairs: list[tuple[np.ndarray, np.ndarray]], atol: float) -> None:
    """Mutlak fark her değerde atol'un altında olmalı (rtol=0: büyük değerlerde tolerans gevşemesin)."""
    worst = max(float(np.max(np.abs(a - b))) for a, b in pairs)
    if not all(np.allclose(a, b, rtol=0, atol=atol) for a, b in pairs):
        raise SystemExit(f"HATA: {name} PyTorch ve ONNX çıktıları farklı (en büyük fark {worst:.2e}, atol {atol})")
    print(f"  {name}: {len(pairs)} girdide PyTorch = ONNX (en büyük fark {worst:.2e}, atol {atol}, rtol 0)")


def export_minifasnet(work: Path, out: Path) -> list[Path]:
    import onnxruntime as ort
    import torch

    print("MiniFASNet (Silent-Face-Anti-Spoofing)")
    rel, digest = SILENT_FACE_MODEL_DEF
    model_def = fetch(f"{SILENT_FACE_RAW}/{rel}", work / "silentface" / rel, digest)

    # Model tanımı orijinal depodan; dosya yalnızca torch'a bağlı.
    spec = importlib.util.spec_from_file_location("silentface_minifasnet", model_def)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    kernel = ((MINIFASNET_INPUT + 15) // 16, (MINIFASNET_INPUT + 15) // 16)

    # Orijinal ön işleme: BGR, 0-255, normalizasyon yok (src/data_io/functional.py to_tensor). Girdiler o aralıkta.
    shape = (1, 3, MINIFASNET_INPUT, MINIFASNET_INPUT)
    rng = np.random.default_rng(491)
    inputs = [rng.uniform(0, 255, shape).astype(np.float32) for _ in range(5)]
    inputs += [np.zeros(shape, np.float32), np.full(shape, 255, np.float32)]

    produced = []
    for out_name, (weights_rel, weights_sha, class_name) in MINIFASNET_MODELS.items():
        weights = fetch(f"{SILENT_FACE_RAW}/{weights_rel}", work / "silentface" / weights_rel, weights_sha)
        model = getattr(module, class_name)(conv6_kernel=kernel)
        state = torch.load(weights, map_location="cpu", weights_only=True)
        # Ağırlıklar DataParallel ile kaydedilmiş; anti_spoof_predict.py'deki gibi "module." öneki atılıyor.
        state = OrderedDict((k.removeprefix("module."), v) for k, v in state.items())
        model.load_state_dict(state)
        model.eval()

        tmp = work / out_name
        torch.onnx.export(model, torch.zeros(shape), tmp, input_names=["input"], output_names=["logits"],
                          opset_version=OPSET, dynamo=False)
        session = ort.InferenceSession(str(tmp), providers=["CPUExecutionProvider"])
        with torch.no_grad():
            pairs = [(model(torch.from_numpy(x)).numpy(), session.run(None, {"input": x})[0]) for x in inputs]
        check_parity(f"{class_name} logit", pairs, atol=1e-4)
        dest = out / out_name
        shutil.move(tmp, dest)
        produced.append(dest)
    return produced


def export_yolo(work: Path, out: Path) -> Path:
    import onnx
    import onnxruntime as ort
    import torch
    from ultralytics import YOLO

    print("YOLO11n (Ultralytics)")
    weights = fetch(YOLO_URL, work / "yolo11n.pt", YOLO_SHA256)
    yolo = YOLO(str(weights))
    exported = Path(yolo.export(format="onnx", imgsz=YOLO_IMGSZ, opset=OPSET, dynamic=False, simplify=False,
                                half=False, nms=False, device="cpu"))

    net = YOLO(str(weights)).model.eval()
    session = ort.InferenceSession(str(exported), providers=["CPUExecutionProvider"])
    in_name = session.get_inputs()[0].name
    rng = np.random.default_rng(491)
    inputs = [rng.uniform(0, 1, (1, 3, YOLO_IMGSZ, YOLO_IMGSZ)).astype(np.float32) for _ in range(5)]

    pairs = []
    with torch.no_grad():
        for x in inputs:
            y = net(torch.from_numpy(x))
            pairs.append(((y[0] if isinstance(y, (list, tuple)) else y).numpy(), session.run(None, {in_name: x})[0]))
    # Çıktı (1, 84, 8400): ilk 4 satır kutu (piksel, 0-640), kalan 80 satır sınıf skoru (0-1).
    check_parity("YOLO11n sınıf skoru", [(a[:, 4:], b[:, 4:]) for a, b in pairs], atol=1e-4)
    check_parity("YOLO11n kutu (piksel)", [(a[:, :4], b[:, :4]) for a, b in pairs], atol=1e-2)
    # Ultralytics dosyaya dışa aktarma anını yazıyor ("date"); grafik aynı olsa da her çalıştırmada sha256
    # değişiyordu. Bu alan atılınca aynı sürümlerle her seferinde aynı dosya çıkıyor.
    model = onnx.load(exported)
    kept = [p for p in model.metadata_props if p.key != "date"]
    del model.metadata_props[:]
    model.metadata_props.extend(kept)
    dest = out / "yolo11n.onnx"
    onnx.save(model, dest)
    return dest


def main() -> int:
    default_out = Path(os.getenv("VISION_MODEL_DIR", VISION_DIR / "models"))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=default_out, help=f"çıktı klasörü (varsayılan: {default_out})")
    parser.add_argument("--only", choices=["minifasnet", "yolo"], help="yalnızca bir modeli üret")
    args = parser.parse_args()

    # Ultralytics eksik paketi kendisi kurmasın, ayar dosyasını kullanıcı klasörüne yazmasın.
    os.environ["YOLO_AUTOINSTALL"] = "false"
    args.out.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="aiguard-export-") as tmp:
        work = Path(tmp)
        os.environ.setdefault("YOLO_CONFIG_DIR", str(work / "ultralytics"))
        produced = []
        if args.only in (None, "minifasnet"):
            produced.extend(export_minifasnet(work, args.out))
        if args.only in (None, "yolo"):
            produced.append(export_yolo(work, args.out))

    for path in produced:
        digest = sha256(path)
        expected = EXPECTED_OUTPUT_SHA256.get(path.name)
        note = "" if expected in (None, digest) else f"  UYARI: beklenen {expected} (farklı paket sürümü?)"
        print(f"{path}  {path.stat().st_size:,} bayt  sha256={digest}{note}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
