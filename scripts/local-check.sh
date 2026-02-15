#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_DIR="$ROOT_DIR/src/Demo.Web"
SLN_PATH="$ROOT_DIR/src/Barcode.Generator.sln"

ok() { printf "[OK] %s\n" "$1"; }
warn() { printf "[WARN] %s\n" "$1"; }
err() { printf "[ERR] %s\n" "$1"; }

check_cmd() {
  local cmd="$1"
  local tip="$2"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    err "Missing required command: $cmd"
    warn "$tip"
    return 1
  fi
  ok "$cmd detected: $(command -v "$cmd")"
}

printf "\n=== Local pre-push checks ===\n"
printf "Repo: %s\n\n" "$ROOT_DIR"

check_cmd node "Install Node.js 20+ (https://nodejs.org/)" || exit 1
check_cmd npm "npm ships with Node.js" || exit 1
check_cmd dotnet "Install .NET SDK 8+ (https://dotnet.microsoft.com/download)" || exit 1

printf "\n--- Backend tests (.NET) ---\n"
dotnet --version
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test "$SLN_PATH" --configuration Release
ok "Backend tests passed"

printf "\n--- Frontend E2E (Playwright) ---\n"
pushd "$WEB_DIR" >/dev/null
npm ci
npx playwright install --with-deps chromium
npm run test:e2e
popd >/dev/null
ok "Frontend E2E passed"

printf "\n✅ All local checks passed. Safe to push.\n"
