import { afterEach, describe, expect, it } from "vitest";
import { applyTheme, currentTheme } from "./theme";

describe("theme", () => {
  afterEach(() => {
    delete document.documentElement.dataset.theme;
    localStorage.clear();
  });

  it("is Salon when nothing has been chosen", () => {
    expect(currentTheme()).toBe("salon");
  });

  it("applies and remembers a chosen theme", () => {
    applyTheme("noir");

    expect(document.documentElement.dataset.theme).toBe("noir");
    expect(localStorage.getItem("reelr.theme")).toBe("noir");
    expect(currentTheme()).toBe("noir");
  });

  it("goes back to Salon without leaving anything behind", () => {
    applyTheme("broadsheet");
    applyTheme("salon");

    expect(document.documentElement.dataset.theme).toBeUndefined();
    expect(localStorage.getItem("reelr.theme")).toBeNull();
  });
});
