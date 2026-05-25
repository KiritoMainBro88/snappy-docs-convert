import type { LogEntry } from "@/lib/types";

interface SessionLogProps {
  logs: LogEntry[];
  open: boolean;
}

export function SessionLog({ logs, open }: SessionLogProps) {
  if (!open) return null;
  return (
    <div className="glass-soft kmb-scroll mono max-h-44 overflow-y-auto border-t border-white/40 px-5 py-2.5 text-[10.5px]">
      {logs.length === 0 ? (
        <div className="text-foreground/40">No log entries yet.</div>
      ) : (
        logs.map((l, i) => (
          <div key={i} className="flex gap-2 leading-relaxed">
            <span className="text-foreground/40">[{l.ts}]</span>
            <span
              className={
                l.level === "error"
                  ? "text-red-600"
                  : l.level === "warn"
                    ? "text-amber-700"
                    : "text-foreground/70"
              }
            >
              {l.text}
            </span>
          </div>
        ))
      )}
    </div>
  );
}
