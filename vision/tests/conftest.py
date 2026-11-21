import os
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

# Testler vision/ klasöründen "pytest" ile çalışıyor; main.py'nin import edilebilmesi için.
VISION_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(VISION_DIR))

# main.py anahtarsız açılmaz; uygulama içe aktarılmadan önce test anahtarı tanımlanmalı.
os.environ.setdefault("VISION_SERVICE_KEY", "test-key")

# Gerçek model dosyalarının klasörü (scripts/download_models.py). "models" işaretli testler bunu kullanır.
REAL_MODEL_DIR = Path(os.environ.get("VISION_MODEL_DIR", VISION_DIR / "models"))
# Uygulama testleri modelsiz çalışır: CI'daki durumla aynı (uçlar 503) ve her istemcide model yüklenmez.
os.environ["VISION_MODEL_DIR"] = str(VISION_DIR / "tests" / "_no_models")

KEY = {"X-Service-Key": "test-key"}
VEC = [0.0] * 512


def pytest_configure(config):
    config.addinivalue_line("markers", "models: gerçek model dosyalarıyla çalışır; dosyalar yoksa atlanır")


@pytest.fixture
def client():
    # main burada içe aktarılıyor: yukarıdaki ortam değişkeni ondan önce ayarlanmış olsun diye.
    from main import app

    with TestClient(app) as c:
        yield c
