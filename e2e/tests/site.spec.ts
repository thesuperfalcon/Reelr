import { expect, test } from "@playwright/test";

// What nginx serves around the app: files for search engines and the headers that keep member pages out of them.
// These need no browser, only HTTP requests.

test("robots.txt is plain text and blocks the API", async ({ request }) => {
  const response = await request.get("/robots.txt");

  expect(response.ok()).toBe(true);
  expect(response.headers()["content-type"]).toContain("text/plain");
  expect(await response.text()).toContain("Disallow: /api/");
});

test("member pages ask not to be indexed, the pages about Reelr do not", async ({ request }) => {
  for (const path of ["/user/1", "/movie/900001", "/review/1", "/list/1", "/activity"]) {
    const response = await request.get(path);
    expect(response.headers()["x-robots-tag"], path).toBe("noindex");
  }

  for (const path of ["/", "/privacy", "/terms", "/accessibility"]) {
    const response = await request.get(path);
    expect(response.headers()["x-robots-tag"], path).toBeUndefined();
  }
});
