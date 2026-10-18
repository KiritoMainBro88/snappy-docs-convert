# PDF Toolbox V1

PDF Toolbox V1 adds local utility operations often found in paid PDF apps.

## Operations

- Merge PDFs: multiple PDFs into one PDF.
- Split PDF: one PDF into one PDF per page.
- Extract pages: page range syntax such as `1,3-5,8`.
- Rotate pages: all pages or selected ranges by 90, 180, or 270 degrees.
- Images to PDF: PNG, JPEG, and WebP inputs into one PDF.
- Compress PDF: lossless re-save with content-stream compression and object-stream packing.
  Text stays selectable, quality is unchanged; no lossy image downsampling yet.
- Watermark PDF: diagonal text watermark with adjustable opacity drawn on every page.
- Encrypt PDF: set a user (open) password and optional owner password. The tool never opens a
  password-protected input by guessing; encrypted inputs fail with a clear, password-free message.
- PDF → Word: converts a PDF into `.docx` via local Microsoft Word automation.
  Known issue: on one verified Windows/Word build the startup PDF import hangs (no dialog, no
  window); the watchdog terminates the hung Word process and the batch keeps running. Verify on the
  target machine before relying on this operation; see `docs/OFFICE_COM_ENGINE.md`.
- OCR to Text: PDF or image inputs; pages are rasterised locally and recognised with a local
  PaddleOCR command line (path configurable in settings; not bundled). Multi-file runs report
  per-file failures and keep every successful `.txt`; one bad file never discards the other results.

## Rules

- Local-only.
- No cloud upload.
- No screenshots.
- No signing or redaction in this phase.
- Source files are never overwritten.
- Existing output files are never silently overwritten: on a name collision the output is renamed
  `name__hash8` (batch-planner convention), then numbered variants.
- Output names cannot escape the selected output folder.

## UI

The WPF app has a `PDF Tools` section in the right settings panel. It uses the same output folder as conversion.

## Page Ranges

Examples:

```text
1
1,3-5
1,3-5,8
```

Duplicates are removed and pages are processed in ascending order.
