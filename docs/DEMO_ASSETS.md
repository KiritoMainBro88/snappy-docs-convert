# Demo Assets

Demo assets are reproducible local artifacts for `kmb file tools`.

## Policy

- Generated media lives under `artifacts/demo/`.
- Large media is not committed: `.mp4`, `.webm`, `.mov`, and `artifacts/demo/` stay ignored.
- Website may include lightweight curated screenshots only after owner approval.
- Demo media can be attached to a GitHub Release later or uploaded elsewhere manually.
- Do not use user documents in demo assets.
- Demo inputs are generated locally by script.
- Desktop screen recording needs FFmpeg or a manual recorder.
- Website screenshots/video use Playwright.
- OBS is optional only when already installed and configured by the owner.

## Generate Inputs

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\create-demo-inputs.ps1
```

Output:

```text
artifacts\demo\inputs\
```

Generated inputs:

- tiny PDF
- tiny RTF
- tiny PNG
- unsupported dummy file

## Website Demo

```powershell
cd website
npm install
npm run demo:website
cd ..
```

Output:

```text
artifacts\demo\website\
  home-light-en.png
  home-dark-en.png
  home-light-vi.png
  mobile-vi.png
  video\
```

The Playwright test does not click external links. It only verifies that GitHub Release, source, and Discord links exist.

## Desktop Demo

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\demo-desktop.ps1
```

If FFmpeg is missing:

```powershell
winget install Gyan.FFmpeg
```

The desktop demo script launches the published WPF app and records the desktop for a bounded duration. Close private windows first.

## Desktop Screenshot

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\capture-app-screenshot.ps1
```

Output:

```text
artifacts\demo\desktop\app-home.png
```

If screen capture is blocked or unavailable, the script skips honestly.
