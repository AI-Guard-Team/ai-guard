"""Geçici vision iskeletinin /health ucu testleri (E1b)."""

from main import app, health


def test_health_returns_healthy() -> None:
    assert health() == {"status": "Healthy"}


def test_health_route_is_registered_as_get() -> None:
    # docker-compose.yml'deki healthcheck GET /health çağırıyor; yol değişirse container "unhealthy" olur.
    routes = {(r.path, m) for r in app.routes for m in getattr(r, "methods", set())}
    assert ("/health", "GET") in routes
