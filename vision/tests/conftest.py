import os
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

# Testler vision/ klasöründen "pytest" ile çalışıyor; main.py'nin import edilebilmesi için.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

# main.py anahtarsız açılmaz; uygulama içe aktarılmadan önce test anahtarı tanımlanmalı.
os.environ.setdefault("VISION_SERVICE_KEY", "test-key")

KEY = {"X-Service-Key": "test-key"}
VEC = [0.0] * 512


@pytest.fixture
def client():
    # main burada içe aktarılıyor: yukarıdaki ortam değişkeni ondan önce ayarlanmış olsun diye.
    from main import app

    with TestClient(app) as c:
        yield c
