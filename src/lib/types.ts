export type JobStatus =
  | "Ready"
  | "Queued"
  | "Converting"
  | "Done"
  | "Failed"
  | "Cancelled"
  | "DesktopRequired";

export type JobMode = "pdf-to-images" | "images-to-pdf" | "merge-pdfs" | "split-pdf";

export type ImageFormat = "png" | "jpeg" | "webp";

export type ImagesToPdfMode = "merge" | "per-image";

export type FileCategory = "pdf" | "image" | "office" | "unsupported";

export interface QueueItem {
  id: string;
  file: File;
  category: FileCategory;
  status: JobStatus;
  progress: number; // 0..1
  message?: string;
  outputName?: string;
  outputSize?: number;
}

export interface LogEntry {
  ts: string;
  level: "info" | "warn" | "error";
  text: string;
}
