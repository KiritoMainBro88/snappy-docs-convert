# Manual GUI QA Checklist

Use this checklist for manual WPF validation. Do not mark a row pass unless the step was actually run on the desktop app.

Launch command:

```powershell
dotnet run --project src\SnappyDocsConvert.App
```

| Step | Expected result | Actual result | Pass/Fail | Evidence path/screenshot optional |
|---|---|---|---|---|
| 1. Launch app with the command above. | Window opens as `Snappy Docs Convert`; no startup crash. |  |  |  |
| 2. Compare engine statuses with `dotnet run --project src\SnappyDocsConvert.App -- --self-check`. | Word, PowerPoint, LibreOffice statuses match local machine. |  |  |  |
| 3. Verify privacy wording. | Header shows local-only/no-upload wording; no cloud upload claims. |  |  |  |
| 4. Drag/drop a generated or known-good PDF. | PDF enters queue with target/engine/status visible. |  |  |  |
| 5. Select output folder. | Output folder text updates; log records folder selection. |  |  |  |
| 6. Select target `Images`. | Target setting changes without freezing UI. |  |  |  |
| 7. Start queue for PDF image output. | Queue item becomes Running then Succeeded; output folder contains `page-001.png` or selected format. |  |  |  |
| 8. Use row `Open` button. | Explorer opens output file/folder location. |  |  |  |
| 9. Add tiny RTF file. | RTF enters queue; unsupported files are not added and log explains rejection. |  |  |  |
| 10. Select target `PdfAndImages`. | Settings show PDF + image target. |  |  |  |
| 11. Start RTF conversion with Office available. | Output PDF and image file are created; item succeeds. |  |  |  |
| 12. Add one valid file and one missing/unsupported path if possible. | Valid file can succeed; bad item fails or is rejected without stopping valid work. |  |  |  |
| 13. Start a longer batch, then press Cancel. | Cancel request logs; current/remaining items become cancelled where possible. |  |  |  |
| 14. Select `LibreOffice` engine when LibreOffice is missing. | Guidance says LibreOffice/soffice is required; no fake success. |  |  |  |
| 15. Click `Recheck`. | Engine cards refresh without blocking UI. |  |  |  |
| 16. Click `Choose soffice` and cancel dialog. | App remains stable; no path is changed. |  |  |  |
| 17. Click official LibreOffice download action. | Browser opens official LibreOffice download page. |  |  |  |
| 18. Resize to 1280x720. | Controls remain readable; no clipped critical buttons. |  |  |  |
| 19. Resize to 1440x900. | Layout expands cleanly; queue/settings/log remain usable. |  |  |  |
| 20. Clear log and clear queue. | Log and queue clear; summary updates honestly. |  |  |  |

Known skips:

- LibreOffice real conversion requires installed LibreOffice.
- PowerPoint real conversion still needs a safe PPT/PPTX fixture.
- Installer validation is not part of this checklist.
