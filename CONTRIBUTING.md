# Contributing

Snappy Docs Convert is a Windows-first local document conversion app.

## Build

```powershell
dotnet restore
dotnet build
dotnet test
```

## Run

```powershell
dotnet run --project src\SnappyDocsConvert.App
```

Self-check:

```powershell
dotnet run --project src\SnappyDocsConvert.App -- --self-check
```

## QA

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\qa-e2e.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-release.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
```

Manual GUI QA:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\new-gui-qa-session.ps1
```

## Rules

- Keep conversion local-only.
- No cloud upload.
- No telemetry.
- No fake conversion success.
- Do not bundle LibreOffice or Microsoft Office.
- Add tests for core behavior changes.
- Keep generated PDFs, images, logs, and release artifacts out of git.
