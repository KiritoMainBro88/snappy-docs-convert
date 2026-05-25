$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$outputDir = Join-Path $repoRoot "docs\ai-context"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Packing AI context with Repomix..."
Write-Host "Repo: $repoRoot"
Write-Host "Output: docs/ai-context/repomix-output.md"
Write-Host ""
Write-Host "Token strategy:"
if (Get-Command rtk -ErrorAction SilentlyContinue) {
  Write-Host "  RTK: FOUND. Use RTK before Repomix for targeted inspection:"
  Write-Host "    rtk git status"
  Write-Host "    rtk read <file>"
  Write-Host "    rtk grep <pattern> ."
} else {
  Write-Host "  RTK: MISSING. Use rg/git targeted search before Repomix."
}
Write-Host "  Repomix: use only after narrowing context with include/exclude filters."
Write-Host ""

if (Get-Command repomix -ErrorAction SilentlyContinue) {
  repomix
  exit $LASTEXITCODE
}

if (Get-Command npm -ErrorAction SilentlyContinue) {
  Write-Host "Repomix command not found. Using npx repomix@latest without global install."
  npx --yes repomix@latest
  exit $LASTEXITCODE
}

Write-Host "Repomix could not run because neither repomix nor npm is available."
Write-Host "Install Node.js, or ask for approval before installing Repomix globally."
exit 0
