$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$siteRoot = Join-Path $repoRoot "website"
$distRoot = Join-Path $siteRoot "dist"
$indexPath = Join-Path $distRoot "index.html"

if (-not (Test-Path -LiteralPath (Join-Path $siteRoot "package.json"))) {
    throw "website/package.json missing."
}

Push-Location $siteRoot
try {
    if (Test-Path -LiteralPath (Join-Path $siteRoot "package-lock.json")) {
        npm ci
        if ($LASTEXITCODE -ne 0) {
            throw "npm ci failed with exit code $LASTEXITCODE."
        }
    }
    else {
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE."
        }
    }

    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $indexPath)) {
    throw "website/dist/index.html missing after build."
}

$forbiddenDirs = @("api", "functions")
foreach ($name in $forbiddenDirs) {
    $matches = Get-ChildItem -LiteralPath $siteRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq $name -and $_.FullName -notmatch "\\node_modules\\" -and $_.FullName -notmatch "\\dist\\" }

    if ($matches) {
        throw "Backend/API folder found: $($matches[0].FullName)"
    }
}

$builtFiles = Get-ChildItem -LiteralPath $distRoot -File -Recurse -Include *.html,*.js,*.css
$externalAssetPattern = '<script[^>]+src=["''][^"'']*https?://|<link[^>]+href=["''][^"'']*https?://|@import\s+url\(["'']?https?://'
$externalAssets = $builtFiles | Select-String -Pattern $externalAssetPattern -CaseSensitive:$false
if ($externalAssets) {
    throw "External CDN/font/script reference found in built website."
}

$distText = ($builtFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$required = @(
    "https://github.com/KiritoMainBro88/snappy-docs-convert/releases",
    "https://github.com/KiritoMainBro88/snappy-docs-convert",
    "No upload",
    "Không tải lên",
    "Free local document converter for Windows",
    "Trình chuyển đổi tài liệu cục bộ miễn phí cho Windows"
)

foreach ($text in $required) {
    if ($distText -notlike "*$text*") {
        throw "Required website text/link missing from dist: $text"
    }
}

Write-Host "Website Vite check: dependencies installed"
Write-Host "Website Vite check: build passed"
Write-Host "Website Vite check: dist/index.html exists"
Write-Host "Website Vite check: no api/functions folder"
Write-Host "Website Vite check: no external CDN/font/script references"
Write-Host "Website Vite check: release/source links exist"
Write-Host "Website Vite check: EN/VI local-only/no-upload copy exists"
Write-Host "Website Vite check: pass"
