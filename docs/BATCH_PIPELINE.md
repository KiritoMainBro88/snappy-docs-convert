# Batch Pipeline

Phase 5A adds core batch orchestration. UI will call this pipeline later.

## Inputs

Supported:

- `.pdf`
- `.doc`
- `.docx`
- `.rtf`
- `.odt`
- `.ppt`
- `.pptx`
- `.odp`

## Targets

- `Pdf`: produce or copy PDF output.
- `Images`: produce images only.
- `PdfAndImages`: produce PDF output and images.

PDF input does not use Office or LibreOffice. Office/OpenDocument input converts to PDF first, then renders images when requested.

## Output Structure

```text
<outputRoot>\
  pdf\
    <safeBaseName>__<hash8>.pdf
  images\
    <safeBaseName>__<hash8>\
      page-001.png
      page-002.png
```

Presentation inputs use `slide` prefix:

```text
slide-001.png
slide-002.png
```

Document and PDF inputs use `page` prefix.

The hash is based on the normalized full input path so duplicate file names from different folders do not collide.

## Engine Selection

Preference options:

- `Auto`
- `MicrosoftOffice`
- `LibreOffice`

Auto mode:

- `.doc`, `.docx`, `.rtf`: prefer Microsoft Word, fallback LibreOffice.
- `.ppt`, `.pptx`: prefer Microsoft PowerPoint, fallback LibreOffice.
- `.odt`, `.odp`: prefer LibreOffice.
- `.pdf`: no document conversion engine.

Forced engine modes fail clearly if the selected engine is unavailable or unsupported for the file.

## Failure And Cancellation

One failed file does not stop the batch. Results include per-item status and summary counts.

Cancellation keeps completed items as succeeded, marks current/remaining items cancelled where possible, and does not report fake success.

## Intermediate PDFs

For `Images` target on Office/OpenDocument input:

- `KeepIntermediatePdf = false`: convert to temp PDF, render images, delete temp PDF.
- `KeepIntermediatePdf = true`: keep planned PDF output under `pdf\`.

For `PdfAndImages`, images render from the final planned PDF.

## Safety

- Local-only.
- No cloud upload.
- No telemetry.
- No screenshots.
- No source file overwrite.
- No concurrent Office COM or PDFium rendering in Phase 5A.
