import { defineConfig, devices } from "@playwright/test";
import { getAdminCredentials } from "./support/credentials";
import { assertResetAllowed } from "./support/reset-guard";

// MB_SAMPLE_BASE_URL lets a second checkout run its own sample site on another port.
const baseURL = process.env.MB_SAMPLE_BASE_URL ?? "http://localhost:5100";

// The run deletes every banner and zone of the site first (support/global-reset.ts), so it refuses a site that is not
// on this machine. Checked here, while the configuration loads, so nothing at all starts (not the sample site either).
assertResetAllowed(baseURL);

// Playwright starts the sample site (webServer below) before the global setup reads the credentials. A run that is
// missing them, or that points at a URL where no site runs, would start a second instance of the sample site on the
// same database, and that instance takes over Umbraco's main domain: the background jobs of the first site (the flush
// job that writes requested zones and counts) stop, and the specs that wait for them time out. So fail before any start.
getAdminCredentials();

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
