import * as pdfjsLib from "pdfjs-dist";
// Vite ?url import — bundled locally, no CDN.
import workerUrl from "pdfjs-dist/build/pdf.worker.min.mjs?url";

let configured = false;
export function ensurePdfWorker() {
  if (configured) return;
  pdfjsLib.GlobalWorkerOptions.workerSrc = workerUrl;
  configured = true;
}

export { pdfjsLib };
