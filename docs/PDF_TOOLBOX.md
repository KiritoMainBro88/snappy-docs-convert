# PDF Toolbox V1

PDF Toolbox V1 adds local utility operations often found in paid PDF apps.

## Operations

- Merge PDFs: multiple PDFs into one PDF.
- Split PDF: one PDF into one PDF per page.
- Extract pages: page range syntax such as `1,3-5,8`.
- Rotate pages: all pages or selected ranges by 90, 180, or 270 degrees.
- Images to PDF: PNG, JPEG, and WebP inputs into one PDF.

## Rules

- Local-only.
- No cloud upload.
- No screenshots.
- No OCR, compression, encryption, signing, redaction, or form filling in this phase.
- Source files are never overwritten.
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
