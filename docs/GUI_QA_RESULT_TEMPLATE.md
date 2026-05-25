# Manual GUI QA Result

Package:
Exe:
Git branch:
Git commit:
Tester:
Machine:
Date:

## Result Summary

- Overall: Pending / Pass / Fail
- Blocking issues:
- Non-blocking issues:

## Checklist

| Step | Expected | Actual | Result | Evidence |
|---|---|---|---|---|
| 1. Launch portable exe. | Window opens as `kmb file tools`; no startup crash. |  | Pending |  |
| 2. Verify engine status cards. | Word, PowerPoint, LibreOffice, and PDF renderer statuses match machine. |  | Pending |  |
| 3. PDF -> PNG. | PDF renders real `page-001.png` file, size > 0. |  | Pending |  |
| 4. PDF -> JPEG. | PDF renders real `page-001.jpg` or `.jpeg` file, size > 0. |  | Pending |  |
| 5. RTF -> PDF. | RTF converts through Word when available; output PDF exists, size > 0. |  | Pending |  |
| 6. RTF -> PDF+Images. | RTF produces PDF plus at least one image file, size > 0. |  | Pending |  |
| 7. Batch partial failure. | One bad/missing item fails; valid item still succeeds. |  | Pending |  |
| 8. Cancel running queue. | Cancel request is logged; current/remaining items cancel where possible. |  | Pending |  |
| 9. Unsupported file warning. | Unsupported file is rejected or logged clearly; app does not crash. |  | Pending |  |
| 10. Missing LibreOffice guidance. | LibreOffice mode explains missing `soffice`; no fake success. |  | Pending |  |
| 11. Output folder open. | Output folder or row output opens in Explorer. |  | Pending |  |
| 12. Resize 1280x720. | Controls remain readable; critical buttons not clipped. |  | Pending |  |
| 13. Resize 1440x900. | Layout expands cleanly; queue/settings/log remain usable. |  | Pending |  |
| 14. Logs clear/readable. | Logs show timestamps and can be cleared. |  | Pending |  |
| 15. No cloud/upload claim violated. | UI and behavior stay local-only; no upload/telemetry claim appears. |  | Pending |  |

## Evidence

- Output paths:
- Screenshots:
- Notes:

## Final Decision

- Ready for personal use: yes/no
- Ready for beta tester: yes/no
- Needs fix before release:
