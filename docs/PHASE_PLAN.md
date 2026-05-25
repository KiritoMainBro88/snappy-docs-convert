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

## Phase 6: WPF UI Polish

- Build the Windows desktop UI around queue, engine status, progress, cancellation, and output browsing.
- Keep the UI local-first and privacy-preserving.

## Phase 7: Packaging And Release

- Package the app for Windows.
- Document prerequisites for Office and LibreOffice modes.
- Add release checklist, smoke tests, and signing/update decisions if needed.
