#requires -Version 5.1
<#
.SYNOPSIS
    Publishes Dwarpal and builds the Inno Setup installer.

.DESCRIPTION
    The setup bundles the official .NET 8 Desktop Runtime (x64) installer so a
    clean machine can install the prerequisite without opening a browser.
    The runtime download is pinned by version and SHA-512 in this script; update
    both values together when servicing the runtime.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version 1.0.2
#>
param(
    [string]$Version = "1.0.2",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotnetPath = "dotnet",
    [string]$RuntimeInstallerUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.32/windowsdesktop-runtime-8.0.32-win-x64.exe",
    [string]$RuntimeInstallerSha512 = "34f64d5e365ba85db6ba4af06d382eb7ca4933ac492cb907f417281ed5683babec4beecacb20b195d8bab8d16a273ff13cb6d82523e17e363dcab64f936404f4"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\Dwarpal.App\Dwarpal.App.csproj"
$publishDir = Join-Path $root "artifacts\publish"
$runtimeDir = Join-Path $root "artifacts\prereq"
$runtimeFileName = "windowsdesktop-runtime-8.0.32-win-x64.exe"
$runtimeInstaller = Join-Path $runtimeDir $runtimeFileName
$iss = Join-Path $root "installer\Dwarpal.iss"
$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"

if (-not (Test-Path $iscc)) {
    throw "Inno Setup 6 not found at '$iscc'. Install it from https://jrsoftware.org/isdl.php and retry."
}

function Get-DotNetDesktopRuntime {
    if (Test-Path $runtimeInstaller) {
        $existingHash = (Get-FileHash $runtimeInstaller -Algorithm SHA512).Hash.ToLowerInvariant()
        if ($existingHash -eq $RuntimeInstallerSha512.ToLowerInvariant()) {
            Write-Host "Using cached .NET 8 Desktop Runtime installer." -ForegroundColor DarkGray
            return
        }

        Remove-Item $runtimeInstaller -Force
    }

    New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null

    Write-Host "Downloading the .NET 8 Desktop Runtime (x64) prerequisite..." -ForegroundColor Cyan
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $RuntimeInstallerUrl -OutFile $runtimeInstaller

    $hash = (Get-FileHash $runtimeInstaller -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($hash -ne $RuntimeInstallerSha512.ToLowerInvariant()) {
        Remove-Item $runtimeInstaller -Force -ErrorAction SilentlyContinue
        throw "The .NET 8 Desktop Runtime download failed SHA-512 verification. Expected $RuntimeInstallerSha512 but got $hash."
    }

    $signature = Get-AuthenticodeSignature $runtimeInstaller
    if ($signature.Status -ne "Valid" -or $signature.SignerCertificate.Subject -notmatch "O=Microsoft Corporation") {
        Remove-Item $runtimeInstaller -Force -ErrorAction SilentlyContinue
        throw "The .NET 8 Desktop Runtime download is not signed by Microsoft (status: $($signature.Status))."
    }
}

Get-DotNetDesktopRuntime

if (-not (Test-Path $runtimeInstaller)) {
    throw "The .NET 8 Desktop Runtime installer is missing: $runtimeInstaller"
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

Write-Host "Building installer (includes the .NET 8 Desktop Runtime)..." -ForegroundColor Cyan

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
