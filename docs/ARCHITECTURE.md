# Architecture Notes

These diagrams show the intended final direction. Phase 2 now adds the first .NET core conversion engine for LibreOffice headless document-to-PDF conversion.

## Phase 2 LibreOffice Engine

```mermaid
flowchart LR
  Request["ConversionRequest"] --> Validate["Validate input and output"]
  Validate --> Locate["Locate soffice.com or soffice.exe"]
  Locate --> Profile["Create isolated temp profile"]
  Profile --> Process["Run LibreOffice headless"]
  Process --> Verify["Verify output PDF"]
  Verify --> Cleanup["Best-effort temp cleanup"]
  Cleanup --> Result["ConversionResult"]
```

Phase 2 code lives in `src/SnappyDocsConvert.Core`. Tests live in `tests/SnappyDocsConvert.Tests` and use fake process runners so LibreOffice is not required for the normal suite.

## Final Desktop Architecture

```mermaid
flowchart LR
  User["User"] --> UI["WPF Desktop UI"]
  UI --> Queue["Batch Queue"]
  Queue --> Selector["Conversion Engine Selector"]
  Selector --> Office["Office COM Engine"]
  Selector --> Libre["LibreOffice Headless Engine"]
  Selector --> Pdf["PDF Image Renderer"]
  Office --> Workspace["Local Temp Workspace"]
  Libre --> Workspace
  Pdf --> Workspace
  Workspace --> Output["Local Output Folder"]
  Output --> UI
```

## Engine Selection Flow

```mermaid
flowchart TD
  Start["Input file"] --> Type{"File type"}
  Type -->|"DOCX/PPTX"| OfficeCheck{"Microsoft Office installed?"}
  OfficeCheck -->|"Yes"| Office["Use Office COM"]
  OfficeCheck -->|"No"| LibreCheck{"LibreOffice available?"}
  LibreCheck -->|"Yes"| Libre["Use LibreOffice headless"]
  LibreCheck -->|"No"| Missing["Show missing engine message"]
  Type -->|"PDF"| Pdf["Use PDF renderer"]
  Type -->|"Image"| Image["Copy or normalize image output"]
  Office --> Done["Write local output"]
  Libre --> Done
  Pdf --> Done
  Image --> Done
```

## Output Folder Structure

```mermaid
flowchart TD
  Root["output/"] --> Job["YYYYMMDD-HHMMSS-job-name/"]
  Job --> Pdfs["pdf/"]
  Job --> Images["images/"]
  Job --> Logs["logs/"]
  Images --> ByFile["source-file-name/"]
  ByFile --> Page001["page-001.png"]
  ByFile --> Page002["page-002.png"]
  Logs --> Summary["conversion-summary.json"]
```

## Future Phase Map

```mermaid
flowchart LR
  P01["Phase 0/1: Local setup"] --> P02["Phase 2: LibreOffice engine"]
  P02 --> P03["Phase 3: Office COM engine"]
  P03 --> P04["Phase 4: PDF image renderer"]
  P04 --> P05["Phase 5: WPF UI polish"]
  P05 --> P06["Phase 6: Packaging and release"]
```
