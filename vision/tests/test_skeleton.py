"""İskelet testleri: anahtar kontrolü, sağlık ucu ve sözleşme şemaları."""
from conftest import KEY, VEC

IMG = "aGVsbG8="  # base64 yer tutucu; şema testi için yeterli


def test_health_is_public(client):
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json() == {"status": "Healthy"}


def test_missing_key_rejected(client):
    r = client.post("/internal/face/enroll", json={"image": IMG})
    assert r.status_code == 401
    assert r.json()["error"]["code"] == "UNAUTHORIZED"


def test_wrong_key_rejected(client):
    r = client.post("/internal/face/enroll", json={"image": IMG}, headers={"X-Service-Key": "yanlis"})
    assert r.status_code == 401


def test_valid_requests_reach_endpoints(client):
    # Modeller yokken geçerli istekler 503 almalı (401/422 değil): anahtar ve şema geçti demektir.
    cases = [
        ("/internal/face/enroll", {"image": IMG}),
        ("/internal/face/verify", {"frames": [IMG] * 3, "referenceVector": VEC}),
        ("/internal/analyze/frame", {"sessionId": "s1", "frame": IMG, "ts": "2026-11-24T10:15:30.250Z",
                                      "tasks": ["face", "pose"]}),
        ("/internal/analyze/audio", {"sessionId": "s1", "chunk": IMG, "format": "webm",
                                      "ts": "2026-11-24T10:15:30.000Z"}),
    ]
    for path, body in cases:
        r = client.post(path, json=body, headers=KEY)
        assert r.status_code == 503, path
        assert r.json()["error"]["code"] == "MODELS_NOT_READY"


def test_verify_needs_3_to_5_frames(client):
    r = client.post("/internal/face/verify", json={"frames": [IMG] * 2, "referenceVector": VEC}, headers=KEY)
    assert r.status_code == 422
    assert r.json()["error"]["code"] == "VALIDATION_ERROR"


def test_vector_must_be_512(client):
    r = client.post("/internal/face/verify", json={"frames": [IMG] * 3, "referenceVector": [0.0] * 10}, headers=KEY)
    assert r.status_code == 422


def test_identity_task_requires_reference_vector(client):
    body = {"sessionId": "s1", "frame": IMG, "ts": "2026-11-24T10:15:30.250Z", "tasks": ["identity"]}
    r = client.post("/internal/analyze/frame", json=body, headers=KEY)
    assert r.status_code == 422


def test_unknown_task_rejected(client):
    body = {"sessionId": "s1", "frame": IMG, "ts": "2026-11-24T10:15:30.250Z", "tasks": ["emotion"]}
    r = client.post("/internal/analyze/frame", json=body, headers=KEY)
    assert r.status_code == 422


def test_docs_hidden_without_debug(client):
    assert client.get("/docs", headers=KEY).status_code == 404


def test_service_starts_without_model_files(client):
    # CI'da ve model indirilmemiş imajda: servis açılır, /health aynı, uçlar 503 (yukarıdaki test).
    from main import app

    assert app.state.models is None
    assert client.get("/health").json() == {"status": "Healthy"}
