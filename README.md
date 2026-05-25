# Snappy Docs Convert

Snappy Docs Convert is planned as a fast local document conversion app for Windows-first workflows.

Current phase: Phase 2C engine setup guidance. This branch contains a small .NET core library, local LibreOffice document-to-PDF conversion, and core dependency guidance for Office/LibreOffice setup. UI, Microsoft Office COM conversion, and PDF-to-image rendering are still later phases.

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

Normal unit tests use fake process runners. They do not require LibreOffice.

## LibreOffice Engine

Phase 2 discovers `soffice.com` or `soffice.exe`, runs LibreOffice headless with an isolated temp profile, and verifies that a PDF was produced in the requested output directory.

Snappy Docs Convert does not bundle LibreOffice in the MVP. If Microsoft Office is not available, users should install LibreOffice from the official site:

https://www.libreoffice.org/download/download-libreoffice/

Manual LibreOffice verify:

```powershell
soffice --version
```

The app will auto-detect common install paths or accept a manually selected `soffice.com` / `soffice.exe` path. See `docs/ENGINE_SETUP_GUIDE.md` and `docs/LIBREOFFICE_ENGINE.md`.

To pack a small AI-friendly context bundle:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\pack-context.ps1
```

The packed output is written to `docs/ai-context/repomix-output.md` and is ignored by git.

## Next Phase Summary

The next recommended implementation phase is Phase 3: Microsoft Office COM engine. Wait for owner approval before starting it.
