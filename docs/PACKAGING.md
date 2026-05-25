# Portable Packaging

Phase 7 creates a portable Windows x64 folder and zip for the WPF MVP. This is not an MSI/MSIX installer. Phase 13B adds GUI launch smoke so a package is not considered healthy unless the real desktop window appears.

## Build Portable Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
```

Default output:

```text
artifacts\
  kmb-file-tools-portable-win-x64-<version>\
  kmb-file-tools-portable-win-x64-<version>.zip
```

The package is framework-dependent by default and requires the .NET 9 Desktop Runtime on Windows x64.

The WPF app uses `OutputType=WinExe`, so normal double-click launch should open the GUI without spawning a console. If the app is launched from an existing terminal, that terminal staying open is normal.

Optional self-contained build:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1 -SelfContained
```

## Build Installer

The beta installer uses Inno Setup and installs per-user by default.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-installer.ps1 -Version v0.1.0-beta.3
```

Expected output:

```text
artifacts\kmb-file-tools-setup-win-x64-v0.1.0-beta.3.exe
```

Installer metadata:

- App name: kmb file tools
- Install scope: per-user
- Start Menu shortcut: yes
- Optional Desktop shortcut: yes
- License included: MIT
- Quickstart/privacy/third-party notices included through portable publish output

Smoke installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-installer.ps1 -InstallerPath .\artifacts\kmb-file-tools-setup-win-x64-v0.1.0-beta.3.exe
```

The smoke script verifies the EXE, reports signature status, silently installs to a temp folder, runs installed `SnappyDocsConvert.App.exe --self-check`, and uninstalls silently.

## Smoke Release Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-release.ps1 -PackagePath .\artifacts\kmb-file-tools-portable-win-x64-<version>.zip
```

The smoke check verifies:

- `SnappyDocsConvert.App.exe` exists.
- `SnappyDocsConvert.App.exe --self-check` exits successfully and reports `selfCheck: ok`.
- `README-QUICKSTART.txt`, `PRIVACY.txt`, and `THIRD-PARTY-NOTICES.txt` exist.
- Source folders, tests, QA output, logs, generated PDFs/images, and nested zips are absent.
- Expected branding asset `Assets\logo.png` is allowed when present.
- Packaged GUI launch smoke starts the extracted app and verifies a real `kmb file tools` window appears.

With `WinExe`, console output from `--self-check` can be unavailable under automation. Release smoke treats exit code 0 as valid when JSON output is not captured.

Direct GUI launch smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-gui-launch.ps1 -ExePath .\artifacts\kmb-file-tools-portable-win-x64-<version>\SnappyDocsConvert.App.exe
```

Startup logs:

```text
%LOCALAPPDATA%\kmb-file-tools\logs\app.log
%LOCALAPPDATA%\kmb-file-tools\logs\crash.log
```

No-console smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-no-console.ps1
```

Hash release artifact:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\hash-artifact.ps1 -PackagePath .\artifacts\kmb-file-tools-portable-win-x64-<version>.zip
```

Write checksum manifests for release ZIP/EXE artifacts:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\write-checksums.ps1
```

Prepare GitHub release notes/instructions without creating a release:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-github-release-notes.ps1
```

## Release Checklist

1. Run QA: `scripts\qa-e2e.ps1`.
2. Build package: `scripts\package-portable.ps1`.
3. Build installer: `scripts\package-installer.ps1`.
4. Smoke release zip: `scripts\smoke-release.ps1`.
5. Smoke installer: `scripts\smoke-installer.ps1`.
6. Hash artifacts: `scripts\hash-artifact.ps1`.
7. Push branch only after owner approval.
8. Merge/review on GitHub.
9. Tag release.
10. Create GitHub prerelease.
11. Upload portable zip and installer exe.
12. Update website if release URL or copy changes.
13. Publish checksums and artifact attestations when using GitHub Actions release-build workflow.

## GitHub Actions Release Build

Manual remote release build:

```powershell
gh workflow run release-build.yml -f version=v0.1.0-beta.3 --repo KiritoMainBro88/snappy-docs-convert
```

Or use GitHub UI:

```text
Repository > Actions > Release build > Run workflow > version
```

The workflow builds, tests, packages, smoke-tests, writes checksums, uploads workflow artifacts, and creates artifact attestations. It does not create or mutate GitHub Releases automatically.

## Website And Vercel

The polished website lives under `website/` and builds to `website\dist\`.

Current production URL:

```text
https://website-sand-xi-15.vercel.app
```

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-website-vite.ps1
```

Preview deploy after local validation:

```powershell
vercel --cwd website
```

Production deploy requires explicit owner approval:

```powershell
vercel --cwd website --prod
```

The site links downloads to GitHub Releases. It must remain frontend-only: no backend, no API routes, no telemetry, no analytics, and no file upload flow.

Current public beta release:

```text
https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.3
```

Support/Discord:

```text
https://discord.gg/kZ3U36ncun
```

The website includes system/light/dark mode, browser language detection with persisted EN/VI selection, direct installer/portable download buttons, and curated demo screenshots/video. Official downloads are free. Because the project uses MIT, documentation should say redistribution is allowed with attribution/license notice; it should warn against misleading paid repackages without claiming resale is prohibited.

Display app name is `kmb file tools`. Public beta package filenames use `kmb-file-tools`; internal executable/project names still use `SnappyDocsConvert` for .NET compatibility.

Demo assets:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-demo-inputs.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\demo-desktop.ps1
cd website
npm run demo:website
cd ..
```

Generated media stays under ignored `artifacts\demo\`.

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

- MSI/MSIX installer pending; Inno Setup EXE installer exists for beta.
- LibreOffice real smoke depends on installed LibreOffice.
- PowerPoint real smoke fixture pending.
- Manual GUI QA remains owner-executed via `docs/GUI_QA_CHECKLIST.md`.
- Public beta should wait for a completed `MANUAL_GUI_QA_RESULT.md`.
- Unsigned builds may show Windows SmartScreen warnings until signing exists.
- Checksum manifests and GitHub artifact attestations improve transparency but do not remove SmartScreen/Unknown Publisher warnings.
- GitHub Release upload is owner-approved for `v0.1.0-beta.3`; packaging scripts still do not publish by themselves.
