import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { signUp, uniqueName } from "./accounts";

// The axe WCAG rules on the main pages in a real browser, so colour contrast is checked too (the Vitest
// checks in frontend/src/test cannot). Every look from Settings is checked, since each has its own colours.
// The /accessibility page promises these checks; keep it in step.

const looks = ["salon", "broadsheet", "marquee", "video-store", "noir"];

async function useLook(page: Page, look: string) {
  await page.addInitScript((value) => {
    if (value === "salon") localStorage.removeItem("reelr.theme");
    else localStorage.setItem("reelr.theme", value);
  }, look);
}

async function expectNoAxeViolations(page: Page) {
  // Wait for lazy pages and their data, so axe sees the finished page rather than a loading state.
  await page.waitForLoadState("networkidle");
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(
    results.violations.map((v) => `${v.id} on ${page.url()}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`),
  ).toEqual([]);
}

const publicPages = ["/", "/login", "/register", "/forgot-password", "/search?q=test", "/movie/900001", "/privacy", "/accessibility", "/contact", "/no-such-page"];

for (const look of looks) {
  test(`public pages pass axe in the ${look} look`, async ({ page }) => {
    await useLook(page, look);

    for (const path of publicPages) {
      await page.goto(path);
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
      await expectNoAxeViolations(page);
    }
  });
}

test("signed-in pages and the rating dialog pass axe in every look", async ({ page }) => {
  await signUp(page, uniqueName("axe"));
  await expect(page.getByRole("link", { name: /^Profile of / })).toBeVisible();

  for (const look of looks) {
    await useLook(page, look);

    for (const path of ["/", "/profile", "/settings", "/activity"]) {
      await page.goto(path);
      await expectNoAxeViolations(page);
    }

    await page.goto("/movie/900001");
    await page.getByRole("button", { name: /^Rate The Test Pattern/ }).click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await expectNoAxeViolations(page);
  }
});
