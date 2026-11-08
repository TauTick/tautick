#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

if ! command -v dotnet >/dev/null 2>&1; then
    printf '%s\n' 'dotnet was not found. Install the .NET SDK pinned in global.json and add it to PATH. See README.md: Installation and quick start.' >&2
    exit 1
fi

dotnet --version || {
    status=$?
    printf '%s\n' 'Install the exact .NET SDK pinned in global.json (roll-forward is disabled). See README.md: Installation and quick start.' >&2
    exit "$status"
}

dotnet restore TauTick.slnx --locked-mode
dotnet build TauTick.slnx --configuration Debug --no-restore
exec dotnet run --project src/TauTick.Desktop --configuration Debug --no-build --no-restore
