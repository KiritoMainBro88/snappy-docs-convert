# Snappy Docs Convert

Snappy Docs Convert is planned as a fast local document conversion app for Windows-first workflows.

Current phase: Phase 4 PDF image renderer. This branch contains a small .NET core library, local LibreOffice document-to-PDF conversion, Microsoft Office availability detection, guarded Office COM PDF export, Office smoke validation, core dependency guidance, and PDF page image export. UI and installer work are still later phases.

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

To pack a small AI-friendly context bundle:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\pack-context.ps1
```

The packed output is written to `docs/ai-context/repomix-output.md` and is ignored by git.

## Next Phase Summary

The next recommended implementation phase is Phase 5: batch pipeline or WPF UI. Wait for owner approval before starting it.
