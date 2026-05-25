1. Activate Caveman if available: `/caveman`
2. Use RTK for command output.
3. Use Serena MCP for symbol search if available.
4. Use Repomix only after narrowing context.
5. End with compressed Caveman-style report.

# PHASE 2 - LibreOffice Headless Engine

Role: Codex coding executor.

Goal: implement the first local LibreOffice headless conversion engine after owner approval.

Scope:

- Discover LibreOffice on Windows without requiring admin access.
- Prefer `soffice.com` for console/headless operations when available.
- Convert supported Office files to PDF through LibreOffice headless.
- Use isolated temp user profiles and explicit output directories.
- Add structured result types, errors, timeouts, and cancellation.
- Add tests for command construction and failure handling.

Out of scope:

- Office COM engine.
- PDF page image rendering.
- Cloud upload, telemetry, analytics, tracking, or remote converter service.
- Full WPF UI polish.

Verification:

- Run relevant unit tests.
- Run a local smoke command only with owner-approved sample files.
- Report exact commands and results.
