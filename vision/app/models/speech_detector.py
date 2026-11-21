"""Silero VAD (silero_vad.onnx, v6.2.3): 16 kHz mono seste 32 ms'lik pencerelerde konuşma olasılığı.

silero-vad paketindeki OnnxWrapper.audio_forward'ın numpy karşılığı (paket torch istediği için kullanılmıyor):
- Her çağrıda durum sıfırlanır (2, 1, 128); servis durumsuz, her ses parçası bağımsız (S-20).
- Ses 512 örneklik pencerelere bölünür, son pencere sıfırla doldurulur.
- Her pencerenin önüne bir önceki pencerenin son 64 örneği eklenir (ilkinde sıfır): modele 576 örnek gider.
Konuşma var/yok kararı `config.yaml`'daki `vad_threshold` ile uçta verilir (D5). Ses çözme (WebM → PCM) D5'te.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np

from app.models.base import VisionModel, onnx_session

SAMPLE_RATE = 16000
WINDOW = 512
CONTEXT = 64
WINDOW_SEC = WINDOW / SAMPLE_RATE  # 0,032 sn


@dataclass(frozen=True)
class SpeechResult:
    probabilities: np.ndarray  # pencere başına konuşma olasılığı, (pencere sayısı,)
    duration_sec: float

    @property
    def max_probability(self) -> float:
        return float(self.probabilities.max()) if self.probabilities.size else 0.0


def windows(audio: np.ndarray) -> np.ndarray:
    """(pencere sayısı, 512); son pencere sıfırla doldurulur."""
    samples = np.asarray(audio)
    if samples.ndim != 1:
        raise ValueError("Ses tek kanallı (1 boyutlu) olmalı")
    if not np.issubdtype(samples.dtype, np.floating):
        raise ValueError("Ses örnekleri -1…1 aralığında float olmalı")
    samples = samples.astype(np.float32)
    pad = (-len(samples)) % WINDOW
    if pad:
        samples = np.concatenate([samples, np.zeros(pad, np.float32)])
    return samples.reshape(-1, WINDOW)


class SpeechDetector(VisionModel):
    name = "Silero VAD"
    files = ("silero_vad.onnx",)

    def _load(self, path: Path) -> None:
        self._session = onnx_session(path)
        names = {i.name for i in self._session.get_inputs()}
        if names != {"input", "state", "sr"}:
            raise ValueError(f"{self.name}: beklenmeyen girdiler {sorted(names)}")

    def predict(self, audio: np.ndarray) -> SpeechResult:
        """16 kHz mono float ses → pencere başına olasılık."""
        chunks = windows(audio)
        state = np.zeros((2, 1, 128), dtype=np.float32)
        context = np.zeros((1, CONTEXT), dtype=np.float32)
        sr = np.array(SAMPLE_RATE, dtype=np.int64)
        probs = np.empty(len(chunks), dtype=np.float32)
        for i, chunk in enumerate(chunks):
            x = np.concatenate([context, chunk[None]], axis=1)
            out, state = self._session.run(None, {"input": x, "state": state, "sr": sr})
            probs[i] = out.reshape(-1)[0]
            context = x[:, -CONTEXT:]
        return SpeechResult(probs, len(np.asarray(audio)) / SAMPLE_RATE)
