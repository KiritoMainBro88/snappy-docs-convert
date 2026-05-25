# Privacy Audit

Date: 2026-05-25

Scope:
- `src/`
- `docs/`
- `website-static/`
- `README.md`
- `.github/`

## Summary

- App conversion path is local-only.
- No app telemetry, analytics, tracking, cloud upload, or remote converter code found.
- Static website contains external links only.
- No secrets found in tracked source/docs by targeted keyword audit.

## Network Behavior

- WPF app can open official external links when user chooses setup/help actions:
  - LibreOffice download
  - LibreOffice CLI/start-parameters help
- Static website links to GitHub Releases and source repository.
- Core conversion engines operate on local files and local tools.
- Microsoft Office and LibreOffice, when used, run locally on the user's machine.

## Audit Command

```powershell
rg -i "http://|https://|telemetry|analytics|upload|post|put|tracking|sentry|appcenter|instrumentation|api_key|apikey|secret|password|token|bearer|authorization" src docs website-static README.md .github
```

RTK could not resolve `rg` through its wrapper in this session, so direct `rg` and PowerShell targeted scans were used.

## Result Notes

- `https://` hits are expected for XAML schemas, GitHub links, LibreOffice links, and docs links.
- `upload`, `telemetry`, `tracking`, and related hits are expected in privacy wording and checklist text.
- `put` hits are mostly false positives inside words such as `output`.
- Browser prototype files under `src/components` include upload icons/text for local file selection, not network upload.
- No source hit indicates a file upload endpoint, telemetry SDK, analytics SDK, App Center integration, Sentry integration, API key, bearer token, or authorization header.
- Secret-specific scan found no `api_key`, `apikey`, `bearer`, or `authorization` matches outside this audit document.

## Known Limitations

- This audit is static text/source scanning, not a packet capture.
- User opening GitHub or LibreOffice links uses the user's browser.
- Future release should repeat this audit after installer, update, or website changes.
