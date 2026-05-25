param(
    [string] $ArtifactsPath = "artifacts",
    [string[]] $FilePath = @(),
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$artifactRoot = if ([System.IO.Path]::IsPathRooted($ArtifactsPath)) {
    $ArtifactsPath
}
else {
    Join-Path $repoRoot $ArtifactsPath
}

$checksumRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $artifactRoot "checksums"
}
elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repoRoot $OutputDirectory
}

if (-not (Test-Path -LiteralPath $artifactRoot)) {
    throw "Artifacts path missing: $artifactRoot"
}

$files = New-Object System.Collections.Generic.List[System.IO.FileInfo]

foreach ($path in $FilePath) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        continue
    }

    $resolved = (Resolve-Path -LiteralPath $path).Path
    $item = Get-Item -LiteralPath $resolved
    if ($item.PSIsContainer) {
        throw "FilePath must point to a file, not a directory: $resolved"
    }

    $files.Add($item)
}

if ($files.Count -eq 0) {
    $patterns = @(
        "SnappyDocsConvert-portable-win-x64-*.zip",
        "SnappyDocsConvert-setup-win-x64-*.exe"
    )

    foreach ($pattern in $patterns) {
        Get-ChildItem -LiteralPath $artifactRoot -File -Filter $pattern -ErrorAction SilentlyContinue |
            ForEach-Object { $files.Add($_) }
    }
}

$uniqueFiles = $files |
    Where-Object { $_.Extension -in @(".zip", ".exe") } |
    Sort-Object FullName -Unique

if (-not $uniqueFiles) {
    throw "No release artifact ZIP/EXE files found under: $artifactRoot"
}

New-Item -ItemType Directory -Force -Path $checksumRoot | Out-Null

$records = foreach ($file in $uniqueFiles) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    [PSCustomObject]@{
        name = $file.Name
        path = $file.FullName
        sizeBytes = $file.Length
        sha256 = $hash.Hash
    }
}

$txtPath = Join-Path $checksumRoot "SHA256SUMS.txt"
$jsonPath = Join-Path $checksumRoot "SHA256SUMS.json"

$txtLines = foreach ($record in $records) {
    "SHA256  $($record.sha256)  $($record.name)"
}

Set-Content -LiteralPath $txtPath -Value $txtLines -Encoding UTF8

$json = [PSCustomObject]@{
    algorithm = "SHA256"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    files = $records
}

$json | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

Write-Host "Checksums text: $txtPath"
Write-Host "Checksums json: $jsonPath"
foreach ($record in $records) {
    Write-Host "$($record.sha256)  $($record.name)"
}
