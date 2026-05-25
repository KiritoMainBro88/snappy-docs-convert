# kmb file tools Website

Frontend-only Vite/React website for `kmb file tools`.

Current beta release link:

https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.1

Production website:

https://website-sand-xi-15.vercel.app

Support:

https://discord.gg/kZ3U36ncun

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

Production deploy requires Vercel auth and owner approval:

```powershell
vercel --cwd website --prod
```

If auth is missing:

```powershell
vercel login
```

## Constraints

- Static frontend only.
- No backend, API routes, or serverless functions.
- No analytics, telemetry, tracking, or external CDN assets.
- Dark mode must stay local-only and may persist only in `localStorage`.
- English/Vietnamese copy must stay static; do not machine-translate at runtime.
- Download links point to GitHub Releases.
- Source link points to the GitHub repository.
- Discord link points to the official support invite.
- Desktop app processes files locally; the website never uploads or processes user files.

## Screenshots

Optional owner-provided screenshots go in:

```text
website/public/screenshots/
```

Expected names:

- `app-home.png`
- `pdf-tools.png`
- `website-dark.png`

Do not include private documents, personal paths, secrets, or generated QA output.

## Demo

```powershell
npm run demo:website
```

Generated screenshots and video go under ignored `..\artifacts\demo\website\`.
