# LibreOffice Headless Engine

Phase 2 adds the first local conversion engine. It converts supported Office-style documents to PDF by launching LibreOffice locally in headless mode.

## Scope

Supported inputs:

- `.doc`
- `.docx`
- `.rtf`
- `.odt`
- `.ppt`
- `.pptx`
- `.odp`

Out of scope:

- Microsoft Office COM export.
- PDF page image rendering.
- Image conversion.
- UI workflows.

PDF input is treated as unsupported/no-op for this engine because it is already PDF.

## How It Works

1. Validate input file exists and extension is supported.
2. Locate LibreOffice in this order:
   - explicit `LibreOfficeOptions.ExecutablePath`
   - `soffice.com` then `soffice.exe` on `PATH`
   - standard Windows install paths under `C:\Program Files`
3. Create output directory if missing.
4. Create a unique temp LibreOffice profile under system temp.
5. Run:

```powershell
soffice.com --headless --nologo --nodefault --nofirststartwizard --norestore -env:UserInstallation=file:///<temp-profile-uri> --convert-to pdf --outdir <output-dir> <input-file>
```

6. Capture stdout/stderr.
7. Verify PDF exists.
8. Best-effort delete temp profile.
9. Return structured `ConversionResult`.

## Manual Install

Install LibreOffice from the official LibreOffice installer. Snappy Docs Convert does not bundle LibreOffice in the MVP and no repo command installs it automatically.

Official download:

https://www.libreoffice.org/download/download-libreoffice/

Verify:

```powershell
soffice --version
```

If `soffice` is not on `PATH`, provide the full executable path in `LibreOfficeOptions.ExecutablePath`.

Accepted manual selections:

- `C:\Program Files\LibreOffice`
- `C:\Program Files\LibreOffice\program`
- `C:\Program Files\LibreOffice\program\soffice.com`
- `C:\Program Files\LibreOffice\program\soffice.exe`

LibreOffice command-line help:

https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html

## Privacy

Conversion is local process execution only. No cloud upload, telemetry, analytics, tracking, or remote converter service is used by the engine.

## Limitations

- Conversion fidelity depends on LibreOffice.
- Password-protected, corrupt, or unsupported documents can fail.
- Existing output PDFs are not overwritten unless `AllowOverwrite` is true.
- Default timeout is 120 seconds and can be overridden per request.

## Troubleshooting

Missing `soffice`:
- Install LibreOffice.
- Verify `soffice --version`.
- Provide explicit `LibreOfficeOptions.ExecutablePath`.
- If selecting manually, choose `soffice.com`, `soffice.exe`, the `program` folder, or the LibreOffice root folder.

Timeout:
- Increase request timeout.
- Check for huge, corrupt, or locked input files.

Locked/corrupt LibreOffice profile:
- Engine uses an isolated temp profile per conversion.
- If cleanup warning appears, manually clear old temp folders under system temp when LibreOffice is not running.

Unsupported file:
- Check extension is in the supported input list.
- PDF and images are handled by later phases, not this engine.

Output not produced:
- Check `StdoutSnippet` and `StderrSnippet`.
- Confirm output directory is writable.
- Try the same file in LibreOffice manually to verify document health.
