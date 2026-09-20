<#
.SYNOPSIS
    Orchestrator — runs all MSSQL (.sql) and MongoDB (.js) seed scripts in order.

.DESCRIPTION
    Discovers every file under scripts/mssql/ and scripts/mongo/ (sorted by name),
    then delegates each to Invoke-MssqlScript.ps1 / Invoke-MongoScript.ps1.

    This script is the entrypoint used by the db-init Docker service, but it can
    also be run locally after `docker compose up -d` to re-apply scripts.

.EXAMPLE
    # From the project root:
    ./scripts/Run-AllScripts.ps1

    # If running from inside the Docker init container (scripts mounted at /scripts):
    pwsh /scripts/Run-AllScripts.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Determine the directory that contains this script so paths are portable
# (works both locally and inside the Docker container where it is mounted at /scripts)
$ScriptsDir = $PSScriptRoot
if (-not $ScriptsDir) { $ScriptsDir = "/scripts" }

Write-Host "==========================================" -ForegroundColor Yellow
Write-Host " SchemaHunter DB Init — Run-AllScripts.ps1"
Write-Host " Scripts root: $ScriptsDir"
Write-Host "==========================================" -ForegroundColor Yellow

# ── MSSQL scripts ─────────────────────────────────────────────────────────────
$mssqlDir = Join-Path $ScriptsDir "mssql"
if (Test-Path $mssqlDir) {
    $sqlFiles = Get-ChildItem -Path $mssqlDir -Filter "*.sql" | Sort-Object Name

    if ($sqlFiles.Count -eq 0) {
        Write-Host "[MSSQL] No .sql files found in $mssqlDir" -ForegroundColor DarkYellow
    } else {
        Write-Host "`n[MSSQL] Found $($sqlFiles.Count) script(s) to execute" -ForegroundColor Cyan
        foreach ($file in $sqlFiles) {
            & "$ScriptsDir/Invoke-MssqlScript.ps1" -FilePath $file.FullName
        }
    }
} else {
    Write-Host "[MSSQL] Directory not found, skipping: $mssqlDir" -ForegroundColor DarkYellow
}

# ── MongoDB scripts ───────────────────────────────────────────────────────────
$mongoDir = Join-Path $ScriptsDir "mongo"
if (Test-Path $mongoDir) {
    $jsFiles = Get-ChildItem -Path $mongoDir -Filter "*.js" | Sort-Object Name

    if ($jsFiles.Count -eq 0) {
        Write-Host "[MONGO] No .js files found in $mongoDir" -ForegroundColor DarkYellow
    } else {
        Write-Host "`n[MONGO] Found $($jsFiles.Count) script(s) to execute" -ForegroundColor Cyan
        foreach ($file in $jsFiles) {
            & "$ScriptsDir/Invoke-MongoScript.ps1" -FilePath $file.FullName
        }
    }
} else {
    Write-Host "[MONGO] Directory not found, skipping: $mongoDir" -ForegroundColor DarkYellow
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host " All init scripts completed successfully."
Write-Host "==========================================" -ForegroundColor Green
