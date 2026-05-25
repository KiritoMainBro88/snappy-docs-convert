# Branding And Logo Assets

Snappy Docs Convert supports owner-provided logo/icon files. No image generation is part of this repo phase.

## Expected Files

```text
src/SnappyDocsConvert.App/Assets/logo.png
src/SnappyDocsConvert.App/Assets/app.ico
website-static/assets/logo.png
website-static/favicon.ico
```

## Windows App

- `app.ico`: Windows executable icon.
- Recommended `.ico` sizes: 16, 32, 48, and 256 px in one file.
- `logo.png`: in-app header logo.
- Recommended `.png`: square, 512x512, transparent background if possible.
- If `logo.png` is missing, the app shows text initials instead.
- If `app.ico` is missing, the project uses the default Windows app icon.

## Website

- `website-static/assets/logo.png`: static website logo.
- `website-static/favicon.ico`: browser favicon.
- Website uses local assets only. No CDN, analytics, or remote image assets.

## Rights

- Use owner-created or properly licensed images for public release.
- A meme/photo/cat mechanic image can be a private dev placeholder only unless rights are clear.
- Do not publish copyrighted image assets as the public app logo without permission.

## Replace Assets

1. Replace `src/SnappyDocsConvert.App/Assets/logo.png`.
2. Replace `src/SnappyDocsConvert.App/Assets/app.ico`.
3. Replace `website-static/assets/logo.png`.
4. Replace `website-static/favicon.ico`.
5. Run:

```powershell
dotnet build
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1 -Version v0.1.0-rc1-ui
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-release.ps1 -PackagePath .\artifacts\SnappyDocsConvert-portable-win-x64-v0.1.0-rc1-ui.zip
```

Source branding experiments can live under `assets/branding/`, which is ignored to avoid accidentally publishing unapproved or copyrighted source images.
