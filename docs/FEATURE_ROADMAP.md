# Feature Roadmap

## Done

- Office documents to PDF through Microsoft Office when installed.
- LibreOffice fallback guidance and headless engine support.
- PDF to PNG/JPEG/WebP images.
- Batch conversion.
- Portable Windows package.
- PDF Tools v1:
  - Merge PDFs
  - Split PDF
  - Extract pages
  - Rotate pages
  - Images to PDF
- Public beta GitHub Release assets:
  - Portable ZIP
  - Inno Setup installer EXE
- Frontend-only website:
  - System/light/dark mode
  - Browser language detection and persisted English/Vietnamese copy
  - GitHub Release beta.2 download links
  - Discord support link
  - Curated demo screenshots/video
- Demo asset pipeline:
  - Generated demo inputs
  - Playwright website screenshots/video
  - FFmpeg desktop recording helper
  - User-facing demo script
- Trust package:
  - SHA256 checksum manifests
  - GitHub Actions CI
  - Manual release-build workflow
  - Artifact attestation steps
  - False-positive reporting docs
- Auto-update design doc:
  - future Velopack + GitHub Releases direction
  - beta-safe manual check-for-updates first
- App update center V1:
  - manual check/download/install from Settings
  - Stable or Prerelease/Beta channel
  - SHA256 digest verification when GitHub provides release asset digest

## Planned

- Full Velopack auto-update implementation after manual update flow stabilizes.
- Winget distribution.
- Paid code signing when budget exists.
- PDF compression.
- OCR Lite:
  - local scan/image/PDF to text
  - Vietnamese + English first
  - low-end machine profile
  - Windows OCR or Tesseract Fast
- Watermark.
- Metadata editing.
- Password/encryption support.
- Shell integration.
- MSI/MSIX installer.
- Code signing.
- PowerPoint real smoke fixture.
- LibreOffice real smoke on a machine with LibreOffice installed.

## OCR Strategy

- OCR is planned, not released.
- First implementation should be OCR Lite, not heavy OCR.
- Default should prefer Windows OCR when available, otherwise Tesseract with `tessdata_fast`.
- `tessdata_best`, PaddleOCR, layout/table/formula handling, and handwriting are future advanced work only after benchmark.
- No OCR model or traineddata files should be committed to the repo.

## Not Planned For Current MVP

- Cloud conversion.
- Telemetry.
- Paid/locked features.
- Browser-only conversion for Office documents.
