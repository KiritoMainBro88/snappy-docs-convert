# Snappy Docs Convert

Snappy Docs Convert is planned as a fast local document conversion app for Windows-first workflows.

Current phase: Phase 9A static marketing website and Vercel preview prep. This branch contains local Office/PDF conversion, PDF image export, batch conversion, PDF toolbox operations, mode-based WPF UI, English/Vietnamese UI strings, E2E QA, portable packaging, manual GUI QA recorder, release readiness docs, release-candidate notes, privacy audit, manual GUI QA result with blockers, and a frontend-only website. MSI/MSIX installer work is still a later phase.

## Final Goal

- Convert DOCX, PPTX, PDF, pages, and slides locally.
- Use Microsoft Office COM on machines with Office installed for high-fidelity Office export.
- Use LibreOffice headless as the no-Office fallback.
- Export PDF pages and presentation slides as real images through renderer/export APIs, not screenshots.
- Support batch files and a polished Windows desktop UI in later phases.

## Privacy Promise

- Local-only processing by default.
- No cloud upload.
- No telemetry, analytics, tracking, or remote converter service.
- Do not print secrets, tokens, credential paths, or private environment values in logs.

## Setup

```powershell
cd D:\vibecode\snappy-docs-convert
git switch ai/phase-00-local-agent-setup
powershell -ExecutionPolicy Bypass -File .\scripts\check-local.ps1
```

## Build And Test

```powershell
dotnet restore
dotnet build
dotnet test
```

Normal unit tests use fake process runners where external document engines are involved. They do not require LibreOffice or Microsoft Office.

Run the desktop app:

```powershell
dotnet run --project src/SnappyDocsConvert.App
```

Self-check without opening the window:

```powershell
dotnet run --project src/SnappyDocsConvert.App -- --self-check
```

## LibreOffice Engine

Phase 2 discovers `soffice.com` or `soffice.exe`, runs LibreOffice headless with an isolated temp profile, and verifies that a PDF was produced in the requested output directory.

Snappy Docs Convert does not bundle LibreOffice in the MVP. If Microsoft Office is not available, users should install LibreOffice from the official site:

https://www.libreoffice.org/download/download-libreoffice/

Manual LibreOffice verify:

```powershell
soffice --version
```

The app will auto-detect common install paths or accept a manually selected `soffice.com` / `soffice.exe` path. See `docs/ENGINE_SETUP_GUIDE.md` and `docs/LIBREOFFICE_ENGINE.md`.

## Microsoft Office Engine

Phase 3A detects `Word.Application` and `PowerPoint.Application` COM ProgIDs and adds a guarded local desktop PDF export engine for `.doc`, `.docx`, `.rtf`, `.ppt`, and `.pptx`.

This engine is local desktop user-session automation only. It is not for server, service, or unattended use. See `docs/OFFICE_COM_ENGINE.md`.

Real Office smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-office.ps1
```

The smoke script skips cleanly when Office is missing. When Word is installed and activated, it creates a tiny temp RTF and converts it through the project Office COM engine.

## PDF Image Renderer

Phase 4 renders PDF pages to real image files through PDFtoImage/PDFium/SkiaSharp. It does not use screenshots.

Supported output formats:

- PNG
- JPEG
- WebP when supported by the runtime

Default DPI is 200. The renderer validates DPI, warns above 300 DPI, writes sequential page files such as `page-001.png`, and serializes PDFium calls because PDFium is not thread-safe. See `docs/PDF_IMAGE_RENDERER.md`.

## PDF Toolbox

Phase 8A adds local PDF tools:

- Merge PDFs
- Split PDF into page PDFs
- Extract page ranges such as `1,3-5,8`
- Rotate selected pages
- Convert PNG/JPEG/WebP images to one PDF

These tools run locally and do not add OCR, compression, encryption, signing, or redaction yet. See `docs/PDF_TOOLBOX.md`.

## Batch Pipeline

Phase 5A adds a core pipeline for one or many files. It orchestrates existing engines only:

- PDF input can copy/keep PDF output and/or render images.
- Office/OpenDocument input converts to PDF through Microsoft Office or LibreOffice, then optionally renders images.
- Targets: `Pdf`, `Images`, `PdfAndImages`.

Output shape:

```text
output\
  pdf\
    source__hash8.pdf
  images\
    source__hash8\
      page-001.png
```

Presentation inputs use `slide-001.png`; document/PDF inputs use `page-001.png`. See `docs/BATCH_PIPELINE.md`.

## WPF UI MVP

Phase 6A adds the first runnable desktop UI. Phase 8D reorganizes it into a clearer mode-based app:

- Queue files/folders with drag/drop or dialogs.
- Choose output folder, target, engine, image format, DPI, and intermediate PDF option.
- Run/cancel batch conversion through the core pipeline.
- See queue status, compact logs, output path, and engine setup guidance.
- See clear local-only privacy wording and engine status cards.
- Use left navigation: Convert, PDF Tools, Engines, Logs, Help.
- Switch UI language between English and Vietnamese.
- Use the manual GUI QA checklist for desktop verification.
- Use the PDF Tools page for merge/split/extract/rotate/images-to-PDF.

See `docs/WPF_UI_MVP.md`, `docs/GUI_QA_CHECKLIST.md`, and `docs/BRANDING.md`.

## Evidence-Based E2E QA

Phase 6B adds a local QA gate that builds, tests, self-checks, publishes the app, and verifies real generated conversion outputs:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\qa-e2e.ps1
```

The script writes generated evidence under `docs\qa-output\current\`, which is ignored by git. Pass means the command ran and output files/results were verified. Skip means a local dependency such as LibreOffice is missing. Unit tests are not treated as full E2E proof. See `docs/QA_E2E_GATE.md`.

## Portable Packaging

Build a Windows x64 portable folder and zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
```

Smoke the zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-release.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
```

The portable MVP does not bundle Microsoft Office or LibreOffice. Microsoft Office is optional and recommended for best DOCX/PPTX fidelity. LibreOffice is an optional fallback; the app guides users to the official download when needed. See `docs/PACKAGING.md`.

Normal double-click launch should open the GUI without a console because the WPF project uses `OutputType=WinExe`. If launched from an existing terminal, that terminal staying open is normal. `--self-check` remains available for smoke scripts, but WinExe console output can be unavailable under automation; smoke checks rely on exit code when needed.

## Website And Open Source Prep

Polished frontend-only website:

```text
website\
```

Run locally:

```powershell
cd website
npm install
npm run dev
npm run build
```

Validate from repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-website-vite.ps1
```

The website is Vite + React + TypeScript. It has no backend, no API routes, no upload flow, no telemetry, and no external CDN fonts/scripts. Vercel preview deploy can use:

```powershell
vercel --cwd website
```

Production deploy requires explicit owner approval:

```powershell
vercel --cwd website --prod
```

Legacy no-build fallback:

```text
website-static\
```

Primary download link points to:

https://github.com/KiritoMainBro88/snappy-docs-convert/releases

Source link points to:

https://github.com/KiritoMainBro88/snappy-docs-convert

Open-source prep files include `CONTRIBUTING.md`, `SECURITY.md`, issue templates, PR template, `docs/OPEN_SOURCE_PLAN.md`, and draft MIT license file. Final license still needs owner approval.

## Release Candidate Prep

Phase 8B adds release-candidate support docs and scripts:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-website-static.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\hash-artifact.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-github-release-notes.ps1
```

Release notes live under `docs/releases/`. Privacy audit status lives in `docs/PRIVACY_AUDIT.md`.

No GitHub push or release upload is done by these scripts. The owner must approve push and release creation.

Owner manual GUI QA for `v0.1.0-rc1` is recorded under `docs/manual-qa/`. Public beta is blocked by GUI responsiveness/feedback issues, untested GUI flows, and missing final license approval.

## License Status

Current license status: draft only. `LICENSE-DRAFT-MIT.txt` exists, but the owner has not explicitly approved MIT in this session. Do not publish a public release until a final license is approved and committed.

## Manual GUI QA Recorder

Create an owner-fillable manual GUI QA session under ignored `docs\qa-output\gui\`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\new-gui-qa-session.ps1
```

This does not run the app or claim GUI pass. The owner fills `MANUAL_GUI_QA_RESULT.md` with output paths, screenshots, and pass/fail notes. Release readiness status is tracked in `docs/RELEASE_READINESS.md`.

To pack a small AI-friendly context bundle:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\pack-context.ps1
```

The packed output is written to `docs/ai-context/repomix-output.md` and is ignored by git.

## Next Phase Summary

The next recommended step is owner manual GUI QA on the `v0.1.0-rc1-ui` package, then fix any remaining GUI issues and finalize license. Owner-approved push/release comes after those blockers are handled or explicitly accepted as pre-release limitations.
