# Snappy Docs Convert Website

Frontend-only Vite/React website for Snappy Docs Convert.

## Run Locally

```powershell
cd website
npm install
npm run dev
npm run build
```

## Deploy Preview

```powershell
vercel --cwd website
```

Production deploy requires owner approval:

```powershell
vercel --cwd website --prod
```

## Constraints

- Static frontend only.
- No backend, API routes, or serverless functions.
- No analytics, telemetry, tracking, or external CDN assets.
- Download links point to GitHub Releases.
- Source link points to the GitHub repository.
- Desktop app processes files locally; the website never uploads or processes user files.
