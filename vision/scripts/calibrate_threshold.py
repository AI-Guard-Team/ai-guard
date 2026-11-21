"""LFW ile yüz benzerliği eşiği (D2): FAR ≈ %1 olan kosinüs eşiği, o eşikteki FRR ve doğruluk.

    cd vision
    python scripts/calibrate_threshold.py                      # datasets/lfw/lfw ve datasets/lfw/pairs.txt
    python scripts/calibrate_threshold.py --far 0.01 --lfw datasets/lfw

Veri seti repoya girmez (/vision/datasets/, .gitignore). Klasör yapısı:
    datasets/lfw/pairs.txt                 resmî "View 2" çift listesi: 10 kat × (300 aynı + 300 farklı kişi)
    datasets/lfw/lfw/<Ad>/<Ad>_0001.jpg    hizalanmamış (orijinal) LFW görüntüleri

Hat servisinkiyle aynı: app.images.decode_bytes → MediaPipe Face Landmarker → 5 noktayla hizalama → ArcFace.
LFW görüntülerinde arka planda başka yüzler de olabildiği için birden fazla yüz bulunursa en büyüğü (LFW'nin
ortadaki öznesi) alınır ve sayılır; servis bu durumda MULTIPLE_FACES döner, ama burada ölçülen vektörün ayırt
ediciliği. Yüz bulunamayan görüntüyü içeren çiftler eşik hesabından çıkarılır ve raporlanır.
"""

from __future__ import annotations

import argparse
import math
import sys
import time
from pathlib import Path

import numpy as np

VISION_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(VISION_DIR))

from app.images import decode_bytes
from app.models import load_models


def read_pairs(path: Path) -> list[tuple[str, int, str, int, bool]]:
    """pairs.txt → (ad1, no1, ad2, no2, aynı_kişi). İlk satır "kat_sayısı çift_sayısı"."""
    lines = path.read_text(encoding="utf-8").split("\n")
    folds, per_fold = (int(x) for x in lines[0].split())
    pairs = []
    for line in lines[1:]:
        f = line.split()
        if len(f) == 3:
            pairs.append((f[0], int(f[1]), f[0], int(f[2]), True))
        elif len(f) == 4:
            pairs.append((f[0], int(f[1]), f[2], int(f[3]), False))
    if len(pairs) != folds * per_fold * 2:
        raise ValueError(f"{path}: {len(pairs)} çift okundu, beklenen {folds * per_fold * 2}")
    return pairs


def image_path(root: Path, name: str, number: int) -> Path:
    return root / name / f"{name}_{number:04d}.jpg"


def embed_all(models, paths: list[Path]) -> tuple[dict[Path, np.ndarray | None], int]:
    """Her görüntü için vektör (yüz yoksa None) ve birden fazla yüz bulunan görüntü sayısı."""
    vectors: dict[Path, np.ndarray | None] = {}
    multiple = 0
    start = time.perf_counter()
    for i, path in enumerate(paths, 1):
        image = decode_bytes(path.read_bytes())
        faces = models.face.predict(image)
        if faces.count > 1:
            multiple += 1
        vectors[path] = models.embedder.predict(image, faces.largest.keypoints) if faces.count else None
        if i % 500 == 0 or i == len(paths):
            print(f"  {i}/{len(paths)} görüntü, {time.perf_counter() - start:.0f} sn", file=sys.stderr)
    return vectors, multiple


def rates(pos: np.ndarray, neg: np.ndarray, threshold: float) -> tuple[float, float, float]:
    """(FAR, FRR, doğruluk); similarity >= threshold "aynı kişi" demek (servisteki kural)."""
    far = float(np.mean(neg >= threshold))
    frr = float(np.mean(pos < threshold))
    accuracy = (np.sum(pos >= threshold) + np.sum(neg < threshold)) / (len(pos) + len(neg))
    return far, frr, float(accuracy)


def threshold_at_far(neg: np.ndarray, target_far: float, decimals: int = 3) -> float:
    """FAR <= hedef olan en küçük eşik, `decimals` basamağa yukarı yuvarlanmış.

    k = ⌊hedef × N⌋ farklı kişi çiftinin eşiği geçmesine izin var: eşik (k+1). en yüksek benzerliğin hemen
    üstü. Yukarı yuvarlama FAR'ı yalnızca düşürebilir; rapordaki oranlar yuvarlanmış eşikte hesaplanır.
    """
    ordered = np.sort(neg)[::-1]
    k = math.floor(target_far * len(ordered))
    raw = float(ordered[k]) if k < len(ordered) else float(ordered[-1])
    step = 10 ** decimals
    threshold = math.floor(raw * step) / step + 1 / step  # raw'dan kesin büyük ilk yuvarlak değer
    return round(threshold, decimals)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--lfw", type=Path, default=VISION_DIR / "datasets" / "lfw")
    parser.add_argument("--far", type=float, default=0.01, help="hedef yanlış kabul oranı (varsayılan 0.01)")
    parser.add_argument("--model-dir", type=Path, default=VISION_DIR / "models")
    args = parser.parse_args()
    # Windows'ta çıktı dosyaya yönlendirilince cp1252 kullanılıyor ve Türkçe karakterde çöküyor.
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    pairs_file, image_root = args.lfw / "pairs.txt", args.lfw / "lfw"
    if not pairs_file.is_file() or not image_root.is_dir():
        print(f"LFW bulunamadı: {pairs_file} ve {image_root} olmalı (bkz. betiğin açıklaması).", file=sys.stderr)
        return 2

    pairs = read_pairs(pairs_file)
    paths = sorted({image_path(image_root, n, i) for a, ia, b, ib, _ in pairs for n, i in ((a, ia), (b, ib))})
    missing = [p for p in paths if not p.is_file()]
    if missing:
        print(f"{len(missing)} görüntü eksik, ör. {missing[0]}", file=sys.stderr)
        return 2

    print(f"{len(pairs)} çift, {len(paths)} farklı görüntü; vektörler çıkarılıyor…", file=sys.stderr)
    models = load_models(args.model_dir)
    vectors, multiple = embed_all(models, paths)
    no_face = sorted(p for p, v in vectors.items() if v is None)

    pos, neg, dropped_pos, dropped_neg = [], [], 0, 0
    for a, ia, b, ib, same in pairs:
        va, vb = vectors[image_path(image_root, a, ia)], vectors[image_path(image_root, b, ib)]
        if va is None or vb is None:
            dropped_pos += same
            dropped_neg += not same
            continue
        (pos if same else neg).append(float(np.dot(va, vb)))
    pos_a, neg_a = np.array(pos), np.array(neg)

    threshold = threshold_at_far(neg_a, args.far)
    far, frr, accuracy = rates(pos_a, neg_a, threshold)
    # Serviste yüz bulunamayan kare NO_FACE ile reddedilir; aynı kişi çiftinde bu da bir yanlış ret.
    frr_with_no_face = (np.sum(pos_a < threshold) + dropped_pos) / (len(pos_a) + dropped_pos)

    pct = [1, 5, 50, 95, 99]
    print(f"\nGörüntü: {len(paths)} | yüz bulunamayan: {len(no_face)} | birden fazla yüz (en büyüğü alındı): {multiple}")
    for p in no_face:
        print(f"  yüz yok: {p.relative_to(image_root)}")
    print(f"Çift: {len(pairs)} | değerlendirilen: aynı {len(pos_a)}, farklı {len(neg_a)} "
          f"| çıkarılan: aynı {dropped_pos}, farklı {dropped_neg}")
    print("Benzerlik yüzdelikleri " + " / ".join(f"p{p}" for p in pct))
    print("  aynı kişi:   " + " / ".join(f"{v:.3f}" for v in np.percentile(pos_a, pct)))
    print("  farklı kişi: " + " / ".join(f"{v:.3f}" for v in np.percentile(neg_a, pct)))
    print(f"\nHedef FAR {args.far:.2%} → eşik {threshold:.3f}")
    print(f"  FAR {far:.2%} ({int(np.sum(neg_a >= threshold))}/{len(neg_a)}) | FRR {frr:.2%} "
          f"({int(np.sum(pos_a < threshold))}/{len(pos_a)}) | doğruluk {accuracy:.2%}")
    print(f"  Yüz bulunamayan aynı kişi çiftleri de ret sayılırsa FRR {frr_with_no_face:.2%}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
