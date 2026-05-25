# AGENTS.md

## Project Identity

Snappy Docs Convert is a local document conversion app. The target operating system is Windows first.

Final direction:
- Local-only conversion.
- No cloud upload.
- No telemetry, analytics, tracking, or remote converter code.
- Desktop app first: C# .NET WPF.
- Browser prototype code is allowed only for PDF/image experiments or UI exploration, not for Office COM or LibreOffice conversion.

## Agent Workflow

1. Inspect the current repo state and relevant files.
2. Plan the smallest useful change.
3. Implement minimal, scoped edits.
4. Test with relevant build/check commands.
5. Report exact commands, results, risks, and next steps.

## Token And Context Policy

- Do not read the whole repo unless necessary.
- Prefer `git status`, `git diff --stat`, `git ls-files`, `rg`, and targeted file reads.
- Use Repomix only with include/exclude filters.
- Use Serena MCP semantic tools for symbol overview, reference lookup, and safer refactoring when available.
- If Serena MCP is unavailable, continue with Codex search/shell tools.
- Avoid pasting full build logs unless the error needs the surrounding lines.
- Prefer official docs for current library, framework, SDK, API, CLI, and cloud-service behavior.

## Git Rules

- Do not commit directly to `main`.
- Do not push without owner approval.
- Do not force-push.
- Do not use `git add -A`.
- Stage files explicitly by path.
- Preserve user changes; never overwrite source files blindly.

## Safety Rules

- No destructive commands without approval.
- No fake success; report blockers and missing tools clearly.
- No secret printing: tokens, auth headers, credential paths, private env values.
- No cloud upload, telemetry, analytics, tracking, or remote conversion implementation.
- Do not implement DOCX/PPTX/PDF conversion until the owner approves the relevant phase.

## Verification

- Always run relevant build, test, or check commands before reporting completion.
- Report exact commands and results.
- If a command cannot be run, explain why and list the residual risk.
