$ErrorActionPreference = 'Stop'

Push-Location -LiteralPath $PSScriptRoot
try {
    if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
        [Console]::Error.WriteLine('dotnet was not found. Install the .NET SDK pinned in global.json and add it to PATH. See README.md: Installation and quick start.')
        exit 1
    }

    & dotnet --version
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine('Install the exact .NET SDK pinned in global.json (roll-forward is disabled). See README.md: Installation and quick start.')
        exit $LASTEXITCODE
    }

    & dotnet restore TauTick.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & dotnet build TauTick.slnx --configuration Debug --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & dotnet run --project src/TauTick.Desktop --configuration Debug --no-build --no-restore
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
