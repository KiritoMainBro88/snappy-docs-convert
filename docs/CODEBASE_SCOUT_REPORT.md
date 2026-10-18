# Codebase Scout Report: snappy-docs-convert

**Date:** May 31, 2026  
**Project:** kmb file tools (SnappyDocsConvert)  
**Phase:** Phase 13B beta.3 hotfix  
**Status:** Production-ready local document conversion desktop app

---

## Executive Summary

**kmb file tools** is a mature, well-architected Windows desktop application for local document conversion. Built with C# .NET 9 and WPF, it converts DOCX, PPTX, PDF, and OpenDocument formats to PDF and/or images without cloud upload, telemetry, or remote services.

**Key Strengths:**
- ✅ Modular, testable architecture with dependency injection
- ✅ Multiple conversion engines (Office COM, LibreOffice, PDF rendering)
- ✅ Responsive WPF UI with batch processing and localization (EN/VI)
- ✅ Comprehensive test coverage with xUnit and E2E smoke tests
- ✅ Robust CI/CD via GitHub Actions with release automation
- ✅ Privacy-first design with local-only processing
- ✅ Professional packaging (portable ZIP + Inno Setup installer)
- ✅ Frontend-only marketing website (Vite + React)

**Current Maturity:** Beta 3 with all core features implemented. Ready for public release pending manual GUI QA and code signing.

---

## 1. Project Identity & Purpose

### What It Does
Converts office documents and PDFs locally on Windows:
- **Input Formats:** DOCX, PPTX, RTF, DOC, ODT, ODP, PDF
- **Output Targets:** PDF only, Images only (PNG/JPEG/WebP), or both
- **Engines:** Microsoft Office COM (high-fidelity), LibreOffice headless (fallback), PDF renderer
- **Batch Processing:** Queue multiple files, convert with progress tracking
- **PDF Toolbox:** Merge, split, extract pages, rotate, images-to-PDF

### Design Philosophy
- **Local-only:** No cloud upload, no remote services
- **Privacy-first:** No telemetry, analytics, or tracking
- **Desktop-first:** Windows WPF app with responsive UI
- **Reliable:** Async/await, cancellation support, error handling
- **Testable:** Dependency injection, fake implementations, no external dependencies for unit tests

### Target Users
Windows users who need fast, private document conversion without cloud services or subscriptions.

---

## 2. Project Structure

```
snappy-docs-convert/
├── src/
│   ├── SnappyDocsConvert.Core/          # Core conversion logic (library)
│   │   ├── Models/                      # Data models (batch jobs, results, requests)
│   │   ├── Services/
│   │   │   ├── Batch/                   # BatchConversionPipeline orchestration
│   │   │   ├── LibreOffice/             # LibreOffice headless engine
│   │   │   ├── Office/                  # Office COM engine (Word, PowerPoint)
│   │   │   ├── Pdf/                     # PDF rendering and toolbox
│   │   │   ├── Setup/                   # Engine setup guidance and detection
│   │   │   └── Process/                 # Process runner with timeout/cancellation
│   │   └── SnappyDocsConvert.Core.csproj
│   │
│   ├── SnappyDocsConvert.App/           # WPF desktop application (WinExe)
│   │   ├── Services/                    # App-level services
│   │   │   ├── AppSettingsService       # Persisted user preferences
│   │   │   ├── ThemeService             # System/Light/Dark theme
│   │   │   ├── LocalizationService      # EN/VI language support
│   │   │   ├── AppUpdateService         # Manual update center (GitHub Releases)
│   │   │   ├── SelfCheckRunner          # Startup validation
│   │   │   └── AppStartupLogger         # Crash diagnostics
│   │   ├── ViewModels/                  # MVVM view models
│   │   │   └── MainWindowViewModel      # Main UI orchestration
│   │   ├── Localization/                # EN/VI string resources
│   │   ├── Styles/                      # XAML styling and themes
│   │   ├── MainWindow.xaml              # Primary UI container
│   │   └── SnappyDocsConvert.App.csproj
│   │
│   └── SnappyDocsConvert.QaHarness/     # E2E QA test harness
│       └── SnappyDocsConvert.QaHarness.csproj
│
├── tests/
│   └── SnappyDocsConvert.Tests/         # xUnit test suite
│       ├── LibreOfficeEngineTests.cs
│       ├── OfficeComEngineTests.cs
│       ├── PdfImageRendererTests.cs
│       ├── BatchConversionPipelineTests.cs
│       ├── PdfToolServiceTests.cs
│       ├── EngineSetupAdvisorTests.cs
│       ├── AppSettingsServiceTests.cs
│       ├── AppUpdateServiceTests.cs
│       └── SnappyDocsConvert.Tests.csproj
│
├── website/                              # Vite + React + TypeScript marketing site
│   ├── src/
│   │   ├── components/                  # React components
│   │   ├── pages/                       # Page components
│   │   ├── App.tsx                      # Main app
│   │   └── main.tsx                     # Entry point
│   ├── package.json                     # npm dependencies
│   └── vite.config.ts                   # Vite configuration
│
├── scripts/                              # PowerShell build/packaging/smoke scripts
│   ├── check-local.ps1                  # Local setup verification
│   ├── build.ps1                        # Build script
│   ├── test.ps1                         # Test runner
│   ├── package-portable.ps1             # Portable ZIP packaging
│   ├── package-installer.ps1            # Inno Setup installer
│   ├── smoke-app-build.ps1              # App build smoke test
│   ├── smoke-gui-launch.ps1             # GUI launch verification
│   ├── smoke-office.ps1                 # Real Office COM smoke test
│   ├── smoke-release.ps1                # Release artifact smoke test
│   ├── qa-e2e.ps1                       # Full E2E QA gate
│   └── artifact-hash-helper.ps1         # SHA256 checksum generation
│
├── docs/                                 # Architecture and design documentation
│   ├── ARCHITECTURE.md                  # System design and phase diagrams
│   ├── PHASE_PLAN.md                    # Detailed phase implementation status
│   ├── ENGINE_SETUP_GUIDE.md            # User guidance for engine setup
│   ├── LIBREOFFICE_ENGINE.md            # LibreOffice engine details
│   ├── OFFICE_COM_ENGINE.md             # Office COM engine details
│   ├── PDF_RENDERER.md                  # PDF rendering details
│   ├── TRUST_AND_SECURITY.md            # Security, privacy, and trust docs
│   ├── CONTRIBUTING.md                  # Contribution guidelines
│   ├── CODEBASE_SCOUT_REPORT.md         # This file
│   └── ...
│
├── artifacts/                            # Build outputs and release artifacts
│   ├── checksums/                       # SHA256 checksums
│   ├── demo/                            # Demo assets and inputs
│   ├── gui-smoke/                       # GUI smoke test screenshots
│   └── kmb-file-tools-portable-win-x64-v0.1.0-beta.2/  # Portable release
│
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                       # CI on push/PR
│   │   └── release-build.yml            # Release build on tag
│   └── ISSUE_TEMPLATE/                  # Issue templates
│
├── .kiro/                                # Kiro agent configuration
│   ├── skills/                          # Custom agent skills
│   └── prompts/                         # Phase-based prompts
│
├── .agents/                              # Agent skills (caveman, cavecrew, etc.)
├── .lovable/                             # Lovable project config
├── SnappyDocsConvert.sln                # Visual Studio solution
├── README.md                             # Project README
├── AGENTS.md                             # Agent workflow rules
├── LICENSE                               # MIT License
└── .gitignore                            # Git ignore rules
```

---

## 3. Core Components & Services

### 3.1 Batch Conversion Pipeline

**File:** `src/SnappyDocsConvert.Core/Services/Batch/BatchConversionPipeline.cs`

**Purpose:** Orchestrates batch conversion of multiple files with engine selection, PDF rendering, and output planning.

**Key Features:**
- Processes items sequentially
- Continues after failures (partial success)
- Supports cancellation (marks remaining items cancelled)
- Reports per-item progress
- Handles three output targets: PDF, Images, PdfAndImages

**Supported Extensions:**
```csharp
.pdf, .doc, .docx, .rtf, .odt, .ppt, .pptx, .odp
```

**Flow:**
1. Validate input/output paths
2. Select conversion engine (Office COM > LibreOffice)
3. Convert to PDF (if needed)
4. Render PDF to images (if needed)
5. Plan output paths with collision avoidance
6. Report results

---

### 3.2 LibreOffice Headless Engine

**File:** `src/SnappyDocsConvert.Core/Services/LibreOffice/LibreOfficeConversionEngine.cs`

**Purpose:** Detects LibreOffice installation and runs headless conversion with isolated temp profiles.

**Key Features:**
- Discovers `soffice.com` or `soffice.exe` on Windows
- Creates isolated temp profile directory per conversion
- Runs headless with timeout and cancellation support
- Verifies output PDF exists and is non-empty
- Best-effort temp cleanup
- Serialized execution (no concurrent instances)

**Supported Formats:**
- Office: DOCX, DOC, RTF
- OpenDocument: ODT, ODP
- Spreadsheets: XLSX, XLS, ODS
- Presentations: PPTX, PPT, ODP

**Command Example:**
```
soffice.com --headless --convert-to pdf --outdir <output> <input>
```

---

### 3.3 Microsoft Office COM Engine

**File:** `src/SnappyDocsConvert.Core/Services/Office/OfficeComConversionEngine.cs`

**Purpose:** Detects Word/PowerPoint COM ProgIDs and exports to PDF via COM on STA thread.

**Key Features:**
- Checks Word/PowerPoint COM ProgIDs without launching Office
- Runs on dedicated STA thread (required for COM)
- Serialized with SemaphoreSlim(1,1) (only one concurrent export)
- Opens file read-only
- Exports as FixedFormat PDF
- Proper COM cleanup (Close, Quit, Release)
- Verifies output PDF is non-empty

**Supported Formats:**
- Word: `.doc`, `.docx`, `.rtf`
- PowerPoint: `.ppt`, `.pptx`

**Scope:** Local desktop user-session automation only (not for server/service/unattended use)

---

### 3.4 PDF Image Renderer

**File:** `src/SnappyDocsConvert.Core/Services/PdfTools/PdfToImageRenderer.cs`

**Purpose:** Renders PDF pages to real image files using PDFtoImage/PDFium/SkiaSharp.

**Key Features:**
- Supports PNG, JPEG, WebP (when runtime supports)
- Configurable DPI (default 200, validation, high-DPI warning)
- Safe page naming: `page-001.png` for documents, `slide-001.png` for presentations
- Serialized PDFium calls (not thread-safe in-process)
- Per-page error handling and verification
- Overwrite policies: Overwrite, Skip, Rename

**Dependencies:**
- PDFtoImage 5.2.1 (wraps PDFium + SkiaSharp)

---

### 3.5 PDF Toolbox Service

**File:** `src/SnappyDocsConvert.Core/Services/PdfTools/PdfToolService.cs`

**Purpose:** Provides PDF manipulation operations: merge, split, extract, rotate, images-to-PDF.

**Operations:**
1. **Merge PDFs** - Combine multiple PDFs into one
2. **Split PDF** - Extract individual pages as separate PDFs
3. **Extract Pages** - Extract page ranges (e.g., `1,3-5,8`)
4. **Rotate Pages** - Rotate selected pages by 90°/180°/270°
5. **Images to PDF** - Convert PNG/JPEG/WebP images to single PDF

**Dependencies:**
- PDFsharp 6.2.4 (PDF manipulation)

---

### 3.6 Engine Setup Advisor

**File:** `src/SnappyDocsConvert.Core/Services/Setup/EngineSetupAdvisor.cs`

**Purpose:** Provides setup guidance based on Office/LibreOffice availability.

**Responsibilities:**
- Checks Microsoft Office availability (Word/PowerPoint COM ProgIDs)
- Locates LibreOffice installation
- Recommends preferred engine
- Provides download/setup actions
- Generates setup status for UI display

**Output:** `EngineSetupStatus` with:
- `CanConvertOfficeDocuments` - Boolean availability
- `PreferredEngine` - Recommended engine (Office > LibreOffice)
- `Actions` - Download, choose soffice, recheck actions

---

### 3.7 WPF Desktop Application

**File:** `src/SnappyDocsConvert.App/MainWindow.xaml`

**Purpose:** Primary user interface for batch conversion and PDF toolbox.

**Architecture:** MVVM with `MainWindowViewModel` orchestrating UI state.

**UI Pages (Mode-Based Navigation):**
1. **Convert** - Queue files, set output folder, target, engine, image format, DPI
2. **PDF Tools** - Merge, split, extract, rotate, images-to-PDF
3. **Engines** - Setup guidance for Office/LibreOffice
4. **Logs** - Batch conversion logs and output paths
5. **Help** - Links to docs, Discord, GitHub

**Key Services:**
- `AppSettingsService` - Persists user preferences (theme, language, engine paths)
- `ThemeService` - System/Light/Dark theme switching
- `LocalizationService` - EN/VI language support with runtime toggle
- `AppUpdateService` - Manual update center (GitHub Releases)
- `SelfCheckRunner` - Validates app startup and engine availability
- `AppStartupLogger` - Crash diagnostics under `%LOCALAPPDATA%\kmb-file-tools\logs\`

**Async Execution:**
- `UiBatchRunner` runs batch conversion on background task
- UI thread stays responsive
- Progress reporting via `IProgress<BatchConversionProgress>`

---

## 4. Technology Stack

### Backend/Core
| Layer | Technology |
|-------|-----------|
| **Language** | C# 12 with nullable reference types |
| **Framework** | .NET 9.0 |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **Process Management** | System.Diagnostics.Process with timeout/cancellation |
| **Async/Await** | Full async/await support with CancellationToken |
| **Dependency Injection** | Manual DI pattern (constructor injection) |
| **Testing** | xUnit 2.9.2 with fake process runners |

### NuGet Dependencies (Core)
- **PDFsharp 6.2.4** - PDF manipulation and creation
- **PDFtoImage 5.2.1** - PDF page rendering to images (uses PDFium + SkiaSharp)

### Frontend (Website)
| Layer | Technology |
|-------|-----------|
| **Framework** | Vite + React 19 + TypeScript |
| **Routing** | TanStack Router 1.168.25 |
| **Styling** | Tailwind CSS 4.2.1 + custom CSS |
| **Components** | Radix UI primitives |
| **State** | React hooks + TanStack Query 5.83.0 |
| **PDF Handling** | pdf-lib 1.17.1, pdfjs-dist 5.7.284 |
| **Deployment** | Vercel (frontend-only, no backend) |

### Build & CI/CD
| Tool | Purpose |
|------|---------|
| **dotnet CLI** | Build, test, publish .NET projects |
| **GitHub Actions** | CI on push/PR, release build on tag |
| **PowerShell** | Build scripts, packaging, smoke tests |
| **Inno Setup** | Windows installer creation |
| **npm/Vite** | Website build and dev server |

---

## 5. Testing Strategy

### Test Framework
- **Framework:** xUnit 2.9.2
- **Location:** `tests/SnappyDocsConvert.Tests/`
- **Coverage:** Code coverage via coverlet

### Test Categories

| Test File | Coverage |
|-----------|----------|
| `LibreOfficeEngineTests.cs` | Discovery, command building, timeout, cancellation |
| `OfficeComEngineTests.cs` | COM availability, Word/PowerPoint detection, export |
| `PdfImageRendererTests.cs` | PDF rendering, DPI validation, page naming, overwrite |
| `BatchConversionPipelineTests.cs` | Batch orchestration, engine selection, partial failure |
| `PdfToolServiceTests.cs` | PDF merge, split, extract, rotate operations |
| `EngineSetupAdvisorTests.cs` | Setup guidance based on availability |
| `AppSettingsServiceTests.cs` | Settings persistence and defaults |
| `AppUpdateServiceTests.cs` | GitHub Release checking and version comparison |

### Test Strategy
- **Fake Process Runners:** Tests use mock process execution; no LibreOffice/Office install required
- **No External Dependencies:** Unit tests run in isolation
- **E2E Harness:** `SnappyDocsConvert.QaHarness` for real engine verification (optional)

### Running Tests
```powershell
dotnet test --configuration Release
```

---

## 6. CI/CD Pipeline

### GitHub Actions Workflows

#### CI Workflow (`.github/workflows/ci.yml`)
**Triggers:** Push, Pull Request  
**Runs on:** Windows-latest

**Steps:**
1. Checkout code
2. Setup .NET 9.0
3. Setup Node 22 (for website)
4. Restore NuGet packages
5. Build (Release config)
6. Run xUnit tests
7. Install website dependencies
8. Build website (Vite)

#### Release Build Workflow (`.github/workflows/release-build.yml`)
**Triggers:** Manual dispatch (with version input) or tag push (v*)  
**Runs on:** Windows-latest

**Steps:**
1. Resolve version from input or tag
2. Setup .NET 9.0, Node 22, Inno Setup
3. Build and test
4. Check website
5. Package portable ZIP
6. Package installer EXE
7. Smoke test portable and installer
8. Generate SHA256 checksums
9. Upload artifacts
10. Create GitHub attestations for artifacts

### Build Outputs
- **Portable:** `kmb-file-tools-portable-win-x64-<version>.zip`
- **Installer:** `kmb-file-tools-setup-win-x64-<version>.exe`
- **Checksums:** `SHA256SUMS.txt`, `SHA256SUMS.json`

---

## 7. Architecture Patterns

### 1. Layered Architecture
- **Core Library:** Conversion logic, engine abstraction, batch orchestration
- **App Layer:** WPF UI, settings, localization, update management
- **Test Layer:** xUnit with dependency injection for testability

### 2. Dependency Injection Pattern
- Constructor-based DI for testability
- Interfaces for all major services
- Fake implementations in tests (e.g., FakeProcessRunner)

### 3. Async/Await Throughout
- All I/O operations async (file, process, network)
- CancellationToken support for graceful cancellation
- UI thread kept responsive via background task execution

### 4. Serialization for Shared Resources
- **Office COM:** SemaphoreSlim(1,1) ensures single concurrent export
- **PDFium:** Serialized page rendering (not thread-safe in-process)
- **LibreOffice:** Sequential processing (no concurrent headless instances)

### 5. Batch Pipeline Pattern
- `BatchConversionPipeline` orchestrates multiple files
- Continues after failures (partial success)
- Per-item status tracking and result aggregation
- Cancellation marks remaining items as cancelled

### 6. Engine Selection Strategy
- `ConversionEngineSelector` auto-selects based on availability
- Preference options: Auto, MicrosoftOffice, LibreOffice
- Forced engine modes fail clearly if unavailable

### 7. Output Planning
- `BatchOutputPlanner` pre-plans output paths with hash-based collision avoidance
- Safe naming: `<safeBaseName>__<hash8>.pdf` and `images/<safeBaseName>__<hash8>/page-001.png`
- Overwrite policies: Overwrite, Skip, Rename

### 8. Temp Directory Management
- Isolated temp profiles for LibreOffice (per-conversion)
- Temp batch workspace under `%TEMP%\SnappyDocsConvert\Batch\`
- Best-effort cleanup with exception handling

### 9. Local-Only Privacy
- No cloud upload, telemetry, analytics, or tracking
- All processing on user's machine
- Settings stored locally under `%LOCALAPPDATA%\kmb-file-tools\`
- Crash logs under `%LOCALAPPDATA%\kmb-file-tools\logs\`

### 10. MVVM Pattern (WPF)
- `MainWindowViewModel` orchestrates UI state
- `ObservableObject` base class for property change notifications
- `RelayCommand` and `AsyncRelayCommand` for user actions
- Async batch runner keeps UI responsive

### 11. Localization Strategy
- `LocalizationService` with EN/VI support
- Runtime language toggle (no app restart required)
- Persisted language preference
- System language auto-detection on first run

### 12. Theme Management
- System/Light/Dark theme support
- Persisted theme preference
- WPF theme brush resources

### 13. Update Center (Manual)
- Checks GitHub Releases API
- Supports Stable or Prerelease/Beta channel selection
- Downloads with progress reporting
- Verifies SHA256 digest when available
- Launches installer or opens ZIP folder (user-initiated)

---

## 8. Key Files & Responsibilities

### Core Services
| File | Responsibility |
|------|-----------------|
| `BatchConversionPipeline.cs` | Orchestrate batch conversion |
| `LibreOfficeConversionEngine.cs` | LibreOffice headless conversion |
| `OfficeComConversionEngine.cs` | Office COM export |
| `PdfToImageRenderer.cs` | PDF page rendering |
| `PdfToolService.cs` | PDF merge, split, extract, rotate |
| `EngineSetupAdvisor.cs` | Setup guidance and detection |
| `ConversionEngineSelector.cs` | Auto-select best engine |
| `ProcessRunner.cs` | Process execution with timeout |
| `LibreOfficeLocator.cs` | Discover LibreOffice paths |
| `MicrosoftOfficeAvailabilityProvider.cs` | Check Office COM ProgIDs |

### App Services
| File | Responsibility |
|------|-----------------|
| `AppSettingsService.cs` | Persist user preferences |
| `ThemeService.cs` | Manage System/Light/Dark themes |
| `LocalizationService.cs` | EN/VI language support |
| `AppUpdateService.cs` | GitHub Releases update checking |
| `SelfCheckRunner.cs` | Startup validation |
| `AppStartupLogger.cs` | Crash diagnostics |
| `UiBatchRunner.cs` | Async batch execution |

### Models
| File | Purpose |
|------|---------|
| `BatchConversionJob.cs` | Batch job definition |
| `BatchConversionItem.cs` | Individual file in batch |
| `BatchConversionResult.cs` | Batch execution result |
| `ConversionRequest.cs` | Single file conversion request |
| `ConversionResult.cs` | Single file conversion result |
| `EngineSetupStatus.cs` | Engine availability status |
| `PdfRenderRequest.cs` | PDF rendering request |
| `PdfRenderResult.cs` | PDF rendering result |
| `PdfToolRequest.cs` | PDF toolbox operation request |
| `PdfToolResult.cs` | PDF toolbox operation result |

---

## 9. Development Workflow

### Local Setup
```powershell
cd D:\vibecode\snappy-docs-convert
git switch ai/phase-00-local-agent-setup
powershell -ExecutionPolicy Bypass -File .\scripts\check-local.ps1
```

### Build & Test
```powershell
dotnet restore
dotnet build
dotnet test
```

### Run Desktop App
```powershell
dotnet run --project src/SnappyDocsConvert.App
```

### Self-Check (No GUI)
```powershell
dotnet run --project src/SnappyDocsConvert.App -- --self-check
```

### Website Development
```powershell
cd website
npm install
npm run dev
npm run build
```

### Packaging
```powershell
# Portable ZIP
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1

# Installer EXE
powershell -ExecutionPolicy Bypass -File .\scripts\package-installer.ps1 -Version v0.1.0-beta.3

# E2E QA Gate
powershell -ExecutionPolicy Bypass -File .\scripts\qa-e2e.ps1
```

---

## 10. Current Phase Status (Phase 13B beta.3)

### Implemented Features ✅
- Local Office/PDF conversion (LibreOffice + Office COM)
- PDF image export (PNG/JPEG/WebP)
- Batch conversion pipeline
- PDF toolbox (merge, split, extract, rotate, images-to-PDF)
- Mode-based WPF UI (Convert, PDF Tools, Engines, Logs, Help)
- EN/VI localization with runtime toggle
- Light/Dark/System theme support
- Manual update center (GitHub Releases)
- Startup crash diagnostics
- GUI launch smoke testing
- E2E QA gate
- Portable packaging (ZIP)
- Inno Setup installer packaging
- Trust docs and GitHub Actions workflows
- Demo tooling and website
- Frontend-only Vercel website

### Not Yet Implemented ❌
- Silent auto-update (manual only)
- Code signing
- MSI/MSIX packaging
- OCR (planned as OCR Lite)
- LibreOffice real smoke test
- PowerPoint real smoke test

---

## 11. Security & Privacy

- **Local-Only:** All processing on user's machine
- **No Cloud Upload:** No remote conversion services
- **No Telemetry:** No analytics, tracking, or data collection
- **No Secrets in Logs:** Credentials and tokens never printed
- **Unsigned App:** Current beta is unsigned (SmartScreen warnings expected)
- **SHA256 Verification:** Release artifacts include checksums
- **GitHub Attestations:** Release build creates provenance attestations

---

## 12. Component Interaction Flow

```
User (WPF Desktop App)
    ↓
MainWindowViewModel (MVVM)
    ↓
UiBatchRunner (async background task)
    ↓
BatchConversionPipeline (orchestration)
    ├→ ConversionEngineSelector (auto-select or enforce preference)
    ├→ OfficeComConversionEngine (Word/PowerPoint COM export)
    ├→ LibreOfficeConversionEngine (headless conversion)
    ├→ PdfToImageRenderer (PDF → PNG/JPEG/WebP)
    └→ PdfToolService (merge, split, rotate, extract)
    ↓
Local Output Folder
    ├→ pdf/
    │   └→ <safeBaseName>__<hash8>.pdf
    └→ images/
        └→ <safeBaseName>__<hash8>/
            ├→ page-001.png
            ├→ page-002.png
            └→ ...
```

---

## 13. Smoke Testing Strategy

### Available Smoke Tests
- `smoke-app-build.ps1` - Verifies build and `--self-check`
- `smoke-gui-launch.ps1` - Verifies packaged GUI window appears
- `smoke-office.ps1` - Real Office COM smoke (Word RTF-to-PDF)
- `smoke-release.ps1` - Portable ZIP smoke
- `smoke-installer.ps1` - Installer EXE smoke
- `qa-e2e.ps1` - Full E2E gate (build, test, self-check, publish, verify outputs)

### Running E2E QA
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\qa-e2e.ps1
```

---

## 14. Known Limitations & Future Work

### Current Limitations
- **No Silent Auto-Update:** Manual update center only (user-initiated)
- **No Code Signing:** Beta builds are unsigned (SmartScreen warnings expected)
- **No MSI/MSIX:** Only portable ZIP and Inno Setup installer
- **No OCR:** Planned as OCR Lite in future phase
- **No Real LibreOffice Smoke:** Smoke tests use fake process runner
- **No Real PowerPoint Smoke:** Smoke tests skip PowerPoint (no safe sample)

### Future Roadmap
1. **Silent Auto-Update** - Background update checking and installation
2. **Code Signing** - Trusted certificate for SmartScreen bypass
3. **MSI/MSIX Packaging** - Windows installer formats
4. **OCR Lite** - Optical character recognition for scanned documents
5. **Advanced PDF Tools** - Compression, encryption, redaction, form filling
6. **Batch Scheduling** - Scheduled batch conversions
7. **Plugin System** - Custom conversion engines

---

## 15. Summary

**snappy-docs-convert** is a production-ready, well-architected local document conversion desktop application with:

✅ **Modular Core Library** - Multiple conversion engines (Office COM, LibreOffice, PDF rendering)  
✅ **Responsive WPF UI** - Batch processing, PDF toolbox, localization (EN/VI)  
✅ **Comprehensive Testing** - xUnit with E2E smoke tests  
✅ **Robust CI/CD** - GitHub Actions with release automation  
✅ **Privacy-First Design** - No cloud, telemetry, or tracking  
✅ **Professional Packaging** - Portable ZIP + Inno Setup installer  
✅ **Frontend-Only Website** - Vite + React deployed to Vercel  

**Architecture Highlights:**
- Testable dependency injection pattern
- Async/await throughout with cancellation support
- Serialization for shared resources (Office COM, PDFium)
- Batch pipeline with partial success and error recovery
- Local-only processing with no external dependencies
- MVVM pattern for responsive UI
- Comprehensive error handling and diagnostics

**Ready for:** Public beta release pending manual GUI QA and code signing.

---

**Report Generated:** May 31, 2026  
**Codebase Version:** Phase 13B beta.3  
**Status:** Production-ready for local document conversion
