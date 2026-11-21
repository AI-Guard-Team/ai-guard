"""Bütün modellerin ortak arayüzü (GAB 3.3.4: her model ortak bir arayüzün arkasında).

Her model bir ya da birkaç dosyadan yüklenir, numpy dizisi alıp sonuç nesnesi döner. Dosya eksikse
`ModelFilesMissing` fırlatılır; servis bu durumda açılır ama /internal uçları 503 MODELS_NOT_READY döner.

Görüntüler her modelde aynı biçimde gelir: BGR, uint8, (yükseklik, genişlik, 3); OpenCV'nin `imdecode` çıktısı.
Renk dönüşümü ve ölçekleme her modelin kendi ön işlemesinde yapılır.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from pathlib import Path
from typing import Any, ClassVar

import numpy as np
import onnxruntime as ort


class ModelFilesMissing(Exception):
    """Model klasöründe beklenen dosyalardan biri ya da birkaçı yok."""

    def __init__(self, missing: list[Path]) -> None:
        self.missing = missing
        super().__init__("Model dosyaları bulunamadı: " + ", ".join(p.name for p in missing))


class VisionModel(ABC):
    """Ortak temel sınıf: dosyaları kontrol eder, yüklemeyi alt sınıfa bırakır."""

    name: ClassVar[str]
    files: ClassVar[tuple[str, ...]]

    def __init__(self, model_dir: Path) -> None:
        paths = [Path(model_dir) / f for f in self.files]
        missing = [p for p in paths if not p.is_file()]
        if missing:
            raise ModelFilesMissing(missing)
        self._load(*paths)

    @abstractmethod
    def _load(self, *paths: Path) -> None:
        """Model dosyalarını belleğe yükler; işçi başına bir kez çağrılır."""

    @abstractmethod
    def predict(self, data: np.ndarray, *args: Any, **kwargs: Any) -> Any:
        """Tek bir girdiyi işler ve modelin sonuç nesnesini döner."""


def onnx_session(path: Path) -> ort.InferenceSession:
    """Yalnızca işlemci (hedef donanımda GPU yok)."""
    return ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])


def check_input_shape(session: ort.InferenceSession, expected: tuple[int, ...], model: str) -> str:
    """Yanlış dosya (ör. farklı girdi boyutlu bir sürüm) açılışta fark edilsin; girdinin adını döner.

    Toplu boyut gibi dinamik eksenler (ad ya da None) her değeri kabul ediyor sayılır.
    """
    first = session.get_inputs()[0]
    shape = tuple(first.shape)
    fixed_ok = len(shape) == len(expected) and all(
        not isinstance(actual, int) or actual == want for actual, want in zip(shape, expected)
    )
    if not fixed_ok:
        raise ValueError(f"{model}: girdi şekli {shape}, beklenen {expected}")
    return first.name


def as_bgr(image: np.ndarray) -> np.ndarray:
    """Gri ve BGRA görüntüleri BGR'ye çevirir; uint8 olmayan ya da boyutu tutmayan diziyi reddeder."""
    if not isinstance(image, np.ndarray) or image.dtype != np.uint8:
        raise ValueError("Görüntü uint8 numpy dizisi olmalı")
    if image.ndim == 2:
        return np.repeat(image[:, :, None], 3, axis=2)
    if image.ndim == 3 and image.shape[2] == 3:
        return image
    if image.ndim == 3 and image.shape[2] == 4:
        return np.ascontiguousarray(image[:, :, :3])
    raise ValueError(f"Görüntü (yükseklik, genişlik, 3) biçiminde olmalı, gelen {image.shape}")
