import { useCallback, useMemo, useRef, useState } from "react";
import { classify, safeBaseName } from "@/lib/fileSupport";
import type {
  ImageFormat,
  ImagesToPdfMode,
  JobMode,
  LogEntry,
  QueueItem,
} from "@/lib/types";
import {
  ConversionError,
  imageToPdf,
  imagesToOnePdf,
  mergePdfs,
  pdfToImages,
  splitPdf,
} from "@/lib/converter";
import { createZipBuilder, downloadBlob, downloadZip } from "@/lib/zipOutput";

let _id = 0;
const nextId = () => `q_${Date.now().toString(36)}_${(_id++).toString(36)}`;

export interface Settings {
  mode: JobMode;
  imageFormat: ImageFormat;
  dpi: number;
  imagesToPdfMode: ImagesToPdfMode;
}

const DEFAULT_SETTINGS: Settings = {
  mode: "pdf-to-images",
  imageFormat: "png",
  dpi: 200,
  imagesToPdfMode: "merge",
};

export function useConvertQueue() {
  const [items, setItems] = useState<QueueItem[]>([]);
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [running, setRunning] = useState(false);
  const [settings, setSettings] = useState<Settings>(DEFAULT_SETTINGS);
  const abortRef = useRef<AbortController | null>(null);

  const log = useCallback((level: LogEntry["level"], text: string) => {
    const ts = new Date().toTimeString().slice(0, 8);
    setLogs((l) => [...l, { ts, level, text }].slice(-200));
  }, []);

  const update = useCallback((id: string, patch: Partial<QueueItem>) => {
    setItems((arr) => arr.map((it) => (it.id === id ? { ...it, ...patch } : it)));
  }, []);

  const addFiles = useCallback(
    (files: FileList | File[]) => {
      const arr = Array.from(files);
      const newItems: QueueItem[] = arr.map((file) => {
        const category = classify(file);
        if (category === "office") {
          return {
            id: nextId(),
            file,
            category,
            status: "DesktopRequired",
            progress: 0,
            message:
              "Browser cannot run Office COM or LibreOffice headless. Use the desktop app for Office conversion.",
          };
        }
        if (category === "unsupported") {
          return {
            id: nextId(),
            file,
            category,
            status: "Failed",
            progress: 0,
            message: "File type not supported in browser.",
          };
        }
        return {
          id: nextId(),
          file,
          category,
          status: "Ready",
          progress: 0,
        };
      });
      setItems((prev) => [...prev, ...newItems]);
      newItems.forEach((it) =>
        log(
          it.status === "DesktopRequired" || it.status === "Failed" ? "warn" : "info",
          `Added: ${it.file.name} (${it.category})`,
        ),
      );
    },
    [log],
  );

  const removeItem = useCallback((id: string) => {
    setItems((arr) => arr.filter((it) => it.id !== id));
  }, []);

  const clear = useCallback(() => {
    setItems([]);
    setLogs([]);
  }, []);

  const supported = useMemo(
    () => items.filter((i) => i.category === "pdf" || i.category === "image"),
    [items],
  );

  const cancel = useCallback(() => {
    if (abortRef.current) {
      abortRef.current.abort();
      log("warn", "Cancel requested.");
    }
  }, [log]);

  const start = useCallback(async () => {
    if (running) return;
    const eligible = items.filter(
      (i) => (i.status === "Ready" || i.status === "Queued") && (i.category === "pdf" || i.category === "image"),
    );
    if (eligible.length === 0) {
      log("warn", "No eligible files to convert.");
      return;
    }
    if (settings.dpi > 200) {
      log("warn", `DPI ${settings.dpi} is high — rendering may be slow or memory-heavy.`);
    }

    const controller = new AbortController();
    abortRef.current = controller;
    setRunning(true);

    // Mark all eligible as Queued.
    eligible.forEach((it) => update(it.id, { status: "Queued", progress: 0, message: undefined }));

    const zip = createZipBuilder();
    let zipUsed = false;
    let successCount = 0;
    const multi = eligible.length > 1;

    const fail = (id: string, err: unknown) => {
      const msg = err instanceof Error ? err.message : String(err);
      const code = err instanceof ConversionError ? err.code : "unknown";
      const status = code === "cancelled" ? "Cancelled" : "Failed";
      update(id, { status, message: msg, progress: 0 });
      log(status === "Cancelled" ? "warn" : "error", `${msg}`);
    };

    try {
      if (settings.mode === "pdf-to-images") {
        const pdfs = eligible.filter((i) => i.category === "pdf");
        const skipped = eligible.filter((i) => i.category !== "pdf");
        skipped.forEach((it) =>
          update(it.id, {
            status: "Failed",
            message: "PDF → Images requires PDF input.",
          }),
        );
        for (const item of pdfs) {
          if (controller.signal.aborted) break;
          update(item.id, { status: "Converting", progress: 0 });
          log("info", `Converting ${item.file.name} → images (${settings.imageFormat.toUpperCase()} @ ${settings.dpi}dpi)`);
          try {
            const pages = await pdfToImages(item.file, {
              dpi: settings.dpi,
              format: settings.imageFormat,
              signal: controller.signal,
              onProgress: (d, t) => update(item.id, { progress: d / t }),
            });
            if (multi) {
              zip.addImages(item.file.name, pages);
              zipUsed = true;
              update(item.id, {
                status: "Done",
                progress: 1,
                outputName: `images/${safeBaseName(item.file.name)}__...`,
                outputSize: pages.reduce((s, p) => s + p.blob.size, 0),
              });
            } else {
              // Single file: still ZIP because multiple page images.
              const singleZip = createZipBuilder();
              singleZip.addImages(item.file.name, pages);
              const blob = await singleZip.generate();
              const name = `${safeBaseName(item.file.name)}-images.zip`;
              downloadBlob(blob, name);
              update(item.id, {
                status: "Done",
                progress: 1,
                outputName: name,
                outputSize: blob.size,
              });
            }
            successCount++;
            log("info", `Done: ${item.file.name} (${pages.length} pages)`);
          } catch (e) {
            fail(item.id, e);
            if (controller.signal.aborted) break;
          }
        }
      } else if (settings.mode === "images-to-pdf") {
        const imgs = eligible.filter((i) => i.category === "image");
        const skipped = eligible.filter((i) => i.category !== "image");
        skipped.forEach((it) =>
          update(it.id, {
            status: "Failed",
            message: "Images → PDF requires image input.",
          }),
        );
        if (settings.imagesToPdfMode === "merge" && imgs.length > 0) {
          imgs.forEach((it) => update(it.id, { status: "Converting", progress: 0 }));
          log("info", `Merging ${imgs.length} image(s) into one PDF`);
          try {
            const blob = await imagesToOnePdf(
              imgs.map((i) => i.file),
              {
                signal: controller.signal,
                onProgress: (d, t) => {
                  imgs.forEach((it, idx) =>
                    update(it.id, {
                      progress: idx < d ? 1 : idx === d - 1 ? d / t : 0,
                    }),
                  );
                },
              },
            );
            if (multi) {
              zip.addPdf("merged-images.pdf", blob);
              zipUsed = true;
            } else {
              downloadBlob(blob, "merged-images.pdf");
            }
            imgs.forEach((it) =>
              update(it.id, {
                status: "Done",
                progress: 1,
                outputName: "merged-images.pdf",
                outputSize: blob.size,
              }),
            );
            successCount += imgs.length;
            log("info", `Done: merged-images.pdf (${blob.size} bytes)`);
          } catch (e) {
            imgs.forEach((it) => fail(it.id, e));
          }
        } else {
          for (const item of imgs) {
            if (controller.signal.aborted) break;
            update(item.id, { status: "Converting", progress: 0 });
            try {
              const blob = await imageToPdf(item.file, controller.signal);
              const name = `${safeBaseName(item.file.name)}.pdf`;
              if (multi) {
                zip.addPdf(name, blob);
                zipUsed = true;
              } else {
                downloadBlob(blob, name);
              }
              update(item.id, {
                status: "Done",
                progress: 1,
                outputName: name,
                outputSize: blob.size,
              });
              successCount++;
              log("info", `Done: ${name}`);
            } catch (e) {
              fail(item.id, e);
              if (controller.signal.aborted) break;
            }
          }
        }
      } else if (settings.mode === "merge-pdfs") {
        const pdfs = eligible.filter((i) => i.category === "pdf");
        const skipped = eligible.filter((i) => i.category !== "pdf");
        skipped.forEach((it) =>
          update(it.id, {
            status: "Failed",
            message: "Merge PDFs requires PDF input.",
          }),
        );
        if (pdfs.length > 0) {
          pdfs.forEach((it) => update(it.id, { status: "Converting", progress: 0 }));
          log("info", `Merging ${pdfs.length} PDF(s)`);
          try {
            const blob = await mergePdfs(
              pdfs.map((p) => p.file),
              {
                signal: controller.signal,
                onProgress: (d, t) => {
                  pdfs.forEach((it, idx) =>
                    update(it.id, {
                      progress: idx < d ? 1 : idx === d - 1 ? d / t : 0,
                    }),
                  );
                },
              },
            );
            downloadBlob(blob, "merged.pdf");
            pdfs.forEach((it) =>
              update(it.id, {
                status: "Done",
                progress: 1,
                outputName: "merged.pdf",
                outputSize: blob.size,
              }),
            );
            successCount += pdfs.length;
            log("info", `Done: merged.pdf`);
          } catch (e) {
            pdfs.forEach((it) => fail(it.id, e));
          }
        }
      } else if (settings.mode === "split-pdf") {
        const pdfs = eligible.filter((i) => i.category === "pdf");
        const skipped = eligible.filter((i) => i.category !== "pdf");
        skipped.forEach((it) =>
          update(it.id, {
            status: "Failed",
            message: "Split PDF requires PDF input.",
          }),
        );
        for (const item of pdfs) {
          if (controller.signal.aborted) break;
          update(item.id, { status: "Converting", progress: 0 });
          log("info", `Splitting ${item.file.name}`);
          try {
            const parts = await splitPdf(item.file, {
              signal: controller.signal,
              onProgress: (d, t) => update(item.id, { progress: d / t }),
            });
            if (multi) {
              zip.addSplit(item.file.name, parts);
              zipUsed = true;
              update(item.id, {
                status: "Done",
                progress: 1,
                outputName: `pdf/${safeBaseName(item.file.name)}__...`,
                outputSize: parts.reduce((s, p) => s + p.blob.size, 0),
              });
            } else {
              const singleZip = createZipBuilder();
              singleZip.addSplit(item.file.name, parts);
              const blob = await singleZip.generate();
              const name = `${safeBaseName(item.file.name)}-pages.zip`;
              downloadBlob(blob, name);
              update(item.id, {
                status: "Done",
                progress: 1,
                outputName: name,
                outputSize: blob.size,
              });
            }
            successCount++;
            log("info", `Done: ${item.file.name} (${parts.length} pages)`);
          } catch (e) {
            fail(item.id, e);
            if (controller.signal.aborted) break;
          }
        }
      }

      if (controller.signal.aborted) {
        // Mark any still-Queued/Converting as Cancelled.
        setItems((arr) =>
          arr.map((it) =>
            it.status === "Queued" || it.status === "Converting"
              ? { ...it, status: "Cancelled", message: "Cancelled by user", progress: 0 }
              : it,
          ),
        );
        log("warn", "Cancelled. Partial outputs discarded.");
      } else if (zipUsed) {
        const blob = await zip.generate();
        downloadZip(blob);
        log("info", `ZIP downloaded (${blob.size} bytes, ${successCount} item(s)).`);
      }
    } finally {
      setRunning(false);
      abortRef.current = null;
    }
  }, [items, running, settings, log, update]);

  const counts = useMemo(() => {
    const done = items.filter((i) => i.status === "Done").length;
    const total = supported.length;
    return { done, total, all: items.length };
  }, [items, supported]);

  return {
    items,
    logs,
    running,
    settings,
    setSettings,
    addFiles,
    removeItem,
    clear,
    start,
    cancel,
    counts,
  };
}
