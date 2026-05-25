$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "Snappy Docs Convert - local check"
Write-Host "Repo: $repoRoot"
Write-Host ""

function Show-ToolVersion {
  param(
    [string]$Name,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$ToolArgs
  )

  $cmd = Get-Command $Name -ErrorAction SilentlyContinue
  if ($null -eq $cmd) {
    Write-Host ("{0}: MISSING" -f $Name)
    return
  }

  Write-Host ("{0}: FOUND" -f $Name)
  try {
    & $Name @ToolArgs 2>&1 | Select-Object -First 5 | ForEach-Object {
      Write-Host ("  {0}" -f $_)
    }
  } catch {
    Write-Host ("  ERROR: {0}" -f $_.Exception.Message)
  }
}

Show-ToolVersion git --version
Show-ToolVersion gh --version
Show-ToolVersion dotnet --version
Show-ToolVersion node --version
Show-ToolVersion npm --version
Show-ToolVersion bun --version
Show-ToolVersion uv --version
Show-ToolVersion codex --version
Write-Host ""

Write-Host "Git status:"
if (Test-Path ".git") {
  git status --short --branch
} else {
  Write-Host "  Not a git repository."
}
Write-Host ""

$requiredFiles = @(
  "README.md",
  "AGENTS.md",
  "repomix.config.json",
  "docs/RESEARCH.md",
  "docs/ARCHITECTURE.md",
  "docs/AI_CONTEXT_POLICY.md",
  "docs/SETUP_LOCAL_CODEX.md",
  "docs/PHASE_PLAN.md"
)

Write-Host "Required files:"
foreach ($file in $requiredFiles) {
  if (Test-Path $file) {
    Write-Host ("  OK      {0}" -f $file)
  } else {
    Write-Host ("  MISSING {0}" -f $file)
  }
}
Write-Host ""

$requiredDirs = @("docs", ".codex/prompts", "scripts")
Write-Host "Required directories:"
foreach ($dir in $requiredDirs) {
  if (Test-Path $dir) {
    Write-Host ("  OK      {0}" -f $dir)
  } else {
    Write-Host ("  MISSING {0}" -f $dir)
  }
}
Write-Host ""

if (Get-Command repomix -ErrorAction SilentlyContinue) {
  Write-Host "Repomix: FOUND"
} elseif (Get-Command npm -ErrorAction SilentlyContinue) {
  Write-Host "Repomix: not found as a command; npm is available, so scripts/pack-context.ps1 can use npx repomix@latest."
} else {
  Write-Host "Repomix: MISSING; install Node.js/npm or install/run Repomix with owner approval."
}

Write-Host ""
Write-Host "Suggested next commands:"
Write-Host "  powershell -ExecutionPolicy Bypass -File .\scripts\pack-context.ps1"
Write-Host "  git status --short"
Write-Host "  git diff --stat"
