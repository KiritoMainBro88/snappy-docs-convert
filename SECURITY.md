# Security Policy

kmb file tools is a local-only desktop utility. It should not upload files, emit telemetry, or call remote conversion services.

## Reporting A Vulnerability

For now, open a private contact path with the maintainer before public disclosure.

Placeholder maintainer:

- GitHub: https://github.com/KiritoMainBro88

Do not include private documents, tokens, passwords, or sensitive screenshots in public issues.

## Supported Versions

No public release channel exists yet. Security support starts after the first tagged release.

## Scope

In scope:

- Local file handling bugs.
- Accidental network upload/telemetry behavior.
- Unsafe path traversal or overwrite behavior.
- Packaging that includes unintended user/generated files.

Out of scope for MVP:

- Unsupported Office/LibreOffice behavior outside this app.
- Password recovery.
- DRM bypass.
