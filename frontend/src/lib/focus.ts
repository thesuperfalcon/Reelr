import { useEffect, useRef } from "react";

// For a button that swaps itself for an inline step, such as "Delete?" with Delete and Keep, or an edit form.
// The step's first control takes focus with autoFocus; when the step closes and the button is back, this puts
// focus on it again, so keyboard and screen reader users are not dropped at the top of the page (WCAG 2.4.3).
export function useReturnFocus<T extends HTMLElement = HTMLButtonElement>(open: boolean) {
  const ref = useRef<T>(null);
  const wasOpen = useRef(open);

  useEffect(() => {
    if (wasOpen.current && !open) {
      ref.current?.focus();
    }
    wasOpen.current = open;
  }, [open]);

  return ref;
}
