$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$siteRoot = Join-Path $repoRoot "website-static"
$indexPath = Join-Path $siteRoot "index.html"

if (-not (Test-Path -LiteralPath $indexPath)) {
    throw "website-static/index.html missing."
}

$html = Get-Content -LiteralPath $indexPath -Raw

$required = @(
    "https://github.com/KiritoMainBro88/snappy-docs-convert/releases/latest",
    "https://github.com/KiritoMainBro88/snappy-docs-convert",
    "No upload",
    "No telemetry",
    "Free local document converter"
)

foreach ($text in $required) {
    if ($html -notmatch [regex]::Escape($text)) {
        throw "Required website text/link missing: $text"
    }
}

$externalAssetLines = Select-String -LiteralPath $indexPath -Pattern '<script[^>]+src=["''][^"'']*https?://|<link[^>]+href=["''][^"'']*https?://' -CaseSensitive:$false
if ($externalAssetLines) {
    throw "External CDN/font/script assets found in website-static/index.html."
}

$externalCss = Get-ChildItem -LiteralPath $siteRoot -Filter "*.css" -File -ErrorAction SilentlyContinue |
    Select-String -Pattern 'https?://' -CaseSensitive:$false
if ($externalCss) {
    throw "External URL found in website CSS."
}

Write-Host "Website static check: index exists"
Write-Host "Website static check: latest release link exists"
Write-Host "Website static check: source link exists"
Write-Host "Website static check: local-only/no-upload/no-telemetry text exists"
Write-Host "Website static check: no external CDN/font/script assets"
Write-Host "Website static check: pass"
