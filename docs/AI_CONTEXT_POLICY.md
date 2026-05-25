# AI Context Policy

Snappy Docs Convert should stay easy for AI agents to inspect without wasting tokens or exposing private data.

## Standard Toolkit

- `AGENTS.md` for durable project rules.
- `git status`, `git diff --stat`, `git ls-files`, and `rg` for targeted local search.
- Repomix for filtered context packing and token counting.
- Serena MCP for semantic symbol lookup and refactoring when available.
- Phase prompts in `.codex/prompts/` for repeatable future work.

## Repomix Usage

Use Repomix only with include/exclude filters. The default config prioritizes project instructions, docs, and future .NET/WPF source files. If a future task needs browser prototype files, add explicit `src/**/*.ts`, `src/**/*.tsx`, and related config includes for that run.

Generated context files belong under `docs/ai-context/` and must not be committed.

## Serena MCP Notes

Serena MCP is recommended for larger codebase work because it can provide:

- Symbol overviews.
- Find references.
- Safer symbol-level refactoring.
- Less token-heavy navigation than reading entire files.

Codex should use Serena for code navigation and symbol edits when it is already configured. For simple operations, Codex built-in search and shell commands are enough.

Serena is optional for Phase 0/1. If unavailable, proceed without it. Do not install it globally without owner approval. If the owner already has MCP configured, prefer the configured MCP tools.

## RTK Naming Note

The owner mentioned `rtk` as a token-saving or indexing idea, but the exact tool is ambiguous. Do not install any package named `rtk` unless it is already part of the configured tooling or the owner explicitly confirms it.

This repo standardizes on the safe toolkit above until a specific tool is approved.

## Log Budget

- Paste only the relevant build/test error lines.
- Summarize successful long logs.
- Never print secrets, tokens, auth headers, credential paths, or private environment values.
