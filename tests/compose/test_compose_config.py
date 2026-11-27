"""docker-compose.yml kural testleri (E1b).

Container başlatmadan, "docker compose config" çıktısı (değişkenler çözülmüş son hâl)
üzerinden kartın kurallarını kontrol ediyor. Docker yoksa testler atlanır.

Çalıştırma (repo kökünde):  python -m pytest tests/compose -q
"""

import json
import re
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
COMPOSE_FILE = ROOT / "docker-compose.yml"
ENV_EXAMPLE = ROOT / ".env.example"


@pytest.fixture(scope="module")
def config() -> dict:
    if shutil.which("docker") is None:
        pytest.skip("docker kurulu değil")
    # --env-file .env.example: README'deki "tek komut" ile aynı değerler kullanılsın.
    result = subprocess.run(
        ["docker", "compose", "-f", str(COMPOSE_FILE), "--env-file", str(ENV_EXAMPLE),
         "config", "--format", "json"],
        capture_output=True, text=True, cwd=ROOT, check=False,
    )
    assert result.returncode == 0, result.stderr
    return json.loads(result.stdout)


def published_ports(service: dict) -> set[tuple[str, int]]:
    return {(str(p.get("published")), int(p["target"])) for p in service.get("ports", [])}


def test_card_services_exist(config: dict) -> None:
    # Kart (revize): web, api, vision ve postgres.
    assert {"web", "api", "vision", "postgres"} <= set(config["services"])


def test_web_is_published_on_5173_and_not_on_vision_network(config: dict) -> None:
    web = config["services"]["web"]
    assert ("5173", 80) in published_ports(web)
    assert "vision-internal" not in web["networks"]


def test_api_trusts_forwarded_ip_only_from_web(config: dict) -> None:
    # K-16: X-Forwarded-For yalnızca web (nginx) adresinden kabul edilmeli; aksi hâlde IP_CHANGE atlatılır.
    web_ip = config["services"]["web"]["networks"]["backend"]["ipv4_address"]
    env = config["services"]["api"]["environment"]
    assert env["ForwardedHeaders__KnownNetworks__0"] == f"{web_ip}/32"
    subnet = config["networks"]["backend"]["ipam"]["config"][0]["subnet"]
    assert web_ip.rsplit(".", 1)[0] == subnet.split("/")[0].rsplit(".", 1)[0]


def test_default_config_files_are_given_to_api_read_only(config: dict) -> None:
    # GAB 3.3.7: varsayılan yapılandırma dosyaları konteynere verilir.
    mounts = {v["target"]: v for v in config["services"]["api"].get("volumes", [])}
    risk = mounts["/app/riskconfig.json"]
    assert risk["type"] == "bind" and risk.get("read_only") is True
    assert Path(risk["source"]).name == "riskconfig.json" and Path(risk["source"]).exists()
    # Y4 (FR-23): VPN/veri merkezi IP blokları da aynı şekilde.
    vpn = mounts["/app/vpn-ip-blocks.json"]
    assert vpn["type"] == "bind" and vpn.get("read_only") is True
    assert Path(vpn["source"]).name == "vpn-ip-blocks.json" and Path(vpn["source"]).exists()


def test_vision_publishes_no_ports(config: dict) -> None:
    # Kart: vision dış ağa port açmamalı.
    assert not config["services"]["vision"].get("ports")


def test_vision_is_only_on_internal_network(config: dict) -> None:
    networks = config["services"]["vision"]["networks"]
    assert set(networks) == {"vision-internal"}
    assert config["networks"]["vision-internal"].get("internal") is True


def test_only_api_shares_network_with_vision(config: dict) -> None:
    # "Sadece api servisi ona erişir": vision'ın ağında api'den başka servis olmamalı.
    neighbours = {
        name for name, svc in config["services"].items()
        if name != "vision" and "vision-internal" in svc.get("networks", {})
    }
    assert neighbours == {"api"}


def test_api_listens_on_8080(config: dict) -> None:
    assert ("8080", 8080) in published_ports(config["services"]["api"])


def test_postgres_is_16_with_port_and_persistent_volume(config: dict) -> None:
    pg = config["services"]["postgres"]
    # Docker'sız yerel kurulumla aynı ana sürüm.
    assert pg["image"] == "postgres:16"
    assert ("5432", 5432) in published_ports(pg)
    data_mounts = [v for v in pg.get("volumes", []) if v["target"] == "/var/lib/postgresql/data"]
    assert data_mounts and data_mounts[0]["type"] == "volume", "veri adlandırılmış volume'da kalıcı olmalı"


def test_api_waits_for_migrations_and_dependencies(config: dict) -> None:
    deps = config["services"]["api"]["depends_on"]
    assert deps["migrate"]["condition"] == "service_completed_successfully"
    assert deps["postgres"]["condition"] == "service_healthy"
    assert deps["vision"]["condition"] == "service_healthy"


def test_api_connects_with_least_privilege_user(config: dict) -> None:
    # GAB 3.3.2 en az ayrıcalık (S-7, K-50): api tabloların sahibiyle değil, db-user adımının açtığı
    # kısıtlı kullanıcıyla bağlanır; göçler sahip kullanıcıyla kalır.
    services = config["services"]
    api_conn = services["api"]["environment"]["ConnectionStrings__Default"]
    migrate_conn = services["migrate"]["environment"]["ConnectionStrings__Default"]
    assert "Username=aiguard_app;" in api_conn
    assert "Username=aiguard_app;" not in migrate_conn
    assert services["api"]["depends_on"]["db-user"]["condition"] == "service_completed_successfully"
    assert services["db-user"]["depends_on"]["migrate"]["condition"] == "service_completed_successfully"


def test_api_reaches_services_by_container_name(config: dict) -> None:
    env = config["services"]["api"]["environment"]
    # localhost container'ın kendisi demek; diğer servislere servis adıyla gidilmeli.
    assert env["VISION_SERVICE_URL"] == "http://vision:8000"
    assert "Host=postgres;" in env["ConnectionStrings__Default"]


def test_compose_variables_are_defined_in_env_example() -> None:
    # Kart: ortam değişkeni adları .env.example ile aynı. Compose'da kullanılan her ${AD}
    # .env.example'da tanımlı olmalı; yoksa "tek komutla çalıştırma" bozulur.
    compose_text = COMPOSE_FILE.read_text(encoding="utf-8")
    used = set(re.findall(r"(?<!\$)\$\{([A-Za-z_][A-Za-z0-9_]*)", compose_text))
    defined = set(re.findall(r"^([A-Za-z_][A-Za-z0-9_]*)=", ENV_EXAMPLE.read_text(encoding="utf-8"), re.M))
    # Varsayılanı compose içinde verilen ve .env.example'a konması gerekmeyenler.
    optional = {"ASPNETCORE_ENVIRONMENT"}
    assert used - defined - optional == set()
