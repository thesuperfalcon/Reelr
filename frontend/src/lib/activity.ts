import type { ActivityItem } from "./types";

const KNOWN_TYPES = new Set(["watched", "reviewed", "listCreated", "listAdded", "watchlistAdded"]);

// The API may send kinds this client does not know yet; those are skipped instead of shown broken.
export function isKnownActivity(item: ActivityItem): boolean {
  return KNOWN_TYPES.has(item.type);
}
