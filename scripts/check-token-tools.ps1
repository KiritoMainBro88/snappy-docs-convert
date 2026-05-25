$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "kmb file tools - token tool check"
Write-Host "Repo: $repoRoot"
Write-Host ""

function Show-Command {
  param([string]$Name)

  $cmd = Get-Command $Name -ErrorAction SilentlyContinue
  if ($null -eq $cmd) {
    Write-Host ("{0}: MISSING" -f $Name)
    return $null
  }

  Write-Host ("{0}: FOUND ({1})" -f $Name, $cmd.Source)
  return $cmd
}

function Show-FirstLines {
  param(
    [string]$Label,
    [scriptblock]$Command,
    [int]$MaxLines = 6
  )

  Write-Host ("{0}:" -f $Label)
  try {
    & $Command 2>&1 | Select-Object -First $MaxLines | ForEach-Object {
      Write-Host ("  {0}" -f $_)
    }
  } catch {
    Write-Host ("  ERROR: {0}" -f $_.Exception.Message)
  }
}

$rtk = Show-Command "rtk"
if ($null -ne $rtk) {
  Show-FirstLines "rtk --version" { rtk --version } 3
  Show-FirstLines "rtk gain" { rtk gain } 8
} else {
  Write-Host "rtk verify: skipped"
}

$rg = Show-Command "rg"
if ($null -ne $rtk -and $null -ne $rg) {
  Write-Host "rtk grep: available when RTK can resolve rg from PATH"
}
Write-Host ""

$node = Show-Command "node"
if ($null -ne $node) {
  Show-FirstLines "node --version" { node --version } 3
}

$npm = Show-Command "npm"
if ($null -ne $npm) {
  Show-FirstLines "npm --version" { npm --version } 3
}
Write-Host ""

$skills = Show-Command "skills"
if ($null -eq $skills) {
  Write-Host "skills executable: not on PATH"
}

if ($null -ne $npm) {
  Show-FirstLines "npx skills --version" { npx --yes skills --version } 5
} else {
  Write-Host "npx skills: skipped because npm is missing"
}
Write-Host ""

$repomix = Show-Command "repomix"
if ($null -ne $repomix) {
  Show-FirstLines "repomix --version" { repomix --version } 5
} elseif ($null -ne $npm) {
  Show-FirstLines "npx repomix@latest --version" { npx --yes repomix@latest --version } 5
} else {
  Write-Host "Repomix through npx: skipped because npm is missing"
}
Write-Host ""

Write-Host "Serena MCP: documented optional. Use configured MCP tools if available; no install performed by this script."
Write-Host ""
Write-Host "Token tool check complete."
