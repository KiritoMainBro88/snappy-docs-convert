# Website And Vercel

Phase 9A adds a polished frontend-only website under `website/`. Phase 13A keeps it frontend-only and adds system/light/dark mode, browser language detection with persisted EN/VI selection, direct beta.2 release download links, Discord support, curated static demo screenshots/video, and clearer open-source/community copy.

Display app name: `kmb file tools`. Internal executable/project identifiers still use `SnappyDocsConvert`, but beta.2 public package filenames use `kmb-file-tools`.

## Rules

- Static frontend only.
- No backend.
- No API routes.
- No serverless functions.
- No analytics, telemetry, tracking, or external CDN fonts/scripts.
- Local files are never uploaded to the website.
- The desktop app processes files locally on Windows.
- Download links point to GitHub Releases.
- Official downloads are free.
- MIT license wording must remain accurate: free use, modification, and redistribution are allowed with attribution/license notice.
- Anti-scam copy should warn against misleading paid repackages without claiming resale is legally prohibited.

The older `website-static/` folder remains as a simple no-build fallback.

## Run Locally

```powershell
cd website
npm install
npm run dev
npm run build
```

## Validate

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-website-vite.ps1
```

The check builds the Vite site, verifies `dist/index.html`, checks required release/source/Discord links, checks EN/VI local-only/no-upload/theme copy, and blocks backend/API folders plus external CDN/font/script references and known analytics provider markers.

## Vercel Preview

Preview deploy, from repo root:

```powershell
vercel --cwd website
```

If the CLI reports an invalid or missing token, run:

```powershell
vercel login
```

Then rerun preview deploy. Do not paste or commit tokens.

Recommended Vercel settings:

- Framework: Vite
- Root directory: `website`
- Build command: `npm run build`
- Output directory: `dist`

## Production Deploy

Production deploy requires Vercel auth and explicit owner approval:

```powershell
vercel --cwd website --prod
```

If auth is missing:

```powershell
vercel login
vercel --cwd website --prod
```

After a successful production deploy, set the GitHub repository homepage to the production URL:

```powershell
gh repo edit KiritoMainBro88/snappy-docs-convert --homepage <production-url> --description "Free local Windows document converter: Office/PDF tools, batch conversion, no upload, no telemetry."
```

Current production URL:

```text
https://website-sand-xi-15.vercel.app
```

Latest Phase 11B deploy:

```text
Preview: https://website-7mbufm22x-kiritomainbro88s-projects.vercel.app
Production deployment: https://website-2v6dq1ywv-kiritomainbro88s-projects.vercel.app
Production alias: https://website-sand-xi-15.vercel.app
```

Current repository homepage metadata points to `https://website-sand-xi-15.vercel.app`.

## Links

- Release: https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.2
- All releases: https://github.com/KiritoMainBro88/snappy-docs-convert/releases
- Source: https://github.com/KiritoMainBro88/snappy-docs-convert
- Discord: https://discord.gg/kZ3U36ncun

## Screenshots

Place owner-approved screenshots in:

```text
website/public/screenshots/
```

Expected names:

- `convert.png`
- `pdf-tools.png`
- `engines.png`

Do not include private documents, personal paths, secrets, or generated QA output in screenshots.

## Demo Assets

Generate website demo screenshots/video:

```powershell
cd website
npm run demo:website
cd ..
```

Generated media goes to ignored `artifacts/demo/website/`. Do not commit generated `.png`, `.webm`, or `.mp4` unless the owner explicitly curates a small website screenshot.

## Future Redesign

Lovable or another frontend tool can redesign the website later, but must preserve:

- frontend-only static hosting
- no backend/API routes
- no upload flow
- no telemetry/analytics/tracking
- GitHub Releases download link
- GitHub source link
