"""Servis ayarları: gizli değerler ortam değişkeninden, eşikler config.yaml'dan okunur."""
import os
from functools import lru_cache
from pathlib import Path

import yaml

VISION_DIR = Path(__file__).resolve().parent.parent
CONFIG_PATH = Path(os.getenv("VISION_CONFIG", VISION_DIR / "config.yaml"))


class Settings:
    def __init__(self) -> None:
        # Anahtar koda veya config.yaml'a yazılmaz; .env / Docker Compose ile gelir (E1a .env.example).
        self.service_key: str = os.getenv("VISION_SERVICE_KEY", "")
        self.debug: bool = os.getenv("VISION_DEBUG", "false").lower() == "true"
        # Model dosyaları repoda değil; scripts/download_models.py buraya indirir.
        self.model_dir: Path = Path(os.getenv("VISION_MODEL_DIR", VISION_DIR / "models"))
        with open(CONFIG_PATH, encoding="utf-8") as f:
            self.thresholds: dict = yaml.safe_load(f) or {}


@lru_cache
def get_settings() -> Settings:
    return Settings()
