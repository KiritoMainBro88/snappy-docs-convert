# Release Readiness

## Current Release Level

- Dev MVP portable: pass
- Core E2E: pass
- Manual GUI QA: completed with blockers
- Installer: Inno Setup EXE pass
- Code signing: pending
- Release candidate docs: prepared
- GitHub Release publication: approved for `v0.1.0-beta.1`
- Public beta: approved as prerelease with known limitations
- Vercel website production deploy: pass

## Verified

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- App `--self-check`
- Portable package creation
- Inno Setup installer creation
- Release smoke from zip
- Installer smoke: silent per-user install, installed self-check, silent uninstall
- No-console smoke verifies `OutputType=WinExe`, exe exists, and self-check exits 0.
- PDF Toolbox V1 unit tests for merge, split, extract, rotate, images-to-PDF, path safety, and page ranges.
- Static website check for local-only copy, release link, source link, and no external CDN/font/script assets.
- Vite website check for frontend-only build, EN/VI copy, release/source links, and no API/functions folders.
- Phase 10B website polish adds dark mode, direct `v0.1.0-beta.1` release links, Discord support, screenshot placeholders, MIT license wording, and anti-misleading-repackage wording.
- Vercel production URL: `https://website-sand-xi-15.vercel.app`.
- Phase 10C demo pipeline adds generated demo inputs, Playwright website screenshots/video, desktop screenshot helper, FFmpeg desktop recording helper, and user-facing demo scripts.
- Display app name changed to `kmb file tools`; current beta executable/package filenames still use `SnappyDocsConvert` identifiers.
- Phase 11A trust package adds unsigned-app safety docs, checksum manifest script, false-positive issue template, GitHub Actions CI, and manual release-build workflow with artifact attestation steps.
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
- Owner review of Vercel production website.
- Owner review of generated demo screenshots/video.
- GitHub prerelease upload for `v0.1.0-beta.1`.

## Known Risks

- Office COM depends on installed and activated Microsoft Office in the logged-in user session.
- LibreOffice fallback has not been real-smoked on this machine because LibreOffice is missing.
- Unsigned executable may trigger Windows SmartScreen.
- Current beta is not code-signed. SmartScreen/Unknown Publisher warnings may remain.
- Checksums and artifact attestations improve transparency but do not replace paid code signing.
- Framework-dependent package requires .NET 9 Desktop Runtime on Windows x64.
- Manual GUI QA found public-beta blockers.
- Normal double-click should not spawn a new console; launching from an existing terminal leaves that terminal open by design.
- Prior manual QA found PDF to Images UI freeze and missing unsupported-file feedback.
- Phase 8D added async safeguards and visible unsupported-file warnings; owner must re-test before public beta.
- Public beta is approved as prerelease; known limitations remain documented.
- MIT license is finalized.

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

Build installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-installer.ps1 -Version v0.1.0-beta.1
```

Smoke installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-installer.ps1 -InstallerPath .\artifacts\SnappyDocsConvert-setup-win-x64-v0.1.0-beta.1.exe
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

If Vercel auth is missing:

```powershell
vercel login
vercel --cwd website --prod
```

Official release:

```text
https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.1
```

Discord support:

```text
https://discord.gg/kZ3U36ncun
```

Generate demo assets:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-demo-inputs.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\capture-app-screenshot.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\demo-desktop.ps1
cd website
npm run demo:website
cd ..
```

Write checksums:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\write-checksums.ps1
```

Trust and false-positive guidance:

```text
docs/TRUST_AND_SECURITY.md
```

Manual GitHub Actions release build:

```text
Actions > Release build > Run workflow > version
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

- OK for public beta prerelease with known limitations.
- GitHub Release allowed for `v0.1.0-beta.1`.
- Recommended next work: owner downloads GitHub Release assets and performs manual installer/UI verification.
