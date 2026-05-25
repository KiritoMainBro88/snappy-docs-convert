# Release Readiness

## Current Release Level

- Dev MVP portable: pass
- Core E2E: pass
- Manual GUI QA: completed with blockers
- Installer: pending
- Code signing: pending
- Release candidate docs: prepared
- GitHub Release publication: pending owner approval
- Public beta: blocked

## Verified

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- App `--self-check`
- Portable package creation
- Release smoke from zip
- No-console smoke verifies `OutputType=WinExe`, exe exists, and self-check exits 0.
- PDF Toolbox V1 unit tests for merge, split, extract, rotate, images-to-PDF, path safety, and page ranges.
- Static website check for local-only copy, release link, source link, and no external CDN/font/script assets.
- Artifact SHA256 helper.
- Release notes helper that prints GitHub Release instructions without creating a release.
- Owner manual GUI QA result recorded in `docs/manual-qa/v0.1.0-rc1-gui-qa.md`.
- Release blockers recorded in `docs/manual-qa/v0.1.0-rc1-blockers.md`.
- Core E2E QA:
  - PDF to PNG
  - PDF to JPEG
  - RTF to PDF through Word when Word is available
  - RTF to PDF plus images through Word when Word is available
  - Batch partial failure
  - Honest LibreOffice-missing reporting

## Skipped Or Pending

- LibreOffice real smoke unless LibreOffice is installed.
- PowerPoint real smoke until a safe PPT/PPTX fixture exists.
- RTF to PDF+Images through GUI.
- Batch partial failure through GUI.
- Cancel behavior through GUI.
- MSI/MSIX installer.
- Code signing.
- GitHub Release publication.
- Final license approval.

## Known Risks

- Office COM depends on installed and activated Microsoft Office in the logged-in user session.
- LibreOffice fallback has not been real-smoked on this machine because LibreOffice is missing.
- Unsigned executable may trigger Windows SmartScreen.
- Framework-dependent package requires .NET 9 Desktop Runtime on Windows x64.
- Manual GUI QA found public-beta blockers.
- Normal double-click should not spawn a new console; launching from an existing terminal leaves that terminal open by design.
- PDF to Images via GUI produced output but froze the app during conversion.
- Unsupported file add did not show clear feedback.
- Public beta is blocked until GUI blockers are fixed or explicitly accepted as pre-release limitations.
- MIT license is draft-only until owner explicitly approves it.

## Commands

Build portable package:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
```

Smoke release zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-release.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
```

Run core E2E QA:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\qa-e2e.ps1
```

Create manual GUI QA session:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\new-gui-qa-session.ps1
```

Run no-console smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-no-console.ps1
```

Check static website:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-website-static.ps1
```

Hash release artifact:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hash-artifact.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-<version>.zip
```

Prepare release notes/instructions:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-github-release-notes.ps1
```

## Recommendation

- OK for owner/dev manual use with known blockers.
- Not public beta ready.
- Do not create GitHub Release until owner explicitly approves.
- Recommended next work: fix UI freeze, add unsupported-file feedback, verify RTF/batch/cancel GUI flows, finalize license.
