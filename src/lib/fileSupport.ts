import type { FileCategory } from "./types";

const IMAGE_EXT = ["png", "jpg", "jpeg", "webp", "gif", "bmp"];
const OFFICE_EXT = ["doc", "docx", "ppt", "pptx", "odt", "odp", "rtf", "xls", "xlsx"];

export function getExt(name: string): string {
  const i = name.lastIndexOf(".");
  return i >= 0 ? name.slice(i + 1).toLowerCase() : "";
}

export function classify(file: File): FileCategory {
  const ext = getExt(file.name);
  if (ext === "pdf" || file.type === "application/pdf") return "pdf";
  if (IMAGE_EXT.includes(ext) || file.type.startsWith("image/")) return "image";
  if (OFFICE_EXT.includes(ext)) return "office";
  return "unsupported";
}

export function hash8(input: string): string {
  // Simple FNV-1a-ish hash, 8 hex chars. Stable for the same input string.
  let h = 0x811c9dc5;
  for (let i = 0; i < input.length; i++) {
    h ^= input.charCodeAt(i);
    h = (h + ((h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24))) >>> 0;
  }
  return h.toString(16).padStart(8, "0").slice(0, 8);
}

export function safeBaseName(name: string): string {
  const dot = name.lastIndexOf(".");
  const base = dot > 0 ? name.slice(0, dot) : name;
  return base.replace(/[\\/:*?"<>|]+/g, "_").replace(/\s+/g, "_");
}
