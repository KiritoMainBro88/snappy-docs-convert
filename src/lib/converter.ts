import { PDFDocument } from "pdf-lib";
import { ensurePdfWorker, pdfjsLib } from "./pdfWorker";
import type { ImageFormat } from "./types";

export class ConversionError extends Error {
  code:
    | "encrypted"
    | "render_failed"
    | "canvas_too_large"
    | "unsupported"
    | "cancelled"
    | "unknown";
  constructor(
    code: ConversionError["code"],
    message: string,
  ) {
    super(message);
    this.code = code;
  }
}

function checkAbort(signal?: AbortSignal) {
  if (signal?.aborted) throw new ConversionError("cancelled", "Cancelled by user");
}

function blobToArrayBuffer(blob: Blob): Promise<ArrayBuffer> {
  return blob.arrayBuffer();
}

function yieldUi() {
  return new Promise<void>((r) => setTimeout(r, 0));
}

const MIME_BY_FORMAT: Record<ImageFormat, string> = {
  png: "image/png",
  jpeg: "image/jpeg",
  webp: "image/webp",
};

export interface PdfToImagesOptions {
  dpi: number;
  format: ImageFormat;
  quality?: number; // 0..1 for jpeg/webp
  signal?: AbortSignal;
  onProgress?: (done: number, total: number) => void;
}

export interface PageImage {
  index: number; // 1-based
  blob: Blob;
  ext: string;
}

export async function pdfToImages(
  file: File,
  opts: PdfToImagesOptions,
): Promise<PageImage[]> {
  ensurePdfWorker();
  checkAbort(opts.signal);

  const buf = await blobToArrayBuffer(file);
  let pdf;
  try {
    pdf = await pdfjsLib.getDocument({ data: new Uint8Array(buf) }).promise;
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    if (/password|encrypted/i.test(msg)) {
      throw new ConversionError(
        "encrypted",
        "Protected PDF — remove the password before converting.",
      );
    }
    throw new ConversionError("render_failed", `Failed to open PDF: ${msg}`);
  }

  const scale = opts.dpi / 72;
  const results: PageImage[] = [];
  const total = pdf.numPages;

  for (let i = 1; i <= total; i++) {
    checkAbort(opts.signal);

    let canvas: HTMLCanvasElement | null = document.createElement("canvas");
    const ctx = canvas.getContext("2d");
    if (!ctx) {
      throw new ConversionError("render_failed", "Canvas 2D context unavailable.");
    }

    try {
      const page = await pdf.getPage(i);
      const viewport = page.getViewport({ scale });
      canvas.width = Math.ceil(viewport.width);
      canvas.height = Math.ceil(viewport.height);

      // Guard against absurd canvas sizes (most browsers cap ~16k x 16k).
      if (canvas.width > 16384 || canvas.height > 16384) {
        throw new ConversionError(
          "canvas_too_large",
          `Page ${i} too large at this DPI — try a lower DPI.`,
        );
      }

      await page.render({ canvasContext: ctx, viewport, canvas }).promise;

      const blob: Blob | null = await new Promise((resolve) =>
        canvas!.toBlob(
          (b) => resolve(b),
          MIME_BY_FORMAT[opts.format],
          opts.format === "png" ? undefined : opts.quality ?? 0.92,
        ),
      );
      if (!blob) {
        throw new ConversionError(
          "canvas_too_large",
          `Page ${i} could not be encoded — try a lower DPI.`,
        );
      }

      results.push({ index: i, blob, ext: opts.format === "jpeg" ? "jpg" : opts.format });
      page.cleanup();
    } catch (e: unknown) {
      if (e instanceof ConversionError) throw e;
      const msg = e instanceof Error ? e.message : String(e);
      throw new ConversionError("render_failed", `Page ${i} render failed: ${msg}`);
    } finally {
      // Release canvas memory.
      canvas.width = 0;
      canvas.height = 0;
      canvas = null;
    }

    opts.onProgress?.(i, total);
    await yieldUi();
  }

  await pdf.cleanup();
  await pdf.destroy();
  return results;
}

export interface ImagesToPdfOptions {
  signal?: AbortSignal;
  onProgress?: (done: number, total: number) => void;
}

async function embedImage(pdf: PDFDocument, file: File) {
  const ext = file.name.toLowerCase();
  const buf = await blobToArrayBuffer(file);
  if (ext.endsWith(".png")) return pdf.embedPng(buf);
  if (ext.endsWith(".jpg") || ext.endsWith(".jpeg")) return pdf.embedJpg(buf);

  // Re-encode unsupported types (webp/gif/bmp) through canvas to PNG.
  const bitmap = await createImageBitmap(file);
  const canvas = document.createElement("canvas");
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new ConversionError("render_failed", "Canvas unavailable for image re-encode.");
  ctx.drawImage(bitmap, 0, 0);
  bitmap.close?.();
  const blob: Blob | null = await new Promise((r) => canvas.toBlob((b) => r(b), "image/png"));
  canvas.width = 0;
  canvas.height = 0;
  if (!blob) throw new ConversionError("render_failed", "Image re-encode failed.");
  return pdf.embedPng(await blob.arrayBuffer());
}

export async function imagesToOnePdf(
  files: File[],
  opts: ImagesToPdfOptions,
): Promise<Blob> {
  const pdf = await PDFDocument.create();
  for (let i = 0; i < files.length; i++) {
    checkAbort(opts.signal);
    const img = await embedImage(pdf, files[i]);
    const page = pdf.addPage([img.width, img.height]);
    page.drawImage(img, { x: 0, y: 0, width: img.width, height: img.height });
    opts.onProgress?.(i + 1, files.length);
    await yieldUi();
  }
  const bytes = await pdf.save();
  return new Blob([new Uint8Array(bytes)], { type: "application/pdf" });
}

export async function imageToPdf(file: File, signal?: AbortSignal): Promise<Blob> {
  return imagesToOnePdf([file], { signal });
}

export interface MergePdfsOptions {
  signal?: AbortSignal;
  onProgress?: (done: number, total: number) => void;
}

export async function mergePdfs(files: File[], opts: MergePdfsOptions): Promise<Blob> {
  const out = await PDFDocument.create();
  for (let i = 0; i < files.length; i++) {
    checkAbort(opts.signal);
    const buf = await blobToArrayBuffer(files[i]);
    let src;
    try {
      src = await PDFDocument.load(buf, { ignoreEncryption: false });
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      if (/encrypt/i.test(msg)) {
        throw new ConversionError(
          "encrypted",
          `${files[i].name} is protected — remove the password before merging.`,
        );
      }
      throw new ConversionError("render_failed", `Failed to read ${files[i].name}: ${msg}`);
    }
    const pages = await out.copyPages(src, src.getPageIndices());
    pages.forEach((p) => out.addPage(p));
    opts.onProgress?.(i + 1, files.length);
    await yieldUi();
  }
  const bytes = await out.save();
  return new Blob([new Uint8Array(bytes)], { type: "application/pdf" });
}

export interface SplitPdfResult {
  index: number; // 1-based
  blob: Blob;
}

export async function splitPdf(
  file: File,
  opts: { signal?: AbortSignal; onProgress?: (d: number, t: number) => void },
): Promise<SplitPdfResult[]> {
  const buf = await blobToArrayBuffer(file);
  let src;
  try {
    src = await PDFDocument.load(buf, { ignoreEncryption: false });
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    if (/encrypt/i.test(msg)) {
      throw new ConversionError(
        "encrypted",
        "Protected PDF — remove the password before splitting.",
      );
    }
    throw new ConversionError("render_failed", `Failed to read PDF: ${msg}`);
  }
  const total = src.getPageCount();
  const out: SplitPdfResult[] = [];
  for (let i = 0; i < total; i++) {
    checkAbort(opts.signal);
    const one = await PDFDocument.create();
    const [p] = await one.copyPages(src, [i]);
    one.addPage(p);
    const bytes = await one.save();
    out.push({
      index: i + 1,
      blob: new Blob([new Uint8Array(bytes)], { type: "application/pdf" }),
    });
    opts.onProgress?.(i + 1, total);
    await yieldUi();
  }
  return out;
}
