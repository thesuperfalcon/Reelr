import { useId, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { ReviewText } from "./ReviewText";

interface MarkdownEditorProps {
  id: string;
  value: string;
  onChange: (value: string) => void;
  maxLength: number;
  placeholder?: string;
  /** Extra hint text, linked to the textarea with aria-describedby. */
  hint?: ReactNode;
}

type Edit = (text: string, start: number, end: number) => { from: number; to: number; insert: string; select: [number, number] };

// Wraps the selection in `marker`, or a placeholder word when nothing is selected.
function wrap(marker: string, placeholder: string): Edit {
  return (text, start, end) => {
    const selected = text.slice(start, end) || placeholder;
    const from = start + marker.length;
    return { from: start, to: end, insert: `${marker}${selected}${marker}`, select: [from, from + selected.length] };
  };
}

// Adds `prefix` to every selected line, or removes it when every line already has it.
function prefixLines(prefix: (index: number) => string, pattern: RegExp): Edit {
  return (text, start, end) => {
    const from = text.lastIndexOf("\n", start - 1) + 1;
    const lineEnd = text.indexOf("\n", end);
    const to = lineEnd === -1 ? text.length : lineEnd;
    const lines = text.slice(from, to).split("\n");
    const remove = lines.every((line) => pattern.test(line));
    const insert = lines.map((line, i) => (remove ? line.replace(pattern, "") : prefix(i) + line)).join("\n");
    return { from, to, insert, select: [from, from + insert.length] };
  };
}

const link: Edit = (text, start, end) => {
  const label = text.slice(start, end) || "link text";
  const insert = `[${label}](https://)`;
  const urlStart = start + label.length + 3;
  return { from: start, to: end, insert, select: [urlStart, urlStart + "https://".length] };
};

const actions: { id: string; label: string; shortcut?: string; edit: Edit; icon: ReactNode }[] = [
  { id: "bold", label: "Bold", shortcut: "b", edit: wrap("**", "bold text"), icon: <span className="font-bold">B</span> },
  { id: "italic", label: "Italic", shortcut: "i", edit: wrap("_", "italic text"), icon: <span className="font-serif italic">I</span> },
  {
    id: "quote",
    label: "Quote",
    edit: prefixLines(() => "> ", /^> /),
    icon: (
      <svg viewBox="0 0 24 24" fill="currentColor" className="size-4" aria-hidden="true">
        <path d="M4 18h5l2-4V6H4v8h3zm9 0h5l2-4V6h-7v8h3z" />
      </svg>
    ),
  },
  {
    id: "bullets",
    label: "Bulleted list",
    edit: prefixLines(() => "- ", /^- /),
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="size-4" aria-hidden="true">
        <path d="M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01" />
      </svg>
    ),
  },
  {
    id: "numbers",
    label: "Numbered list",
    edit: prefixLines((i) => `${i + 1}. `, /^\d+\. /),
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="size-4" aria-hidden="true">
        <path d="M10 6h10M10 12h10M10 18h10M4 4v4M3 18h3l-3 3h3" />
      </svg>
    ),
  },
  {
    id: "link",
    label: "Link",
    shortcut: "k",
    edit: link,
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="size-4" aria-hidden="true">
        <path d="M10 14a4 4 0 0 0 5.66 0l3-3a4 4 0 0 0-5.66-5.66l-1 1M14 10a4 4 0 0 0-5.66 0l-3 3a4 4 0 0 0 5.66 5.66l1-1" />
      </svg>
    ),
  },
];

const isMac = typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.platform);

export function MarkdownEditor({ id, value, onChange, maxLength, placeholder, hint }: MarkdownEditorProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [preview, setPreview] = useState(false);
  const counterId = useId();
  const hintId = useId();
  const nearLimit = value.length >= maxLength * 0.9;

  function apply(edit: Edit) {
    const textarea = textareaRef.current;
    if (!textarea) {
      return;
    }

    const { from, to, insert, select } = edit(textarea.value, textarea.selectionStart, textarea.selectionEnd);
    if (textarea.value.length - (to - from) + insert.length > maxLength) {
      return;
    }

    textarea.focus();
    textarea.setSelectionRange(from, to);
    // insertText keeps the browser's undo history; the fallback loses it but still edits.
    if (!document.execCommand("insertText", false, insert)) {
      textarea.setRangeText(insert, from, to, "end");
      onChange(textarea.value);
    }
    textarea.setSelectionRange(select[0], select[1]);
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (!(isMac ? event.metaKey : event.ctrlKey) || event.altKey || event.shiftKey) {
      return;
    }

    const action = actions.find((a) => a.shortcut === event.key.toLowerCase());
    if (action) {
      event.preventDefault();
      apply(action.edit);
    }
  }

  const modifier = isMac ? "⌘" : "Ctrl+";

  return (
    <div className="mt-2 rounded-sm bg-salon ring-1 ring-white/15 focus-within:ring-2 focus-within:ring-projector">
      <div className="flex items-center gap-1 border-b border-white/10 px-2 py-1.5">
        <div role="toolbar" aria-label="Formatting" aria-controls={id} className="flex items-center gap-0.5">
          {actions.map((action) => {
            const title = action.shortcut ? `${action.label} (${modifier}${action.shortcut.toUpperCase()})` : action.label;
            return (
              <button
                key={action.id}
                type="button"
                onClick={() => apply(action.edit)}
                disabled={preview}
                aria-label={action.label}
                title={title}
                className="inline-flex size-8 items-center justify-center rounded-sm text-sm text-haze transition hover:bg-row hover:text-screen disabled:opacity-40 disabled:hover:bg-transparent"
              >
                {action.icon}
              </button>
            );
          })}
        </div>
        <div className="ml-auto flex gap-1 text-sm">
          {(["Write", "Preview"] as const).map((mode) => {
            const pressed = (mode === "Preview") === preview;
            return (
              <button
                key={mode}
                type="button"
                aria-pressed={pressed}
                onClick={() => setPreview(mode === "Preview")}
                className={`rounded-sm px-2.5 py-1 font-medium transition ${pressed ? "bg-row-raised text-screen" : "text-haze hover:text-screen"}`}
              >
                {mode}
              </button>
            );
          })}
        </div>
      </div>

      <textarea
        ref={textareaRef}
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={onKeyDown}
        maxLength={maxLength}
        rows={8}
        placeholder={placeholder}
        aria-describedby={`${counterId}${hint ? ` ${hintId}` : ""}`}
        hidden={preview}
        className="block max-h-[50vh] min-h-40 w-full resize-y bg-transparent px-3 py-2 text-screen placeholder:text-haze/60 focus:outline-none"
      />
      {preview && (
        <div className="max-h-[50vh] min-h-40 overflow-y-auto px-3 py-2">
          {value.trim() ? <ReviewText text={value} /> : <p className="text-haze">Nothing to preview.</p>}
        </div>
      )}

      <div className="flex items-start justify-between gap-4 border-t border-white/10 px-3 py-1.5 text-xs text-haze">
        <p id={hintId}>{hint}</p>
        <p id={counterId} className={`shrink-0 tabular-nums ${nearLimit ? "text-alarm" : ""}`}>
          {value.length.toLocaleString("en")} / {maxLength.toLocaleString("en")}
          <span className="sr-only"> characters</span>
        </p>
      </div>
    </div>
  );
}
