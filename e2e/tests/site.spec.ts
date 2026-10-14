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

  for (const path of ["/", "/privacy", "/terms", "/accessibility", "/contact"]) {
    const response = await request.get(path);
    expect(response.headers()["x-robots-tag"], path).toBeUndefined();
  }
});

test("the web app manifest is served as one, with icons that exist", async ({ request }) => {
  const response = await request.get("/manifest.webmanifest");
  expect(response.headers()["content-type"]).toContain("application/manifest+json");

  const manifest = (await response.json()) as { icons: { src: string }[] };
  for (const icon of [...manifest.icons.map((i) => i.src), "/apple-touch-icon.png", "/favicon.ico", "/favicon.svg"]) {
    const file = await request.get(icon);
    expect(file.headers()["content-type"], icon).toMatch(/^image\//);
  }
});

test("the link preview image named in the page exists", async ({ request }) => {
  const html = await (await request.get("/")).text();
  const image = /<meta property="og:image" content="https:\/\/[^/"]+(\/[^"]+)"/.exec(html)?.[1];
  expect(image).toBeDefined();

  const response = await request.get(image!);
  expect(response.headers()["content-type"]).toBe("image/png");
});

test("the sitemap lists only pages that may be indexed", async ({ request }) => {
  const response = await request.get("/sitemap.xml");
  expect(response.headers()["content-type"]).toContain("xml");

  const paths = [...(await response.text()).matchAll(/<loc>https:\/\/[^/<]+(\/[^<]*)<\/loc>/g)].map((m) => m[1]);
  expect(paths).toContain("/");
  for (const path of paths) {
    const page = await request.get(path);
    expect(page.headers()["x-robots-tag"], path).toBeUndefined();
  }
});
