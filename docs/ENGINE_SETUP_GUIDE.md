# Engine Setup Guide

Snappy Docs Convert converts Office documents locally. It needs at least one local Office conversion engine.

## Product Decision

- The MVP does not bundle LibreOffice to keep the installer light.
- The app does not download or install LibreOffice automatically.
- No cloud upload is used.
- No fake success: if no local engine exists, conversion is blocked with setup guidance.

## If User Has Microsoft Office

Phase 3A adds Microsoft Office COM detection and guarded PDF export. When Word/PowerPoint COM is available, that engine should be preferred for best DOCX/PPTX fidelity.

Office engine support:

- Word: `.doc`, `.docx`, `.rtf`
- PowerPoint: `.ppt`, `.pptx`

Office must be installed, activated, and usable by the logged-in Windows user.

## If User Does Not Have Microsoft Office

Install LibreOffice from the official site:

https://www.libreoffice.org/download/download-libreoffice/

After install, verify:

```powershell
soffice --version
```

The app will try to auto-detect:

- `soffice.com`
- `soffice.exe`
- `C:\Program Files\LibreOffice\program\soffice.com`
- `C:\Program Files\LibreOffice\program\soffice.exe`
- x86 equivalents under `C:\Program Files (x86)`

## Manual LibreOffice Path

If auto-detect fails, user can choose:

- `C:\Program Files\LibreOffice`
- `C:\Program Files\LibreOffice\program`
- `C:\Program Files\LibreOffice\program\soffice.com`
- `C:\Program Files\LibreOffice\program\soffice.exe`

The core advisor returns choose-path and recheck actions for future UI surfaces.

## Official Links

- LibreOffice download: https://www.libreoffice.org/download/download-libreoffice/
- LibreOffice CLI help: https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html

## Core Types

- `EngineSetupAdvisor`
- `EngineSetupStatus`
- `EngineSetupRecommendation`
- `EngineSetupAction`
- `ExternalToolLink`
- `OfficeAvailability`
- `OfficeComConversionEngine`

These types are UI-ready but do not create a UI window in this phase.
