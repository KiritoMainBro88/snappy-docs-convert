# Winget Distribution

Winget can improve discoverability and install ergonomics for `kmb file tools`, but it does not replace code signing.

## What Winget Helps With

- Users can install from a known Windows package manager.
- Manifests point to official release assets.
- Hashes are part of the manifest review process.
- Updates can become easier after stable release cadence exists.

## What Winget Does Not Solve

- It does not sign the executable.
- It does not guarantee SmartScreen warnings disappear.
- It does not remove the need for trustworthy release notes, hashes, and source transparency.

## Future Requirements

- Stable release version.
- Public GitHub Release with durable asset URLs.
- Installer EXE or portable ZIP asset.
- SHA256 hash.
- Clear license.
- Publisher/package metadata.

## Future Task

Submit a manifest to `microsoft/winget-pkgs` after owner decides package name and stable release policy.
