param(
    [string]$PackagePath = "",
    [string]$ReleaseDir = "",
    [switch]$SkipGuiLaunch
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$tempExtract = $null
$releaseRoot = $null

try {
    if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
        $resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
        if ([System.IO.Path]::GetExtension($resolvedPackage) -ne ".zip") {
            throw "PackagePath must be a zip file: $resolvedPackage"
        }

        $tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) ("SnappyDocsConvertReleaseSmoke-" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Force -Path $tempExtract | Out-Null
        Expand-Archive -LiteralPath $resolvedPackage -DestinationPath $tempExtract -Force
        $releaseRoot = $tempExtract
        Write-Host "Release smoke: extracted $resolvedPackage"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($ReleaseDir)) {
        $releaseRoot = (Resolve-Path -LiteralPath $ReleaseDir).Path
    }
    else {
        $latestZip = Get-ChildItem -LiteralPath (Join-Path $repoRoot "artifacts") -Filter "SnappyDocsConvert-portable-*.zip" -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if ($null -eq $latestZip) {
            throw "No PackagePath/ReleaseDir provided and no portable zip found under artifacts."
        }

        $tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) ("SnappyDocsConvertReleaseSmoke-" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Force -Path $tempExtract | Out-Null
        Expand-Archive -LiteralPath $latestZip.FullName -DestinationPath $tempExtract -Force
        $releaseRoot = $tempExtract
        Write-Host "Release smoke: extracted $($latestZip.FullName)"
    }

    $exe = Get-ChildItem -LiteralPath $releaseRoot -Recurse -Filter "SnappyDocsConvert.App.exe" -File |
        Select-Object -First 1
    if ($null -eq $exe) {
        throw "SnappyDocsConvert.App.exe not found."
    }

    $appDir = $exe.Directory.FullName
    Write-Host "Release smoke: exe $($exe.FullName)"

    foreach ($requiredDoc in @("README-QUICKSTART.txt", "PRIVACY.txt", "THIRD-PARTY-NOTICES.txt")) {
        $docPath = Join-Path $appDir $requiredDoc
        if (-not (Test-Path -LiteralPath $docPath)) {
            throw "Required release doc missing: $requiredDoc"
        }
    }

    $forbiddenDirectories = @("src", "tests", ".git", "qa-output", "obj", "bin")
    $foundForbiddenDirectories = Get-ChildItem -LiteralPath $releaseRoot -Recurse -Directory |
        Where-Object { $forbiddenDirectories -contains $_.Name }
    if ($foundForbiddenDirectories) {
        throw "Forbidden directories found: $($foundForbiddenDirectories.FullName -join '; ')"
    }

    $allowedBrandingFiles = @(
        "Assets\logo.png"
    )
    $forbiddenExtensions = @(".pdf", ".png", ".jpg", ".jpeg", ".webp", ".log", ".zip")
    $foundForbiddenFiles = Get-ChildItem -LiteralPath $releaseRoot -Recurse -File |
        Where-Object {
            $relativePath = $_.FullName
            if ($_.FullName.StartsWith($appDir, [StringComparison]::OrdinalIgnoreCase)) {
                $relativePath = $_.FullName.Substring($appDir.Length).TrimStart('\', '/')
            }
            ($forbiddenExtensions -contains $_.Extension.ToLowerInvariant()) -and
                ($allowedBrandingFiles -notcontains $relativePath)
        }
    if ($foundForbiddenFiles) {
        throw "Forbidden files found: $($foundForbiddenFiles.FullName -join '; ')"
    }

    $selfCheck = Start-Process -FilePath $exe.FullName -ArgumentList "--self-check" -Wait -PassThru -WindowStyle Hidden
    if ($selfCheck.ExitCode -ne 0) {
        throw "Self-check failed with exit $($selfCheck.ExitCode)."
    }

    Write-Host "Release smoke: self-check output not captured; WinExe console output can be unavailable under automation."

    Write-Host "Release smoke: required docs present"
    Write-Host "Release smoke: forbidden content absent"
    Write-Host "Release smoke: self-check exit ok"
    if (-not $SkipGuiLaunch.IsPresent) {
        & (Join-Path $PSScriptRoot "smoke-gui-launch.ps1") -ExePath $exe.FullName
    }
    Write-Host "Release smoke: pass"
}
finally {
    if ($tempExtract -and (Test-Path -LiteralPath $tempExtract)) {
        if ($env:KEEP_SNAPPY_RELEASE_SMOKE -eq "1") {
            Write-Host "Release smoke extract kept: $tempExtract"
        }
        else {
            Start-Sleep -Milliseconds 200
            Remove-Item -LiteralPath $tempExtract -Recurse -Force
            Write-Host "Release smoke extract cleaned."
        }
    }
}
