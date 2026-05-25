## kmb file tools — macOS 26 Liquid Glass browser converter

### Scope
Local-only web preview of the future desktop converter. Everything runs in-tab; no uploads, no telemetry, no remote assets. A visible **Browser Preview** badge sits in the titlebar.

### Design lock — macOS 26 "Liquid Glass"
- Aesthetic: macOS 26 Tahoe liquid glass — translucent layered surfaces, heavy backdrop blur, soft inner highlights, refractive borders, glass pill controls, very rounded corners (`rounded-2xl` / `rounded-3xl`), generous spacing.
- Layout: Single Pane Focus — window with traffic lights, dropzone, queue list, glass inspector drawer on the right, glass bottom action bar, session log strip.
- Background: soft animated gradient wallpaper behind the window so the glass blur reads.
- Surfaces: `backdrop-blur-2xl` + `bg-white/40` (light) / `bg-zinc-900/40` (dark-ready), inner `ring-1 ring-white/40`, outer `shadow-2xl`, subtle specular highlight via top gradient.
- Tokens (in `src/styles.css`): `--glass-bg`, `--glass-border`, `--glass-highlight`, `--accent` `#0a84ff` (macOS system blue), traffic-light colors, radius `1rem`/`1.5rem`. System font stack only: `-apple-system, BlinkMacSystemFont, "SF Pro Display", "Segoe UI", Roboto, ...` — no Google Fonts, no CDN.
- Titlebar: traffic lights + centered "kmb file tools" + glass pill **Browser Preview**.

### Browser-supported conversions (real)
- PDF → Images (PNG/JPG/WebP, DPI selectable, default **200**, warn when > 200).
- Images → PDF — mode: **merge into one PDF (default)** or one PDF per image.
- Merge multiple PDFs.
- Split PDF — one PDF per page.

### Browser-unsupported (Office)
DOC/DOCX/PPT/PPTX/ODT/ODP/RTF accepted into a separate **Desktop required** section (not Pending). Status pill `Desktop Required`, message: "Browser cannot run Office COM or LibreOffice headless. Use the desktop app for Office conversion." Start Queue ignores them.

### Files

**New**
- `src/components/MacWindow.tsx` — liquid-glass frame: traffic lights, titlebar, Browser Preview badge, animated gradient backdrop, glass body.
- `src/components/DropZone.tsx` — glass dashed dropzone, drag/drop + file input; classifies into supported / desktop-required.
- `src/components/QueueRow.tsx` — glass row: icon, name, target, progress, status pill (Ready/Queued/Converting/Done/Failed/Cancelled/Desktop Required), per-row error.
- `src/components/InspectorDrawer.tsx` — glass drawer: Job mode (PDF→Images, Images→PDF, Merge PDFs, Split PDF), Image format segmented (PNG/JPG/WebP), DPI input + >200 warning, Images→PDF mode toggle, Dependencies (Browser PDF ✓, Office ✗ desktop-only, LibreOffice ✗ desktop-only), **Browser download behavior** explainer.
- `src/components/BottomBar.tsx` — glass bar: counter "x of y", View Log, Cancel, Start Queue (glass primary button with accent glow).
- `src/components/SessionLog.tsx` — mono log with timestamps, glass surface.
- `src/components/EmptyState.tsx` — no-files state.
- `src/lib/types.ts` — `QueueItem`, `JobStatus` (Ready|Queued|Converting|Done|Failed|Cancelled|DesktopRequired), `JobMode`, `ImageFormat`.
- `src/lib/fileSupport.ts` — classify by extension/MIME.
- `src/lib/converter.ts` — sequential PDF→image (page-by-page, release canvas after each), images→PDF (merge or per-image), merge PDFs, split PDF; all accept `AbortSignal`; typed errors for encrypted PDF, render failure, oversized canvas.
- `src/lib/zipOutput.ts` — JSZip builder: `kmb-file-tools-output/pdf/...`, `kmb-file-tools-output/images/<name>__<hash8>/page-001.png`. Multi-file jobs always ZIP.
- `src/lib/pdfWorker.ts` — local pdf.js worker: `import worker from 'pdfjs-dist/build/pdf.worker.min.mjs?url'` → `GlobalWorkerOptions.workerSrc`. No CDN.
- `src/hooks/useConvertQueue.ts` — queue state, real cancel via `AbortController`: current item → Cancelled, remaining Queued → Cancelled, partial outputs discarded.

**Modified**
- `src/routes/index.tsx` — replace placeholder; render full app; `head()` SEO "kmb file tools".
- `src/routes/__root.tsx` — default title "kmb file tools".
- `src/styles.css` — liquid-glass tokens + system font stack on body + gradient backdrop keyframes.

**Deps (bun add)**: `pdfjs-dist`, `pdf-lib`, `jszip`, `file-saver`.

### Behavior
- PDF→Images: loop pages with `await`, render canvas at DPI scale, `toBlob`, push to ZIP, null canvas refs, yield to UI, abort check each page.
- Errors surfaced as row error + log entry: encrypted PDF, render failure, canvas memory limit ("Page too large at this DPI — try a lower DPI"), unsupported file, empty queue.
- Browser download behavior text: "Browser builds download files or a ZIP through your browser. The desktop build will save outputs directly to a folder you choose."

### Out of scope
Office/LibreOffice engines, native folder writes, .NET WPF code, xUnit tests, CLI — flagged as desktop-only in UI.
