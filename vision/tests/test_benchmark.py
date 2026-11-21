"""scripts/benchmark.py'nin hesap fonksiyonları ve VISION_ORT_THREADS ayarı; kamera ve model gerekmez."""

import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

import benchmark
from benchmark import (
    Stats,
    candidate_load_ms,
    capacity,
    descendants,
    summarize,
    synthetic_audio,
    time_runs,
)

from app.models import base


def test_summarize_mean_percentiles_and_max():
    s = summarize([float(x) for x in range(1, 101)])  # 1..100 ms
    assert s == Stats(mean=50.5, p50=50.5, p95=pytest.approx(95.05), max=100.0, n=100)
    assert summarize([7.0]).p95 == 7.0
    with pytest.raises(ValueError):
        summarize([])


def test_time_runs_drops_warmup_and_cycles_inputs():
    seen = []
    ticks = iter(range(1000))
    clock = lambda: next(ticks) / 1000  # her çağrı 1 ms ilerliyor

    samples = time_runs(seen.append, ["a", "b", "c"], runs=4, warmup=2, clock=clock)

    assert seen == ["a", "b", "c", "a", "b", "c"]  # 2 ısınma + 4 ölçüm, girdiler dönerek
    assert samples == pytest.approx([1.0] * 4)     # ısınma süreleri atıldı
    with pytest.raises(ValueError):
        time_runs(seen.append, [])


def test_candidate_load_follows_d4_schedule():
    # yüz her saniye, telefon 3 sn'de bir, kimlik 60 sn'de bir, ses her saniye.
    assert candidate_load_ms(face=30, phone=90, match=120, speech=10) == pytest.approx(30 + 30 + 2 + 10)


def test_capacity_workers_and_fit():
    c = capacity(candidates=25, per_candidate_ms=56, physical_cores=8)
    assert c.load_ms_per_s == pytest.approx(1400)
    assert c.cores_needed == pytest.approx(1.4)
    assert c.workers == 2  # ⌈1.4 / 0.7⌉
    assert c.fits

    exact = capacity(candidates=10, per_candidate_ms=70, physical_cores=8)  # 0.7 çekirdek → tam 1 işçi
    assert exact.workers == 1

    big = capacity(candidates=50, per_candidate_ms=200, physical_cores=8)  # 10 çekirdek → 15 işçi
    assert big.workers == 15 and not big.fits

    assert capacity(0, 50, 8).workers == 1  # en az bir işçi
    with pytest.raises(ValueError):
        capacity(10, 50, 0)


def test_descendants_walks_process_tree():
    procs = [
        {"pid": 1, "ppid": 0}, {"pid": 10, "ppid": 1},   # 10: uvicorn ana süreç
        {"pid": 11, "ppid": 10}, {"pid": 12, "ppid": 10},  # işçiler
        {"pid": 13, "ppid": 11},                         # işçinin alt süreci
        {"pid": 20, "ppid": 1},                          # ilgisiz
    ]
    assert sorted(p["pid"] for p in descendants(procs, 10)) == [11, 12, 13]
    assert descendants(procs, 99) == []


def test_synthetic_audio_is_one_second_16k_float():
    audio = synthetic_audio()
    assert audio.shape == (16000,) and audio.dtype == np.float32
    assert 0.1 < float(np.abs(audio).max()) <= 0.5
    np.testing.assert_array_equal(audio, synthetic_audio())  # tekrarlanabilir


def test_targets_match_card():
    assert benchmark.TARGETS_MS == {"face": 150, "match": 200, "phone": 300}
    assert benchmark.RUNS == 100 and benchmark.WARMUP == 5


# ---------- VISION_ORT_THREADS ----------

@pytest.mark.parametrize("raw,expected", [(None, 0), ("", 0), ("0", 0), ("1", 1), (" 4 ", 4)])
def test_ort_threads_from_env(monkeypatch, raw, expected):
    if raw is None:
        monkeypatch.delenv(base.ORT_THREADS_ENV, raising=False)
    else:
        monkeypatch.setenv(base.ORT_THREADS_ENV, raw)
    assert base.ort_threads() == expected


@pytest.mark.parametrize("raw", ["-1", "iki", "1.5"])
def test_ort_threads_rejects_invalid(monkeypatch, raw):
    monkeypatch.setenv(base.ORT_THREADS_ENV, raw)
    with pytest.raises(ValueError):
        base.ort_threads()


def test_onnx_session_applies_thread_setting(monkeypatch):
    captured = {}

    def fake_session(path, sess_options, providers):
        captured.update(intra=sess_options.intra_op_num_threads, providers=providers)
        return object()

    monkeypatch.setattr(base.ort, "InferenceSession", fake_session)
    monkeypatch.setenv(base.ORT_THREADS_ENV, "2")
    base.onnx_session(Path("x.onnx"))
    assert captured == {"intra": 2, "providers": ["CPUExecutionProvider"]}

    monkeypatch.setenv(base.ORT_THREADS_ENV, "0")
    base.onnx_session(Path("x.onnx"))
    assert captured["intra"] == 0  # 0: onnxruntime kendi varsayılanını seçer
