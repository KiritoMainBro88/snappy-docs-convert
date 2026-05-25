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

$sourceFiles = Get-ChildItem -LiteralPath $siteRoot -File -Recurse -Include *.html,*.ts,*.tsx,*.js,*.css,*.json |
    Where-Object { $_.FullName -notmatch "\\node_modules\\" -and $_.FullName -notmatch "\\dist\\" }

# Plain "No telemetry" copy is allowed; provider/integration markers are not.
$providerPatterns = @(
    "google-analytics",
    "googletagmanager",
    "\bgtag\s*\(",
    "posthog",
    "plausible",
    "sentry",
    "appcenter",
    "trackEvent",
    "sendTelemetry",
    "telemetryEndpoint"
)

foreach ($pattern in $providerPatterns) {
    $matches = $sourceFiles | Select-String -Pattern $pattern -CaseSensitive:$false
    if ($matches) {
        throw "Analytics/telemetry integration marker found in website source: $pattern"
    }
}

$distText = ($builtFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$required = @(
    "https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.2",
    "https://github.com/KiritoMainBro88/snappy-docs-convert",
    "https://discord.gg/kZ3U36ncun",
    "No upload",
    "No telemetry",
    "Free and open-source Windows desktop app",
    "EN/VI",
    "Dark",
    "System",
    "Portable ZIP"
)

foreach ($text in $required) {
    if ($distText -notlike "*$text*") {
        throw "Required website text/link missing from dist: $text"
    }
}

$sourceText = ($sourceFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$sourceRequired = @(
    "navigator.language",
    "localStorage",
    "prefers-color-scheme",
    "website-demo.webm",
    "/demo/home-light-en.png"
)

foreach ($text in $sourceRequired) {
    if ($sourceText -notlike "*$text*") {
        throw "Required website source behavior missing: $text"
    }
}

Write-Host "Website Vite check: dependencies installed"
Write-Host "Website Vite check: build passed"
Write-Host "Website Vite check: dist/index.html exists"
Write-Host "Website Vite check: no api/functions folder"
Write-Host "Website Vite check: no external CDN/font/script references"
Write-Host "Website Vite check: no analytics provider integration markers"
Write-Host "Website Vite check: release/source/Discord links exist"
Write-Host "Website Vite check: EN/VI language detect, local-only/no-upload/theme/demo copy exists"
Write-Host "Website Vite check: pass"
