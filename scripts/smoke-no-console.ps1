param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appProject = Join-Path $repoRoot "src\SnappyDocsConvert.App\SnappyDocsConvert.App.csproj"
$publishRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SnappyDocsConvertNoConsole-" + [Guid]::NewGuid().ToString("N"))

Push-Location $repoRoot
try {
    [xml]$projectXml = Get-Content -LiteralPath $appProject
    $outputType = $projectXml.Project.PropertyGroup.OutputType
    if ($outputType -ne "WinExe") {
        throw "Expected WPF OutputType WinExe, got '$outputType'."
    }

    dotnet publish $appProject -c $Configuration -r $Runtime --self-contained false -o $publishRoot
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }

    $exePath = Join-Path $publishRoot "SnappyDocsConvert.App.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Published exe missing: $exePath"
    }

    $selfCheck = Start-Process -FilePath $exePath -ArgumentList "--self-check" -Wait -PassThru -WindowStyle Hidden
    if ($selfCheck.ExitCode -ne 0) {
        throw "Self-check failed with exit code $($selfCheck.ExitCode)."
    }

    Write-Host "No-console smoke: OutputType WinExe"
    Write-Host "No-console smoke: exe exists"
    Write-Host "No-console smoke: self-check exit ok"
    Write-Host "No-console smoke: double-click no-console requires owner manual check"
    Write-Host "No-console smoke: pass"
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $publishRoot) {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }
}
