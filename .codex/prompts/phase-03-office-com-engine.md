# PHASE 3 - Microsoft Office COM Engine

Role: Codex coding executor.

Goal: implement local Microsoft Office COM export after owner approval.

Scope:

- Detect Word and PowerPoint availability.
- Export DOCX to PDF through Word COM.
- Export PPTX to PDF through PowerPoint COM.
- Export PowerPoint slides to real images through PowerPoint export APIs.
- Release COM objects and prevent orphaned Office processes.
- Add structured errors for missing Office, blocked files, timeouts, and export failures.

Out of scope:

- LibreOffice engine changes unless needed for shared abstractions.
- PDF renderer implementation.
- Cloud upload, telemetry, analytics, tracking, or remote converter service.

Verification:

- Run relevant tests.
- Smoke-test only with owner-approved local sample files.
- Report exact commands and results.
