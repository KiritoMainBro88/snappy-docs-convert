[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$qaRoot = Join-Path $repoRoot "docs\qa-output\current"
$logsRoot = Join-Path $qaRoot "logs"
$publishRoot = Join-Path $qaRoot "publish"
$rows = New-Object System.Collections.Generic.List[object]
$failed = $false

function Add-Row {
    param(
        [Parameter(Mandatory = $true)][string]$Case,
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$Result,
        [Parameter(Mandatory = $true)][string]$Evidence
    )

    $script:rows.Add([pscustomobject]@{
        Case = $Case
        Expected = $Expected
        Result = $Result
        Evidence = ($Evidence -replace '\|', '/')
    })

    if ($Result -eq "Fail") {
        $script:failed = $true
    }
}

function Invoke-Captured {
    param(
        [Parameter(Mandatory = $true)][string]$Case,
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$LogName,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    $logPath = Join-Path $logsRoot $LogName
    $output = & $Command 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath $logPath

    if ($exitCode -eq 0 -or $null -eq $exitCode) {
        Add-Row $Case $Expected "Pass" "exit 0; log=$logPath"
    }
    else {
        Add-Row $Case $Expected "Fail" "exit $exitCode; log=$logPath"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
        LogPath = $logPath
    }
}

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $qaRoot) {
        Remove-Item -LiteralPath $qaRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $logsRoot, $publishRoot | Out-Null

    Invoke-Captured "Build" "dotnet build exits 0" "build.log" { dotnet build } | Out-Null
    Invoke-Captured "Tests" "dotnet test exits 0" "test.log" { dotnet test } | Out-Null

    $selfCheck = Invoke-Captured "Self-check" "selfCheck ok JSON" "self-check.log" {
        dotnet run --project .\src\SnappyDocsConvert.App -- --self-check
    }
    if ($selfCheck.ExitCode -eq 0 -and (($selfCheck.Output -join "`n") -match '"selfCheck"\s*:\s*"ok"')) {
        $rows[$rows.Count - 1].Evidence = "selfCheck=ok; log=$($selfCheck.LogPath)"
    }
    else {
        $rows[$rows.Count - 1].Result = "Fail"
        $failed = $true
    }

    Invoke-Captured "Publish exe" "SnappyDocsConvert.App.exe exists" "publish.log" {
        dotnet publish .\src\SnappyDocsConvert.App\SnappyDocsConvert.App.csproj -c Debug -o $publishRoot
    } | Out-Null
    $exePath = Join-Path $publishRoot "SnappyDocsConvert.App.exe"
    if (Test-Path -LiteralPath $exePath) {
        $rows[$rows.Count - 1].Evidence = "$exePath ($((Get-Item -LiteralPath $exePath).Length) bytes)"
    }
    else {
        $rows[$rows.Count - 1].Result = "Fail"
        $rows[$rows.Count - 1].Evidence = "missing exe: $exePath"
        $failed = $true
    }

    $harnessLog = Join-Path $logsRoot "qa-harness.log"
    $harnessOutput = dotnet run --project .\src\SnappyDocsConvert.QaHarness -- --qa-root $qaRoot 2>&1
    $harnessExit = $LASTEXITCODE
    $harnessOutput | Set-Content -LiteralPath $harnessLog

    foreach ($line in $harnessOutput) {
        $text = [string]$line
        if (-not $text.StartsWith("QA_ROW|")) {
            continue
        }

        $parts = $text.Split("|", 5)
        if ($parts.Length -eq 5) {
            Add-Row $parts[1] $parts[2] $parts[3] $parts[4]
        }
    }

    if ($harnessExit -ne 0) {
        $failed = $true
    }

    Write-Host "QA MATRIX"
    Write-Host "| Case | Expected | Result | Evidence |"
    Write-Host "|---|---|---|---|"
    foreach ($row in $rows) {
        Write-Host "| $($row.Case) | $($row.Expected) | $($row.Result) | $($row.Evidence) |"
    }

    Write-Host "QA output: $qaRoot"
    if ($failed) {
        exit 1
    }
}
finally {
    Pop-Location
}
