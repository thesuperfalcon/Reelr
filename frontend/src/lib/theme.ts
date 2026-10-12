import { useState } from "react";

// The looks a member can pick in Settings. The colours and fonts are in index.css; public/theme.js
// applies the saved choice before the first paint and must know the same names and backgrounds.
// The choice is kept per browser, so it also works before signing in.
export type Theme = "salon" | "broadsheet" | "marquee" | "video-store" | "noir";

export const themes: { value: Theme; label: string; description: string; background: string; accent: string; text: string }[] = [
  { value: "salon", label: "Salon", description: "Dark plum with a projector-gold accent. The standard look.", background: "#1a1420", accent: "#f2b544", text: "#ede6da" },
  { value: "broadsheet", label: "Broadsheet", description: "Light and editorial, like a newspaper's film pages.", background: "#f3efe6", accent: "#9b1c14", text: "#161616" },
  { value: "marquee", label: "Marquee", description: "Near black and poster first, with a bright lime accent.", background: "#0c0c0e", accent: "#c8f031", text: "#f5f5f2" },
  { value: "video-store", label: "Video Store", description: "A light, loud eighties video shop.", background: "#fff3d4", accent: "#b8195a", text: "#1b1d4a" },
  { value: "noir", label: "Noir", description: "Black and white film noir with a single red.", background: "#0a0a0a", accent: "#ef5a4c", text: "#f2efe8" },
];

const STORAGE_KEY = "reelr.theme";

function isTheme(value: string | null): value is Theme {
  return themes.some((theme) => theme.value === value);
}

export function currentTheme(): Theme {
  const value = document.documentElement.dataset.theme ?? null;
  return isTheme(value) ? value : "salon";
}

export function applyTheme(theme: Theme) {
  const root = document.documentElement;
  if (theme === "salon") delete root.dataset.theme;
  else root.dataset.theme = theme;

  const background = themes.find((t) => t.value === theme)!.background;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", background);

  try {
    if (theme === "salon") localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, theme);
  } catch {
    // Storage can be blocked; the look then lasts until the page is reloaded.
  }
}

export function useTheme(): [Theme, (theme: Theme) => void] {
  const [theme, setTheme] = useState(currentTheme);
  return [
    theme,
    (next) => {
      applyTheme(next);
      setTheme(next);
    },
  ];
}
