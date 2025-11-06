#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$EngineDir = Resolve-Path (Join-Path $ScriptDir "../../../engine")
$OutputDir = Join-Path $ScriptDir "../Hako/Resources"

# Check if wasm-opt is available
if (-not (Get-Command wasm-opt -ErrorAction SilentlyContinue)) {
    Write-Error "Error: wasm-opt not found in PATH"
    exit 1
}

# Change to engine directory
Push-Location $EngineDir

try {
    # Build
    make clean
    if ($LASTEXITCODE -ne 0) {
        throw "make clean failed with exit code $LASTEXITCODE"
    }

    make
    if ($LASTEXITCODE -ne 0) {
        throw "make failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path "hako.wasm")) {
        Write-Error "Error: hako.wasm not found after make"
        exit 1
    }

    # Create output directory if it doesn't exist
    $OutputDirResolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDir)
    if (-not (Test-Path $OutputDirResolved)) {
        New-Item -ItemType Directory -Path $OutputDirResolved | Out-Null
    }

    # Optimize
    $OutputFile = Join-Path $OutputDirResolved "hako.wasm"
    wasm-opt hako.wasm `
        --enable-bulk-memory `
        --enable-simd `
        --enable-nontrapping-float-to-int `
        --enable-tail-call `
        -O3 `
        -o $OutputFile

    if ($LASTEXITCODE -ne 0) {
        throw "wasm-opt failed with exit code $LASTEXITCODE"
    }

    Write-Host "Built and optimized hako -> $OutputFile"
}
finally {
    Pop-Location
}