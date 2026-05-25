# Evidence-Based E2E QA Gate

Phase 6B adds a local QA gate that proves the MVP with real commands and generated output files. It does not automate the WPF window and it does not install external engines.

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\qa-e2e.ps1
```

The script writes generated inputs, outputs, logs, and published app files under:

```text
docs\qa-output\current\
```

That folder is ignored by git.

## What It Verifies

- `dotnet build` exits successfully.
- `dotnet test` exits successfully.
- App `--self-check` exits successfully.
- App publish produces `SnappyDocsConvert.App.exe`.
- Generated PDF renders to PNG.
- Generated PDF renders to JPEG.
- Generated RTF converts to PDF through Word when Word is installed.
- Generated RTF converts to PDF plus images through Word when Word is installed.
- A batch with one valid PDF and one missing file reports partial success/failure correctly.
- LibreOffice missing state is reported honestly by setup guidance.

## Truth Rules

- Pass means the command ran and the script verified the expected output file or result.
- Skip means a local dependency is missing or a fixture is intentionally not available.
- Fail means a required command or verified output did not meet expectations.
- Unit tests are not treated as E2E proof by themselves.
- Generated PDFs/images/logs are evidence files, not source artifacts.

## Current Known Gaps

- LibreOffice real smoke is skipped unless LibreOffice is installed.
- PowerPoint real smoke still needs a safe PPT/PPTX fixture.
- Installer and packaging are not part of this gate.
- Manual WPF interaction remains a developer QA step until UI automation is added.
