import { defineConfig, devices } from "@playwright/test";
import { assertResetAllowed } from "./support/reset-guard";

// MB_SAMPLE_BASE_URL lets a second checkout run its own sample site on another port.
const baseURL = process.env.MB_SAMPLE_BASE_URL ?? "http://localhost:5100";

// The run deletes every banner and zone of the site first (support/global-reset.ts), so it refuses a site that is not
// on this machine. Checked here, while the configuration loads, so nothing at all starts (not the sample site either).
assertResetAllowed(baseURL);

export default defineConfig({
  testDir: ".",
  // Takes every banner and zone off the sample site first, so a rerun on a used database starts like a fresh one.
  globalSetup: "./support/global-reset.ts",
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
