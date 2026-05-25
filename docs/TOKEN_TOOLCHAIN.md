# Token Toolchain

kmb file tools uses two real token-saving tools for Codex work:

- RTK: Rust Token Killer. Official repo: https://github.com/rtk-ai/rtk
- Caveman. Official repo: https://github.com/JuliusBrussee/caveman

## RTK

RTK is a local CLI proxy that reduces command-output token use before output enters the agent context. It is useful for git, file reads, grep/search, build output, and test logs.

Verified commands from the official docs:

```powershell
rtk --version
rtk gain
rtk init -g --codex
```

Use RTK explicitly when hook rewriting is not clearly active:

```powershell
rtk git status
rtk git diff
rtk git log
rtk read AGENTS.md
rtk grep "Mandatory Token Toolchain" .
rtk test <command>
```

Current Windows machine notes:

- RTK was found at `C:\Users\Kirito\.local\bin\rtk.exe`.
- Verified version: `rtk 0.38.0`.
- `rtk gain` works, so this is Rust Token Killer, not the unrelated Rust Type Kit package.
- `rtk init -g --codex` configured Codex instructions at `C:\Users\Kirito\.codex\RTK.md`.
- The current session may still need explicit `rtk ...` commands if hook rewriting is not active.
- If `rtk grep` cannot resolve `rg` while `rg --version` works, normalize the current process `PATH` before retrying:

```powershell
$rg = (Get-Command rg).Source
$env:PATH = (Split-Path $rg) + ';' + $env:PATH
$env:Path = $env:PATH
rtk grep "pattern" .
```

Official Windows install fallback:

1. Download `rtk-x86_64-pc-windows-msvc.zip` from https://github.com/rtk-ai/rtk/releases.
2. Extract `rtk.exe` to a user-local tools folder such as `C:\Users\<you>\.local\bin`.
3. Add that folder to the current process `PATH` if needed.
4. Verify with `rtk --version` and `rtk gain`.

Uninstall, if documented global setup must be removed:

```powershell
rtk init -g --uninstall
```

If installed by Cargo:

```powershell
cargo uninstall rtk
```

## Caveman

Caveman is a Codex-compatible skill set that reduces agent output by making replies shorter while preserving technical accuracy.

Official Codex install command:

```powershell
npx skills add JuliusBrussee/caveman -a codex
```

For user-level install when owner-approved:

```powershell
npx skills add JuliusBrussee/caveman -a codex -g
```

Current Windows machine notes:

- Caveman was installed for Codex through `npx skills add JuliusBrussee/caveman -a codex`.
- Caveman was also installed globally through `npx skills add JuliusBrussee/caveman -a codex -g`.
- Global skill files were verified under `C:\Users\Kirito\.agents\skills\caveman*` and `C:\Users\Kirito\.agents\skills\cavecrew`.
- The `skills` binary is not necessarily on PATH; use `npx skills ...`.
- Caveman for Codex is per-session by default.
- Start future Codex sessions with `/caveman`.
- This current Codex session may not become compressed until the skill is loaded and activated.

Verify:

```powershell
npx skills list
npx skills list -g
```

Uninstall, per Caveman docs:

```powershell
npx -y github:JuliusBrussee/caveman -- --uninstall
```

Skills installed via `npx skills add` are managed by the skills CLI:

```powershell
npx skills remove caveman
npx skills remove caveman -g
```

## Recommended Agent Order

1. Start with `/caveman` if available.
2. Use RTK for command output.
3. Use Serena MCP for semantic symbol search when configured.
4. Use Repomix only after narrowing context.
5. Fall back to `rg`, `git`, and targeted file reads if RTK or Serena is unavailable.

## Fallback

If RTK is unavailable, use normal `git`, `rg`, and targeted PowerShell reads, but keep output narrow. If Caveman is unavailable, manually keep final reports short, blunt, and evidence-based.
