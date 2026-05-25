import { Play, Square, ScrollText } from "lucide-react";

interface BottomBarProps {
  done: number;
  total: number;
  running: boolean;
  canStart: boolean;
  onStart: () => void;
  onCancel: () => void;
  onToggleLog: () => void;
}

export function BottomBar({
  done,
  total,
  running,
  canStart,
  onStart,
  onCancel,
  onToggleLog,
}: BottomBarProps) {
  return (
    <div className="glass-strong flex h-14 shrink-0 items-center justify-between border-t border-white/40 px-5">
      <div className="flex items-center gap-3 text-[11.5px] text-foreground/60">
        <span>
          <span className="font-medium text-foreground/85">{done} of {total}</span> files
          processed
        </span>
        <span className="h-3 w-px bg-white/60" />
        <button
          onClick={onToggleLog}
          className="inline-flex items-center gap-1 font-medium text-[var(--accent)] transition hover:underline"
        >
          <ScrollText className="size-3" />
          View activity log
        </button>
      </div>

      <div className="flex items-center gap-2">
        <button
          onClick={onCancel}
          disabled={!running}
          className="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12.5px] font-medium text-foreground/75 transition hover:bg-white/70 disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Square className="size-3.5" />
          Cancel
        </button>
        <button
          onClick={onStart}
          disabled={!canStart || running}
          className="group relative inline-flex items-center gap-1.5 rounded-lg bg-[var(--accent)] px-3.5 py-1.5 text-[12.5px] font-medium text-white shadow-[0_8px_24px_-8px_var(--accent),inset_0_1px_0_rgba(255,255,255,0.4)] ring-1 ring-[var(--accent)]/50 transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Play className="size-3.5 fill-white" />
          {running ? "Running…" : "Start Queue"}
        </button>
      </div>
    </div>
  );
}
