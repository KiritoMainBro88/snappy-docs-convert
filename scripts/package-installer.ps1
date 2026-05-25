param(
    [string]$Version = "v0.1.0-beta.1",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$portableScript = Join-Path $repoRoot "scripts\package-portable.ps1"
$issPath = Join-Path $repoRoot "installer\inno\SnappyDocsConvert.iss"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$portableDir = Join-Path $artifactsRoot "SnappyDocsConvert-portable-$Runtime-$Version"
$installerPath = Join-Path $artifactsRoot "SnappyDocsConvert-setup-$Runtime-$Version.exe"
$appVersion = $Version.TrimStart("v")

if (-not (Test-Path -LiteralPath $portableScript)) {
    throw "Portable package script missing: $portableScript"
}

if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Inno Setup script missing: $issPath"
}

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot "LICENSE"))) {
    throw "LICENSE missing. Public installer requires final license."
}

& powershell -ExecutionPolicy Bypass -File $portableScript -Version $Version -Configuration $Configuration -Runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "Portable package script failed."
}

if (-not (Test-Path -LiteralPath (Join-Path $portableDir "SnappyDocsConvert.App.exe"))) {
    throw "Portable app executable missing: $portableDir"
}

$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$isccPath = if ($isccCommand) { $isccCommand.Source } else { "" }
if ([string]::IsNullOrWhiteSpace($isccPath)) {
    $commonPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $isccPath = $commonPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($isccPath)) {
    throw "Inno Setup compiler ISCC.exe not found."
}

if (Test-Path -LiteralPath $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}

$isccArgs = @(
    "/DReleaseVersion=$Version",
    "/DAppVersion=$appVersion",
    "/DSourceDir=$portableDir",
    $issPath
)

$setupIconPath = Join-Path $repoRoot "src\SnappyDocsConvert.App\Assets\app.ico"
if (Test-Path -LiteralPath $setupIconPath) {
    $isccArgs = @(
        "/DReleaseVersion=$Version",
        "/DAppVersion=$appVersion",
        "/DSourceDir=$portableDir",
        "/DSetupIconFile=$setupIconPath",
        $issPath
    )
}

& $isccPath @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed."
}

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer EXE missing: $installerPath"
}

$installer = Get-Item -LiteralPath $installerPath
if ($installer.Length -le 0) {
    throw "Installer EXE is empty: $installerPath"
}

Write-Host "Installer: $installerPath"
Write-Host "Installer size: $($installer.Length) bytes"
Write-Host "Installer complete."
