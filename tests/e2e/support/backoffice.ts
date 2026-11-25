import { expect, type Page } from "@playwright/test";
import { getAdminCredentials } from "./credentials";

/** Signs in to the sample site back office as the administrator. */
export async function loginAsAdmin(page: Page): Promise<void> {
  const { email, password } = getAdminCredentials();
  await page.goto("/umbraco");
  await page.getByLabel("Email").fill(email);
  await page.locator("input[type=password]").fill(password);
  await page.getByRole("button", { name: "Login" }).click();
  await expect(page).toHaveURL(/\/umbraco\/section\//, { timeout: 60_000 });
}

/** Opens a view of the Banners section by its path, for example "zones" or "banners". */
export async function openBannersView(page: Page, path: string): Promise<void> {
  await page.goto(`/umbraco/section/meresoft-banners/${path}`);
}
