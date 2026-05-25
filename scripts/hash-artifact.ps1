param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

$resolved = (Resolve-Path -LiteralPath $PackagePath).Path
$item = Get-Item -LiteralPath $resolved
if ($item.PSIsContainer) {
    throw "PackagePath must point to a release artifact file: $resolved"
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $resolved
Write-Host "Artifact: $resolved"
Write-Host "Size bytes: $($item.Length)"
Write-Host "SHA256: $($hash.Hash)"
