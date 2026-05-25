param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$Version = "",
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appProject = Join-Path $repoRoot "src\SnappyDocsConvert.App\SnappyDocsConvert.App.csproj"

if (-not (Test-Path -LiteralPath $appProject)) {
    throw "App project not found: $appProject"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $gitSha = ""
    try {
        $gitSha = (& git -C $repoRoot rev-parse --short HEAD 2>$null).Trim()
    }
    catch {
        $gitSha = ""
    }

    $Version = if ([string]::IsNullOrWhiteSpace($gitSha)) {
        Get-Date -Format "yyyyMMdd-HHmmss"
    }
    else {
        $gitSha
    }
}

$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $repoRoot $OutputRoot
}

$packageName = "SnappyDocsConvert-portable-$Runtime-$Version"
$portableDir = Join-Path $outputRootPath $packageName
$zipPath = Join-Path $outputRootPath "$packageName.zip"
$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }

Write-Host "Portable package: $packageName"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Self-contained: $selfContainedValue"

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

if (Test-Path -LiteralPath $portableDir) {
    Remove-Item -LiteralPath $portableDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

dotnet restore $appProject
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed."
}

dotnet publish $appProject -c $Configuration -r $Runtime --self-contained $selfContainedValue -o $portableDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Get-ChildItem -LiteralPath $portableDir -Recurse -File |
    Where-Object { $_.Extension -ieq ".pdb" } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

$runtimeNote = if ($SelfContained.IsPresent) {
    "This self-contained build includes .NET runtime files for $Runtime."
}
else {
    "This framework-dependent build requires the .NET 9 Desktop Runtime on Windows x64."
}

$quickstart = @"
Snappy Docs Convert - Portable Quickstart

1. Run SnappyDocsConvert.App.exe.
2. Add files or folders: PDF, DOC, DOCX, RTF, ODT, PPT, PPTX, ODP.
3. Choose an output folder.
4. Choose target: PDF, Images, or PDF + Images.
5. Choose engine: Auto, Microsoft Office, or LibreOffice.
6. Start queue.

$runtimeNote

Microsoft Office is optional and recommended for best DOCX/PPTX fidelity.
LibreOffice is optional fallback and is not bundled. The app can guide you to install it or choose soffice.com/soffice.exe.

No cloud upload. No telemetry.
"@

$privacy = @"
Snappy Docs Convert - Privacy

- Local-only conversion.
- No cloud upload.
- No telemetry, analytics, or tracking.
- No bundled remote converter.
- Microsoft Office and LibreOffice, when used, run locally in the logged-in Windows user session.
"@

$thirdParty = @"
Snappy Docs Convert - Third-Party Notices (Initial MVP)

This portable package contains the Snappy Docs Convert WPF app and .NET publish output.

$runtimeNote

PDF rendering uses PDFtoImage at a high level, backed by PDFium and SkiaSharp runtime components.

LibreOffice is not bundled. If needed, install LibreOffice from the official site:
https://www.libreoffice.org/download/download-libreoffice/

Microsoft Office is not bundled. If installed and activated locally, the app can use Word/PowerPoint automation for document export.

These are initial MVP notices, not a complete legal attribution audit.
"@

Set-Content -LiteralPath (Join-Path $portableDir "README-QUICKSTART.txt") -Value $quickstart -Encoding UTF8
Set-Content -LiteralPath (Join-Path $portableDir "PRIVACY.txt") -Value $privacy -Encoding UTF8
Set-Content -LiteralPath (Join-Path $portableDir "THIRD-PARTY-NOTICES.txt") -Value $thirdParty -Encoding UTF8

$exePath = Join-Path $portableDir "SnappyDocsConvert.App.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Published executable missing: $exePath"
}

Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$zipItem = Get-Item -LiteralPath $zipPath
$dirSize = (Get-ChildItem -LiteralPath $portableDir -Recurse -File | Measure-Object -Property Length -Sum).Sum

Write-Host "Portable dir: $portableDir"
Write-Host "Portable dir size: $dirSize bytes"
Write-Host "Zip: $zipPath"
Write-Host "Zip size: $($zipItem.Length) bytes"
Write-Host "Exe: $exePath"
Write-Host "Package complete."
