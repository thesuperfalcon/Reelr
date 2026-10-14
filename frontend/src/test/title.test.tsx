import { render } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { shortDescription, usePageTitle } from "../lib/title";

function Page({ title, description }: { title: string | undefined; description?: string | null }) {
  usePageTitle(title, description);
  return null;
}

function description() {
  return document.querySelector<HTMLMetaElement>('meta[name="description"]')!.content;
}

describe("page title and description", () => {
  beforeEach(() => {
    document.head.innerHTML = '<meta name="description" content="Site description">';
  });

  it("sets the page's own description and falls back to the site's", () => {
    const { rerender } = render(<Page title="Heat" description="A heist film." />);
    expect(document.title).toBe("Heat · Reelr");
    expect(description()).toBe("A heist film.");

    rerender(<Page title="Search" />);
    expect(description()).toBe("Site description");
  });

  it("leaves both alone while the page's data loads", () => {
    document.title = "Before";
    render(<Page title={undefined} description="Not yet" />);

    expect(document.title).toBe("Before");
    expect(description()).toBe("Site description");
  });

  it("shortens long texts at a word", () => {
    const text = `${"word ".repeat(40)}end`;
    const short = shortDescription(text);

    expect(short.length).toBeLessThanOrEqual(160);
    expect(short).toMatch(/word…$/);
    expect(shortDescription("  Two\n\nlines ")).toBe("Two lines");
  });
});
