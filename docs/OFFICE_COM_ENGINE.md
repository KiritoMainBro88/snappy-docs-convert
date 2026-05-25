# Microsoft Office COM Engine

Phase 3A adds local Microsoft Office COM detection and guarded PDF export.

## Scope

Supported inputs:

- Word: `.doc`, `.docx`, `.rtf`
- PowerPoint: `.ppt`, `.pptx`

Out of scope:

- WPF UI.
- PDF page image rendering.
- LibreOffice smoke validation.
- Running as a Windows service or server-side converter.

## Local Desktop Only

Microsoft Office automation is intended here only for the logged-in desktop user session. Do not run this engine as a Windows service, background server, or unattended automation job.

Office must be installed, activated, and usable by the logged-in user.

## Detection

Availability checks are lightweight:

- `Word.Application` ProgID
- `PowerPoint.Application` ProgID

Detection does not launch Office.

## Conversion Flow

1. Validate input exists.
2. Map extension to Word or PowerPoint.
3. Check required Office COM app is available.
4. Create output directory.
5. Serialize through a process-wide semaphore.
6. Run COM automation on a dedicated STA thread.
7. Open source read-only.
8. Disable alerts/macros where supported.
9. Export PDF with fixed-format APIs:
   - Word `ExportAsFixedFormat(..., 17)`
   - PowerPoint `ExportAsFixedFormat(..., 2)`
10. Close document/presentation.
11. Quit only the app instance created by the engine.
12. Release COM objects.
13. Verify output PDF exists and is non-empty.

Default timeout is 180 seconds.

## Safety

- No cloud upload.
- No telemetry.
- No fake success.
- No parallel Office COM conversions.
- No deletion of source files.
- No killing unrelated user Office processes.

## Fallback

If Office is unavailable, the app should guide users to install LibreOffice or select `soffice.com`. LibreOffice remains the fallback for `.odt`, `.odp`, and Office documents when Office is unavailable.

## Known Limitations

- Office dialogs or protected-view prompts may block or fail conversion.
- Password-protected files are unsupported in MVP.
- Corrupt documents may fail.
- Office must be activated and usable by the current Windows user.
- Do not run as Windows service.
