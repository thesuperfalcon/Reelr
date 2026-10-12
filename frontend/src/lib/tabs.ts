import type { KeyboardEvent } from "react";

// Arrow keys, Home and End move between the tabs of a tablist and select the tab they land on, as the
// ARIA tabs pattern expects. Only the selected tab is in the Tab order (tabIndex 0, the others -1), so
// Tab moves on to the panel instead of through every tab.
export function moveBetweenTabs(event: KeyboardEvent<HTMLElement>) {
  const tabs = [...event.currentTarget.querySelectorAll<HTMLElement>('[role="tab"]')];
  const current = tabs.indexOf(event.target as HTMLElement);

  if (current < 0) {
    return;
  }

  const next =
    event.key === "ArrowRight" ? (current + 1) % tabs.length
    : event.key === "ArrowLeft" ? (current - 1 + tabs.length) % tabs.length
    : event.key === "Home" ? 0
    : event.key === "End" ? tabs.length - 1
    : null;

  if (next === null) {
    return;
  }

  event.preventDefault();
  tabs[next].focus();
  tabs[next].click();
}
