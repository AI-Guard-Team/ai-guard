"""AI-Guard vision servisi — GEÇİCİ İSKELET (E1b).

Docker Compose'un üç servisi ayağa kaldırabilmesi için yalnızca /health ucu var.
Asıl uygulama D1 kartında (Dilan) bu dosyanın yerine geçecek; /health ucunun
korunması yeterli (docker-compose.yml'deki healthcheck onu çağırıyor).
"""

from fastapi import FastAPI

app = FastAPI(title="AI-Guard vision")


@app.get("/health")
def health() -> dict[str, str]:
    # API'nin /health yanıtıyla aynı kelime, kontroller tek biçimde yazılsın diye.
    return {"status": "Healthy"}
