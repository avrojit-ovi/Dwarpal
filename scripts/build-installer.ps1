#requires -Version 5.1
<#
.SYNOPSIS
    Publishes Dwarpal and builds the Inno Setup installer.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version 1.0.0
#>
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\Dwarpal.App\Dwarpal.App.csproj"
$publishDir = Join-Path $root "artifacts\publish"
$iss = Join-Path $root "installer\Dwarpal.iss"
$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"

if (-not (Test-Path $iscc)) {
    throw "Inno Setup 6 not found at '$iscc'. Install it from https://jrsoftware.org/isdl.php and retry."
}

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "Publishing Dwarpal $Version ($Configuration/$Runtime)..." -ForegroundColor Cyan

& $DotnetPath publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $publishDir "Dwarpal.exe"))) {
    throw "Publish output does not contain Dwarpal.exe."
}

Write-Host "Building installer..." -ForegroundColor Cyan

& $iscc "/DAppVersion=$Version" $iss

if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE."
}

$setup = Join-Path $root "installer\Output\Dwarpal-Setup-$Version.exe"

if (-not (Test-Path $setup)) {
    throw "Installer output missing: $setup"
}

$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)

Write-Host "Installer ready: $setup ($size MB)" -ForegroundColor Green
