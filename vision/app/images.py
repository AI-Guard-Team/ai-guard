"""Base64 görüntü çözme (docs/contracts.md 1. bölüm). Görüntü yalnızca bellekte tutulur, diske yazılmaz.

Kabul edilen biçimler JPEG, PNG ve WebP; dosyanın ilk baytlarına bakılır (api'deki ImageFile ile aynı kural).
Piksel boyutu çözmeden önce başlıktan okunur: 5 MB'lık sıkıştırılmış bir PNG bile çözülünce gigabaytlarca
bellek isteyebilir. Sınırı aşan, başlığı okunamayan ya da çözülemeyen her görüntü INVALID_IMAGE'dır.
OpenCV'nin `imdecode`'u EXIF yönünü uygular: telefonun yan kaydettiği fotoğraf dik gelir.
"""

from __future__ import annotations

import base64
import binascii
import struct

import cv2
import numpy as np

from app.errors import ServiceError

# Uzun kenar sınırı. 4096 bazı telefonların varsayılan çözünürlüğünü (ör. 24 MP, 5712x4284) reddederdi;
# 6000 pikselde en kötü durum 6000x6000x3 ≈ 108 MB bellek.
MAX_SIDE_PX = 6000

# api'deki sınırlar (ImageFile.MaxPhotoBytes / MaxFrameBytes) ve base64 karşılıkları: 4 * ceil(n / 3).
MAX_PHOTO_BYTES = 5 * 1024 * 1024
MAX_FRAME_BYTES = 1024 * 1024


def base64_length(n_bytes: int) -> int:
    return 4 * ((n_bytes + 2) // 3)


MAX_PHOTO_B64 = base64_length(MAX_PHOTO_BYTES)  # 6 990 508
MAX_FRAME_B64 = base64_length(MAX_FRAME_BYTES)  # 1 398 104

_JPEG = b"\xff\xd8\xff"
_PNG = b"\x89PNG\r\n\x1a\n"
# Uzunluk alanı olmayan JPEG işaretleri: TEM, RST0–7, SOI, EOI.
_JPEG_STANDALONE = {0x01, *range(0xD0, 0xDA)}
# Kare başlıkları (SOFn); DHT (C4), JPG (C8) ve DAC (CC) değil.
_JPEG_SOF = {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}


def invalid_image(message: str = "Görüntü çözülemedi.") -> ServiceError:
    return ServiceError(422, "INVALID_IMAGE", message)


def _jpeg_size(data: bytes) -> tuple[int, int] | None:
    i = 2
    while i + 4 <= len(data):
        if data[i] != 0xFF:
            return None
        marker = data[i + 1]
        if marker == 0xFF:  # dolgu baytı
            i += 1
            continue
        if marker in _JPEG_STANDALONE:
            i += 2
            continue
        (length,) = struct.unpack(">H", data[i + 2:i + 4])
        if marker in _JPEG_SOF:
            if i + 9 > len(data):
                return None
            height, width = struct.unpack(">HH", data[i + 5:i + 9])
            return width, height
        i += 2 + length
    return None


def _webp_size(data: bytes) -> tuple[int, int] | None:
    chunk = data[12:16]
    if chunk == b"VP8 " and len(data) >= 30:  # kayıplı: 3 bayt kare etiketi + 3 bayt imza, sonra 14 bit en/boy
        width, height = struct.unpack("<HH", data[26:30])
        return width & 0x3FFF, height & 0x3FFF
    if chunk == b"VP8L" and len(data) >= 25:  # kayıpsız: 1 bayt imza, sonra 14 bit (en − 1), 14 bit (boy − 1)
        (bits,) = struct.unpack("<I", data[21:25])
        return (bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1
    if chunk == b"VP8X" and len(data) >= 30:  # genişletilmiş: 24 bit (en − 1), 24 bit (boy − 1)
        width = int.from_bytes(data[24:27], "little") + 1
        height = int.from_bytes(data[27:30], "little") + 1
        return width, height
    return None


def image_size(data: bytes) -> tuple[int, int] | None:
    """Başlıktan (genişlik, yükseklik). Biçim desteklenmiyorsa ya da başlık okunamıyorsa None."""
    if data.startswith(_PNG):
        if len(data) < 24 or data[12:16] != b"IHDR":
            return None
        return struct.unpack(">II", data[16:24])
    if data.startswith(_JPEG):
        return _jpeg_size(data)
    if len(data) >= 16 and data.startswith(b"RIFF") and data[8:12] == b"WEBP":
        return _webp_size(data)
    return None


def decode_bytes(data: bytes) -> np.ndarray:
    """JPEG/PNG/WebP baytları → BGR uint8 (yükseklik, genişlik, 3), EXIF yönü uygulanmış."""
    size = image_size(data)
    if size is None:
        raise invalid_image("Görüntü JPEG, PNG ya da WebP değil veya başlığı okunamadı.")
    if max(size) > MAX_SIDE_PX or min(size) <= 0:
        raise invalid_image(f"Görüntünün kenarı en fazla {MAX_SIDE_PX} piksel olabilir.")
    image = cv2.imdecode(np.frombuffer(data, np.uint8), cv2.IMREAD_COLOR)
    if image is None:
        raise invalid_image()
    return image


def decode_base64(value: str) -> np.ndarray:
    try:
        data = base64.b64decode(value, validate=True)
    except (binascii.Error, ValueError):
        raise invalid_image("Base64 çözülemedi.") from None
    return decode_bytes(data)
