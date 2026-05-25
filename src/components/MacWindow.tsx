import type { ReactNode } from "react";

interface MacWindowProps {
  title: string;
  badge?: string;
  children: ReactNode;
}

export function MacWindow({ title, badge, children }: MacWindowProps) {
  return (
    <div className="relative mx-auto w-full max-w-6xl">
      <div className="glass overflow-hidden rounded-[20px]">
        {/* Titlebar */}
        <div className="relative flex h-12 shrink-0 select-none items-center px-4">
          <div className="absolute inset-x-0 top-0 h-px bg-white/60" />
          <div className="flex w-24 items-center gap-2">
            <div className="tl" style={{ background: "var(--tl-red)" }} />
            <div className="tl" style={{ background: "var(--tl-yellow)" }} />
            <div className="tl" style={{ background: "var(--tl-green)" }} />
          </div>
          <div className="flex flex-1 items-center justify-center gap-2">
            <span className="text-[13px] font-semibold tracking-tight text-foreground/80">
              {title}
            </span>
            {badge ? (
              <span className="glass-soft rounded-full px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-foreground/70">
                {badge}
              </span>
            ) : null}
          </div>
          <div className="w-24" />
        </div>

        {/* Body */}
        <div className="relative">{children}</div>
      </div>
    </div>
  );
}
