import { AlertTriangle, Folder } from "lucide-react";
import type { ImageFormat, ImagesToPdfMode, JobMode } from "@/lib/types";
import type { Settings } from "@/hooks/useConvertQueue";

interface InspectorDrawerProps {
  settings: Settings;
  onChange: (next: Settings) => void;
}

const MODES: { id: JobMode; label: string; hint: string }[] = [
  { id: "pdf-to-images", label: "PDF → Images", hint: "Render each page as PNG/JPG/WebP" },
  { id: "images-to-pdf", label: "Images → PDF", hint: "Combine or convert images" },
  { id: "merge-pdfs", label: "Merge PDFs", hint: "Concatenate multiple PDFs" },
  { id: "split-pdf", label: "Split PDF", hint: "Each page becomes its own PDF" },
];

export function InspectorDrawer({ settings, onChange }: InspectorDrawerProps) {
  const set = <K extends keyof Settings>(k: K, v: Settings[K]) =>
    onChange({ ...settings, [k]: v });

  return (
    <aside className="glass-strong hidden h-full w-[300px] shrink-0 border-l border-white/40 lg:block">
      <div className="kmb-scroll h-full space-y-7 overflow-y-auto p-5">
        {/* Job mode */}
        <section>
          <h3 className="mb-2 text-[10px] font-semibold uppercase tracking-widest text-foreground/45">
            Job Mode
          </h3>
          <div className="space-y-1.5">
            {MODES.map((m) => {
              const active = settings.mode === m.id;
              return (
                <button
                  key={m.id}
                  onClick={() => set("mode", m.id)}
                  className={`flex w-full flex-col rounded-lg px-2.5 py-2 text-left transition ${
                    active
                      ? "bg-[var(--accent)]/12 ring-1 ring-[var(--accent)]/40"
                      : "hover:bg-white/60"
                  }`}
                >
                  <span
                    className={`text-[12.5px] font-medium ${active ? "text-[var(--accent)]" : "text-foreground/85"}`}
                  >
                    {m.label}
                  </span>
                  <span className="text-[11px] text-foreground/50">{m.hint}</span>
                </button>
              );
            })}
          </div>
        </section>

        {/* PDF → Images settings */}
        {settings.mode === "pdf-to-images" && (
          <section className="space-y-4">
            <h3 className="text-[10px] font-semibold uppercase tracking-widest text-foreground/45">
              Image Output
            </h3>

            <div className="space-y-1.5">
              <label className="text-[11px] font-medium text-foreground/60">Format</label>
              <div className="glass-soft flex gap-1 rounded-lg p-0.5">
                {(["png", "jpeg", "webp"] as ImageFormat[]).map((f) => {
                  const active = settings.imageFormat === f;
                  return (
                    <button
                      key={f}
                      onClick={() => set("imageFormat", f)}
                      className={`flex-1 rounded-md py-1 text-[11px] font-medium transition ${
                        active
                          ? "bg-white shadow-sm ring-1 ring-black/5 text-foreground"
                          : "text-foreground/55 hover:text-foreground"
                      }`}
                    >
                      {f.toUpperCase()}
                    </button>
                  );
                })}
              </div>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-medium text-foreground/60">DPI</label>
              <input
                type="number"
                min={72}
                max={600}
                step={10}
                value={settings.dpi}
                onChange={(e) => set("dpi", Number(e.target.value) || 200)}
                className="glass-soft w-full rounded-md px-2 py-1.5 text-[12.5px] outline-none focus:ring-2 focus:ring-[var(--accent)]/40"
              />
              {settings.dpi > 200 && (
                <p className="mt-1 flex items-start gap-1.5 text-[10.5px] text-amber-700">
                  <AlertTriangle className="mt-px size-3 shrink-0" />
                  High DPI may be slow or hit canvas memory limits.
                </p>
              )}
            </div>
          </section>
        )}

        {/* Images → PDF mode */}
        {settings.mode === "images-to-pdf" && (
          <section className="space-y-2">
            <h3 className="text-[10px] font-semibold uppercase tracking-widest text-foreground/45">
              Images → PDF Mode
            </h3>
            <div className="space-y-1.5">
              {(["merge", "per-image"] as ImagesToPdfMode[]).map((m) => {
                const active = settings.imagesToPdfMode === m;
                return (
                  <button
                    key={m}
                    onClick={() => set("imagesToPdfMode", m)}
                    className={`w-full rounded-lg px-2.5 py-2 text-left text-[12px] transition ${
                      active
                        ? "bg-[var(--accent)]/12 ring-1 ring-[var(--accent)]/40 text-[var(--accent)] font-medium"
                        : "hover:bg-white/60 text-foreground/80"
                    }`}
                  >
                    {m === "merge"
                      ? "Merge selected images → one PDF"
                      : "One PDF per image"}
                  </button>
                );
              })}
            </div>
          </section>
        )}

        {/* Dependencies */}
        <section>
          <h3 className="mb-2 text-[10px] font-semibold uppercase tracking-widest text-foreground/45">
            Dependencies
          </h3>
          <div className="space-y-1.5">
            <DepRow ok label="Browser PDF renderer" detail="pdf.js (local worker)" />
            <DepRow label="Microsoft Office" detail="Desktop build only" />
            <DepRow label="LibreOffice headless" detail="Desktop build only" />
          </div>
        </section>

        {/* Browser download behavior */}
        <section>
          <h3 className="mb-2 text-[10px] font-semibold uppercase tracking-widest text-foreground/45">
            Browser Download Behavior
          </h3>
          <div className="glass-soft rounded-lg p-2.5">
            <div className="mb-1 flex items-center gap-1.5 text-[11.5px] font-medium text-foreground/80">
              <Folder className="size-3.5 text-foreground/60" />
              Downloads via browser
            </div>
            <p className="text-[11px] leading-relaxed text-foreground/60">
              Browser builds download files or a ZIP through your browser. The desktop
              build will save outputs directly to a folder you choose.
            </p>
          </div>
        </section>
      </div>
    </aside>
  );
}

function DepRow({ ok, label, detail }: { ok?: boolean; label: string; detail?: string }) {
  return (
    <div className="flex items-center gap-2">
      <div
        className={`size-1.5 shrink-0 rounded-full ${
          ok ? "bg-emerald-500 shadow-[0_0_6px_rgba(16,185,129,0.55)]" : "bg-zinc-300"
        }`}
      />
      <div className="min-w-0 flex-1">
        <div className={`text-[11.5px] ${ok ? "text-foreground/80" : "text-foreground/55"}`}>
          {label}
        </div>
        {detail && (
          <div className="text-[10px] text-foreground/45">{detail}</div>
        )}
      </div>
    </div>
  );
}
