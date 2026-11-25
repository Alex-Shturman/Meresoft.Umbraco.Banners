import { devices, expect, test, type APIRequestContext, type Browser, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./support/backoffice";

/**
 * User story 5 on the sample site: quickstart scenario 13 (an impression limit of 3, five views, the banner stops
 * showing after the next flush and the back office says "Limit reached"), and US5 scenario 2 (raising the limit and
 * publishing shows it again). The sample site flushes every 5 seconds. All data is invented. The spec makes sure the
 * zone home-top exists and takes any banner that is already published off the site first, so it runs on a fresh
 * sample database and after the other specs.
 */
test.describe.configure({ mode: "serial" });

const IMAGE_NAME = "Invented sample banner image";
const BANNER_NAME = "Limited campaign";
const ALT = "Invented limited campaign";
const LIMIT = 3;
const VIEWS = 5;

let admin: Page;

test.beforeAll(async ({ browser }) => {
  admin = await browser.newPage();
  await loginAsAdmin(admin);
});

test.afterAll(async () => {
  await admin.close();
});

function toast(text: string) {
  return admin.getByRole("alert").filter({ hasText: text });
}

async function ensureZone(): Promise<void> {
  await openBannersView(admin, "zones");
  await expect(admin.getByRole("heading", { name: "Zones" })).toBeVisible();
  if ((await admin.getByRole("row", { name: /home-top/ }).count()) > 0) {
    return;
  }
  await admin.getByRole("button", { name: "Create zone", exact: true }).click();
  await admin.getByLabel("Alias").fill("home-top");
  await admin.getByLabel("Name", { exact: true }).fill("Home page, top");
  await admin.getByLabel("Banners shown at once").fill("1");
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Zone saved")).toBeVisible();
}

async function unpublishEverything(): Promise<void> {
  for (;;) {
    await openBannersView(admin, "banners");
    await expect(admin.getByRole("heading", { name: "Banners" }).first()).toBeVisible();
    const published = admin.getByRole("row").filter({ hasText: /Published/ });
    if ((await published.count()) === 0) {
      return;
    }
    await published.first().getByRole("link").first().click();
    await admin.getByRole("button", { name: "Unpublish" }).click();
    await expect(toast("Banner unpublished")).toBeVisible();
  }
}

async function homeShowsBanner(request: APIRequestContext): Promise<boolean> {
  return (await (await request.get("/")).text()).includes(`alt="${ALT}"`);
}

/** One visitor who loads the home page and keeps it open until the package script has reported the view. */
async function view(browser: Browser): Promise<number> {
  const context = await browser.newContext({ ...devices["Desktop Chrome"] });
  const reported: string[] = [];
  await context.route("**/_banners/seen", async (route) => {
    reported.push(...(route.request().postData() ?? "").split("\n").filter((line) => line.length > 0));
    await route.continue();
  });
  const page = await context.newPage();
  await page.goto("/");
  await expect(page.locator("[data-mbb]")).toBeAttached();
  // Visible for one second at 50 percent counts one view; the hidden tab then sends the queue at once.
  await page.waitForTimeout(1_500);
  await page.evaluate(() => {
    Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "hidden" });
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await expect.poll(() => reported.length, { timeout: 6_000 }).toBe(1);
  await context.close();
  return reported.length;
}

test("setup: the zone exists, nothing else is published, and a banner with a limit of 3 is published", async ({ request }) => {
  test.setTimeout(240_000);
  await ensureZone();
  await unpublishEverything();

  await openBannersView(admin, "banners");
  await admin.getByRole("button", { name: "Create banner" }).click();
  await admin.getByLabel("Name", { exact: true }).fill(BANNER_NAME);
  await admin.getByRole("button", { name: "Choose", exact: true }).click();
  const picker = admin.locator("umb-backoffice-modal-container");
  await picker.locator("uui-card-media").filter({ hasText: IMAGE_NAME.slice(0, 15) }).first().click();
  await picker.getByRole("button", { name: "Choose", exact: true }).click();
  await expect(admin.getByRole("link", { name: IMAGE_NAME })).toBeVisible();
  await expect(admin.locator("uui-modal-sidebar")).toHaveCount(0);
  await admin.getByLabel("Link").fill("https://example.org/limited");
  await admin.getByLabel("Alternative text").fill(ALT);

  // Both limit fields are there for an image banner (the click limit is hidden for an Html banner, FR-006).
  await expect(admin.getByLabel("Click limit")).toBeVisible();
  await admin.getByLabel("Impression limit").fill(String(LIMIT));

  await admin.locator("uui-checkbox", { hasText: "Home page, top" }).click();
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await admin.getByRole("button", { name: "Publish" }).click();
  await expect(admin.getByText("Published", { exact: true })).toBeVisible();

  await expect.poll(() => homeShowsBanner(request), { timeout: 15_000, intervals: [250] }).toBeTruthy();
});

test("an invalid limit is refused with a message at the field", async () => {
  await admin.getByLabel("Impression limit").fill("0");
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(admin.getByText("Enter a whole number of 1 or more, or leave the field empty.")).toBeVisible();
  await admin.getByLabel("Impression limit").fill("2.5");
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(admin.getByText("Enter a whole number of 1 or more, or leave the field empty.")).toBeVisible();
  await admin.getByLabel("Impression limit").fill(String(LIMIT));
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
});

test("scenario 13: five views, then the banner stops showing and the list says Limit reached", async ({ browser, request }) => {
  test.setTimeout(240_000);
  for (let i = 0; i < VIEWS; i++) {
    expect(await homeShowsBanner(request), `still shown before view ${i + 1}`).toBeTruthy();
    await view(browser);
  }

  // The flush job writes every 5 seconds and then takes the banner out of the snapshot (overshoot: one interval).
  await expect.poll(() => homeShowsBanner(request), { timeout: 60_000, intervals: [1_000] }).toBeFalsy();

  await openBannersView(admin, "banners");
  const row = admin.getByRole("row").filter({ hasText: BANNER_NAME });
  await expect(row.getByText("Limit reached")).toBeVisible();
});

test("US5 scenario 2: raising the limit and publishing shows the banner again", async ({ request }) => {
  await openBannersView(admin, "banners");
  await admin.getByRole("row").filter({ hasText: BANNER_NAME }).getByRole("link").first().click();
  await expect(admin.getByLabel("Impression limit")).toHaveValue(String(LIMIT));
  await admin.getByLabel("Impression limit").fill("100");
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await admin.getByRole("button", { name: "Publish", exact: true }).click();
  await expect(toast("Banner published")).toBeVisible();

  await expect.poll(() => homeShowsBanner(request), { timeout: 15_000, intervals: [250] }).toBeTruthy();
  await expect(admin.getByText("Limit reached")).toHaveCount(0);
});
