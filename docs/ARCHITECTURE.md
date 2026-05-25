# Architecture Notes

These diagrams show the intended final direction. Phase 0/1 only sets up the local workspace.

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
