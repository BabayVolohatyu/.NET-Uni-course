<#
.SYNOPSIS
    Executes a single .js file against a MongoDB instance using mongosh.

.DESCRIPTION
    Generic script to send a MongoDB transaction / seed script.
    Connection details are read from environment variables (set by docker-compose or .env),
    but can be overridden with an explicit -Uri parameter.

.PARAMETER FilePath
    Absolute or relative path to the .js file to execute. (Required)

.PARAMETER Uri
    MongoDB connection URI, e.g. "mongodb://admin:pass@localhost:27017".
    Defaults to $env:MONGO_CONNECTION_STRING, or is assembled from
    $env:MONGO_ROOT_USER, $env:MONGO_ROOT_PASSWORD, $env:MONGO_HOST, $env:MONGO_PORT.

.EXAMPLE
    # From the project root (reads env vars automatically):
    ./scripts/Invoke-MongoScript.ps1 -FilePath ./scripts/mongo/01-context3-indexes.js

    # Override URI explicitly:
    ./scripts/Invoke-MongoScript.ps1 -FilePath ./scripts/mongo/01-context3-indexes.js `
        -Uri "mongodb://myuser:mypass@remotehost:27017"
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string] $FilePath,

    [Parameter(Mandatory = $false)]
    [string] $Uri
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Resolve connection URI from env when not provided ─────────────────────────
if (-not $Uri) {
    if ($env:MONGO_CONNECTION_STRING) {
        $Uri = $env:MONGO_CONNECTION_STRING
    } else {
        $user = if ($env:MONGO_ROOT_USER)     { $env:MONGO_ROOT_USER }     else { "admin" }
        $pass = if ($env:MONGO_ROOT_PASSWORD) { $env:MONGO_ROOT_PASSWORD } else {
            throw "MongoDB password not provided. Set -Uri or set MONGO_ROOT_PASSWORD environment variable."
        }
        $host_ = if ($env:MONGO_HOST) { $env:MONGO_HOST } else { "localhost" }
        $port  = if ($env:MONGO_PORT) { $env:MONGO_PORT } else { "27017" }
        $Uri   = "mongodb://${user}:${pass}@${host_}:${port}"
    }
}

# ── Resolve and validate file path ────────────────────────────────────────────
$resolvedPath = Resolve-Path -Path $FilePath -ErrorAction SilentlyContinue
if (-not $resolvedPath) {
    throw "JS file not found: '$FilePath'"
}
$FilePath = $resolvedPath.Path

# ── Locate mongosh ────────────────────────────────────────────────────────────
$mongosh = Get-Command mongosh -ErrorAction SilentlyContinue
if (-not $mongosh) {
    $candidates = @(
        "/usr/bin/mongosh",
        "/usr/local/bin/mongosh",
        "C:\Program Files\MongoDB\mongosh\mongosh.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $mongosh = $c; break }
    }
    if (-not $mongosh) {
        throw "mongosh not found. Install MongoDB Shell (mongosh) from https://www.mongodb.com/try/download/shell"
    }
}

Write-Host "[MONGO] Executing: $FilePath" -ForegroundColor Cyan
Write-Host "        URI      : $([regex]::Replace($Uri, '//[^:]+:[^@]+@', '//<redacted>@'))" -ForegroundColor DarkGray

# ── Execute ───────────────────────────────────────────────────────────────────
& "$mongosh" $Uri "$FilePath" --quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "[MONGO] mongosh exited with code $LASTEXITCODE for file: $FilePath"
    exit $LASTEXITCODE
}

Write-Host "[MONGO] Done: $FilePath" -ForegroundColor Green
