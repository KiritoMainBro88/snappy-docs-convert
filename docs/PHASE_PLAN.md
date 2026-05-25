# Phase Plan

## Phase 0/1: Local Setup

- Clone and inspect the repository.
- Create a safe local setup branch.
- Add agent instructions, context policy, research notes, phase prompts, Repomix config, RTK/Caveman token toolchain docs, and local check scripts.
- Do not implement conversion behavior.

## Phase 2: LibreOffice Engine

- Activate Caveman if available and use RTK for command output.
- Use Serena MCP for symbol search if available.
- Use Repomix only after narrowing context.
- Implemented: discover LibreOffice installation on Windows.
- Implemented: run headless conversion with isolated profile directories.
- Implemented: convert supported Office files to PDF where supported.
- Implemented: add timeouts, cancellation, and structured result reporting.
- Implemented: add unit tests with fake process runner; no LibreOffice install required for normal tests.
- Implemented: core setup guidance for Office/LibreOffice dependency decisions.
- Implemented: official LibreOffice download/CLI links and choose-path/recheck actions.

## Phase 3: Office COM Engine

- Implemented: detect Word and PowerPoint COM ProgIDs.
- Implemented: add Word `.doc`, `.docx`, `.rtf` to PDF export through COM.
- Implemented: add PowerPoint `.ppt`, `.pptx` to PDF export through COM.
- Implemented: serialize Office COM conversions, run on STA thread, enforce timeout, and release COM objects.
- Implemented: add guarded real Office smoke script for Word RTF-to-PDF through the project engine.
- Implemented: smoke script reports PowerPoint availability and skips PowerPoint conversion until a safe lightweight PPT/PPTX sample is added.
- Not implemented: slide image export; reserved for later renderer/export phases.

## Phase 4: PDF Image Renderer

- Implemented: choose PDFtoImage for MVP PDF rendering.
- Implemented: export PDF pages to PNG, JPEG, and WebP when runtime supports WebP.
- Implemented: configurable DPI with default 200, validation, and high-DPI warning.
- Implemented: safe page naming with prefix, zero padding, and overwrite policies.
- Implemented: serialize PDFium calls and render pages sequentially.
- Implemented: add tests for missing/non-PDF input, naming, overwrite policy, DPI/quality validation, cancellation, and tiny PDF rendering.
- Not implemented: batch/UI folder orchestration for multiple source PDFs.

## Phase 5A: Batch Conversion Pipeline

- Implemented: add batch job, item, option, progress, result, output-plan, and target models.
- Implemented: plan `pdf\<safeBaseName>__<hash8>.pdf` and `images\<safeBaseName>__<hash8>\`.
- Implemented: choose `slide` prefix for PPT/PPTX/ODP image output and `page` for document/PDF output.
- Implemented: select Microsoft Office or LibreOffice based on preference and availability.
- Implemented: orchestrate PDF, Images, and PdfAndImages targets.
- Implemented: continue after failed items; cancellation cancels current/remaining work where possible.
- Implemented: keep/delete intermediate PDFs for images-only jobs based on option.
- Not implemented: UI queue, progress window, or user-facing batch controls.

## Phase 6A: WPF UI MVP

- Implemented: create `SnappyDocsConvert.App` WPF project.
- Implemented: add files/folders, drag/drop queue, remove/clear queue.
- Implemented: output folder, target, engine, image format, DPI, and keep-intermediate-PDF settings.
- Implemented: run/cancel batch conversion through `BatchConversionPipeline`.
- Implemented: per-file status, output path, summary, and compact logs.
- Implemented: engine setup status with Office/LibreOffice checks, choose `soffice`, recheck, and LibreOffice download action.
- Implemented: `--self-check` mode and app smoke build script.
- Not implemented: final UI polish, installer, or automated GUI interaction tests.

## Phase 6B: Evidence-Based E2E QA Gate

- Implemented: add `scripts/qa-e2e.ps1`.
- Implemented: add `SnappyDocsConvert.QaHarness` for non-UI E2E checks through real core services.
- Implemented: verify build, tests, app self-check, and publish exe.
- Implemented: generate local PDF/RTF inputs under ignored QA output.
- Implemented: verify PDF to PNG/JPEG image output files.
- Implemented: verify Word RTF to PDF and Word RTF to PDF+Images when Word is installed.
- Implemented: verify batch partial failure continues after one missing file.
- Implemented: report LibreOffice missing as skipped/honest guidance, not pass.
- Not implemented: automated WPF GUI interaction, installer validation, or PowerPoint fixture smoke.

## Phase 6C: WPF UI Polish

- Implemented: polish WPF header, local-only badge, drop zone, queue table, settings groups, engine cards, action bar, and log panel.
- Implemented: show Word, PowerPoint, LibreOffice, and PDF renderer status with text labels.
- Implemented: add clearer engine guidance for Auto, Microsoft Office, and LibreOffice selection.
- Implemented: add row-level output open action and clear-log action.
- Implemented: add manual GUI QA checklist.
- Not implemented: automated GUI interaction tests, installer, packaging, or new engine behavior.

## Phase 7: Packaging And Release

- Implemented: add portable Windows x64 packaging script.
- Implemented: publish WPF app to `artifacts\SnappyDocsConvert-portable-win-x64-<version>\`.
- Implemented: create portable zip artifact.
- Implemented: include quickstart, privacy, and initial third-party notices in release folder.
- Implemented: add release smoke script for exe, `--self-check`, required docs, and forbidden content.
- Implemented: document dependency behavior for Microsoft Office and LibreOffice.
- Implemented: add manual GUI QA session recorder and result template.
- Implemented: add release readiness report for owner/dev use versus public beta.
- Not implemented: MSI/MSIX installer, signing, auto-update, or final legal attribution audit.
- Not implemented: completed owner manual GUI QA result.

## Phase 8A: PDF Toolbox V1 And Open Source Prep

- Implemented: set WPF app `OutputType` to `WinExe` for normal double-click no-console GUI launch.
- Implemented: keep `--self-check` usable for smoke scripts; console output may be unavailable under WinExe automation, so scripts accept exit code.
- Implemented: add smoke-no-console script.
- Implemented: add PDF Toolbox V1 core operations: merge, split, extract pages, rotate pages, and images to PDF.
- Implemented: add WPF PDF Tools section.
- Implemented: add tests for PDF toolbox parsing and operations.
- Implemented: add static website skeleton with GitHub Releases/source links.
- Implemented: add open-source prep docs, draft MIT license, security/contributing docs, issue templates, PR template.
- Not implemented: OCR, compression, encryption/signing, redaction, form filling, installer, publishing GitHub release.

## Phase 8B: Release Candidate Gate And GitHub Release Prep

- Implemented: run release-candidate build, test, E2E, package, release smoke, and no-console smoke checks.
- Implemented: add privacy audit doc covering local-only behavior, external links, and scan result notes.
- Implemented: add v0.1.0-rc1 release notes with verified/skipped status and artifact checksum.
- Implemented: add artifact hash helper.
- Implemented: add GitHub Release notes/instructions helper that does not publish.
- Implemented: add static website checker for required links/copy and no external CDN/font/script assets.
- Implemented: expand owner manual GUI QA checklist for packaged app, no-console double-click, and PDF Tools checks.
- Not implemented: push, GitHub Release creation, manual GUI QA execution, code signing, or installer.

## Phase 8C: Owner Manual GUI QA Record

- Implemented: record owner manual GUI QA for `v0.1.0-rc1`.
- Implemented: record public-beta blockers for UI freeze, unsupported-file feedback, untested GUI flows, and license approval.
- Not implemented: release publication, license finalization, or UI bug fixes.

## Phase 8D: UI/UX Overhaul, EN/VI, Branding Prep

- Implemented: reorganize WPF into mode-based navigation: Convert, PDF Tools, Engines, Logs, Help.
- Implemented: add English/Vietnamese UI string service with runtime toggle.
- Implemented: move PDF Tools into its own page.
- Implemented: add visible unsupported-file/rejected-input feedback.
- Implemented: run batch conversion and PDF tools on background tasks to keep UI responsive.
- Implemented: add logo/icon asset handling and branding docs.
- Not implemented: new conversion engines, installer, release publication, image generation, or final GUI QA pass.

## Phase 9A: Static Marketing Website And Vercel Preview

- Implemented: add `website/` Vite + React + TypeScript frontend-only marketing site.
- Implemented: use owner-provided local logo/favicon/hero assets where present.
- Implemented: add English/Vietnamese website content toggle.
- Implemented: link downloads to GitHub Releases and source to GitHub repository.
- Implemented: document Vercel preview and production deploy commands.
- Implemented: add Vite website checker for build, required links/copy, no API/functions folders, and no external CDN/font/script references.
- Not implemented: backend, API routes, telemetry, analytics, production Vercel deploy, GitHub Release creation, or GitHub push.

## Phase 10A: Public Beta Release

- Implemented: finalize MIT `LICENSE`.
- Implemented: add Inno Setup installer script.
- Implemented: add installer packaging and smoke scripts.
- Implemented: build public beta portable zip and installer exe.
- Implemented: smoke portable zip and installer.
- Implemented: add `v0.1.0-beta.1` release notes with hashes.
- Planned: push source, fast-forward main, tag, and create GitHub prerelease with both assets.
- Not implemented: production Vercel deploy, code signing, MSI/MSIX, or LibreOffice real smoke.

## Phase 10B: Website Production Polish And Deploy

- Implemented: polish the Vite website landing page with clearer download, local-only, features, PDF Toolbox, screenshots, open-source/community, roadmap, FAQ, and support sections.
- Implemented: add dark mode with persisted theme preference.
- Implemented: preserve English/Vietnamese website copy and add natural Vietnamese wording for free/open-source, local-only, official downloads, and Discord support.
- Implemented: link direct beta installer/portable assets, the GitHub Release page, source repository, and Discord support.
- Implemented: add screenshot placeholders and `website/public/screenshots/README.md` for owner-provided app screenshots.
- Implemented: update website checker for Discord/release/source links, EN/VI theme copy, no backend/API/functions folders, no external CDN assets, and no known analytics provider markers.
- Implemented: Vercel production deploy to `https://website-sand-xi-15.vercel.app`.
- Not implemented: backend, telemetry, analytics, new desktop features, new GitHub app release, or auto-update.

## Phase 10C: Auto Demo Kit

- Implemented: adopt user-facing display name `kmb file tools` while preserving existing `SnappyDocsConvert` package/binary identifiers for beta compatibility.
- Implemented: add generated demo input script for PDF, RTF, PNG, and unsupported dummy file.
- Implemented: add Playwright website demo test for light/dark EN, light VI, mobile VI screenshots, link verification, and video recording.
- Implemented: add desktop screenshot helper and FFmpeg desktop recording helper.
- Implemented: add demo asset policy and English/Vietnamese demo narration script.
- Not implemented: backend, telemetry, analytics, new GitHub app release, committed generated media, or OBS automation.

## Phase 11A: Trust Package And Attested Build Prep

- Implemented: add unsigned-app trust and security guide with English/Vietnamese SmartScreen, checksum, and false-positive guidance.
- Implemented: add checksum manifest writer for release ZIP/EXE artifacts.
- Implemented: add GitHub Actions CI for Windows build/test/website validation.
- Implemented: add manual GitHub Actions release-build workflow for portable ZIP, installer EXE, checksum manifests, upload-artifact, and artifact attestation steps.
- Implemented: add antivirus false-positive issue template.
- Implemented: add Winget distribution prep docs.
- Not implemented: code signing, paid certificate, updater, backend, telemetry, automatic scanner upload, or new GitHub Release.

## Phase 11B: Post-Release Audit, Website Deploy, Tooling Hygiene

- Implemented: verify GitHub Release `v0.1.0-beta.1` still has portable ZIP and installer EXE assets.
- Implemented: fix local `rtk grep` by installing ripgrep and placing `rg.exe` in the user-local RTK tools folder.
- Implemented: document RTK grep/ripgrep dependency and Windows recovery steps.
- Implemented: verify frontend-only website content for dark mode, EN/VI, release links, Discord, screenshots placeholders, open-source/community copy, no upload, and no telemetry.
- Implemented: deploy Vercel preview and production website.
- Implemented: update GitHub repository homepage metadata to the Vercel production URL.
- Implemented: document manual GitHub Actions `release-build` run steps.
- Implemented: add auto-update design doc only.
- Not implemented: auto-update code, backend/API routes, telemetry, new GitHub Release, new release assets, LibreOffice smoke, or PowerPoint smoke.

## Phase 12A-Lite: Low-End OCR Strategy

- Implemented: define OCR Lite, OCR Balanced, and OCR Advanced tiers.
- Implemented: choose OCR Lite as recommended MVP default for low-end Windows machines.
- Implemented: document Windows OCR first, Tesseract `tessdata_fast` fallback, Vietnamese + English first, DPI 150/200, sequential page processing, cancellation, and progress.
- Implemented: document packaging rules: no traineddata/model files in repo and no heavy OCR bundled by default.
- Implemented: document PaddleOCR as future optional advanced plugin/model pack only.
- Implemented: update website roadmap copy to say OCR Lite is planned, local-first, and not available yet.
- Not implemented: OCR engine, OCR UI, model download, PaddleOCR, benchmark, release.

## Phase 13A: Settings, Update Center, Website Demo, Beta.2

- Implemented: add app Settings page with Appearance, Language, Updates, and About sections.
- Implemented: app supports System/Light/Dark theme and persists selected theme under user local settings.
- Implemented: app detects system UI language on first run, supports Auto/System, English, Vietnamese, and persists selected language.
- Implemented: add manual update center that checks GitHub Releases, supports Stable or Prerelease/Beta channel, downloads selected asset with progress, verifies GitHub SHA256 digest when present, and launches installer or opens ZIP folder only after user action.
- Implemented: update website to link `v0.1.0-beta.2`, detect browser language, persist language/theme in localStorage, keep frontend-only/no telemetry constraints, and show curated demo screenshots/video.
- Implemented: rename public beta package output to `kmb-file-tools-portable-*` and `kmb-file-tools-setup-*`.
- Not implemented: silent background auto-update, Velopack, code signing, LibreOffice smoke, PowerPoint smoke, OCR engine.

## Phase 13B: Portable Launch Hotfix And App Demo Website

- Implemented: reproduce packaged portable GUI launch failure instead of trusting `--self-check` alone.
- Implemented: fix startup crash caused by mutating read-only WPF theme brush resources.
- Implemented: add local startup diagnostics and crash logging under `%LOCALAPPDATA%\kmb-file-tools\logs\`.
- Implemented: add packaged GUI launch smoke that verifies the real `kmb file tools` window appears.
- Implemented: update release smoke to include GUI launch smoke.
- Implemented: update website demo section to prioritize desktop app screenshot/video assets.
- Planned: release `v0.1.0-beta.3` only after packaged GUI launch smoke passes.
- Not implemented: code signing, silent auto-update, backend, telemetry, LibreOffice smoke, PowerPoint smoke, OCR engine.
