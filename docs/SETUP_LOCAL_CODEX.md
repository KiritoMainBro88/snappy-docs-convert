# Local Codex Setup

This document describes the local workflow for future Codex sessions.

## Start

```powershell
cd D:\vibecode\snappy-docs-convert
git status --short --branch
git switch ai/phase-00-local-agent-setup
powershell -ExecutionPolicy Bypass -File .\scripts\check-local.ps1
```

If Caveman is available in Codex, activate it once per session:

```text
/caveman
```

Use RTK explicitly when hook-based rewriting is uncertain:

```powershell
rtk git status
rtk git diff
rtk read AGENTS.md
rtk grep "pattern" .
```

Verify token tools:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-token-tools.ps1
```

## Context Packing

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\pack-context.ps1
```

The script:

- Creates `docs/ai-context/` if missing.
- Reminds agents to use RTK and targeted search before packing context.
- Runs `repomix` when available.
- Falls back to `npx repomix@latest` when npm is available.
- Prints clear instructions if neither path is available.

No global install is performed by the script.

## Git Safety

Use feature branches for all work. Do not commit to `main`, do not force-push, and do not use `git add -A`.

Stage explicitly:

```powershell
git add README.md AGENTS.md .gitignore .editorconfig repomix.config.json docs scripts .codex
```

Push only after owner approval.
