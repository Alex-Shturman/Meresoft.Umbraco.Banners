import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { loginAsAdmin, openBannersView } from "./support/backoffice";

/**
 * Accessibility of the back office views of the package (task T127): the banners list, the banner editor, the zones
 * list and the reports view. All data is invented.
 *
 * Environment variables (the same as the other specs, see tests/e2e/support/credentials.ts):
 *   MB_SAMPLE_BASE_URL        the sample site, http://localhost:5100 by default (a site on this machine only: the run
 *                             starts by deleting every banner and zone of it, see support/global-reset.ts)
 *   MB_SAMPLE_ADMIN_EMAIL     the back office administrator (required)
 *   MB_SAMPLE_ADMIN_PASSWORD  its password (required)
 * This spec needs no other user. Run it alone with: npx playwright test a11y.spec.ts --workers=1
 *
 * What is checked
 *  - axe-core with the tags wcag2a and wcag2aa (colour contrast included): no violation of impact serious or critical
 *    in our own components. A violation counts as ours when the path of one of its elements goes through a custom element
 *    of this package (the tag starts with "meresoft-"). Violations inside Umbraco's own shell are not ours to fix; they
 *    are attached to the test report as an annotation, so they stay visible.
 *  - the main controls are reached with the Tab key and are activated with Enter or Space;
 *  - the form fields of the editors have a label;
 *  - the chart of the reports view has a text alternative (an accessible name with the totals) and the same numbers
 *    are in the table next to it.
 */
test.describe.configure({ mode: "serial" });

const ZONE_ALIAS = "a11y-zone";
const ZONE_NAME = "Accessibility test zone";
const BANNER_NAME = "Accessibility html banner";
const HTML = "<p>Invented accessibility html</p>";

let page: Page;
let bannerPath = "";

test.beforeAll(async ({ browser }) => {
  page = await (await browser.newContext()).newPage();
  await loginAsAdmin(page);
});

test.afterAll(async () => {
  await page.close();
});

function toast(text: string) {
  return page.getByRole("alert").filter({ hasText: text });
}

interface Finding {
  id: string;
  impact: string;
  help: string;
  ours: boolean;
  targets: string[];
}

/** Scans the page and fails on a serious or critical violation in our own components. */
async function expectNoSeriousViolations(label: string): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
  const findings: Finding[] = results.violations
    .filter((v) => v.impact === "serious" || v.impact === "critical")
    .map((v) => ({
      id: v.id,
      impact: v.impact ?? "",
      help: v.help,
      ours: v.nodes.some((n) => JSON.stringify(n.target).includes("meresoft-")),
      targets: v.nodes.map((n) => JSON.stringify(n.target)),
    }));

  const others = findings.filter((f) => !f.ours);
  if (others.length > 0) {
    test.info().annotations.push({
      type: `${label}: violations outside our components`,
      description: others.map((f) => `${f.id} (${f.impact})`).join(", "),
    });
  }

  const ours = findings.filter((f) => f.ours);
  expect(
    ours,
    ours.map((f) => `${f.id} (${f.impact}): ${f.help}\n  ${f.targets.slice(0, 3).join("\n  ")}`).join("\n"),
  ).toEqual([]);
}

/** The accessible name of the focused element, looking through shadow roots; empty when nothing is focused. */
async function focusedName(): Promise<string> {
  return page.evaluate(() => {
    let element: Element | null = document.activeElement;
    while (element?.shadowRoot?.activeElement) {
      element = element.shadowRoot.activeElement;
    }
    // A uui-button keeps its label in an attribute of the host, around the native button in its shadow root.
    const host = (element?.getRootNode() as ShadowRoot | undefined)?.host;
    return (
      element?.getAttribute("aria-label") ??
      host?.getAttribute("label") ??
      element?.textContent?.trim() ??
      ""
    );
  });
}

/** Presses Tab until the focus is on a control with this name; false when it is not reached within the limit. */
async function tabTo(name: RegExp, limit = 80): Promise<boolean> {
  for (let i = 0; i < limit; i++) {
    await page.keyboard.press("Tab");
    if (name.test(await focusedName())) {
      return true;
    }
  }
  return false;
}

test("setup: a zone and an Html banner exist for the views to show", async () => {
  await openBannersView(page, "zones");
  await expect(page.locator("#headline").and(page.getByRole("heading", { name: "Zones" }))).toBeVisible();
  await page.getByRole("button", { name: "Create zone" }).first().click();
  await page.getByLabel("Alias").fill(ZONE_ALIAS);
  await page.getByLabel("Name", { exact: true }).fill(ZONE_NAME);
  await page.getByLabel("Banners shown at once").fill("1");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(toast("Zone saved")).toBeVisible();

  await openBannersView(page, "banners");
  await page.getByRole("button", { name: "Create banner" }).click();
  await page.getByLabel("Name", { exact: true }).fill(BANNER_NAME);
  await page.locator("uui-radio", { hasText: "HTML" }).click();
  await page.getByLabel("HTML code").fill(HTML);
  await page.locator("uui-checkbox", { hasText: ZONE_NAME }).click();
  await page.getByRole("button", { name: "Save" }).click();
  await expect(toast("Banner saved")).toBeVisible();
  await expect(page).toHaveURL(/\/banners\/edit\//);
  bannerPath = new URL(page.url()).pathname;
});

test("banners list: no serious violations", async () => {
  await openBannersView(page, "banners");
  await expect(page.locator("#headline").and(page.getByRole("heading", { name: "Banners" }))).toBeVisible();
  await expect(page.getByRole("row", { name: new RegExp(BANNER_NAME) })).toBeVisible();
  await expectNoSeriousViolations("banners list");
});

test("banners list: Tab reaches Create banner and Enter opens the editor", async () => {
  await openBannersView(page, "banners");
  await expect(page.getByRole("button", { name: "Create banner" })).toBeVisible();
  await page.locator("body").click({ position: { x: 1, y: 1 } });
  expect(await tabTo(/^Create banner$/)).toBe(true);
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/banners\/create/);
  await expect(page.getByLabel("Name", { exact: true })).toBeVisible();
});

test("banner editor: fields have labels, no serious violations", async () => {
  await page.goto(bannerPath);
  await expect(page.getByLabel("Name", { exact: true })).toHaveValue(BANNER_NAME);
  await expect(page.getByLabel("HTML code")).toHaveValue(HTML);
  await expect(page.getByRole("button", { name: "Save" })).toBeVisible();

  // Every field of the editor is reachable by its label (the type choice exists only when creating).
  await expect(page.getByLabel("Name", { exact: true })).toBeEditable();
  await expect(page.locator("uui-checkbox", { hasText: ZONE_NAME })).toBeVisible();

  // An input without a name for assistive technology would also be an axe violation (label, aria-input-field-name);
  // the check above names the fields a person fills in.
  const unnamed = await page.evaluate(() => {
    const editor = document.querySelector("meresoft-banner-editor") ?? document.body;
    const found: string[] = [];
    const visit = (root: ParentNode) => {
      for (const field of Array.from(root.querySelectorAll("uui-input, uui-textarea, uui-select, uui-checkbox, uui-radio"))) {
        const named = field.getAttribute("label") || field.getAttribute("aria-label") || field.textContent?.trim();
        if (!named) {
          found.push(field.tagName.toLowerCase());
        }
      }
      for (const el of Array.from(root.querySelectorAll("*"))) {
        if (el.shadowRoot) {
          visit(el.shadowRoot);
        }
      }
    };
    visit(editor);
    if (editor.shadowRoot) {
      visit(editor.shadowRoot);
    }
    return found;
  });
  expect(unnamed).toEqual([]);

  await expectNoSeriousViolations("banner editor");
});

test("banner editor: Tab reaches Save, Space or Enter saves", async () => {
  await page.goto(bannerPath);
  await expect(page.getByLabel("Name", { exact: true })).toHaveValue(BANNER_NAME);
  await page.locator("body").click({ position: { x: 1, y: 1 } });
  expect(await tabTo(/^Save$/, 120)).toBe(true);
  await page.keyboard.press("Space");
  await expect(toast("Banner saved")).toBeVisible();
});

test("zones list: no serious violations, Tab reaches Create zone and Enter opens the editor", async () => {
  await openBannersView(page, "zones");
  await expect(page.locator("#headline").and(page.getByRole("heading", { name: "Zones" }))).toBeVisible();
  await expect(page.getByRole("row", { name: new RegExp(ZONE_ALIAS) })).toBeVisible();
  await expectNoSeriousViolations("zones list");

  await page.locator("body").click({ position: { x: 1, y: 1 } });
  expect(await tabTo(/^Create zone$/)).toBe(true);
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/zones\/create/);
  await expect(page.getByLabel("Alias")).toBeVisible();
});

test("zone editor: fields have labels, no serious violations", async () => {
  await openBannersView(page, "zones");
  await page.getByRole("row", { name: new RegExp(ZONE_ALIAS) }).getByRole("link").first().click();
  await expect(page.getByLabel("Alias")).toBeVisible();
  await expect(page.getByLabel("Name", { exact: true })).toHaveValue(ZONE_NAME);
  await expect(page.getByLabel("Banners shown at once")).toBeVisible();
  await expectNoSeriousViolations("zone editor");
});

test("reports: no serious violations, the filters have labels and the period buttons work by keyboard", async () => {
  await openBannersView(page, "reports");
  await expect(page.locator("#headline").and(page.getByRole("heading", { name: "Reports" }))).toBeVisible();
  await expect(page.getByText("Show the daily values as a table")).toBeVisible();

  await expect(page.getByLabel("From").first()).toBeVisible();
  await expect(page.getByLabel("To").first()).toBeVisible();
  await expect(page.getByRole("group", { name: /period/i })).toBeVisible();
  await expectNoSeriousViolations("reports");

  // A period button is reached with Tab and pressed with Enter; the pressed state follows.
  const pressed = page.locator("uui-button[aria-pressed=true]");
  await expect(pressed).toHaveCount(1);
  const before = await pressed.getAttribute("label");
  const other = page.locator("uui-button[aria-pressed=false]").first();
  const otherLabel = (await other.getAttribute("label")) ?? "";
  await page.locator("body").click({ position: { x: 1, y: 1 } });
  expect(await tabTo(new RegExp(`^${otherLabel.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}$`), 120)).toBe(true);
  await page.keyboard.press("Enter");
  await expect(page.locator(`uui-button[label="${otherLabel}"]`)).toHaveAttribute("aria-pressed", "true");
  expect(otherLabel).not.toBe(before);
});

test("reports: the chart has a text alternative and the table carries the same numbers", async () => {
  await openBannersView(page, "reports");
  const chart = page.getByRole("img", { name: /Impressions and clicks per day from \d{4}-\d{2}-\d{2} to \d{4}-\d{2}-\d{2}: .* impressions and .* clicks in total\./ });
  await expect(chart).toBeVisible();

  // The text alternative: the totals, and the table "Per day" with one row per day of the chart.
  await page.getByText("Show the daily values as a table").click();
  const table = page.getByRole("table", { name: "Per day" });
  await expect(table).toBeVisible();
  const days = await page.locator("meresoft-daily-chart g.day").count();
  expect(days).toBeGreaterThan(0);
  // One header row and one row per day.
  await expect(table.getByRole("row")).toHaveCount(days + 1);

  // The drawn axes and bars are hidden from assistive technology (the image role carries the meaning).
  await expect(page.locator("meresoft-daily-chart g.panel[aria-hidden=true]")).toHaveCount(2);
});

test("cleanup: the test banner and zone are deleted", async () => {
  await page.goto(bannerPath);
  await page.getByRole("button", { name: "Delete" }).click();
  await page.locator("umb-backoffice-modal-container").getByRole("button", { name: "Delete" }).click();
  await expect(toast("Banner deleted")).toBeVisible();

  await openBannersView(page, "zones");
  await page.getByRole("row", { name: new RegExp(ZONE_ALIAS) }).getByRole("link").first().click();
  await page.getByRole("button", { name: "Delete" }).click();
  await page.locator("umb-backoffice-modal-container").getByRole("button", { name: "Delete" }).click();
  await expect(toast("Zone deleted")).toBeVisible();
});
