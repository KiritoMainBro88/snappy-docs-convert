param(
    [Parameter(Mandatory = $true)]
    [string] $ExePath,
    [int] $TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$smokeRoot = Join-Path $repoRoot "artifacts\gui-smoke"
$screenshotPath = Join-Path $smokeRoot "gui-launch.png"
$logRoot = Join-Path $env:LOCALAPPDATA "kmb-file-tools\logs"
New-Item -ItemType Directory -Force -Path $smokeRoot | Out-Null

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeWindowProbe {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

function Capture-Window {
    param([System.Diagnostics.Process] $Process, [string] $Path)

    if ($Process.MainWindowHandle -eq [IntPtr]::Zero) {
        return $false
    }

    Add-Type -AssemblyName System.Drawing
    Add-Type -AssemblyName System.Windows.Forms

    [void][NativeWindowProbe]::SetForegroundWindow($Process.MainWindowHandle)
    Start-Sleep -Milliseconds 400

    $rect = New-Object NativeWindowProbe+RECT
    if (-not [NativeWindowProbe]::GetWindowRect($Process.MainWindowHandle, [ref]$rect)) {
        return $false
    }

    $width = [Math]::Max(1, $rect.Right - $rect.Left)
    $height = [Math]::Max(1, $rect.Bottom - $rect.Top)
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        return $true
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Write-Diagnostics {
    Write-Host "GUI launch diagnostics: recent Application errors"
    Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-10)} -ErrorAction SilentlyContinue |
        Where-Object { $_.LevelDisplayName -eq 'Error' } |
        Select-Object -First 5 TimeCreated, ProviderName, Id, Message |
        Format-List

    if (Test-Path -LiteralPath $logRoot) {
        Write-Host "GUI launch diagnostics: app logs"
        Get-ChildItem -LiteralPath $logRoot -File -ErrorAction SilentlyContinue |
            Select-Object FullName, Length, LastWriteTime |
            Format-Table -AutoSize
        $crash = Join-Path $logRoot "crash.log"
        if (Test-Path -LiteralPath $crash) {
            Write-Host "Crash log tail:"
            Get-Content -LiteralPath $crash -Tail 80
        }
    }
}

$process = $null
try {
    Write-Host "GUI launch smoke: exe $resolvedExe"
    $process = Start-Process -FilePath $resolvedExe -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $title = ""

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) {
            throw "Process exited before window appeared. ExitCode=$($process.ExitCode)"
        }

        $title = $process.MainWindowTitle
        if (-not [string]::IsNullOrWhiteSpace($title) -and
            [string]::Equals($title, "kmb file tools", [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
    }

    $process.Refresh()
    $title = $process.MainWindowTitle
    if ($process.HasExited) {
        throw "Process exited before smoke completed. ExitCode=$($process.ExitCode)"
    }

    if ([string]::IsNullOrWhiteSpace($title) -or
        -not [string]::Equals($title, "kmb file tools", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Main window title not detected within $TimeoutSeconds seconds. Title='$title'"
    }

    $captured = Capture-Window -Process $process -Path $screenshotPath
    Write-Host "GUI launch smoke: window title '$title'"
    if ($captured) {
        Write-Host "GUI launch smoke: screenshot $screenshotPath"
    }
    else {
        Write-Host "GUI launch smoke: screenshot skipped"
    }
    Write-Host "GUI launch smoke: pass"
}
catch {
    Write-Warning "GUI launch smoke failed: $($_.Exception.Message)"
    Write-Diagnostics
    throw
}
finally {
    if ($process -and -not $process.HasExited) {
        [void]$process.CloseMainWindow()
        Start-Sleep -Seconds 2
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
