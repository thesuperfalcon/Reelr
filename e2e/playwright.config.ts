import { defineConfig, devices } from "@playwright/test";

// Runs against the whole stack started with:
//   docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml up -d --build --wait
// The site is on E2E_BASE_URL (default http://localhost:8080) and Mailpit's API on E2E_MAILPIT_URL.
export default defineConfig({
  testDir: "./tests",
  // A journey signs up members and waits for their emails, which can take longer than the 30 s default on a
  // stack that has just started.
  timeout: 120_000,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:8080",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
