import { expect, test, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./support/backoffice";

/**
 * User story 2 on the sample site: quickstart scenario 6 (weights 1 and 3, the second shows more often) and
 * banners that never show because their window has not started or has ended. The back office runs in the
 * Asia/Tokyo time zone, so a time typed there only becomes the right UTC moment if the editor converts it.
 * All data is invented. The spec makes sure the zone home-top exists and takes any banner that is already
 * published off the site first, so it runs on a fresh sample database and after the US1 spec.
 */
test.describe.configure({ mode: "serial" });

const IMAGE_NAME = "Invented sample banner image";
const TIME_ZONE = "Asia/Tokyo";
const VIEWS = 120;

let page: Page;

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage({ timezoneId: TIME_ZONE });
  await loginAsAdmin(page);
});

test.afterAll(async () => {
  await page.close();
});

function toast(text: string) {
  return page.getByRole("alert").filter({ hasText: text });
}

/** The current time plus an offset, as a datetime-local value in the back office time zone. */
function localTime(offsetMinutes: number): string {
  const moment = new Date(Date.now() + offsetMinutes * 60_000);
  return moment.toLocaleString("sv-SE", { timeZone: TIME_ZONE }).replace(" ", "T").slice(0, 16);
}

async function ensureZone(): Promise<void> {
  await openBannersView(page, "zones");
  await expect(page.getByRole("heading", { name: "Zones" })).toBeVisible();
  if ((await page.getByRole("row", { name: /home-top/ }).count()) > 0) {
    return;
  }
  await page.getByRole("button", { name: "Create zone", exact: true }).click();
  await page.getByLabel("Alias").fill("home-top");
  await page.getByLabel("Name", { exact: true }).fill("Home page, top");
  await page.getByLabel("Banners shown at once").fill("1");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(toast("Zone saved")).toBeVisible();
}

async function unpublishEverything(): Promise<void> {
  for (;;) {
    await openBannersView(page, "banners");
    await expect(page.getByRole("heading", { name: "Banners" }).first()).toBeVisible();
    const published = page.getByRole("row").filter({ hasText: /Published/ });
    if ((await published.count()) === 0) {
      return;
    }
    await published.first().getByRole("link").first().click();
    await page.getByRole("button", { name: "Unpublish" }).click();
    await expect(toast("Banner unpublished")).toBeVisible();
  }
}

interface NewBanner {
  name: string;
  alt: string;
  weight: number;
  from?: string;
  until?: string;
}

async function createAndPublish(banner: NewBanner): Promise<void> {
  await openBannersView(page, "banners");
  await page.getByRole("button", { name: "Create banner" }).click();
  await page.getByLabel("Name", { exact: true }).fill(banner.name);
  await page.getByRole("button", { name: "Choose", exact: true }).click();
  const picker = page.locator("umb-backoffice-modal-container");
  await picker.locator("uui-card-media").filter({ hasText: IMAGE_NAME.slice(0, 15) }).first().click();
  await picker.getByRole("button", { name: "Choose", exact: true }).click();
  await expect(page.getByRole("link", { name: IMAGE_NAME })).toBeVisible();
  // The picker slides away for about 300 ms; text typed while it is still there is lost by the back office.
  await expect(page.locator("uui-modal-sidebar")).toHaveCount(0);
  await page.getByLabel("Link").fill("https://example.org/rotation");
  await page.getByLabel("Alternative text").fill(banner.alt);
  await page.getByLabel("Weight").fill(String(banner.weight));
  if (banner.from) {
    await page.getByLabel("Show from").fill(banner.from);
  }
  if (banner.until) {
    await page.getByLabel("Show until").fill(banner.until);
  }
  await page.locator("uui-checkbox", { hasText: "Home page, top" }).click();
  await page.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await page.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByText("Published", { exact: true })).toBeVisible();
}

/** Loads the home page many times and counts how often each invented alt text was in the markup. */
async function countViews(request: import("@playwright/test").APIRequestContext, alts: string[]): Promise<Record<string, number>> {
  const counts: Record<string, number> = Object.fromEntries(alts.map((a) => [a, 0]));
  for (let i = 0; i < VIEWS; i++) {
    const html = await (await request.get("/")).text();
    for (const alt of alts) {
      if (html.includes(`alt="${alt}"`)) {
        counts[alt]++;
      }
    }
  }
  return counts;
}

async function waitForSite(request: import("@playwright/test").APIRequestContext, alt: string): Promise<void> {
  await expect
    .poll(async () => (await (await request.get("/")).text()).includes(`alt="${alt}"`), { timeout: 15_000, intervals: [250] })
    .toBeTruthy();
}

test("setup: the zone exists and nothing else is published", async () => {
  test.setTimeout(180_000);
  await ensureZone();
  await unpublishEverything();
});

test("scenario 6: weights 1 and 3 show both banners, the second more often", async ({ request }) => {
  test.setTimeout(180_000);
  await createAndPublish({ name: "Rotation A", alt: "Invented rotation A", weight: 1 });
  await createAndPublish({ name: "Rotation B", alt: "Invented rotation B", weight: 3 });
  await waitForSite(request, "Invented rotation B");

  const counts = await countViews(request, ["Invented rotation A", "Invented rotation B"]);

  expect(counts["Invented rotation A"], "the lighter banner still shows").toBeGreaterThan(0);
  expect(counts["Invented rotation B"], "the heavier banner shows more often").toBeGreaterThan(counts["Invented rotation A"]);
  expect(counts["Invented rotation A"] + counts["Invented rotation B"], "one banner per view").toBe(VIEWS);
});

test("an expired banner never shows and is flagged Expired (a time typed in Tokyo is sent as UTC)", async ({ request }) => {
  test.setTimeout(180_000);
  await createAndPublish({ name: "Expired promo", alt: "Invented expired promo", weight: 100, until: localTime(-60) });

  // If the typed time were taken as UTC, the end would be 8 hours ahead and this banner would show.
  const counts = await countViews(request, ["Invented expired promo"]);
  expect(counts["Invented expired promo"]).toBe(0);

  await openBannersView(page, "banners");
  const row = page.getByRole("row", { name: /Expired promo/ });
  await expect(row.getByText("Expired", { exact: true })).toBeVisible();
});

test("a banner that starts in the future never shows and is flagged Scheduled", async ({ request }) => {
  test.setTimeout(180_000);
  await createAndPublish({ name: "Future promo", alt: "Invented future promo", weight: 100, from: localTime(24 * 60) });

  const counts = await countViews(request, ["Invented future promo"]);
  expect(counts["Invented future promo"]).toBe(0);

  await openBannersView(page, "banners");
  const row = page.getByRole("row", { name: /Future promo/ });
  await expect(row.getByText("Scheduled", { exact: true })).toBeVisible();
});

test("the saved times come back unchanged in the editor", async () => {
  await openBannersView(page, "banners");
  await page.getByRole("row", { name: /Future promo/ }).getByRole("link").first().click();

  const shown = await page.getByLabel("Show from").inputValue();
  expect(shown).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);
  expect(Math.abs(new Date(shown).getTime() - new Date(localTime(24 * 60)).getTime())).toBeLessThan(5 * 60_000);
});
