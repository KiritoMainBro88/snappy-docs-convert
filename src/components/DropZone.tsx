import { useRef, useState, type DragEvent } from "react";
import { Upload } from "lucide-react";

interface DropZoneProps {
  onFiles: (files: FileList | File[]) => void;
}

export function DropZone({ onFiles }: DropZoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);

  function handleDrop(e: DragEvent) {
    e.preventDefault();
    setOver(false);
    if (e.dataTransfer?.files?.length) onFiles(e.dataTransfer.files);
  }

  return (
    <div className="px-6 pt-6 pb-4">
      <div
        onDragOver={(e) => {
          e.preventDefault();
          setOver(true);
        }}
        onDragLeave={() => setOver(false)}
        onDrop={handleDrop}
        onClick={() => inputRef.current?.click()}
        className={`group glass-soft flex cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed p-7 transition ${
          over
            ? "border-[var(--accent)] bg-white/60"
            : "border-white/60 hover:bg-white/55"
        }`}
      >
        <div className="glass mb-3 grid size-11 place-items-center rounded-xl">
          <Upload className="size-5 text-foreground/70" />
        </div>
        <h2 className="text-[15px] font-medium text-foreground">
          Add files for conversion
        </h2>
        <p className="mt-1 text-[12.5px] text-foreground/60">
          Drag and drop PDF or image files here, or click to browse
        </p>
        <input
          ref={inputRef}
          type="file"
          multiple
          className="hidden"
          accept=".pdf,.png,.jpg,.jpeg,.webp,.gif,.bmp,.doc,.docx,.ppt,.pptx,.odt,.odp,.rtf"
          onChange={(e) => {
            if (e.target.files?.length) onFiles(e.target.files);
            e.target.value = "";
          }}
        />
      </div>
    </div>
  );
}
