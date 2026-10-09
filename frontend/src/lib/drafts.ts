// Unsaved text kept per browser tab, so a review survives a closed dialog, a reload or an expired session.
// sessionStorage is cleared when the tab closes, so a draft does not stay behind on a shared computer.
const PREFIX = "reelr.draft.";

export function readDraft(key: string): string | null {
  try {
    return sessionStorage.getItem(PREFIX + key);
  } catch {
    return null;
  }
}

export function writeDraft(key: string, text: string) {
  try {
    sessionStorage.setItem(PREFIX + key, text);
  } catch {
    // Storage can be blocked or full. The text is then only kept while the dialog is open.
  }
}

export function clearDraft(key: string) {
  try {
    sessionStorage.removeItem(PREFIX + key);
  } catch {
    // Nothing was stored.
  }
}
