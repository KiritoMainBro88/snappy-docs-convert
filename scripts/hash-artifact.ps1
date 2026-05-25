param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

$resolved = (Resolve-Path -LiteralPath $PackagePath).Path
$item = Get-Item -LiteralPath $resolved
if ($item.PSIsContainer -or $item.Extension -ine ".zip") {
    throw "PackagePath must point to a release zip file: $resolved"
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $resolved
Write-Host "Package: $resolved"
Write-Host "Size bytes: $($item.Length)"
Write-Host "SHA256: $($hash.Hash)"
