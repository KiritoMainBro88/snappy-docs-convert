# OCR Research

Status: research and architecture only. OCR is not implemented in the app yet.

## Goals

- Local-only OCR.
- No cloud OCR.
- No GPU requirement.
- Works acceptably on low-end Windows machines.
- Vietnamese and English first.
- No large model files committed to the repository.
- No public claims for handwriting, formulas, tables, or complex layout until benchmarked.

## OCR Mode Tiers

### OCR Lite

Recommended default for MVP.

- Target: normal users and weak PCs.
- Engine preference: Windows OCR when available, otherwise Tesseract with `tessdata_fast`.
- Languages: Vietnamese + English first.
- DPI: 150 or 200 default.
- Page concurrency: 1.
- Cancellation: required.
- Progress: per page.
- Best for: simple scanned documents, receipts, basic screenshots, readable typed text.

### OCR Balanced

Optional later mode.

- Target: users who can trade speed for better recognition.
- Engine preference: Tesseract with selected language packs.
- DPI: 200 default, warn above 300.
- Page concurrency: still 1 by default on low-end machines.
- Best for: clearer scans where Lite result is not enough.

### OCR Advanced

Future plugin/model-pack path only.

- Target: users who opt into heavier dependencies.
- Candidate: PaddleOCR or other layout-aware engines.
- Packaging: optional download/plugin, not base app.
- Must be benchmarked before public claims.
- Not for MVP default.

## Tesseract Traineddata Notes

Tesseract has multiple traineddata families:

- `tessdata_fast`: fastest, least accurate. Preferred default for low-end machines.
- `tessdata_best`: most accurate, slowest. Do not use by default.
- standard `tessdata`: middle ground when available.

MVP guidance:

- Use `tessdata_fast` first.
- Do not bundle all languages.
- Only ship or download selected language packs.
- Start with `vie` and `eng`.
- Avoid high DPI unless the user explicitly asks.
- No claims for handwriting, formulas, tables, or layout extraction before benchmark.

## Windows OCR Notes

Windows OCR can be the lightest path when available because it uses OS-provided capabilities instead of bundled traineddata.

MVP guidance:

- Prefer Windows OCR in Auto mode when available.
- Fall back to Tesseract Fast when Windows OCR is unavailable or unsupported.
- Keep result expectations modest until real Vietnamese and English samples are benchmarked.

## Heavy OCR Notes

PaddleOCR and similar systems can improve layout/table scenarios but may require large model files and more CPU/RAM.

MVP guidance:

- Do not include PaddleOCR in base app.
- Do not download models automatically.
- Consider it later as an optional advanced pack.
- Benchmark on low-end Windows before exposing it as a public feature.

## Open Questions

- Which Windows OCR API path is cleanest for WPF/.NET 9 packaging?
- Which Tesseract wrapper is smallest and most maintainable for Windows?
- How accurate is `vie+eng` with `tessdata_fast` at 150 DPI and 200 DPI?
- How much RAM is used for 10, 50, and 100 page PDFs?
- What quality warning should the UI show for low-resolution scans?
