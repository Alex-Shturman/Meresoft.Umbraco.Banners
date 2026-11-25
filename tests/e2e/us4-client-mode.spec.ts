import { devices, expect, test, type Browser, type BrowserContext, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./support/backoffice";

/**
 * User story 4 on the sample site: client mode for cached pages (quickstart scenario 5) and Html banners (FR-006).
 * All data is invented. The sample site must start with an empty banners database. The page /cached is output cached
 * for 60 seconds and holds the client mode zones `cached-top` (an image banner) and `cached-html` (an Html banner).
 * The page /long holds the server mode zone `long-html` (an Html banner). The browser really runs the package script;
 * what it reports is captured at the network.
 */
test.describe.configure({ mode: "serial" });

const IMAGE_NAME = "Invented sample banner image";
const IMAGE_BANNER = "Cached page promo";
const CLIENT_HTML_BANNER = "Cached html offer";
const SERVER_HTML_BANNER = "Long html offer";
const HTML_BOX = (id: string) => `<p id="${id}">Invented html offer</p><script>document.getElementById("${id}").textContent = "script ran";</script>`;

let admin: Page;
/** Every line the package script posted to the seen endpoint, over all visitors of this run. */
const reported: string[] = [];
/** How often the lines of one banner were reported, by banner id. */
const reportedFor = (id: string) => reported.filter((line) => line.endsWith(` ${id}`)).length;
/** The banner ids, read from the rendered pages. */
const ids = { image: "", clientHtml: "", serverHtml: "" };

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
async function visitor(browser: Browser, javaScriptEnabled = true): Promise<{ context: BrowserContext; page: Page; own: string[]; errors: string[] }> {
  const context = await browser.newContext({ ...devices["Desktop Chrome"], javaScriptEnabled });
  const own: string[] = [];
  const errors: string[] = [];
  await context.route("**/_banners/seen", async (route) => {
    const lines = (route.request().postData() ?? "").split("\n").filter((line) => line.length > 0);
    own.push(...lines);
    reported.push(...lines);
    await route.continue();
  });
  const page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  return { context, page, own, errors };
}

async function createClientZone(alias: string, name: string, mode: "Server" | "Client"): Promise<void> {
  await admin.getByRole("button", { name: `Create zone ${alias}` }).click();
  await admin.getByLabel("Name", { exact: true }).fill(name);
  await admin.getByLabel("Banners shown at once").fill("1");
  await admin.getByLabel("Image width").fill("728");
  await admin.getByLabel("Image height").fill("90");
  await admin.locator("uui-radio", { hasText: new RegExp(`^\\s*${mode}`) }).click();
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Zone saved")).toBeVisible();
}

async function publishHtmlBanner(name: string, zoneName: string, html: string): Promise<void> {
  await openBannersView(admin, "banners");
  await admin.getByRole("button", { name: "Create banner" }).click();
  await admin.getByLabel("Name", { exact: true }).fill(name);
  await admin.locator("uui-radio", { hasText: "HTML" }).click();
  await admin.getByLabel("HTML code").fill(html);
  await admin.locator("uui-checkbox", { hasText: zoneName }).click();
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await admin.getByRole("button", { name: "Publish" }).click();
  await expect(admin.getByText("Published", { exact: true })).toBeVisible();
}

test("setup: client mode zones for the cached page, a server mode zone for the long page, and published banners", async ({ request }) => {
  test.setTimeout(300_000);
  expect((await request.get("/cached")).ok()).toBeTruthy();
  expect((await request.get("/long")).ok()).toBeTruthy();
  await expectEventually("zones", "cached-top");
  await expectEventually("zones", "cached-html");
  await expectEventually("zones", "long-html");

  await createClientZone("cached-top", "Cached page, top", "Client");
  await openBannersView(admin, "zones");
  await createClientZone("cached-html", "Cached page, html", "Client");
  await openBannersView(admin, "zones");
  await createClientZone("long-html", "Long page, html", "Server");

  await openBannersView(admin, "banners");
  await admin.getByRole("button", { name: "Create banner" }).click();
  await admin.getByLabel("Name", { exact: true }).fill(IMAGE_BANNER);
  await admin.getByRole("button", { name: "Choose", exact: true }).click();
  const picker = admin.locator("umb-backoffice-modal-container");
  await picker.locator("uui-card-media").filter({ hasText: IMAGE_NAME.slice(0, 15) }).first().click();
  await picker.getByRole("button", { name: "Choose", exact: true }).click();
  await expect(admin.getByRole("link", { name: IMAGE_NAME })).toBeVisible();
  await expect(admin.locator("uui-modal-sidebar")).toHaveCount(0);
  await admin.getByLabel("Link").fill("https://example.org/cached-offer");
  await admin.getByLabel("Alternative text").fill("Invented cached page promo");
  await admin.locator("uui-checkbox", { hasText: "Cached page, top" }).click();
  await admin.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await admin.getByRole("button", { name: "Publish" }).click();
  await expect(admin.getByText("Published", { exact: true })).toBeVisible();

  await publishHtmlBanner(CLIENT_HTML_BANNER, "Cached page, html", HTML_BOX("client-html"));
  await publishHtmlBanner(SERVER_HTML_BANNER, "Long page, html", HTML_BOX("server-html"));

  // The long page is not cached: the server mode banner shows at once. The cached page keeps its old markup
  // (written before the zones existed) for up to 60 seconds, then holds the client mode placeholders.
  await expect
    .poll(async () => (await (await request.get("/long")).text()).includes("data-mbz-token"), { timeout: 10_000, intervals: [250] })
    .toBeTruthy();
  await expect
    .poll(async () => (await (await request.get("/cached")).text()).includes('data-mbz-mode="client"'), { timeout: 90_000, intervals: [2_000] })
    .toBeTruthy();
});

test("the cached page holds a placeholder and no banner, token or chosen content", async ({ request }) => {
  const html = await (await request.get("/cached")).text();

  expect(html).toContain('data-mbz="cached-top"');
  expect(html).toContain('data-mbz-mode="client"');
  expect(html).not.toContain("data-mbz-token");
  expect(html).not.toContain("data-mbb");
});

test("scenario 1: two visitors on the same cached page each get a banner, and each impression counts", async ({ browser }) => {
  const first = await visitor(browser);
  const second = await visitor(browser);
  const tokens = new Set<string>();

  for (const { page } of [first, second]) {
    page.on("response", async (response) => {
      if (response.url().includes("/_banners/zone/cached-top")) {
        tokens.add((await response.json()).token as string);
      }
    });
    await page.goto("/cached");
    const banner = page.locator('[data-mbz="cached-top"] [data-mbb]');
    await expect(banner.locator("a")).toBeVisible();
    expect(await banner.locator("a").getAttribute("href")).toMatch(/^\/_banners\/c\/\d+\?z=\d+$/);
    ids.image = (await banner.getAttribute("data-mbb")) ?? "";
    ids.clientHtml = (await page.locator('[data-mbz="cached-html"] [data-mbb]').getAttribute("data-mbb")) ?? "";
  }

  // The page holds two zones, so a visitor reports the image banner and the Html banner, each once.
  await expect.poll(() => first.own.filter((l) => l.endsWith(` ${ids.image}`)).length, { timeout: 8_000 }).toBe(1);
  await expect.poll(() => second.own.filter((l) => l.endsWith(` ${ids.image}`)).length, { timeout: 8_000 }).toBe(1);
  expect(tokens.size, "every visitor got a token of their own").toBe(2);
  const tokenOf = (own: string[]) => own.find((l) => l.endsWith(` ${ids.image}`))!.split(" ")[0];
  expect(tokenOf(first.own)).not.toBe(tokenOf(second.own));
  await first.context.close();
  await second.context.close();
});

test("scenario 2: with scripts off the placeholder stays, shows no banner, and nothing breaks", async ({ browser }) => {
  const { context, page, own, errors } = await visitor(browser, false);
  await page.goto("/cached");
  await expect(page.locator('[data-mbz="cached-top"]')).toBeAttached();
  await page.waitForTimeout(2_500);

  await expect(page.locator("[data-mbb]")).toHaveCount(0);
  expect(own).toHaveLength(0);
  expect(errors).toEqual([]);
  await context.close();
});

test("an Html banner in client mode renders and its inline script runs", async ({ browser }) => {
  const { context, page, errors } = await visitor(browser);
  await page.goto("/cached");

  await expect(page.locator('[data-mbz="cached-html"] .mb-html #client-html')).toHaveText("script ran");
  expect(await page.locator('[data-mbz="cached-html"] a').count(), "foreign code gets no click link").toBe(0);
  expect(errors).toEqual([]);
  await context.close();
});

test("an Html banner in client mode counts one impression when it is 50% visible for one second", async ({ browser }) => {
  const { context, page, own } = await visitor(browser);
  await page.goto("/cached");
  await expect(page.locator('[data-mbz="cached-html"] #client-html')).toHaveText("script ran");

  await expect.poll(() => own.filter((l) => l.endsWith(` ${ids.clientHtml}`)).length, { timeout: 8_000 }).toBe(1);
  await page.waitForTimeout(2_500);
  expect(own.filter((l) => l.endsWith(` ${ids.clientHtml}`))).toHaveLength(1);
  await context.close();
});

test("an Html banner in server mode counts one impression when it is 50% visible for one second", async ({ browser }) => {
  const { context, page, own } = await visitor(browser);
  await page.goto("/long");
  await expect(page.locator('[data-mbz="long-html"] #server-html')).toHaveText("script ran");
  ids.serverHtml = (await page.locator('[data-mbz="long-html"] [data-mbb]').getAttribute("data-mbb")) ?? "";

  await expect.poll(() => own.length, { timeout: 8_000 }).toBe(1);
  await page.waitForTimeout(2_500);
  expect(own).toHaveLength(1);
  await context.close();
});

test("the report shows the impressions that were counted", async () => {
  test.setTimeout(180_000);
  await openBannersView(admin, "reports");
  await admin.getByRole("button", { name: "Today" }).click();
  const expected: Array<[string, string]> = [
    [IMAGE_BANNER, `cached-top ${reportedFor(ids.image)} 0`],
    [CLIENT_HTML_BANNER, `cached-html ${reportedFor(ids.clientHtml)} 0`],
    [SERVER_HTML_BANNER, `long-html ${reportedFor(ids.serverHtml)} 0`],
  ];
  expect(reportedFor(ids.image), "the two visitors of scenario 1").toBeGreaterThanOrEqual(2);
  expect(reportedFor(ids.serverHtml)).toBe(1);
  const stop = Date.now() + 90_000;
  for (const [banner, text] of expected) {
    const row = admin.getByRole("row", { name: new RegExp(banner) });
    for (;;) {
      const seen = (await row.innerText({ timeout: 2_000 }).catch(() => "")).replace(/\s+/g, " ");
      if (seen.includes(text)) {
        break;
      }
      if (Date.now() > stop) {
        throw new Error(`The report row of "${banner}" never showed "${text}". Last seen: "${seen}"`);
      }
      await admin.waitForTimeout(3_000);
      await admin.getByRole("button", { name: "Today" }).click();
    }
  }
});
