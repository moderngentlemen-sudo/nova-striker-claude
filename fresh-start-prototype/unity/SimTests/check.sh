#!/usr/bin/env bash
# Failing checks fail the process; keep logs outside tracked source.
set -euo pipefail
cd "$(dirname "$0")"
NS_DOTNET=${DOTNET:-${HOME}/.dotnet/dotnet}
NS_LOG_DIR=${NS_LOG_DIR:-${TMPDIR:-/tmp}/nova-striker-checks}
mkdir -p "$NS_LOG_DIR"
"$NS_DOTNET" run --project ShieldTests/ShieldTests.csproj | tee "$NS_LOG_DIR/shield.log"
"$NS_DOTNET" run --project LevelTests/LevelTests.csproj | tee "$NS_LOG_DIR/levels.log"
"$NS_DOTNET" build SimTests.csproj -nologo | tee "$NS_LOG_DIR/trace-build.log"
python3 validate_assets.py | tee "$NS_LOG_DIR/assets.log"
