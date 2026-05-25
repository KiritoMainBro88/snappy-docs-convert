param(
    [int] $WaitSeconds = 5
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$demoRoot = Join-Path $repoRoot "artifacts\demo\desktop"
$publishRoot = Join-Path $demoRoot "publish"
$screenshotPath = Join-Path $demoRoot "app-home.png"

New-Item -ItemType Directory -Force -Path $demoRoot | Out-Null

Write-Host "Privacy warning: close private windows before desktop screenshot capture."

try {
    dotnet publish (Join-Path $repoRoot "src\SnappyDocsConvert.App\SnappyDocsConvert.App.csproj") -c Debug -o $publishRoot | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $exe = Join-Path $publishRoot "SnappyDocsConvert.App.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Published app exe missing: $exe"
    }

    $process = Start-Process -FilePath $exe -PassThru
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
        $process.Refresh()
        if ($process.HasExited) {
            throw "App exited before screenshot. ExitCode=$($process.ExitCode)"
        }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            break
        }
    }

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ScreenshotNativeWindow {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

    [void][ScreenshotNativeWindow]::SetForegroundWindow($process.MainWindowHandle)
    Start-Sleep -Milliseconds 400
    $rect = New-Object ScreenshotNativeWindow+RECT
    if (-not [ScreenshotNativeWindow]::GetWindowRect($process.MainWindowHandle, [ref]$rect)) {
        throw "Could not get app window rectangle."
    }
    $width = [Math]::Max(1, $rect.Right - $rect.Left)
    $height = [Math]::Max(1, $rect.Bottom - $rect.Top)
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
        $bitmap.Save($screenshotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    Write-Host "Desktop screenshot: $screenshotPath"
}
catch {
    Write-Warning "Desktop screenshot skipped: $($_.Exception.Message)"
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
