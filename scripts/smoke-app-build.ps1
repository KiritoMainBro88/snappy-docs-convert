[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SnappyDocsConvertAppPublish-" + [System.Guid]::NewGuid().ToString("N"))
$keepOutput = $env:KEEP_SNAPPY_APP_PUBLISH -eq "1"

try {
    Push-Location $repoRoot

    Write-Host "App smoke: dotnet restore"
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

    Write-Host "App smoke: dotnet build"
    dotnet build
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

    Write-Host "App smoke: dotnet test"
    dotnet test
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed." }

    Write-Host "App smoke: dotnet publish"
    dotnet publish .\src\SnappyDocsConvert.App\SnappyDocsConvert.App.csproj -c Debug -o $publishRoot
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    $exePath = Join-Path $publishRoot "SnappyDocsConvert.App.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Published app executable not found: $exePath"
    }

    Write-Host "App smoke: executable found"
    Write-Host "App smoke: self-check"
    $selfCheck = Start-Process -FilePath $exePath -ArgumentList "--self-check" -Wait -PassThru -WindowStyle Hidden
    if ($selfCheck.ExitCode -ne 0) {
        throw "App self-check failed with exit code $($selfCheck.ExitCode)."
    }

    Write-Host "App smoke: pass"
}
finally {
    Pop-Location

    if (Test-Path -LiteralPath $publishRoot) {
        if ($keepOutput) {
            Write-Host "App smoke publish kept: $publishRoot"
        }
        else {
            Remove-Item -LiteralPath $publishRoot -Recurse -Force
            Write-Host "App smoke publish cleaned."
        }
    }
}
