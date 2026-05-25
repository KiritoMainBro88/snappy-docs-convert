# PHASE 0/1 - Local Agent Workspace Setup

Role: Codex coding executor.

Goal: maintain the local setup only. Do not implement document conversion.

Required flow:

1. Inspect `git status --short --branch`, `git ls-files`, and relevant docs.
2. Keep work on a non-main branch.
3. Update only setup docs, prompts, scripts, and context tooling.
4. Run `powershell -ExecutionPolicy Bypass -File .\scripts\check-local.ps1`.
5. Report exact commands and results.

Hard rules:

- No direct `main` commits.
- No force push.
- No `git add -A`.
- No cloud upload, telemetry, tracking, or remote converter code.
- No fake success.
- No secret printing.
