# Release Readiness

## Current Release Level

- Dev MVP portable: pass
- Core E2E: pass
- Manual GUI QA: completed with blockers
- Installer: pending
- Code signing: pending
- Release candidate docs: prepared
- GitHub Release publication: pending owner approval
- Public beta: blocked pending owner re-test after Phase 8D UI overhaul
- Vercel website preview: pending Phase 9A deploy check

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
- Vite website check for frontend-only build, EN/VI copy, release/source links, and no API/functions folders.
- Artifact SHA256 helper.
- Release notes helper that prints GitHub Release instructions without creating a release.
- Owner manual GUI QA result recorded in `docs/manual-qa/v0.1.0-rc1-gui-qa.md`.
- Release blockers recorded in `docs/manual-qa/v0.1.0-rc1-blockers.md`.
- Phase 8D UI overhaul adds separate Convert/PDF Tools/Engines/Logs/Help pages, EN/VI strings, unsupported-file feedback, and async UI safeguards.
- Branding asset handling documented in `docs/BRANDING.md`.
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
- Owner manual GUI re-test on `v0.1.0-rc1-ui`.
- RTF to PDF+Images through GUI after UI overhaul.
- Batch partial failure through GUI after UI overhaul.
- Cancel behavior through GUI after UI overhaul.
- MSI/MSIX installer.
- Code signing.
- GitHub Release publication.
- Final license approval.
- Owner review of Vercel preview website.

## Known Risks

- Office COM depends on installed and activated Microsoft Office in the logged-in user session.
- LibreOffice fallback has not been real-smoked on this machine because LibreOffice is missing.
- Unsigned executable may trigger Windows SmartScreen.
- Framework-dependent package requires .NET 9 Desktop Runtime on Windows x64.
- Manual GUI QA found public-beta blockers.
- Normal double-click should not spawn a new console; launching from an existing terminal leaves that terminal open by design.
- Prior manual QA found PDF to Images UI freeze and missing unsupported-file feedback.
- Phase 8D added async safeguards and visible unsupported-file warnings; owner must re-test before public beta.
- Public beta is blocked until owner re-test passes or limitations are explicitly accepted.
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

Check Vite website:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-website-vite.ps1
```

Deploy Vercel preview:

```powershell
vercel --cwd website
```

Production Vercel deploy requires explicit owner approval:

```powershell
vercel --cwd website --prod
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
- Recommended next work: owner reviews Vercel preview, owner re-runs manual GUI QA on `v0.1.0-rc1-ui`, then finalize license.
