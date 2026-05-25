# OCR Architecture

Status: design only. No OCR engine is implemented yet.

## Recommended MVP: OCR Lite

The first OCR implementation should be OCR Lite.

Default behavior:

- Engine preference: Auto.
- Auto order: Windows OCR when available, then Tesseract with `tessdata_fast`.
- Languages: Vietnamese + English.
- DPI: 200 default, allow 150 for low-end mode.
- Page concurrency: 1.
- Cancellation: required.
- Progress: page-level.
- Output: plain text first.
- No layout/table/formula/handwriting claims.

## Proposed User Options

- OCR mode:
  - Lite
  - Balanced
  - Advanced
- Performance profile:
  - Low-end
  - Balanced
  - Quality
- Language packs:
  - English
  - Vietnamese
- DPI:
  - 150
  - 200 default
  - warn above 300
- Engine:
  - Auto
  - Windows OCR
  - Tesseract Fast
  - Tesseract Best
  - PaddleOCR later only

## Pipeline Shape

1. Input PDF/image.
2. Render PDF pages to images if needed.
3. Process pages sequentially.
4. Run selected OCR engine.
5. Emit page-level text result.
6. Merge text output.
7. Clean temp files.
8. Report warnings and failures without fake success.

## Interfaces To Add Later

Possible model names for implementation phase:

- `OcrMode`: `Lite`, `Balanced`, `Advanced`
- `OcrEnginePreference`: `Auto`, `WindowsOcr`, `TesseractFast`, `TesseractBest`, `PaddleOcr`
- `OcrPerformanceProfile`: `LowEnd`, `Balanced`, `Quality`
- `OcrRequest`
- `OcrResult`
- `OcrPageResult`
- `IOcrEngine`
- `IOcrPipeline`

Do not add these until code implementation starts unless needed by UI planning.

## Engine Rules

Windows OCR:

- Use when available.
- Good default candidate for low-end machines.
- Must handle unavailable OS/API gracefully.

Tesseract:

- Default to `tessdata_fast`.
- Do not default to `tessdata_best`.
- Do not bundle all languages.
- Keep `vie` and `eng` first.
- Show language-pack missing guidance.

PaddleOCR:

- Optional advanced plugin/model pack only.
- Not base app.
- Not MVP default.
- Requires benchmark before public claims.

## Error Handling

Return clear failures for:

- no OCR engine available
- language pack missing
- invalid input
- unsupported format
- OCR timeout/cancellation
- temp render failure
- page-level OCR failure

Partial success should be explicit. If 9 of 10 pages succeed, result must say which page failed.

## Privacy

OCR must remain local-only:

- no cloud upload
- no remote OCR API
- no telemetry
- no background model download without user action
