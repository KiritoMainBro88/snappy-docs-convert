# PHASE 4 - PDF Image Renderer

Role: Codex coding executor.

Goal: export PDF pages to real images after owner approval.

Scope:

- Select a maintained local PDF renderer for .NET.
- Render PDF pages to PNG/JPEG at explicit DPI or pixel scale.
- Preserve page order with stable output naming.
- Add batch handling, cancellation, and invalid-PDF errors.
- Add tests for output naming, page count, and failure paths.

Rules:

- Do not use screenshots as conversion output.
- Do not upload PDFs.
- Do not add telemetry or tracking.

Verification:

- Run relevant tests.
- Smoke-test with owner-approved local PDFs only.
- Report exact commands and results.
