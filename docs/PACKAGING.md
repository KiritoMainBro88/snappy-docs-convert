# Portable Packaging

Phase 7 creates a portable Windows x64 folder and zip for the WPF MVP. This is not an MSI/MSIX installer.

## Build Portable Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
```

Default output:

```text
artifacts\
  SnappyDocsConvert-portable-win-x64-<version>\
  SnappyDocsConvert-portable-win-x64-<version>.zip
```

The package is framework-dependent by default and requires the .NET 9 Desktop Runtime on Windows x64.

Optional self-contained build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1 -SelfContained
```

## Smoke Release Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-release.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
```

The smoke check verifies:

- `SnappyDocsConvert.App.exe` exists.
- `SnappyDocsConvert.App.exe --self-check` exits successfully and reports `selfCheck: ok`.
- `README-QUICKSTART.txt`, `PRIVACY.txt`, and `THIRD-PARTY-NOTICES.txt` exist.
- Source folders, tests, QA output, logs, generated PDFs/images, and nested zips are absent.

## Dependency Behavior

- Microsoft Office is optional and recommended for best DOCX/PPTX fidelity.
- LibreOffice is optional fallback and is not bundled.
- If LibreOffice is missing, the app guides the user to the official LibreOffice download or lets the user choose `soffice.com` / `soffice.exe`.
- The app must not fake conversion success when a selected engine is missing.

## Privacy

- Local-only conversion.
- No cloud upload.
- No telemetry, analytics, or tracking.
- Microsoft Office and LibreOffice run locally when used.

## Known Gaps

- Installer pending.
- LibreOffice real smoke depends on installed LibreOffice.
- PowerPoint real smoke fixture pending.
- Manual GUI QA remains owner-executed via `docs/GUI_QA_CHECKLIST.md`.
