#!/usr/bin/env bash
# macOS ajanının .pkg kurulum paketini oluşturur (S6). Windows'taki publish-windows.ps1'in karşılığı.
#
# Kurumun sunucu adresi ve sınav sitesinin adresi paketin içine gömülür; aday hiçbir ayar yapmaz.
# Çıktı: <output>/aiguard-agent-arm64.pkg (Apple Silicon) ve aiguard-agent-x64.pkg (Intel).
# Paket tek bir yürütülebilir dosya (self-contained, .NET kurulumu gerektirmez) ve çift tıklanan bir
# "Ajanı Başlat.command" dosyası kurar: /Applications/AI-Guard Ajanı/
#
# Örnek:
#   ./publish-macos.sh --api-base-url https://api.sinav.ornek.edu.tr --allowed-origin https://sinav.ornek.edu.tr
#
# Yerel deneme (API ve Vite önyüzü bu bilgisayarda):
#   ./publish-macos.sh --api-base-url http://localhost:5080 --allowed-origin http://localhost:5173
#
# Yalnızca bir mimari için: --arch arm64   ya da   --arch x64
set -euo pipefail

API_BASE_URL=""
ORIGINS=()
OUTPUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/dist/macos"
ARCHS=(arm64 x64)
VERSION="1.0.0"

usage() {
  echo "Kullanım: $0 --api-base-url <adres> --allowed-origin <adres> [--allowed-origin <adres> ...] [--output <klasör>] [--arch arm64|x64]" >&2
  exit 2
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-base-url) API_BASE_URL="${2:-}"; shift 2 ;;
    --allowed-origin) ORIGINS+=("${2:-}"); shift 2 ;;
    --output) OUTPUT="${2:-}"; shift 2 ;;
    --arch) ARCHS=("${2:-}"); shift 2 ;;
    --version) VERSION="${2:-}"; shift 2 ;;
    *) usage ;;
  esac
done

[[ -n "$API_BASE_URL" && ${#ORIGINS[@]} -gt 0 ]] || usage

for tool in dotnet pkgbuild; do
  command -v "$tool" >/dev/null || { echo "$tool bulunamadı. Bu betik macOS'ta çalışır (pkgbuild macOS ile gelir)." >&2; exit 1; }
done

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/src/AIGuard.Agent.MacOS/AIGuard.Agent.MacOS.csproj"
STAGING="$(mktemp -d "${TMPDIR:-/tmp}/aiguard-agent-XXXXXX")"
trap 'rm -rf "$STAGING"' EXIT
mkdir -p "$OUTPUT"

# Ajan açılışta bu ayarları doğruluyor (https zorunluluğu, origin biçimi); hatalıysa ajan açılmaz.
origins_json=""
for o in "${ORIGINS[@]}"; do
  origins_json+="${origins_json:+, }\"$o\""
done
SETTINGS="$STAGING/deployment.json"
printf '{ "Agent": { "ApiBaseUrl": "%s", "AllowedOrigins": [ %s ] } }\n' "$API_BASE_URL" "$origins_json" > "$SETTINGS"

for arch in "${ARCHS[@]}"; do
  case "$arch" in
    arm64) rid="osx-arm64" ;;
    x64) rid="osx-x64" ;;
    *) echo "Bilinmeyen mimari: $arch (arm64 ya da x64)" >&2; exit 2 ;;
  esac

  build="$STAGING/build-$arch"
  payload="$STAGING/payload-$arch"
  app_dir="$payload/Applications/AI-Guard Ajanı"
  mkdir -p "$app_dir"

  dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained \
    -p:PublishSingleFile=true -p:DebugType=none \
    -p:DeploymentSettingsFile="$SETTINGS" \
    -o "$build"

  cp "$build/aiguard-agent" "$app_dir/aiguard-agent"
  chmod 755 "$app_dir/aiguard-agent"

  # Aday bu dosyaya çift tıklıyor; Terminal açılıp ajanı başlatıyor ve pencere ajanın çalıştığını gösteriyor.
  cat > "$app_dir/Ajanı Başlat.command" <<'EOF'
#!/bin/bash
cd "$(dirname "$0")"
# Tarayıcıdan indirilen dosyalara macOS karantina işareti koyuyor; ajan bu klasörde çalışırken kaldırılıyor.
xattr -dr com.apple.quarantine . 2>/dev/null
exec ./aiguard-agent
EOF
  chmod 755 "$app_dir/Ajanı Başlat.command"

  pkg="$OUTPUT/aiguard-agent-$arch.pkg"
  # Paket içindeki dosyalar paketi yapan kullanıcıya ait kaydedilir (başka Mac'te yanlış kullanıcıya düşer).
  # Kurulumdan sonra root olarak çalışan bu betik sahipliği düzeltir.
  scripts="$STAGING/scripts-$arch"
  mkdir -p "$scripts"
  cat > "$scripts/postinstall" <<'EOF'
#!/bin/bash
APP="/Applications/AI-Guard Ajanı"
chown -R root:wheel "$APP"
chmod 755 "$APP" "$APP/aiguard-agent" "$APP/Ajanı Başlat.command"
exit 0
EOF
  chmod 755 "$scripts/postinstall"

  pkgbuild --root "$payload" --scripts "$scripts" --identifier "tr.edu.aiguard.agent" --version "$VERSION" \
    --install-location "/" "$pkg" >/dev/null

  size=$(du -m "$pkg" | cut -f1)
  echo "Kurulum paketi hazır: $pkg (${size} MB)"
done

echo "Kurulum notu: docs/ajan-kurulum-macos.md"
