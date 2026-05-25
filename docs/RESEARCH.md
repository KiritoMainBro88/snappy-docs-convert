# Research Notes

Checked on 2026-05-25. These notes are for architecture direction only; they do not implement conversion.

## Direction

- WPF/.NET is the preferred final desktop shell because the app is Windows first, needs local process control, and will eventually call Office COM and LibreOffice headless from a trusted local desktop environment.
- A browser prototype can help with UI and PDF/image experiments, but it cannot directly run Microsoft Office COM automation or manage a local LibreOffice headless process from the browser sandbox.
- Microsoft Office COM is planned for highest-fidelity Office export on machines with Office installed. Word and PowerPoint expose fixed-format PDF export APIs, and PowerPoint also exposes slide image export.
- LibreOffice headless is planned as the no-Office fallback. The engine should use `soffice.com`/`soffice.exe` carefully on Windows, with isolated user profiles and explicit output directories.
- PDF-to-image must use a real PDF renderer or export API. Do not use screenshots as conversion output.

## Primary References

- WPF overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- Office primary interop assemblies: https://learn.microsoft.com/en-us/visualstudio/vsto/office-primary-interop-assemblies?view=vs-2022
- Word `Document.ExportAsFixedFormat`: https://learn.microsoft.com/en-us/office/vba/api/word.document.exportasfixedformat
- PowerPoint `Presentation.ExportAsFixedFormat`: https://learn.microsoft.com/en-us/office/vba/api/powerpoint.presentation.exportasfixedformat
- PowerPoint `Presentation.Export`: https://learn.microsoft.com/office/vba/api/PowerPoint.Presentation.Export
- LibreOffice command-line parameters: https://help.libreoffice.org/latest/en-US/text/shared/guide/start_parameters.html
- LibreOffice API docs: https://api.libreoffice.org/
- PDFium project: https://pdfium.googlesource.com/pdfium/
- MuPDF documentation: https://mupdf.com/docs/

## Open Questions For Later Phases

- Which PDF renderer should be used for the first .NET implementation: PDFium wrapper, MuPDF binding, Windows API, or another maintained local renderer?
- How should engine health checks report Office/LibreOffice availability to the UI?
- What timeout, cancellation, and temp-profile isolation rules are needed for batch conversions?
- Which output image formats and DPI presets should Phase 4 support first?
