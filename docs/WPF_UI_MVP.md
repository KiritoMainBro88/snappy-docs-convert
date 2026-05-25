# WPF UI MVP

Phase 6A adds the first runnable Windows desktop app. Phase 8D reorganizes the MVP into a clearer mode-based desktop utility without changing engine behavior.

## Run

```powershell
dotnet run --project src/SnappyDocsConvert.App
```

## Build And Test

```powershell
dotnet build
dotnet test
```

## Self-Check

```powershell
dotnet run --project src/SnappyDocsConvert.App -- --self-check
```

Self-check instantiates app services, checks Office/LibreOffice availability, prints compact JSON, and exits without opening the window.

## Smoke

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-app-build.ps1
```

The smoke script verifies restore, build, tests, publish output, app executable existence, and `--self-check`.

## UI Capabilities

- Header with app name, local converter subtitle, and local-only/no-upload badge.
- Engine status cards for Word, PowerPoint, LibreOffice, and PDF renderer.
- Add files.
- Add folders recursively.
- Drag/drop files and folders.
- Remove selected item.
- Clear queue.
- Select output folder.
- Select target: PDF, Images, or PDF + Images.
- Select engine: Auto, Microsoft Office, or LibreOffice.
- Select image format: PNG, JPEG, or WebP.
- Set DPI.
- Keep/delete intermediate PDF for images-only jobs.
- Start/cancel batch conversion.
- See per-file status and compact logs.
- Open output folder.
- Open individual output location from a queue row after conversion.
- Clear compact log.
- Recheck engine setup.
- Choose `soffice.com` / `soffice.exe` for current session.
- Open official LibreOffice download page.
- Use left navigation for Convert, PDF Tools, Engines, Logs, and Help.
- Switch UI language between English and Vietnamese.
- See visible warnings for unsupported or missing files.
- Use owner-provided logo/icon assets when present, with text fallback when missing.

## Limitations

- Dev MVP, not beta/release build.
- No installer yet.
- No telemetry, cloud upload, or remote conversion.
- LibreOffice smoke still depends on installed LibreOffice.
- PowerPoint real smoke still waits for a safe fixture.
- `--self-check` verifies service wiring and engine availability, not full conversion E2E.
- Unit tests are not full UI automation.
- Automated GUI interaction tests are not implemented.
- Manual GUI QA must be re-run after the Phase 8D UI overhaul.

## Manual GUI QA

Use:

```powershell
dotnet run --project src\SnappyDocsConvert.App
```

Then follow `docs/GUI_QA_CHECKLIST.md`. Do not claim manual GUI pass unless those steps were actually run. Evidence-based non-UI QA is documented in `docs/QA_E2E_GATE.md`.

## QA Truth Policy

Smoke scripts must say what they actually verified. Do not claim full E2E conversion unless a real fixture conversion ran and output was checked.
