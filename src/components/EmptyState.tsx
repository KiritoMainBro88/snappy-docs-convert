import { Inbox } from "lucide-react";

export function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-10 text-center">
      <div className="glass mb-3 grid size-12 place-items-center rounded-2xl">
        <Inbox className="size-5 text-foreground/55" />
      </div>
      <h3 className="text-[13px] font-medium text-foreground/80">No files yet</h3>
      <p className="mt-1 text-[11.5px] text-foreground/55">
        Drop files above to begin. Supported: PDF, PNG, JPG, WebP, GIF, BMP.
      </p>
    </div>
  );
}
