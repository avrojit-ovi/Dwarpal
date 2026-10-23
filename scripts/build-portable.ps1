#requires -Version 5.1
<#
.SYNOPSIS
    Publishes Dwarpal and packages a portable (no-installer) ZIP.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build-portable.ps1 -Version 1.0.3

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build-portable.ps1 -Version 1.0.3 -SelfContained
#>
param(
    [string]$Version = "1.0.3",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\Dwarpal.App\Dwarpal.App.csproj"
$stage = Join-Path $root "artifacts\portable\Dwarpal-$Version-portable"
$zip = Join-Path $root "artifacts\Dwarpal-$Version-portable.zip"
$readme = Join-Path $root "docs\portable-readme.txt"

if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}

New-Item -ItemType Directory -Path $stage -Force | Out-Null

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing Dwarpal $Version portable ($Configuration/$Runtime, self-contained=$selfContainedValue)..." -ForegroundColor Cyan

& $DotnetPath publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $stage

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $stage "Dwarpal.exe"))) {
    throw "Portable output does not contain Dwarpal.exe."
}

Copy-Item $readme (Join-Path $stage "README-portable.txt") -Force

if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Write-Host "Packaging ZIP..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal

if (-not (Test-Path $zip)) {
    throw "Portable ZIP missing: $zip"
}

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)

Write-Host "Portable build ready: $zip ($size MB)" -ForegroundColor Green
