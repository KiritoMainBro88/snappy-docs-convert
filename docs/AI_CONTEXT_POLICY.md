# AI Context Policy

kmb file tools should stay easy for AI agents to inspect without wasting tokens or exposing private data.

## Standard Toolkit

- `AGENTS.md` for durable project rules.
- RTK for compact command output, especially git, file reads, grep/search, and test logs.
- Caveman for compact Codex replies and final reports.
- Serena MCP for semantic symbol lookup and refactoring when available.
- Repomix for filtered context packing and token counting after narrowing the target area.
- `rg`, `git status`, `git diff --stat`, and `git ls-files` as fallback targeted local search.
- Phase prompts in `.codex/prompts/` for repeatable future work.

## Mandatory Token Toolchain

Use RTK first for command output:

```powershell
rtk git status
rtk git diff
rtk git log
rtk read AGENTS.md
rtk grep "Mandatory Token Toolchain" .
rtk test <command>
```

Use Caveman for agent output when the skill is available:

```text
/caveman
```

Caveman is per-session for Codex by default. If it is not active in the current session, keep reports short, direct, and evidence-based manually.

## Repomix Usage

Use Repomix only with include/exclude filters, and only after RTK or targeted search has narrowed the context. The default config prioritizes project instructions, docs, and future .NET/WPF source files. If a future task needs browser prototype files, add explicit `src/**/*.ts`, `src/**/*.tsx`, and related config includes for that run.

Generated context files belong under `docs/ai-context/` and must not be committed.

## Serena MCP Notes

Serena MCP is recommended for larger codebase work because it can provide:

- Symbol overviews.
- Find references.
- Safer symbol-level refactoring.
- Less token-heavy navigation than reading entire files.

Codex should use Serena for code navigation and symbol edits when it is already configured. For simple operations, Codex built-in search and shell commands are enough.

Serena is optional. If unavailable, proceed without it. Do not install it globally without owner approval. If the owner already has MCP configured, prefer the configured MCP tools.

## Tool References

See `docs/TOKEN_TOOLCHAIN.md` for install, verify, Windows notes, and fallback commands for RTK and Caveman.

## Log Budget

- Paste only the relevant build/test error lines.
- Summarize successful long logs.
- Never print secrets, tokens, auth headers, credential paths, or private environment values.
