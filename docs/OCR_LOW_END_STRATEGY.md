# OCR Low-End Strategy

Status: strategy only. OCR is planned, not shipped.

## Default

Default OCR mode should be **OCR Lite**.

Low-end defaults:

- DPI: 150 or 200.
- Page concurrency: 1.
- Language packs: English + Vietnamese only.
- Engine order: Windows OCR, then Tesseract Fast.
- Cancel button: required.
- Progress: page by page.
- Temp files: cleaned after run.

## Why Not Heavy OCR First

Low-end Windows users may have:

- weak CPU
- low RAM
- slow disk
- no GPU
- large scanned PDFs

Heavy OCR engines and large model packs can make the app feel broken even when technically working. The first OCR feature should prioritize predictable speed and low memory use.

## Tesseract Settings

Use `tessdata_fast` by default.

Do not default to:

- `tessdata_best`
- all languages
- high DPI
- multi-page parallel OCR

Warn when:

- DPI is above 300
- selected language pack is missing
- user selects quality mode on low-end profile
- document has many pages

## Packaging

Base installer should stay lightweight.

Do not commit:

- `.traineddata` files
- PaddleOCR models
- large OCR samples

Future installer options:

- install no OCR packs by default
- offer optional OCR Lite language pack
- offer `eng` and `vie` first
- allow user-selected language pack download later

## UI Guidance

Show plain wording:

- "OCR is slower than normal conversion."
- "Low-end mode uses lower DPI and one page at a time."
- "Better accuracy can be slower."
- "Handwriting, formulas, tables, and complex layouts are not guaranteed."

## Benchmark Plan

Before enabling public OCR claims:

1. Test English printed text at 150 and 200 DPI.
2. Test Vietnamese printed text at 150 and 200 DPI.
3. Test a 10-page scanned PDF.
4. Record time and peak memory on a low-end Windows machine.
5. Compare Windows OCR vs Tesseract Fast.
6. Only then decide whether Balanced mode is worth exposing.

## Future Advanced Mode

PaddleOCR may be useful later for layout/table-heavy documents, but only as opt-in advanced mode.

Rules:

- optional plugin/model pack
- no automatic model download
- benchmark first
- no GPU requirement for advertised baseline
- no table/formula/handwriting claims before evidence
