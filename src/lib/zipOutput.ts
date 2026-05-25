import JSZip from "jszip";
import { saveAs } from "file-saver";
import { hash8, safeBaseName } from "./fileSupport";
import type { PageImage, SplitPdfResult } from "./converter";

const ROOT = "kmb-file-tools-output";

export interface ZipBuilder {
  addPdf(originalName: string, blob: Blob): void;
  addImages(originalName: string, pages: PageImage[]): void;
  addSplit(originalName: string, parts: SplitPdfResult[]): void;
  generate(): Promise<Blob>;
}

export function createZipBuilder(): ZipBuilder {
  const zip = new JSZip();
  const root = zip.folder(ROOT)!;
  const pdfDir = root.folder("pdf")!;
  const imgDir = root.folder("images")!;

  return {
    addPdf(originalName, blob) {
      const base = safeBaseName(originalName);
      pdfDir.file(`${base}.pdf`, blob);
    },
    addImages(originalName, pages) {
      const base = safeBaseName(originalName);
      const dir = imgDir.folder(`${base}__${hash8(originalName)}`)!;
      const pad = String(pages.length).length;
      pages.forEach((p) => {
        const n = String(p.index).padStart(Math.max(3, pad), "0");
        dir.file(`page-${n}.${p.ext}`, p.blob);
      });
    },
    addSplit(originalName, parts) {
      const base = safeBaseName(originalName);
      const dir = pdfDir.folder(`${base}__${hash8(originalName)}`)!;
      const pad = String(parts.length).length;
      parts.forEach((p) => {
        const n = String(p.index).padStart(Math.max(3, pad), "0");
        dir.file(`page-${n}.pdf`, p.blob);
      });
    },
    generate() {
      return zip.generateAsync({ type: "blob" });
    },
  };
}

export async function downloadZip(blob: Blob, name = "kmb-file-tools-output.zip") {
  saveAs(blob, name);
}

export function downloadBlob(blob: Blob, name: string) {
  saveAs(blob, name);
}
