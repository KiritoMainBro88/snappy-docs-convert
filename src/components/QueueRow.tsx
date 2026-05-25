import { FileText, Image as ImageIcon, FileQuestion, X, CheckCircle2 } from "lucide-react";
import type { QueueItem, JobStatus } from "@/lib/types";

const STATUS_STYLE: Record<JobStatus, string> = {
  Ready: "bg-white/60 text-foreground/70",
  Queued: "bg-zinc-100/70 text-zinc-700",
  Converting: "bg-blue-100/80 text-blue-700",
  Done: "bg-emerald-100/80 text-emerald-700",
  Failed: "bg-red-100/80 text-red-700",
  Cancelled: "bg-amber-100/80 text-amber-700",
  DesktopRequired: "bg-purple-100/80 text-purple-700",
};

const STATUS_LABEL: Record<JobStatus, string> = {
  Ready: "Ready",
  Queued: "Queued",
  Converting: "Converting",
  Done: "Done",
  Failed: "Failed",
  Cancelled: "Cancelled",
  DesktopRequired: "Desktop Required",
};

function ExtIcon({ item }: { item: QueueItem }) {
  if (item.category === "pdf")
    return (
      <div className="grid size-9 shrink-0 place-items-center rounded-lg bg-red-100/80 ring-1 ring-red-200/80">
        <FileText className="size-4 text-red-700" />
      </div>
    );
  if (item.category === "image")
    return (
      <div className="grid size-9 shrink-0 place-items-center rounded-lg bg-blue-100/80 ring-1 ring-blue-200/80">
        <ImageIcon className="size-4 text-blue-700" />
      </div>
    );
  if (item.category === "office")
    return (
      <div className="grid size-9 shrink-0 place-items-center rounded-lg bg-purple-100/80 ring-1 ring-purple-200/80">
        <FileText className="size-4 text-purple-700" />
      </div>
    );
  return (
    <div className="grid size-9 shrink-0 place-items-center rounded-lg bg-zinc-100/80 ring-1 ring-zinc-200/80">
      <FileQuestion className="size-4 text-zinc-600" />
    </div>
  );
}

interface QueueRowProps {
  item: QueueItem;
  onRemove: (id: string) => void;
}

export function QueueRow({ item, onRemove }: QueueRowProps) {
  const showProgress =
    item.status === "Converting" || (item.status === "Done" && item.progress > 0);
  const pct = Math.round((item.progress || 0) * 100);

  return (
    <div className="group glass-soft flex items-center gap-3 rounded-xl px-3 py-2.5 transition hover:bg-white/65">
      <ExtIcon item={item} />

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate text-[13px] font-medium text-foreground">
            {item.file.name}
          </span>
          <span className="shrink-0 text-[11px] text-foreground/40">
            {(item.file.size / 1024).toFixed(1)} KB
          </span>
        </div>
        {showProgress ? (
          <div className="mt-1.5 h-1 w-40 overflow-hidden rounded-full bg-white/70">
            <div
              className="h-full rounded-full bg-[var(--accent)] transition-[width] duration-200"
              style={{ width: `${pct}%` }}
            />
          </div>
        ) : item.message ? (
          <p
            className={`mt-0.5 truncate text-[11px] ${
              item.status === "Failed"
                ? "text-red-600/90"
                : item.status === "DesktopRequired"
                  ? "text-purple-700/90"
                  : "text-foreground/55"
            }`}
            title={item.message}
          >
            {item.message}
          </p>
        ) : item.outputName ? (
          <p className="mt-0.5 truncate text-[11px] text-emerald-600/90">
            Output: {item.outputName}
          </p>
        ) : null}
      </div>

      <span
        className={`hidden shrink-0 rounded-full px-2 py-0.5 text-[10.5px] font-medium md:inline-flex ${STATUS_STYLE[item.status]}`}
      >
        {item.status === "Done" ? (
          <span className="inline-flex items-center gap-1">
            <CheckCircle2 className="size-3" />
            {STATUS_LABEL[item.status]}
          </span>
        ) : (
          STATUS_LABEL[item.status]
        )}
      </span>

      <button
        onClick={() => onRemove(item.id)}
        className="grid size-6 shrink-0 place-items-center rounded-md text-foreground/40 transition hover:bg-white/70 hover:text-foreground"
        aria-label="Remove"
      >
        <X className="size-3.5" />
      </button>
    </div>
  );
}
