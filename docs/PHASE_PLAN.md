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
