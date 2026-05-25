import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { MacWindow } from "@/components/MacWindow";
import { DropZone } from "@/components/DropZone";
import { QueueRow } from "@/components/QueueRow";
import { InspectorDrawer } from "@/components/InspectorDrawer";
import { BottomBar } from "@/components/BottomBar";
import { SessionLog } from "@/components/SessionLog";
import { EmptyState } from "@/components/EmptyState";
import { useConvertQueue } from "@/hooks/useConvertQueue";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "kmb file tools — Liquid Glass file converter" },
      {
        name: "description",
        content:
          "Local browser converter for PDF and images with a macOS 26 liquid-glass interface. No uploads, no telemetry.",
      },
      { property: "og:title", content: "kmb file tools" },
      {
        property: "og:description",
        content: "Browser preview of the kmb file tools desktop converter.",
      },
    ],
  }),
  component: Index,
});

function Index() {
  const {
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
  } = useConvertQueue();
  const [logOpen, setLogOpen] = useState(false);

  const supportedItems = useMemo(
    () => items.filter((i) => i.category === "pdf" || i.category === "image"),
    [items],
  );
  const desktopItems = useMemo(
    () => items.filter((i) => i.status === "DesktopRequired"),
    [items],
  );
  const otherItems = useMemo(
    () => items.filter((i) => i.category === "unsupported"),
    [items],
  );

  const canStart = supportedItems.some(
    (i) => i.status === "Ready" || i.status === "Queued" || i.status === "Cancelled" || i.status === "Failed",
  );

  return (
    <>
      <div className="kmb-wallpaper" aria-hidden />
      <main className="flex min-h-screen items-center justify-center p-4 md:p-8 lg:p-12">
        <MacWindow title="kmb file tools" badge="Browser Preview">
          <div className="flex h-[78vh] max-h-[820px] min-h-[560px]">
            {/* Main column */}
            <div className="flex min-w-0 flex-1 flex-col">
              <DropZone onFiles={addFiles} />

              <div className="kmb-scroll flex-1 overflow-y-auto px-6 pb-6">
                {items.length === 0 ? (
                  <EmptyState />
                ) : (
                  <div className="space-y-6">
                    {supportedItems.length > 0 && (
                      <Section
                        title="Queue"
                        action={
                          <button
                            onClick={clear}
                            className="text-[11px] font-medium text-foreground/55 transition hover:text-foreground"
                          >
                            Clear all
                          </button>
                        }
                      >
                        <div className="space-y-1.5">
                          {supportedItems.map((it) => (
                            <QueueRow key={it.id} item={it} onRemove={removeItem} />
                          ))}
                        </div>
                      </Section>
                    )}

                    {desktopItems.length > 0 && (
                      <Section title="Desktop required">
                        <div className="space-y-1.5">
                          {desktopItems.map((it) => (
                            <QueueRow key={it.id} item={it} onRemove={removeItem} />
                          ))}
                        </div>
                      </Section>
                    )}

                    {otherItems.length > 0 && (
                      <Section title="Unsupported">
                        <div className="space-y-1.5">
                          {otherItems.map((it) => (
                            <QueueRow key={it.id} item={it} onRemove={removeItem} />
                          ))}
                        </div>
                      </Section>
                    )}
                  </div>
                )}
              </div>

              <SessionLog logs={logs} open={logOpen} />
              <BottomBar
                done={counts.done}
                total={counts.total}
                running={running}
                canStart={canStart}
                onStart={start}
                onCancel={cancel}
                onToggleLog={() => setLogOpen((v) => !v)}
              />
            </div>

            {/* Inspector */}
            <InspectorDrawer settings={settings} onChange={setSettings} />
          </div>
        </MacWindow>

        <p className="mt-4 hidden text-center text-[11px] text-foreground/55 md:block">
          All processing happens in your browser. No files leave this tab.
        </p>
      </main>
    </>
  );
}

function Section({
  title,
  action,
  children,
}: {
  title: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section>
      <div className="mb-2 flex items-center justify-between px-1">
        <h3 className="text-[10px] font-semibold uppercase tracking-widest text-foreground/45">
          {title}
        </h3>
        {action}
      </div>
      {children}
    </section>
  );
}
