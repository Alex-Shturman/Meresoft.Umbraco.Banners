import { expect, test, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./support/backoffice";

/**
 * User story 1 on the sample site (quickstart scenarios 1 to 3, 5 and 14, and scenario 4 up to the
 * home page showing the banner with a link to the stored URL). All data is invented. The sample
 * site must start with an empty banners database (no zones, no banners).
 */
test.describe.configure({ mode: "serial" });

const STORED_URL = "https://example.org/show";
const IMAGE_NAME = "Invented sample banner image";

let page: Page;

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage();
  await loginAsAdmin(page);
});

test.afterAll(async () => {
  await page.close();
});

/** A notification toast with the text (the text is also in a visually hidden live region). */
function toast(text: string) {
  return page.getByRole("alert").filter({ hasText: text });
}

/** Reloads the list until the text is visible: the flush job writes requested zones in the background. */
async function expectEventually(view: string, text: string | RegExp, timeoutMs = 120_000): Promise<void> {
  const stop = Date.now() + timeoutMs;
  for (;;) {
    await openBannersView(page, view);
    const shown = await page
      .getByText(text)
      .first()
      .waitFor({ state: "visible", timeout: 5_000 })
      .then(() => true)
      .catch(() => false);
    if (shown) {
      return;
    }
    if (Date.now() > stop) {
      throw new Error(`"${text}" never appeared in the ${view} view`);
    }
    await page.waitForTimeout(3_000);
  }
}

test("scenario 1: the section is visible and the template's zone is listed as placed but not set up", async ({ request }) => {
  test.setTimeout(180_000); // the flush job writes requested zones 30 s after start, then every 5 s
  // The home page asks for the zone; the flush job records it.
  const home = await request.get("/");
  expect(home.ok()).toBeTruthy();

  await openBannersView(page, "zones");
  await expect(page.getByRole("heading", { name: "Zones" })).toBeVisible();

  await expectEventually("zones", "home-top");
  await expect(page.getByText("Placed but not set up")).toBeVisible();
});

test("scenario 2: creating the zone from the requested list removes the entry", async () => {
  await openBannersView(page, "zones");
  await page.getByRole("button", { name: "Create zone home-top" }).click();

  await expect(page.getByLabel("Alias")).toHaveValue("home-top");
  await page.getByLabel("Name", { exact: true }).fill("Home page, top");
  await page.getByLabel("Banners shown at once").fill("1");
  await page.getByLabel("Image width").fill("728");
  await page.getByLabel("Image height").fill("90");
  await page.getByRole("button", { name: "Save" }).click();

  await expect(toast("Zone saved")).toBeVisible();
  await openBannersView(page, "zones");
  await expect(page.getByRole("row", { name: /home-top/ })).toBeVisible();
  await expect(page.getByText("Placed but not set up")).toHaveCount(0);
});

test("scenario 3: a saved draft does not show on the site", async ({ request }) => {
  await openBannersView(page, "banners");
  await page.getByRole("button", { name: "Create banner" }).click();

  await page.getByLabel("Name", { exact: true }).fill("Summer promo");
  await page.getByRole("button", { name: "Choose", exact: true }).click();
  const picker = page.locator("umb-backoffice-modal-container");
  await picker.locator("uui-card-media").filter({ hasText: IMAGE_NAME.slice(0, 15) }).first().click();
  await picker.getByRole("button", { name: "Choose", exact: true }).click();
  await expect(page.getByRole("link", { name: IMAGE_NAME })).toBeVisible(); // the image is now in the editor
  // The picker slides away for about 300 ms; text typed while it is still there is lost by the back office.
  await expect(page.locator("uui-modal-sidebar")).toHaveCount(0);
  await page.getByLabel("Link").fill(STORED_URL);
  await page.getByLabel("Alternative text").fill("Invented summer promo");
  await page.locator("uui-checkbox", { hasText: "Home page, top" }).click();
  await page.getByRole("button", { name: "Save" }).click();

  await expect(toast("Banner saved")).toBeVisible();
  await expect(page.getByText("Draft", { exact: true })).toBeVisible();
  const home = await (await request.get("/")).text();
  expect(home).not.toContain("mb-banner");
});

test("scenario 4: publishing shows the banner on the home page within 5 seconds, linking to the stored URL", async ({ request }) => {
  await page.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByText("Published", { exact: true })).toBeVisible();

  await expect
    .poll(async () => (await (await request.get("/")).text()).includes("mb-banner"), { timeout: 5_000, intervals: [250] })
    .toBeTruthy();

  const html = await (await request.get("/")).text();
  expect(html).toContain(`href="${STORED_URL}"`);
  expect(html).toContain('alt="Invented summer promo"');
});

test("scenario 5: changing the alt text and only saving keeps the published version on the site", async ({ request }) => {
  await page.getByLabel("Alternative text").fill("Changed alt text");
  await page.getByRole("button", { name: "Save" }).click();

  await expect(page.getByText("Published with unpublished changes")).toBeVisible();
  const html = await (await request.get("/")).text();
  expect(html).toContain('alt="Invented summer promo"');
  expect(html).not.toContain("Changed alt text");
});

test("scenario 14: deleting a zone with assigned banners is refused with the banner list", async () => {
  await openBannersView(page, "zones");
  await page.getByRole("row", { name: /home-top/ }).getByRole("link").first().click();
  await page.getByRole("button", { name: "Delete" }).click();
  await page.locator("umb-backoffice-modal-container").getByRole("button", { name: "Delete" }).click();

  await expect(page.getByText("Banners are still assigned to this zone")).toBeVisible();
  await expect(page.getByText("Summer promo")).toBeVisible();
});
