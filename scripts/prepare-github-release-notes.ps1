param(
    [string]$Version = "v0.1.0-rc1",
    [string]$ReleaseNotesPath = "docs\releases\v0.1.0-rc1.md",
    [string]$PackagePath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$notesPath = if ([System.IO.Path]::IsPathRooted($ReleaseNotesPath)) {
    $ReleaseNotesPath
}
else {
    Join-Path $repoRoot $ReleaseNotesPath
}

if (-not (Test-Path -LiteralPath $notesPath)) {
    throw "Release notes file missing: $notesPath"
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $latestZip = Get-ChildItem -LiteralPath (Join-Path $repoRoot "artifacts") -Filter "SnappyDocsConvert-portable-win-x64-*.zip" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($latestZip) {
        $PackagePath = $latestZip.FullName
    }
}
else {
    $PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
}

Write-Host "Release title: kmb file tools $Version"
Write-Host "Release notes: $notesPath"

if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
    $packageItem = Get-Item -LiteralPath $PackagePath
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $PackagePath
    Write-Host "Package: $PackagePath"
    Write-Host "Package size bytes: $($packageItem.Length)"
    Write-Host "Package SHA256: $($hash.Hash)"
}
else {
    Write-Host "Package: not found. Run scripts\\package-portable.ps1 first."
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
    Write-Host ""
    Write-Host "Recommended GitHub CLI commands after owner approval:"
    Write-Host "  git push origin ai/phase-00-local-agent-setup"
    Write-Host "  git tag $Version"
    Write-Host "  git push origin $Version"
    if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
        Write-Host "  gh release create $Version `"$PackagePath`" --title `"kmb file tools $Version`" --notes-file `"$notesPath`" --draft"
    }
    else {
        Write-Host "  gh release create $Version --title `"kmb file tools $Version`" --notes-file `"$notesPath`" --draft"
    }
}
else {
    Write-Host ""
    Write-Host "gh not found. Manual GitHub UI steps:"
    Write-Host "  1. Push branch only after owner approval."
    Write-Host "  2. Open GitHub > Releases > Draft a new release."
    Write-Host "  3. Tag: $Version"
    Write-Host "  4. Title: kmb file tools $Version"
    Write-Host "  5. Paste body from: $notesPath"
    Write-Host "  6. Upload portable zip artifact."
    Write-Host "  7. Keep draft until owner approves publish."
}

Write-Host ""
Write-Host "No release created by this script."
