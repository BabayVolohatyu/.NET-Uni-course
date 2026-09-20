<#
.SYNOPSIS
    Executes a single .sql file against the SQL Server instance.

.DESCRIPTION
    Generic script to send a SQL transaction to MSSQL.
    Connection details are read from environment variables (set by docker-compose or .env),
    but can be overridden with explicit parameters.

.PARAMETER FilePath
    Absolute or relative path to the .sql file to execute. (Required)

.PARAMETER Server
    SQL Server host and port, e.g. "localhost,1433".
    Defaults to $env:MSSQL_HOST + "," + $env:MSSQL_PORT (or "localhost,1433").

.PARAMETER Username
    Login username. Defaults to "sa".

.PARAMETER Password
    Login password. Defaults to $env:MSSQL_SA_PASSWORD.

.EXAMPLE
    # From the project root (reads env vars automatically):
    ./scripts/Invoke-MssqlScript.ps1 -FilePath ./scripts/mssql/01-context1-schema.sql

    # Override connection explicitly:
    ./scripts/Invoke-MssqlScript.ps1 -FilePath ./scripts/mssql/01-context1-schema.sql `
        -Server "myserver,1433" -Password "S3cr3t!"
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string] $FilePath,

    [Parameter(Mandatory = $false)]
    [string] $Server,

    [Parameter(Mandatory = $false)]
    [string] $Username = "sa",

    [Parameter(Mandatory = $false)]
    [string] $Password
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Resolve connection parameters from env when not provided ──────────────────
if (-not $Server) {
    $host_  = if ($env:MSSQL_HOST) { $env:MSSQL_HOST } else { "localhost" }
    $port   = if ($env:MSSQL_PORT) { $env:MSSQL_PORT } else { "1433" }
    $Server = "${host_},${port}"
}

if (-not $Password) {
    if (-not $env:MSSQL_SA_PASSWORD) {
        throw "MSSQL password not provided. Set -Password or set the MSSQL_SA_PASSWORD environment variable."
    }
    $Password = $env:MSSQL_SA_PASSWORD
}

# ── Resolve and validate file path ───────────────────────────────────────────
$resolvedPath = Resolve-Path -Path $FilePath -ErrorAction SilentlyContinue
if (-not $resolvedPath) {
    throw "SQL file not found: '$FilePath'"
}
$FilePath = $resolvedPath.Path

# ── Locate sqlcmd ─────────────────────────────────────────────────────────────
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    # Try well-known paths inside the Microsoft SQL tools image
    $candidates = @(
        "/opt/mssql-tools18/bin/sqlcmd",
        "/opt/mssql-tools/bin/sqlcmd",
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $sqlcmd = $c; break }
    }
    if (-not $sqlcmd) {
        throw "sqlcmd not found. Install mssql-tools18 (Linux) or SQL Server Command Line Tools (Windows)."
    }
}

Write-Host "[MSSQL] Executing: $FilePath" -ForegroundColor Cyan
Write-Host "        Server   : $Server" -ForegroundColor DarkGray

# ── Execute ───────────────────────────────────────────────────────────────────
# -C  = Trust Server Certificate (self-signed in dev containers)
# -I  = Enable QUOTED_IDENTIFIER (required for filtered indexes)
# -b  = Exit with error code on batch failure
& "$sqlcmd" -S $Server -U $Username -P $Password -i "$FilePath" -C -I -b

if ($LASTEXITCODE -ne 0) {
    Write-Error "[MSSQL] sqlcmd exited with code $LASTEXITCODE for file: $FilePath"
    exit $LASTEXITCODE
}

Write-Host "[MSSQL] Done: $FilePath" -ForegroundColor Green
