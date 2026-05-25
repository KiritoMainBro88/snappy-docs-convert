param(
    [string]$PackagePath = "",
    [string]$ReleaseDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$templatePath = Join-Path $repoRoot "docs\GUI_QA_RESULT_TEMPLATE.md"

if (-not (Test-Path -LiteralPath $templatePath)) {
    throw "GUI QA template missing: $templatePath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionRoot = Join-Path $repoRoot "docs\qa-output\gui\$timestamp"
$evidenceRoot = Join-Path $sessionRoot "evidence"
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $latestZip = Get-ChildItem -LiteralPath (Join-Path $repoRoot "artifacts") -Filter "SnappyDocsConvert-portable-*.zip" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $PackagePath = if ($latestZip) { $latestZip.FullName } else { "TODO - run scripts\\package-portable.ps1 or pass -PackagePath" }
}
else {
    $PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
}

if ([string]::IsNullOrWhiteSpace($ReleaseDir)) {
    $candidateDir = $null
    if ($PackagePath -like "*.zip") {
        $candidateDir = Join-Path (Split-Path -Parent $PackagePath) ([System.IO.Path]::GetFileNameWithoutExtension($PackagePath))
    }

    if ($candidateDir -and (Test-Path -LiteralPath $candidateDir)) {
        $ReleaseDir = $candidateDir
    }
}
else {
    $ReleaseDir = (Resolve-Path -LiteralPath $ReleaseDir).Path
}

$exePath = "TODO - extract/open package and record portable exe path"
if (-not [string]::IsNullOrWhiteSpace($ReleaseDir) -and (Test-Path -LiteralPath $ReleaseDir)) {
    $exe = Get-ChildItem -LiteralPath $ReleaseDir -Recurse -Filter "SnappyDocsConvert.App.exe" -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($exe) {
        $exePath = $exe.FullName
    }
}

$branch = (& git -C $repoRoot branch --show-current 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($branch)) {
    $branch = "unknown"
}

$commit = (& git -C $repoRoot rev-parse --short HEAD 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($commit)) {
    $commit = "unknown"
}

$template = Get-Content -Raw -LiteralPath $templatePath
$result = $template
$result = $result -replace '(?m)^Package:\s*$', "Package: $PackagePath"
$result = $result -replace '(?m)^Exe:\s*$', "Exe: $exePath"
$result = $result -replace '(?m)^Git branch:\s*$', "Git branch: $branch"
$result = $result -replace '(?m)^Git commit:\s*$', "Git commit: $commit"
$result = $result -replace '(?m)^Tester:\s*$', "Tester: TODO - owner/tester name"
$result = $result -replace '(?m)^Machine:\s*$', "Machine: TODO - Windows version, display scale, Office/LibreOffice status"
$result = $result -replace '(?m)^Date:\s*$', "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
$result = $result -replace '(?m)^- Screenshots:\s*$', "- Screenshots: $evidenceRoot"

$resultPath = Join-Path $sessionRoot "MANUAL_GUI_QA_RESULT.md"
Set-Content -LiteralPath $resultPath -Value $result -Encoding UTF8

Write-Host "Manual GUI QA session created."
Write-Host "Session: $sessionRoot"
Write-Host "Result file: $resultPath"
Write-Host "Evidence folder: $evidenceRoot"
Write-Host "Package: $PackagePath"
Write-Host "Exe: $exePath"
Write-Host "Manual execution required. No GUI pass claimed."
