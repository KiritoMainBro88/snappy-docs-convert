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

The WPF app uses `OutputType=WinExe`, so normal double-click launch should open the GUI without spawning a console. If the app is launched from an existing terminal, that terminal staying open is normal.

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

With `WinExe`, console output from `--self-check` can be unavailable under automation. Release smoke treats exit code 0 as valid when JSON output is not captured.

No-console smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-no-console.ps1
```

Hash release artifact:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hash-artifact.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
```

Prepare GitHub release notes/instructions without creating a release:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-github-release-notes.ps1
```

## Release Checklist

1. Run QA: `scripts\qa-e2e.ps1`.
2. Build package: `scripts\package-portable.ps1`.
3. Smoke release: `scripts\smoke-release.ps1`.
4. Hash artifact: `scripts\hash-artifact.ps1`.
5. Push branch only after owner approval.
6. Merge/review on GitHub.
7. Tag release candidate.
8. Create GitHub Release draft.
9. Upload portable zip.
10. Update website if release URL or copy changes.

## Manual GUI QA Session

Create a timestamped owner-fillable QA report:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\new-gui-qa-session.ps1
```

Output goes under ignored `docs\qa-output\gui\YYYYMMDD-HHMMSS\`. This recorder does not launch the app and does not claim a GUI pass.

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
- Public beta should wait for a completed `MANUAL_GUI_QA_RESULT.md`.
- Unsigned builds may show Windows SmartScreen warnings until signing exists.
- GitHub Release upload is manual/owner-approved; packaging scripts do not publish.
