import { defineConfig, devices } from "@playwright/test";

// MB_SAMPLE_BASE_URL lets a second checkout run its own sample site on another port.
const baseURL = process.env.MB_SAMPLE_BASE_URL ?? "http://localhost:5100";

export default defineConfig({
  testDir: ".",
  testMatch: "**/*.spec.ts",
  fullyParallel: false,
  // The specs share one sample site and its banners database, so they run one after the other.
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL,
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command:
      `dotnet run --project ../../samples/Meresoft.Umbraco.Banners.Sample --urls ${baseURL}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
});
