import { chromium, expect, type FullConfig, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./backoffice";
import { assertResetAllowed } from "./reset-guard";

/**
 * Runs once before the specs: takes every banner and zone off the sample site, so that a rerun on a database that
 * an earlier run used starts from the same state as a fresh one (the specs create zones and banners of their own
 * and expect to be the only ones). Counted statistics stay; the specs read their rows by banner name.
 * The sample site holds invented data only.
 */
export default async function globalReset(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0].use.baseURL;
  // Also checked when the configuration loads, which is before the sample site is started; kept here too, so the
  // reset can never run without it.
  assertResetAllowed(baseURL);
  const browser = await chromium.launch();
  try {
    const page = await (await browser.newContext({ baseURL })).newPage();
    await loginAsAdmin(page);
    await removeAll(page, "banners", "Banners", /No banners match/, "Banner deleted");
    await removeAll(page, "zones", "Zones", /No zones yet/, "Zone deleted");
  } finally {
    await browser.close();
  }
}

/** Opens the first row of the list, deletes it, and repeats until the list has no rows left. */
async function removeAll(page: Page, view: string, heading: string, emptyText: RegExp, deletedToast: string): Promise<void> {
  for (let guard = 0; guard < 200; guard++) {
    await openBannersView(page, view);
    await expect(page.getByRole("heading", { name: heading }).first()).toBeVisible();
    // The list shows a loader first, then either its rows or its empty message: wait for one of the two.
    const rows = page.getByRole("row").filter({ has: page.getByRole("link") });
    await expect(rows.first().or(page.getByText(emptyText))).toBeVisible();
    if ((await rows.count()) === 0) {
      return;
    }

    await rows.first().getByRole("link").first().click();
    const unpublish = page.getByRole("button", { name: "Unpublish" });
    if ((await unpublish.count()) > 0) {
      await unpublish.click();
      await expect(page.getByRole("alert").filter({ hasText: "Banner unpublished" })).toBeVisible();
    }

    await page.getByRole("button", { name: "Delete" }).click();
    await page.locator("umb-backoffice-modal-container").getByRole("button", { name: "Delete" }).click();
    await expect(page.getByRole("alert").filter({ hasText: deletedToast })).toBeVisible();
  }

  throw new Error(`The ${view} list still has rows after 200 deletions.`);
}
