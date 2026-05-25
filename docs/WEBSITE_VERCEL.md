# Website And Vercel

Phase 9A adds a polished frontend-only website under `website/`.

## Rules

- Static frontend only.
- No backend.
- No API routes.
- No serverless functions.
- No analytics, telemetry, tracking, or external CDN fonts/scripts.
- Local files are never uploaded to the website.
- The desktop app processes files locally on Windows.
- Download links point to GitHub Releases.

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

The check builds the Vite site, verifies `dist/index.html`, checks required release/source links, checks EN/VI local-only copy, and blocks backend/API folders plus external CDN/font/script references.

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

Production deploy requires explicit owner approval:

```powershell
vercel --cwd website --prod
```

## Future Redesign

Lovable or another frontend tool can redesign the website later, but must preserve:

- frontend-only static hosting
- no backend/API routes
- no upload flow
- no telemetry/analytics/tracking
- GitHub Releases download link
- GitHub source link
