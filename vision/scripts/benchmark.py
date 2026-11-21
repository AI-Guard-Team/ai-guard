"""D1 benchmark: modellerin bu bilgisayarın işlemcisindeki süreleri ve gereken işçi sayısı (Talimat 9. adım).

    cd vision
    python scripts/benchmark.py                      # kamera, iş parçacığı: onnxruntime varsayılanı
    python scripts/benchmark.py --threads 1,2,0      # 1, 2 ve varsayılan (0) ayrı ayrı
    python scripts/benchmark.py --frames-dir <klasör>  # kamera yerine yerel görüntüler (repoya eklenmez)

Kişisel veri: kamera kareleri yalnızca bellekte tutulur, hiçbir görüntü diske yazılmaz. Çıktı belgesinde
(docs/benchmark-D1.md) yalnızca sayılar ve bilgisayar bilgisi var.

Her ölçüm 105 kez çalışır, ilk 5'i ısınma sayılıp atılır; kalan 100 çalıştırmanın ortalaması, p50, p95 ve en kötüsü
raporlanır. Kart hedefleri p95 ile karşılaştırılır.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import platform
import re
import secrets
import subprocess
import sys
import threading
import time
import urllib.request
from dataclasses import dataclass
from datetime import date
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path

import numpy as np

VISION_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(VISION_DIR))

RUNS = 100
WARMUP = 5
CAMERA_WARMUP_FRAMES = 30  # kameranın pozlama ayarı otursun
FACE_RATIO_WARNING = 0.9
CANDIDATES = (10, 25, 50)  # H3 deneyi
UTILIZATION = 0.7  # çekirdeklerin en fazla %70'i dolsun: MediaPipe'ın kendi iş parçacıkları, JSON/Base64, ağ
SAMPLE_RATE = 16000

# Kart hedefleri (ms, p95 ile karşılaştırılır).
TARGETS_MS = {
    "face": 150,   # yüz/kafa yönü
    "match": 200,  # eşleştirme: yüz noktaları + ArcFace
    "phone": 300,  # telefon
}

# Ölçülen işler: anahtar → (belgede görünen ad, kart hedefinin anahtarı)
TASKS = {
    "face": ("Yüz noktaları + kafa yönü (MediaPipe)", "face"),
    "embed": ("ArcFace (hizalama dahil)", None),
    "match": ("Eşleştirme: yüz noktaları + ArcFace", "match"),
    "liveness": ("Canlılık: MiniFASNetV2 + V1SE (kırpma dahil)", None),
    "verify": ("Doğrulama zinciri: yüz noktaları + ArcFace + canlılık (1 kare)", None),
    "phone": ("Telefon: YOLO11n (letterbox dahil)", "phone"),
    "speech": ("Konuşma: Silero VAD (1 sn, 16 kHz)", None),
}


# ---------- hesaplar (birim testli) ----------

@dataclass(frozen=True)
class Stats:
    mean: float
    p50: float
    p95: float
    max: float
    n: int


def summarize(samples_ms: list[float]) -> Stats:
    """Ortalama, p50, p95 (numpy'nin doğrusal ara değerlemesi) ve en kötü değer."""
    if not samples_ms:
        raise ValueError("Ölçüm yok")
    a = np.asarray(samples_ms, dtype=np.float64)
    p50, p95 = np.percentile(a, [50, 95])
    return Stats(float(a.mean()), float(p50), float(p95), float(a.max()), len(a))


def time_runs(fn, inputs: list, runs: int = RUNS, warmup: int = WARMUP, clock=time.perf_counter) -> list[float]:
    """fn'i girdiler üzerinde dönerek warmup + runs kez çalıştırır; ısınma dışındaki süreleri (ms) döner."""
    if not inputs:
        raise ValueError("Girdi yok")
    samples = []
    for i in range(warmup + runs):
        x = inputs[i % len(inputs)]
        start = clock()
        fn(x)
        elapsed = (clock() - start) * 1000
        if i >= warmup:
            samples.append(elapsed)
    return samples


def candidate_load_ms(face: float, phone: float, match: float, speech: float) -> float:
    """Bir adayın saniyede gerektirdiği işlemci süresi (ms/sn).

    Zamanlama (D4, contracts.md 5. bölüm): yüz + kafa yönü her saniye, telefon 3 sn'de bir, kimlik 60 sn'de bir;
    ses 1 sn'lik parçalar (S-22). Kimlik için eşleştirme zinciri (yüz noktaları + ArcFace) alınıyor; aynı karede yüz
    noktaları zaten hesaplandığı için bu biraz fazla tahmin ama 60'a bölündüğü için etkisi küçük.
    """
    return face + phone / 3 + match / 60 + speech


@dataclass(frozen=True)
class Capacity:
    candidates: int
    load_ms_per_s: float  # toplam işlemci süresi, ms/sn
    cores_needed: float   # aynı anda dolu olması gereken çekirdek
    workers: int          # önerilen VISION_WORKERS (her biri VISION_ORT_THREADS=1)
    fits: bool            # bu bilgisayarın fiziksel çekirdeklerine sığıyor mu


def capacity(candidates: int, per_candidate_ms: float, physical_cores: int,
             utilization: float = UTILIZATION) -> Capacity:
    """Teorik hesap: her işçi istekleri sırayla, tek iş parçacığıyla (≈ bir çekirdek) işler.

    Gereken işçi = ⌈ gereken çekirdek / kullanım oranı ⌉. İşçi sayısı fiziksel çekirdeği aşarsa sığmıyor sayılır.
    """
    if candidates < 0 or per_candidate_ms < 0 or physical_cores < 1 or not 0 < utilization <= 1:
        raise ValueError("Geçersiz kapasite girdisi")
    load = candidates * per_candidate_ms
    cores = load / 1000
    workers = max(1, math.ceil(round(cores / utilization, 9)))
    return Capacity(candidates, load, cores, workers, workers <= physical_cores)


def descendants(processes: list[dict], root: int) -> list[dict]:
    """Süreç listesinden (pid, ppid alanlarıyla) root'un bütün alt süreçleri."""
    children: dict[int, list[dict]] = {}
    for p in processes:
        children.setdefault(p["ppid"], []).append(p)
    found, stack = [], [root]
    while stack:
        for child in children.get(stack.pop(), []):
            if child["pid"] != root and child not in found:
                found.append(child)
                stack.append(child["pid"])
    return found


def synthetic_audio(seconds: float = 1.0, seed: int = 491) -> np.ndarray:
    """Konuşmaya benzer sentetik ses: değişen temel frekans, harmonikler, hece gibi zarf ve gürültü.

    Silero'nun süresi içerikten neredeyse bağımsız; amaç sessizlik dışında gerçekçi bir girdi.
    """
    rng = np.random.default_rng(seed)
    t = np.arange(int(SAMPLE_RATE * seconds)) / SAMPLE_RATE
    f0 = 140 + 30 * np.sin(2 * np.pi * 1.5 * t)
    phase = 2 * np.pi * np.cumsum(f0) / SAMPLE_RATE
    voice = sum(np.sin(k * phase) / k for k in range(1, 6))
    envelope = 0.5 * (1 + np.sin(2 * np.pi * 4 * t))  # saniyede ~4 hece
    signal = 0.3 * voice * envelope + 0.01 * rng.standard_normal(t.size)
    return (signal / np.max(np.abs(signal)) * 0.5).astype(np.float32)


# ---------- kareler ----------

def capture_frames(camera: int, count: int) -> list[np.ndarray]:
    import cv2

    backend = cv2.CAP_DSHOW if os.name == "nt" else cv2.CAP_ANY
    cap = cv2.VideoCapture(camera, backend)
    if not cap.isOpened():
        raise SystemExit(f"HATA: {camera} numaralı kamera açılamadı. Başka bir kamera için --camera, "
                         "kamera yerine yerel görüntüler için --frames-dir kullanın.")
    try:
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        print("\n>>> KAMERAYA BAKIN. Yüzünüz karede ve ortada olsun; ölçüm boyunca yerinizde kalın.")
        for s in (3, 2, 1):
            print(f"    {s}...", flush=True)
            time.sleep(1)
        for _ in range(CAMERA_WARMUP_FRAMES):
            cap.read()
        frames = []
        while len(frames) < count:
            ok, frame = cap.read()
            if not ok or frame is None:
                raise SystemExit("HATA: kameradan kare okunamadı.")
            frames.append(frame.copy())
            if len(frames) % 25 == 0:
                print(f"    {len(frames)}/{count} kare", flush=True)
        print(">>> Kareler alındı, kameradan uzaklaşabilirsiniz. Kareler yalnızca bellekte; diske yazılmıyor.\n")
        return frames
    finally:
        cap.release()


def load_frames_dir(folder: Path, count: int) -> list[np.ndarray]:
    import cv2

    paths = sorted(p for p in folder.iterdir() if p.suffix.lower() in {".jpg", ".jpeg", ".png", ".webp"})
    frames = [img for img in (cv2.imread(str(p)) for p in paths[:count]) if img is not None]
    if not frames:
        raise SystemExit(f"HATA: {folder} klasöründe okunabilir görüntü yok.")
    return frames


# ---------- ölçüm ----------

def measure(models, frames: list[np.ndarray], faces: list, audio: np.ndarray) -> dict[str, Stats]:
    with_face = [(f, face) for f, face in zip(frames, faces) if face is not None]
    if not with_face:
        raise SystemExit("HATA: hiçbir karede yüz bulunamadı; ArcFace ve canlılık ölçülemiyor.")
    face_frames = [f for f, _ in with_face]

    def match(frame):
        face = models.face.predict(frame).largest
        if face is not None:
            models.embedder.predict(frame, face.keypoints)

    def verify(frame):
        face = models.face.predict(frame).largest
        if face is not None:
            models.embedder.predict(frame, face.keypoints)
            models.liveness.predict(frame, face.box)

    runs = {
        "face": (models.face.predict, frames),
        "embed": (lambda p: models.embedder.predict(p[0], p[1].keypoints), with_face),
        "match": (match, face_frames),
        "liveness": (lambda p: models.liveness.predict(p[0], p[1].box), with_face),
        "verify": (verify, face_frames),
        "phone": (models.phone.predict, frames),
        "speech": (models.speech.predict, [audio]),
    }
    results = {}
    for key, (fn, inputs) in runs.items():
        results[key] = summarize(time_runs(fn, inputs))
        print(f"  {TASKS[key][0]}: p95 {results[key].p95:.1f} ms")
    return results


# ---------- bilgisayar ve bellek ----------

def windows_cim(query: str) -> list[dict]:
    cmd = ["powershell", "-NoProfile", "-Command",
           f"[Console]::OutputEncoding=[Text.Encoding]::UTF8; {query} | ConvertTo-Json -Compress"]
    out = subprocess.run(cmd, capture_output=True, check=True).stdout.decode("utf-8-sig").strip()
    data = json.loads(out) if out else []
    return data if isinstance(data, list) else [data]


def machine_info() -> dict:
    info = {"os": f"{platform.system()} {platform.release()} ({platform.version()})",
            "python": platform.python_version(), "logical": os.cpu_count()}
    if os.name == "nt":
        cpu = windows_cim("Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores")[0]
        mem = windows_cim("Get-CimInstance Win32_ComputerSystem | Select-Object TotalPhysicalMemory")[0]
        # platform.release() Windows 11'de de "10" diyor; adı işletim sisteminden al.
        osinfo = windows_cim("Get-CimInstance Win32_OperatingSystem | Select-Object Caption,BuildNumber")[0]
        info.update(cpu=cpu["Name"].strip(), physical=int(cpu["NumberOfCores"]),
                    ram_gb=int(mem["TotalPhysicalMemory"]) / 2**30,
                    os=f"{osinfo['Caption'].strip()} (derleme {osinfo['BuildNumber']})")
    else:
        text = Path("/proc/cpuinfo").read_text()
        info["cpu"] = re.search(r"model name\s*:\s*(.+)", text).group(1).strip()
        cores = set()
        for block in text.split("\n\n"):  # her mantıksal işlemci ayrı blok
            pid, cid = re.search(r"physical id\s*:\s*(\d+)", block), re.search(r"core id\s*:\s*(\d+)", block)
            if pid and cid:
                cores.add((pid.group(1), cid.group(1)))
        info["physical"] = len(cores) or os.cpu_count()
        kb = int(re.search(r"MemTotal:\s+(\d+)", Path("/proc/meminfo").read_text()).group(1))
        info["ram_gb"] = kb / 2**20
    pkgs = {}
    for name in ("numpy", "onnxruntime", "mediapipe", "opencv-contrib-python"):
        try:
            pkgs[name] = version(name)
        except PackageNotFoundError:
            pkgs[name] = "yok"
    info["packages"] = pkgs
    return info


def process_list() -> list[dict]:
    """pid, ppid, cmd, rss (bayt), private (bayt; Windows'ta özel bellek, Linux'ta yok)."""
    if os.name == "nt":
        rows = windows_cim("Get-CimInstance Win32_Process | Select-Object "
                           "ProcessId,ParentProcessId,CommandLine,WorkingSetSize,PrivatePageCount")
        return [{"pid": r["ProcessId"], "ppid": r["ParentProcessId"], "cmd": r["CommandLine"] or "",
                 "rss": int(r["WorkingSetSize"] or 0), "private": int(r["PrivatePageCount"] or 0)} for r in rows]
    result = []
    for d in Path("/proc").iterdir():
        if d.name.isdigit():
            try:
                status = (d / "status").read_text()
                cmd = (d / "cmdline").read_bytes().replace(b"\0", b" ").decode(errors="replace")
            except OSError:
                continue
            ppid = int(re.search(r"PPid:\s+(\d+)", status).group(1))
            rss = re.search(r"VmRSS:\s+(\d+)", status)
            result.append({"pid": int(d.name), "ppid": ppid, "cmd": cmd,
                           "rss": int(rss.group(1)) * 1024 if rss else 0, "private": 0})
    return result


def worker_memory(model_dir: Path, workers: int = 2, port: int = 8799, timeout: float = 180) -> list[dict]:
    """uvicorn'u `workers` işçiyle açar, modeller yüklendikten sonra her işçinin belleğini ölçer, kapatır."""
    env = dict(os.environ, VISION_SERVICE_KEY=secrets.token_hex(16), VISION_MODEL_DIR=str(model_dir))
    cmd = [sys.executable, "-m", "uvicorn", "main:app", "--host", "127.0.0.1", "--port", str(port),
           "--workers", str(workers)]
    proc = subprocess.Popen(cmd, cwd=VISION_DIR, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    lines: list[str] = []
    reader = threading.Thread(target=lambda: lines.extend(
        ln.decode(errors="replace") for ln in proc.stderr), daemon=True)
    reader.start()
    tree: list[dict] = []
    try:
        deadline = time.monotonic() + timeout
        while sum("Application startup complete" in ln for ln in lines) < workers:
            if proc.poll() is not None or time.monotonic() > deadline:
                raise SystemExit("HATA: uvicorn işçileri açılmadı:\n" + "".join(lines[-20:]))
            time.sleep(0.5)
        if not any("Modeller yüklendi" in ln for ln in lines):
            raise SystemExit("HATA: işçiler modelleri yüklemedi (model klasörü?)")
        urllib.request.urlopen(f"http://127.0.0.1:{port}/health", timeout=5).read()
        time.sleep(2)  # bellek otursun
        tree = descendants(process_list(), proc.pid)
        found = [p for p in tree if "multiprocessing" in p["cmd"]]
        if len(found) != workers:
            raise SystemExit(f"HATA: {workers} işçi bekleniyordu, {len(found)} bulundu")
        return found
    finally:
        for p in tree:
            try:
                os.kill(p["pid"], 9)
            except OSError:
                pass
        proc.kill()
        proc.wait(timeout=10)


# ---------- belge ----------

def fmt(x: float) -> str:
    return f"{x:.1f}"


def render(info, source, frame_shape, face_ratio, results, capacities, load_key, memory) -> str:
    lines = [
        "# D1 benchmark: görüntü/ses modellerinin işlemci süreleri",
        "",
        (f"*{date.today().isoformat()} · `vision/scripts/benchmark.py` ile üretildi; elle düzenlemeyin, betiği yeniden "
        "çalıştırın. Kart: D1, Talimat 9. adım. Kararlar: MIMARI-KARARLAR K-28.*"),
        "",
        "## Bilgisayar",
        "",
        "| | |",
        "|---|---|",
        f"| İşlemci | {info['cpu']} |",
        f"| Çekirdek / iş parçacığı | {info['physical']} fiziksel / {info['logical']} mantıksal |",
        f"| RAM | {info['ram_gb']:.1f} GB |",
        f"| İşletim sistemi | {info['os']} |",
        f"| Python | {info['python']} |",
        "| Paketler | " + ", ".join(f"{k} {v}" for k, v in info["packages"].items()) + " |",
        "| GPU | Kullanılmıyor (yalnızca işlemci) |",
        "",
        "## Yöntem",
        "",
        (f"- Kareler: {source}, {frame_shape[1]}x{frame_shape[0]}, {RUNS} kare. Görüntüler yalnızca bellekte tutuldu, "
        "diske yazılmadı; bu belgede görüntü yok."),
        (f"- Yüz bulunan kare oranı: **%{face_ratio * 100:.0f}**. ArcFace, canlılık ve zincirler yalnızca yüzlü karelerde "
        "ölçüldü."),
        ("- Ses: 1 sn, 16 kHz mono **sentetik** sinyal (harmonikli, hece zarflı ses + gürültü). Silero'nun süresi içerikten "
        "neredeyse bağımsız."),
        (f"- Her ölçüm {WARMUP + RUNS} kez çalıştı; ilk {WARMUP}'i ısınma sayılıp atıldı. Süreler tek bir çağrının duvar saati "
        "süresi (ms); isteğin çözülmesi (Base64, JPEG) ve ağ dahil değil."),
        "- Kart hedefleri p95 ile karşılaştırıldı.",
        ("- `VISION_ORT_THREADS` yalnızca onnxruntime modellerini (ArcFace, MiniFASNet, YOLO11n, Silero) etkiler; "
        "MediaPipe'ın Python API'sinde iş parçacığı ayarı yok. 0 = onnxruntime varsayılanı."),
        "",
        "## Sonuçlar",
        "",
    ]
    for threads, res in results.items():
        heading = f"### VISION_ORT_THREADS = {threads}" + (" (onnxruntime varsayılanı)" if threads == 0 else "")
        lines += [heading,
                  "",
                  "| İş | Ortalama | p50 | p95 | En kötü | Kart hedefi (p95) |",
                  "|---|---:|---:|---:|---:|---|"]
        for key, (title, target_key) in TASKS.items():
            s = res[key]
            if target_key:
                target = TARGETS_MS[target_key]
                verdict = f"{target} ms: {'geçti' if s.p95 <= target else '**geçmedi**'}"
            else:
                verdict = "—"
            lines.append(f"| {title} | {fmt(s.mean)} | {fmt(s.p50)} | {fmt(s.p95)} | {fmt(s.max)} | {verdict} |")
        lines.append("")

    per = candidate_load_ms(*(results[load_key][k].p95 for k in ("face", "phone", "match", "speech")))
    lines += [
        "## İşçi kapasitesi (teorik)",
        "",
        (f"Bir adayın saniyede gerektirdiği işlemci süresi = yüz+kafa + telefon/3 + kimlik/60 + ses = **{fmt(per)} ms/sn** "
        f"(VISION_ORT_THREADS = {load_key} ölçümünün p95 değerleriyle)."),
        "",
        ("| Aday | Toplam yük (ms/sn) | Gereken çekirdek | Önerilen VISION_WORKERS (VISION_ORT_THREADS=1) | "
        f"Bu bilgisayara sığıyor mu ({info['physical']} fiziksel çekirdek) |"),
        "|---:|---:|---:|---:|---|",
    ]
    for c in capacities(per):
        lines.append(f"| {c.candidates} | {c.load_ms_per_s:.0f} | {c.cores_needed:.2f} | {c.workers} | "
                     f"{'evet' if c.fits else '**hayır**'} |")
    lines += [
        "",
        "Varsayımlar:",
        "",
        ("- Zamanlama D4'e göre: yüz + kafa yönü her saniye, telefon 3 sn'de bir, kimlik 60 sn'de bir; ses 1 sn'lik parçalar "
        "(S-22). Kimlik için eşleştirme zinciri (yüz noktaları + ArcFace) sayıldı."),
        "- Sınav başı doğrulama (3–5 kare) adaybaşına bir kez yapıldığı için sürekli yüke katılmadı.",
        ("- Her işçi istekleri sırayla, `VISION_ORT_THREADS=1` ile yaklaşık bir çekirdekte işler. İşçi sayısı = "
        f"⌈gereken çekirdek / {UTILIZATION}⌉; %{int(UTILIZATION * 100)} kullanım payı MediaPipe'ın kendi iş "
        "parçacıkları, Base64/JPEG çözme, JSON ve ağ için."),
        "- p95 değerleri toplandığı için hesap kötümser; istekler aynı anda gelirse kuyruk gecikmesi hesaba katılmadı.",
        "- **Bu teorik bir hesap.** Gerçek eşzamanlılık H3 deneyinde (A6, Locust) 10, 25 ve 50 adayla ölçülecek.",
        "",
        "## Varsayılanlar ve H3",
        "",
        ("- `.env.example` ve `docker-compose.yml`: `VISION_WORKERS=2`, `VISION_ORT_THREADS=1`. Her işçi modelleri ayrı "
         "yüklediği için (aşağıdaki tablo) günlük kullanım ve entegrasyon için 2 işçi seçildi; 25 adaya kadar yetiyor."),
        "- H3'ün 50 adaylık senaryosunda .env'de VISION_WORKERS=4 yapılmalı (A6).",
        ("- Bu ölçüm geliştirme bilgisayarında yapıldı. H3 başka bir bilgisayarda koşacaksa benchmark orada da "
         "çalıştırılmalı ve işçi sayısı o sonuca göre seçilmeli:"),
        "",
        "  ```",
        "  cd vision",
        "  python scripts/download_models.py",
        "  python scripts/benchmark.py --threads 1,2,0",
        "  ```",
        "",
        "## İşçi başına bellek",
        "",
        (f"uvicorn `--workers {len(memory)}` ile açıldı, modeller yüklendikten sonra ölçüldü (modeller her işçide ayrı "
        "yükleniyor)."),
        "",
        "| İşçi | Çalışma kümesi (RSS) | Özel bellek |",
        "|---:|---:|---:|",
    ]
    for i, p in enumerate(memory, 1):
        private = f"{p['private'] / 2**20:.0f} MB" if p["private"] else "—"
        lines.append(f"| {i} | {p['rss'] / 2**20:.0f} MB | {private} |")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--camera", type=int, default=0, help="kamera numarası (varsayılan 0)")
    parser.add_argument("--frames-dir", type=Path, help="kamera yerine bu klasördeki görüntüler")
    parser.add_argument("--threads", default="0", help="virgülle iş parçacığı sayıları, 0 = varsayılan (ör. 1,2,0)")
    parser.add_argument("--model-dir", type=Path, default=Path(os.getenv("VISION_MODEL_DIR", VISION_DIR / "models")))
    parser.add_argument("--out", type=Path, default=VISION_DIR.parent / "docs" / "benchmark-D1.md")
    parser.add_argument("--skip-memory", action="store_true", help="uvicorn bellek ölçümünü atla")
    args = parser.parse_args()
    thread_options = [int(t) for t in args.threads.split(",")]

    from app.models import ModelFilesMissing, load_models
    from app.models.base import ORT_THREADS_ENV

    info = machine_info()
    print(f"{info['cpu']} · {info['physical']} çekirdek / {info['logical']} iş parçacığı · {info['ram_gb']:.1f} GB")

    try:
        models = load_models(args.model_dir)
    except ModelFilesMissing as e:
        raise SystemExit(f"HATA: {e}. Önce scripts/download_models.py") from None

    if args.frames_dir:
        frames, source = load_frames_dir(args.frames_dir, RUNS), "yerel görüntü klasörü"
    else:
        frames, source = capture_frames(args.camera, RUNS), "bilgisayarın kamerası"
    faces = [models.face.predict(f).largest for f in frames]
    face_ratio = sum(f is not None for f in faces) / len(frames)
    print(f"Yüz bulunan kare: {sum(f is not None for f in faces)}/{len(frames)}")
    if face_ratio < FACE_RATIO_WARNING:
        print(f"UYARI: karelerin yalnızca %{face_ratio * 100:.0f}'inde yüz var; yüzsüz karelerle ölçüm yanıltıcı olur.")
    audio = synthetic_audio()

    results = {}
    for threads in thread_options:
        os.environ[ORT_THREADS_ENV] = str(threads)
        print(f"\nVISION_ORT_THREADS = {threads}")
        models = load_models(args.model_dir)  # oturumlar yeni ayarla açılsın
        results[threads] = measure(models, frames, faces, audio)
    frame_shape = frames[0].shape
    del frames, faces  # kareler ölçümden sonra tutulmuyor

    # Kapasite tek iş parçacığı ölçümüyle: işlemci süresi ≈ duvar saati süresi.
    load_key = 1 if 1 in results else thread_options[0]
    memory = [] if args.skip_memory else worker_memory(args.model_dir)
    text = render(info, source, frame_shape, face_ratio, results,
                  lambda per: [capacity(n, per, info["physical"]) for n in CANDIDATES], load_key, memory)
    args.out.write_text(text, encoding="utf-8")
    print(f"\nYazıldı: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
