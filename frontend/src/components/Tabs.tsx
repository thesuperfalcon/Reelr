import { moveBetweenTabs } from "../lib/tabs";

interface Tab<T extends string> {
  id: T;
  label: string;
  /** Shown after the label, such as the number of followers. */
  count?: number;
}

interface TabsProps<T extends string> {
  /** Accessible name of the tab list, e.g. "Profile sections". */
  label: string;
  tabs: Tab<T>[];
  selected: T;
  onSelect: (id: T) => void;
  /** Element id of each tab; the panel is labelled by the selected one. */
  tabId: (id: T) => string;
  /** Element id of the panel that shows the selected tab. */
  panelId: string;
  className?: string;
}

// The row of tabs above a tab panel, following the ARIA tabs pattern: only the selected tab is in the Tab
// order, and arrow keys, Home and End move between tabs and select them. The caller renders the panel.
export function Tabs<T extends string>({ label, tabs, selected, onSelect, tabId, panelId, className = "" }: TabsProps<T>) {
  return (
    <div role="tablist" aria-label={label} onKeyDown={moveBetweenTabs} className={`tablist ${className}`}>
      {tabs.map(({ id, label: tabLabel, count }) => (
        <button
          key={id}
          type="button"
          role="tab"
          id={tabId(id)}
          aria-selected={selected === id}
          aria-controls={selected === id ? panelId : undefined}
          tabIndex={selected === id ? 0 : -1}
          onClick={() => onSelect(id)}
          className="tab"
        >
          {tabLabel}
          {/* The space keeps screen readers from reading "Followers12" as one word. */}
          {count !== undefined && (
            <>
              {" "}
              <span className="ml-0.5 text-haze tabular-nums">{count}</span>
            </>
          )}
        </button>
      ))}
    </div>
  );
}
