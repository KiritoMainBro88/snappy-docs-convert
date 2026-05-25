# WPF UI MVP

Phase 6A adds the first runnable Windows desktop app.

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
- Recheck engine setup.
- Choose `soffice.com` / `soffice.exe` for current session.
- Open official LibreOffice download page.

## Limitations

- UI MVP, not final polish.
- No installer yet.
- No telemetry, cloud upload, or remote conversion.
- LibreOffice smoke still depends on installed LibreOffice.
- PowerPoint real smoke still waits for a safe fixture.
- `--self-check` verifies service wiring and engine availability, not full conversion E2E.
- Unit tests are not full UI automation.

## QA Truth Policy

Smoke scripts must say what they actually verified. Do not claim full E2E conversion unless a real fixture conversion ran and output was checked.
