# Microsoft Office COM Engine

Phase 3A adds local Microsoft Office COM detection and guarded PDF export. Phase 14 hardening adds
per-app sessions (one Application reused across a batch), a watchdog that terminates only
ownership-verified hung Office processes, and an AutomationSecurity save/restore envelope.

## Scope

Supported inputs:

- Word: `.doc`, `.docx`, `.rtf` → PDF
- PowerPoint: `.ppt`, `.pptx` → PDF
- Excel: `.xlsx`, `.xls`, `.csv`, `.ods` → PDF (new in Phase 14)
- Word PDF import: `.pdf` → `.docx` (known-hang on one verified Word build; see Known Issues)

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

## Real Smoke

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-office.ps1
```

The smoke script:

- Prints Word and PowerPoint COM availability.
- Uses only temp files under the system temp directory.
- Creates a tiny RTF sample and converts it through `OfficeComConversionEngine` when Word is available.
- Cleans generated input/output unless `KEEP_SNAPPY_SMOKE_OUTPUT=1`.
- Skips cleanly when Office COM apps are missing.

Office must be installed, activated, and usable by the logged-in user for real smoke conversion. PowerPoint availability is reported; a real PowerPoint sample fixture is deferred until a safe lightweight sample is added.

## Safety

- No cloud upload.
- No telemetry.
- No fake success.
- No parallel Office COM conversions.
- No deletion of source files.
- No killing unrelated user Office processes.

## Phase 14 Hardening

All hardening behaviors below are verified end-to-end on a real Office machine (see
`docs/qa-output/current/logs/**` for unit/QA evidence and the Phase 14 report for the harness run).

### One session per batch

Batch conversion uses `OfficeComSession` with `ComOfficeAppFactory`: one `Application` per app kind
is reused across every file, so Word/Excel/PowerPoint start once, not per file. Reuse counts are
tracked; dispose is bounded (5 s quit grace) and cannot hang app shutdown.

### Ownership proof (never kill by name)

The watchdog may terminate a hung Office process only when exclusive ownership is PROVEN by two
independent signals that converge:

1. Spawn diff: exactly one new `WINWORD`/`EXCEL`/`POWERPNT` process appeared after activation.
2. Window proof: a window of OUR Application object resolves to that same pid
   (Word.Window.Hwnd, Excel Application.Hwnd, PowerPoint `PPTFrameClass` frame — modern PowerPoint
   removed the late-bound `Window.HWND`).

Anything ambiguous — attach, multiple candidates, unreadable start time (PID-reuse guard via
`OfficeProcessIdentity` + `IProcessVerifier`), or a stale identity — is fail-closed: no kill AND no
Quit. An unproven instance may be the user's and may hold unsaved work.

### PowerPoint attach safety

PowerPoint activation can ATTACH to a POWERPNT.EXE the user already has open. Verified behaviors:

- Attach case: the user's instance is never quit, its presentations stay open, `AutomationSecurity`
  is restored around every file, and per-file conversion still succeeds through the shared instance.
- Spawn case: exclusive ownership is proven to the watchdog, so the session quits the instance and
  disposes cleanly after the batch.

### AutomationSecurity envelope

Every file open runs inside `OfficeAutomationSecurity.RunWithForceDisabled`:

- Before the open, ForceDisable (`msoAutomationSecurityForceDisable = 3`) is engaged so macros in
  converted documents cannot execute.
- The previous value is restored afterwards, even when the open/export fails.
- Documented engine limitation (Microsoft): Excel 4.0 (XLM) macros are an edge case that
  `AutomationSecurity` may not fully block; the app cannot change that behavior.

## Fallback

If Office is unavailable, the app should guide users to install LibreOffice or select `soffice.com`. LibreOffice remains the fallback for `.odt`, `.odp`, and Office documents when Office is unavailable.

## Known Issues

- Word COM PDF import (`.pdf` → editable document) hangs deterministically on one verified
  Windows/Word build with every Open parameter shape and without any modal window. The watchdog
  terminates the hung process and the next file starts a fresh instance; the app never hangs, but
  the conversion itself cannot complete on that build until Word is repaired/updated. Verify on the
  target machine before relying on PDF → Word.
- The "fail fast with session disabled" path (poison after UNSAFE recovery) is covered by unit
  tests; staging it deterministically end-to-end would require hanging a possibly user-held
  Office instance, which the project refuses to do.

## Known Limitations

- Office dialogs or protected-view prompts may block or fail conversion.
- Password-protected files are unsupported in MVP.
- Corrupt documents may fail.
- Office must be activated and usable by the current Windows user.
- Do not run as Windows service.
