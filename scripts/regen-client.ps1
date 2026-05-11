# Regenerate the Kiota-generated audit client from the committed OpenAPI snapshot.
#
# Usage:
#   pwsh ./scripts/regen-client.ps1
#
# What it does:
#   1. Restores the local Kiota dotnet tool (idempotent).
#   2. Regenerates src/Spydersoft.Audit.Client/Generated/ from the snapshot at
#      src/Spydersoft.AuditApi/openapi/audit-api-v1.json.
#
# Prereqs:
#   - The OpenAPI snapshot must already be up-to-date. If you've changed the API,
#     run scripts/regen-openapi.ps1 first.
#
# Run this whenever:
#   - The OpenAPI snapshot changes (new endpoints, response shapes, etc.).
#   - Kiota itself is upgraded in .config/dotnet-tools.json.
#
# Commit the regenerated files alongside the snapshot/API change so consumer
# apps see them through a normal NuGet bump.

$ErrorActionPreference = 'Stop'

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..')
$openApi    = Join-Path $repoRoot 'src/Spydersoft.AuditApi/openapi/audit-api-v1.json'
$outputDir  = Join-Path $repoRoot 'src/Spydersoft.Audit.Client/Generated'

Write-Host "Restoring local tools (Kiota)..."
Push-Location $repoRoot
try {
    dotnet tool restore | Out-Null
} finally {
    Pop-Location
}

Write-Host "Regenerating audit client..."
Write-Host "  spec:   $openApi"
Write-Host "  output: $outputDir"
Write-Host ""

dotnet kiota generate `
    --openapi "$openApi" `
    --language CSharp `
    --output "$outputDir" `
    --namespace-name Spydersoft.Audit.Client.Internal.Generated `
    --class-name AuditApiClient `
    --clean-output `
    --clear-cache

if ($LASTEXITCODE -ne 0) {
    Write-Error "Kiota generation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Done. Review the diff and commit the regenerated client:"
Write-Host "  git diff src/Spydersoft.Audit.Client/Generated/"
