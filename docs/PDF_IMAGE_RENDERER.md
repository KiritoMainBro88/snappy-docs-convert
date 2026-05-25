# PDF Image Renderer

Phase 4 adds core PDF-to-image rendering.

## Scope

Supported outputs:

- PNG
- JPEG
- WebP when supported by the current runtime

Out of scope:

- WPF UI.
- Installer or packaging.
- Screenshot-based export.
- Parallel rendering of multiple PDFs in one app process.

## Implementation

The renderer uses the `PDFtoImage` NuGet package, built on PDFium and SkiaSharp.

Flow:

1. Validate input exists and has `.pdf` extension.
2. Validate output directory, DPI, JPEG quality, and page range.
3. Create output directory if missing.
4. Get page count.
5. Render pages sequentially.
6. Encode each page to PNG, JPEG, or WebP.
7. Verify each output image exists and is non-empty.
8. Return `PdfRenderResult`.

Default DPI is 200. DPI below 72 or above 600 is rejected. DPI above 300 is allowed but returns a memory/disk warning.

## Output Naming

Default page names:

```text
page-001.png
page-002.png
```

Callers can set `PagePrefix`, for example `slide`, to produce:

```text
slide-001.png
```

Overwrite policies:

- `AutoRename`: create `page-001 (1).png` when target exists.
- `Overwrite`: replace existing output file.
- `Skip`: leave existing file and record a warning.

Future batch/UI code should place each source PDF in its own output folder before calling this renderer.

## Safety

- Local process only.
- No cloud upload.
- No telemetry.
- No screenshots.
- No source PDF mutation.
- No fake success.

PDFium is not thread-safe in this package. The renderer uses a process-wide semaphore and processes one PDF at a time.

## Limitations

- Large PDFs or high DPI values can use significant memory and disk space.
- WebP support depends on the runtime/native SkiaSharp encoder.
- Encrypted/password-protected PDFs are not supported in the MVP.
