# Phase Plan

## Phase 0/1: Local Setup

- Clone and inspect the repository.
- Create a safe local setup branch.
- Add agent instructions, context policy, research notes, phase prompts, Repomix config, and local check scripts.
- Do not implement conversion behavior.

## Phase 2: LibreOffice Engine

- Discover LibreOffice installation on Windows.
- Run headless conversion with isolated profile directories.
- Convert supported Office files to PDF where supported.
- Add timeouts, cancellation, and structured result reporting.

## Phase 3: Office COM Engine

- Detect Microsoft Office availability.
- Add Word DOCX to PDF export through COM.
- Add PowerPoint PPTX to PDF and slide image export through COM.
- Handle cleanup, COM release, and Office process safety.

## Phase 4: PDF Image Renderer

- Choose a maintained local PDF renderer.
- Export PDF pages to real images at selected DPI.
- Add tests for page counts, output naming, and invalid PDFs.

## Phase 5: WPF UI Polish

- Build the Windows desktop UI around queue, engine status, progress, cancellation, and output browsing.
- Keep the UI local-first and privacy-preserving.

## Phase 6: Packaging And Release

- Package the app for Windows.
- Document prerequisites for Office and LibreOffice modes.
- Add release checklist, smoke tests, and signing/update decisions if needed.
