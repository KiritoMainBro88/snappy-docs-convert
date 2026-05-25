param(
    [int] $DurationSeconds = 30
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$demoRoot = Join-Path $repoRoot "artifacts\demo\desktop"
$publishRoot = Join-Path $demoRoot "publish"
$videoPath = Join-Path $demoRoot "kmb-file-tools-desktop-demo.mp4"

New-Item -ItemType Directory -Force -Path $demoRoot | Out-Null

Write-Host "Privacy warning: close private windows before screen recording."

& (Join-Path $PSScriptRoot "create-demo-inputs.ps1")

dotnet publish (Join-Path $repoRoot "src\SnappyDocsConvert.App\SnappyDocsConvert.App.csproj") -c Debug -o $publishRoot | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $publishRoot "SnappyDocsConvert.App.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Published app exe missing: $exe"
}

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) {
    Write-Warning "FFmpeg missing. Desktop video skipped."
    Write-Host "Install hint: winget install Gyan.FFmpeg"
    exit 0
}

$process = $null
try {
    $process = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 5

    $arguments = @(
        "-y",
        "-hide_banner",
        "-loglevel", "error",
        "-f", "gdigrab",
        "-framerate", "15",
        "-t", "$DurationSeconds",
        "-i", "title=kmb file tools",
        "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
        "-vcodec", "libx264",
        "-preset", "veryfast",
        "-pix_fmt", "yuv420p",
        $videoPath
    )

    & $ffmpeg.Source @arguments
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Window capture failed; falling back to desktop capture."
        $arguments = @(
            "-y",
            "-hide_banner",
            "-loglevel", "error",
            "-f", "gdigrab",
            "-framerate", "15",
            "-t", "$DurationSeconds",
            "-i", "desktop",
            "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
            "-vcodec", "libx264",
            "-preset", "veryfast",
            "-pix_fmt", "yuv420p",
            $videoPath
        )
        & $ffmpeg.Source @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "ffmpeg failed with exit code $LASTEXITCODE."
        }
    }

    if (-not (Test-Path -LiteralPath $videoPath)) {
        throw "Desktop video was not created."
    }

    $size = (Get-Item -LiteralPath $videoPath).Length
    if ($size -le 0) {
        throw "Desktop video is empty."
    }

    Write-Host "Desktop video: $videoPath ($size bytes)"
}
catch {
    Write-Warning "Desktop video skipped/failed: $($_.Exception.Message)"
}
finally {
    if ($process -and -not $process.HasExited) {
        try {
            [void] $process.CloseMainWindow()
            Start-Sleep -Seconds 2
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
            }
        }
        catch {
            Write-Warning "Could not close app process cleanly: $($_.Exception.Message)"
        }
    }
}
