param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [switch]$MetadataOnly
)

$ErrorActionPreference = "Stop"

$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$installer = Get-Item -LiteralPath $resolvedInstaller
if ($installer.Length -le 0) {
    throw "Installer EXE is empty: $resolvedInstaller"
}

$signature = Get-AuthenticodeSignature -LiteralPath $resolvedInstaller
Write-Host "Installer exists: $resolvedInstaller"
Write-Host "Installer size: $($installer.Length) bytes"
Write-Host "Installer signature: $($signature.Status)"

if ($MetadataOnly.IsPresent) {
    Write-Host "Installer smoke: metadata only"
    Write-Host "Manual installer test required."
    exit 0
}

$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SnappyDocsConvertInstallerSmoke-" + [Guid]::NewGuid().ToString("N"))
$installDir = Join-Path $smokeRoot "app"
$installLog = Join-Path $smokeRoot "install.log"
$uninstallLog = Join-Path $smokeRoot "uninstall.log"
New-Item -ItemType Directory -Force -Path $smokeRoot | Out-Null

try {
    $installArgs = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/CURRENTUSER",
        "/DIR=$installDir",
        "/LOG=$installLog"
    )

    $installProcess = Start-Process -FilePath $resolvedInstaller -ArgumentList $installArgs -Wait -PassThru -WindowStyle Hidden
    if ($installProcess.ExitCode -ne 0) {
        throw "Silent installer failed with exit code $($installProcess.ExitCode). Log: $installLog"
    }

    $exePath = Join-Path $installDir "SnappyDocsConvert.App.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Installed app exe missing: $exePath"
    }

    $selfCheckProcess = Start-Process -FilePath $exePath -ArgumentList @("--self-check") -Wait -PassThru -WindowStyle Hidden
    if ($selfCheckProcess.ExitCode -ne 0) {
        throw "Installed app self-check failed with exit code $($selfCheckProcess.ExitCode)."
    }

    $uninstaller = Join-Path $installDir "unins000.exe"
    if (Test-Path -LiteralPath $uninstaller) {
        $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=$uninstallLog") -Wait -PassThru -WindowStyle Hidden
        if ($uninstallProcess.ExitCode -ne 0) {
            throw "Silent uninstall failed with exit code $($uninstallProcess.ExitCode). Log: $uninstallLog"
        }
    }

    Write-Host "Installer smoke: silent install pass"
    Write-Host "Installer smoke: installed self-check pass"
    Write-Host "Installer smoke: silent uninstall pass"
}
finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
