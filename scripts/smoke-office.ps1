[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$coreProject = Join-Path $repoRoot "src\SnappyDocsConvert.Core\SnappyDocsConvert.Core.csproj"
$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SnappyDocsConvertOfficeSmoke-" + [System.Guid]::NewGuid().ToString("N"))
$keepOutput = $env:KEEP_SNAPPY_SMOKE_OUTPUT -eq "1"
$wordSmokeRan = $false
$wordSmokePassed = $false

function Test-ProgId {
    param([Parameter(Mandatory = $true)][string]$ProgId)

    try {
        return [type]::GetTypeFromProgID($ProgId) -ne $null
    }
    catch {
        return $false
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        $output = & dotnet @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Write-StepOutput {
    param([object[]]$Output)

    foreach ($line in $Output) {
        Write-Host $line
    }
}

function New-OfficeSmokeHarness {
    param([Parameter(Mandatory = $true)][string]$HarnessDirectory)

    New-Item -ItemType Directory -Force -Path $HarnessDirectory | Out-Null

    $newResult = Invoke-DotNet `
        -Arguments @("new", "console", "--framework", "net9.0", "--force", "--output", $HarnessDirectory, "--name", "OfficeSmokeHarness") `
        -WorkingDirectory $repoRoot
    if ($newResult.ExitCode -ne 0) {
        Write-StepOutput $newResult.Output
        throw "Failed to create smoke harness project."
    }

    $harnessProject = Join-Path $HarnessDirectory "OfficeSmokeHarness.csproj"
    $addRefResult = Invoke-DotNet `
        -Arguments @("add", $harnessProject, "reference", $coreProject) `
        -WorkingDirectory $repoRoot
    if ($addRefResult.ExitCode -ne 0) {
        Write-StepOutput $addRefResult.Output
        throw "Failed to reference SnappyDocsConvert.Core from smoke harness."
    }

    $program = @'
using SnappyDocsConvert.Core.Models;
using SnappyDocsConvert.Core.Services.Office;

static string Clean(string? value)
    => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Replace("\r", " ").Replace("\n", " ");

if (args.Length < 3)
{
    Console.WriteLine("error=usage: <inputPath> <outputDirectory> <timeoutSeconds>");
    return 64;
}

var inputPath = args[0];
var outputDirectory = args[1];
var timeoutSeconds = int.Parse(args[2]);

var engine = new OfficeComConversionEngine();
var request = new ConversionRequest(inputPath, outputDirectory)
{
    AllowOverwrite = true,
    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
};

var result = await engine.ConvertToPdfAsync(request, CancellationToken.None);
var outputExists = result.OutputPdfPath is not null
    && File.Exists(result.OutputPdfPath)
    && new FileInfo(result.OutputPdfPath).Length > 0;

Console.WriteLine($"success={result.Success}");
Console.WriteLine($"status={result.Status}");
Console.WriteLine($"output={result.OutputPdfPath}");
Console.WriteLine($"outputExists={outputExists}");
Console.WriteLine($"error={Clean(result.ErrorMessage)}");
Console.WriteLine($"warnings={Clean(string.Join(" | ", result.Warnings))}");

return result.Success && outputExists ? 0 : 2;
'@

    Set-Content -LiteralPath (Join-Path $HarnessDirectory "Program.cs") -Value $program -Encoding UTF8

    $buildResult = Invoke-DotNet `
        -Arguments @("build", $harnessProject, "--nologo", "--verbosity", "quiet") `
        -WorkingDirectory $repoRoot
    if ($buildResult.ExitCode -ne 0) {
        Write-StepOutput $buildResult.Output
        throw "Failed to build smoke harness."
    }

    return $harnessProject
}

try {
    $wordAvailable = Test-ProgId "Word.Application"
    $powerPointAvailable = Test-ProgId "PowerPoint.Application"

    Write-Host "Word available: $wordAvailable"
    Write-Host "PowerPoint available: $powerPointAvailable"

    if (-not $wordAvailable -and -not $powerPointAvailable) {
        Write-Host "Smoke: skipped (Microsoft Office COM ProgIDs missing)."
        exit 0
    }

    $inputDir = Join-Path $smokeRoot "input"
    $outputDir = Join-Path $smokeRoot "output"
    $harnessDir = Join-Path $smokeRoot "harness"
    New-Item -ItemType Directory -Force -Path $inputDir, $outputDir | Out-Null

    Write-Host "Smoke output path: $outputDir"

    $harnessProject = New-OfficeSmokeHarness -HarnessDirectory $harnessDir

    if ($wordAvailable) {
        $wordSmokeRan = $true
        $rtfPath = Join-Path $inputDir "word-smoke.rtf"
        $rtf = '{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs24 Snappy Docs Convert Office smoke.\par}'
        Set-Content -LiteralPath $rtfPath -Value $rtf -Encoding ASCII

        Write-Host "Word smoke: running RTF to PDF through OfficeComConversionEngine"
        $runResult = Invoke-DotNet `
            -Arguments @("run", "--project", $harnessProject, "--no-build", "--", $rtfPath, $outputDir, "$TimeoutSeconds") `
            -WorkingDirectory $repoRoot
        Write-StepOutput $runResult.Output

        if ($runResult.ExitCode -ne 0) {
            throw "Word smoke failed."
        }

        $wordSmokePassed = $true
        Write-Host "Word smoke: pass"
    }
    else {
        Write-Host "Word smoke: skipped (Word.Application missing)."
    }

    if ($powerPointAvailable) {
        Write-Host "PowerPoint smoke: skipped (safe PPT/PPTX sample generation not added in Phase 3B)."
    }
    else {
        Write-Host "PowerPoint smoke: skipped (PowerPoint.Application missing)."
    }

    if ($wordSmokeRan -and $wordSmokePassed) {
        Write-Host "Smoke: pass"
    }
    else {
        Write-Host "Smoke: skipped (no real conversion path executed)."
    }
}
catch {
    Write-Host "Smoke: fail"
    Write-Error $_
    exit 1
}
finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $smokeFullPath = [System.IO.Path]::GetFullPath($smokeRoot)
        $smokeLeaf = Split-Path -Leaf $smokeFullPath

        if ($keepOutput) {
            Write-Host "Smoke temp kept: $smokeFullPath"
        }
        elseif ($smokeFullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            $smokeLeaf.StartsWith("SnappyDocsConvertOfficeSmoke-", [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $smokeFullPath -Recurse -Force
            Write-Host "Smoke temp cleaned."
        }
        else {
            Write-Warning "Smoke temp cleanup skipped for unexpected path: $smokeFullPath"
        }
    }
}
