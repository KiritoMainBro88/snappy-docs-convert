# Auto-Update Plan

Auto-update is not implemented yet.

## Recommended Direction

Use Velopack with GitHub Releases after the release process stabilizes.

Why:

- Designed for desktop app update packages.
- Works with installer/update flows.
- Can use GitHub Releases as an update source.
- Fits the current Windows-first WPF app.

## Beta-Safe MVP Behavior

Do not silently auto-install updates during beta.

Recommended first implementation:

1. Add in-app **Check for updates**.
2. Fetch latest GitHub Release metadata.
3. Show latest version, release notes, and asset links.
4. Open the GitHub Release page for download.
5. Let the user choose installer or portable ZIP.

This keeps trust clear while the app remains unsigned.

## Risks

- Unsigned updates can still trigger SmartScreen or antivirus warnings.
- Update source must be verified.
- Silent background install is risky before code signing and release process maturity.
- GitHub prereleases should not surprise stable users.

## Later

After code signing or a stable release process:

- Add Velopack update packages.
- Verify update metadata and download source.
- Keep manual confirmation before installing.
- Document rollback behavior.
