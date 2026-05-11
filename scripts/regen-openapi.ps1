# Regenerate the committed OpenAPI snapshot from the AuditApi project.
#
# Usage:
#   pwsh ./scripts/regen-openapi.ps1
#
# What it does:
#   1. Builds Spydersoft.AuditApi (release config not required; uses the existing
#      Debug build for speed).
#   2. Runs the project with `--export-openapi <abs-path>` which spins up a
#      minimal in-process host and writes the document to disk.
#
# Run this whenever the API's controllers, DTOs, or OpenAPI document
# transformers change. Commit the resulting JSON alongside the API change so
# Kiota's generated client stays in sync.

$ErrorActionPreference = 'Stop'

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..')
$apiProject = Join-Path $repoRoot 'src/Spydersoft.AuditApi'
$outputPath = Join-Path $repoRoot 'src/Spydersoft.AuditApi/openapi/audit-api-v1.json'

Write-Host "Regenerating OpenAPI snapshot..."
Write-Host "  project: $apiProject"
Write-Host "  output:  $outputPath"
Write-Host ""

dotnet run --project $apiProject -- --export-openapi $outputPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "OpenAPI export failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Done. Review the diff and commit the snapshot if it changed:"
Write-Host "  git diff src/Spydersoft.AuditApi/openapi/audit-api-v1.json"
