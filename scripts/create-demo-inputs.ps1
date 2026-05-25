$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$inputRoot = Join-Path $repoRoot "artifacts\demo\inputs"
New-Item -ItemType Directory -Force -Path $inputRoot | Out-Null

function New-TinyPdf {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Text
    )

    $content = "BT /F1 18 Tf 72 720 Td ($Text) Tj ET`n"
    $objects = @(
        "1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n",
        "2 0 obj`n<< /Type /Pages /Kids [3 0 R] /Count 1 >>`nendobj`n",
        "3 0 obj`n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>`nendobj`n",
        "4 0 obj`n<< /Length $($content.Length) >>`nstream`n$content`nendstream`nendobj`n",
        "5 0 obj`n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>`nendobj`n"
    )

    $encoding = [System.Text.Encoding]::ASCII
    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.Append("%PDF-1.4`n")
    $offsets = New-Object System.Collections.Generic.List[int]
    $offsets.Add(0)

    foreach ($object in $objects) {
        $offsets.Add($encoding.GetByteCount($builder.ToString()))
        [void] $builder.Append($object)
    }

    $xrefOffset = $encoding.GetByteCount($builder.ToString())
    [void] $builder.Append("xref`n0 $($objects.Count + 1)`n")
    [void] $builder.Append("0000000000 65535 f `n")

    for ($i = 1; $i -le $objects.Count; $i++) {
        [void] $builder.Append(("{0:D10} 00000 n `n" -f $offsets[$i]))
    }

    [void] $builder.Append("trailer`n<< /Size $($objects.Count + 1) /Root 1 0 R >>`nstartxref`n$xrefOffset`n%%EOF`n")
    [System.IO.File]::WriteAllBytes($Path, $encoding.GetBytes($builder.ToString()))
}

$pdfPath = Join-Path $inputRoot "demo-one-page.pdf"
$rtfPath = Join-Path $inputRoot "demo-document.rtf"
$pngPath = Join-Path $inputRoot "demo-image.png"
$unsupportedPath = Join-Path $inputRoot "unsupported-file.xyz"

New-TinyPdf -Path $pdfPath -Text "kmb file tools demo PDF"

$rtf = "{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs24 kmb file tools demo RTF.\par Local-only conversion demo.\par}"
Set-Content -LiteralPath $rtfPath -Value $rtf -Encoding ASCII

$pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="
[System.IO.File]::WriteAllBytes($pngPath, [Convert]::FromBase64String($pngBase64))

Set-Content -LiteralPath $unsupportedPath -Value "unsupported demo file for kmb file tools" -Encoding UTF8

Write-Host "Demo inputs created:"
Write-Host "  $pdfPath"
Write-Host "  $rtfPath"
Write-Host "  $pngPath"
Write-Host "  $unsupportedPath"
