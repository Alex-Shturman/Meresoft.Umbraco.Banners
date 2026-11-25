import { devices, expect, test, type Browser, type BrowserContext, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./support/backoffice";

/**
 * User story 3 on the sample site: viewable impressions, clicks and the report (quickstart scenarios 4 (click), 7, 8, 10 and 11).
 * All data is invented. The sample site must start with an empty banners database. It uses the zone `long-bottom`
 * (below the fold of the long page) and one banner. The browser really runs the package script; what it reports is
 * captured at the network, so timing rules are checked without waiting for the flush job.
 */
test.describe.configure({ mode: "serial" });

const STORED_URL = "https://example.org/long-offer";
const BANNER_NAME = "Long page promo";
const IMAGE_NAME = "Invented sample banner image";

let admin: Page;
/** Every line the package script posted to the seen endpoint, over all visitors of this run. */
const reported: string[] = [];
/** Clicks that the click endpoint must have counted. */
let clicksCounted = 0;

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

/** Reloads the view until the text is visible: the flush job writes in the background. */
async function expectEventually(view: string, text: string | RegExp, timeoutMs = 120_000): Promise<void> {
  const stop = Date.now() + timeoutMs;
  for (;;) {
    await openBannersView(admin, view);
    const shown = await admin
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
    await admin.waitForTimeout(3_000);
  }
}

/** A visitor: a browser context whose reports to the seen endpoint are recorded. */
async function visitor(browser: Browser, javaScriptEnabled = true): Promise<{ context: BrowserContext; page: Page; own: string[] }> {
  const context = await browser.newContext({ ...devices["Desktop Chrome"], javaScriptEnabled });
  const own: string[] = [];
  await context.route("**/_banners/seen", async (route) => {
    const lines = (route.request().postData() ?? "").split("\n").filter((line) => line.length > 0);
    own.push(...lines);
    reported.push(...lines);
    await route.continue();
  });
  await context.route("https://example.org/**", (route) => route.fulfill({ status: 200, contentType: "text/html", body: "<h1>Invented target</h1>" }));
  return { context, page: await context.newPage(), own };
}

/** Pretends the tab is hidden or visible, the way a browser does when the person switches tabs. */
async function setVisibility(page: Page, state: "hidden" | "visible"): Promise<void> {
  await page.evaluate((value) => {
    Object.defineProperty(document, "visibilityState", { configurable: true, get: () => value });
    document.dispatchEvent(new Event("visibilitychange"));
  }, state);
}

test("setup: the long page's zone and a published banner", async ({ request }) => {
  test.setTimeout(240_000);
  expect((await request.get("/long")).ok()).toBeTruthy();
  await expectEventually("zones", "long-bottom");

  await admin.getByRole("button", { name: "Create zone long-bottom" }).click();
  await admin.getByLabel("Name", { exact: true }).fill("Long page, bottom");
  await admin.getByLabel("Banners shown at once").fill("1");
  await admin.getByLabel("Image width").fill("728");
  await admin.getByLabel("Image height").fill("90");
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Zone saved")).toBeVisible();

  await openBannersView(admin, "banners");
  await admin.getByRole("button", { name: "Create banner" }).click();
  await admin.getByLabel("Name", { exact: true }).fill(BANNER_NAME);
  await admin.getByRole("button", { name: "Choose", exact: true }).click();
  const picker = admin.locator("umb-backoffice-modal-container");
  await picker.locator("uui-card-media").filter({ hasText: IMAGE_NAME.slice(0, 15) }).first().click();
  await picker.getByRole("button", { name: "Choose", exact: true }).click();
  await expect(admin.getByRole("link", { name: IMAGE_NAME })).toBeVisible();
  await expect(admin.locator("uui-modal-sidebar")).toHaveCount(0);
  await admin.getByLabel("Link").fill(STORED_URL);
  await admin.getByLabel("Alternative text").fill("Invented long page promo");
  await admin.locator("uui-checkbox", { hasText: "Long page, bottom" }).click();
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await admin.getByRole("button", { name: "Publish" }).click();
  await expect(admin.getByText("Published", { exact: true })).toBeVisible();

  await expect
    .poll(async () => (await (await request.get("/long")).text()).includes("data-mbz-token"), { timeout: 10_000, intervals: [250] })
    .toBeTruthy();
});

test("a banner below the fold that is never scrolled to counts 0", async ({ browser }) => {
  const { context, page, own } = await visitor(browser);
  await page.goto("/long");
  await expect(page.locator("[data-mbb]")).toBeAttached();
  await page.waitForTimeout(3_500);
  await page.close();
  await context.close();

  expect(own).toHaveLength(0);
});

test("scrolled into view at 50% for one second counts exactly 1, however often it scrolls in and out", async ({ browser }) => {
  const { context, page, own } = await visitor(browser);
  await page.goto("/long");
  const banner = page.locator("[data-mbb]");

  await banner.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1_500);
  for (let i = 0; i < 3; i++) {
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(200);
    await banner.scrollIntoViewIfNeeded();
    await page.waitForTimeout(1_300);
  }

  await expect.poll(() => own.length, { timeout: 6_000 }).toBe(1);
  await page.waitForTimeout(2_500);
  expect(own).toHaveLength(1);
  expect(own[0]).toMatch(/^\S+ \d+$/);
  await context.close();
});

test("a hidden tab resets the timer", async ({ browser }) => {
  const { context, page, own } = await visitor(browser);
  await page.goto("/long");
  await page.locator("[data-mbb]").scrollIntoViewIfNeeded();

  // 500 ms visible, hidden, 300 ms pause, visible again: only 600 ms of a fresh timer, then hidden flushes the queue.
  await page.waitForTimeout(500);
  await setVisibility(page, "hidden");
  await page.waitForTimeout(300);
  await setVisibility(page, "visible");
  await page.waitForTimeout(600);
  await setVisibility(page, "hidden");
  await page.waitForTimeout(500);
  expect(own, "without a reset the first 500 ms and the next 600 ms would have been enough").toHaveLength(0);

  await setVisibility(page, "visible");
  await page.waitForTimeout(1_400);
  await setVisibility(page, "hidden");
  await expect.poll(() => own.length, { timeout: 5_000 }).toBe(1);
  await context.close();
});

test("with scripts off the banner shows and its link works, and nothing is counted", async ({ browser, request }) => {
  const { context, page, own } = await visitor(browser, false);
  await page.goto("/long");
  const link = page.locator("[data-mbb] a");
  await expect(link).toBeVisible();
  const href = (await link.getAttribute("href")) ?? "";
  expect(href).toMatch(/^\/_banners\/c\/\d+\?z=\d+$/);

  const response = await request.get(href, { maxRedirects: 0 });
  expect(response.status()).toBe(302);
  expect(response.headers()["location"]).toBe(STORED_URL);
  clicksCounted++;

  await page.waitForTimeout(2_500);
  expect(own).toHaveLength(0);
  await context.close();
});

test("a click redirects to the stored link", async ({ browser }) => {
  const { context, page } = await visitor(browser);
  await page.goto("/long");
  const link = page.locator("[data-mbb] a");
  await link.scrollIntoViewIfNeeded();
  await link.click();
  await expect(page).toHaveURL(STORED_URL);
  clicksCounted++;
  await context.close();
});

test("a click address cannot be used as an open redirect", async ({ request }) => {
  const response = await request.get("/_banners/c/99999?url=https://evil.example", { maxRedirects: 0 });

  expect(response.status()).toBe(404);
  expect(response.headers()["location"]).toBeUndefined();
});

test("scenario 10: the report and the CSV match what was counted", async () => {
  test.setTimeout(180_000);
  const impressions = reported.length;
  expect(impressions).toBe(2);

  await openBannersView(admin, "reports");
  await admin.getByRole("button", { name: "Today" }).click();
  const row = admin.getByRole("row", { name: new RegExp(BANNER_NAME) });
  const stop = Date.now() + 90_000;
  for (;;) {
    const text = (await row.innerText({ timeout: 2_000 }).catch(() => "")).replace(/\s+/g, " ");
    if (text.includes(`long-bottom ${impressions} ${clicksCounted}`)) {
      break;
    }
    if (Date.now() > stop) {
      throw new Error(`The report row never showed ${impressions} impressions and ${clicksCounted} clicks. Last seen: "${text}"`);
    }
    await admin.waitForTimeout(3_000);
    await admin.getByRole("button", { name: "Today" }).click();
  }

  // The totals cover everything counted today, also by banners of earlier runs on this database (deleted banners keep
  // their statistics), so they are read for this banner only.
  await admin.locator('uui-select[label^="Banner"]').locator("select").selectOption({ label: BANNER_NAME });
  await expect(admin.locator('[data-total="impressions"]')).toHaveText(String(impressions));
  await expect(admin.locator('[data-total="clicks"]')).toHaveText(String(clicksCounted));
  await expect(admin.locator('[data-total="ctr"]')).toHaveText("100.0 %");

  const download = admin.waitForEvent("download");
  await admin.getByRole("button", { name: "Download CSV" }).click();
  const file = await (await download).createReadStream();
  const chunks: Buffer[] = [];
  for await (const chunk of file) {
    chunks.push(chunk as Buffer);
  }
  const csv = Buffer.concat(chunks).toString("utf8");
  expect(csv.split("\r\n")[0]).toBe("date,zone,banner,impressions,clicks");
  expect(csv).toContain(`,long-bottom,${BANNER_NAME},${impressions},${clicksCounted}\r\n`);
});
