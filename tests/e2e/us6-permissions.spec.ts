import { expect, test, type Page } from "@playwright/test";
import { loginAs, loginAsAdmin, openBannersView } from "./support/backoffice";
import { getBannerEditorCredentials, getEditorCredentials } from "./support/credentials";

/**
 * User story 6 on the sample site: the section is hidden from the sample editor (quickstart scenario 12), visible to
 * the administrator, a group with the section but without the HTML permission sees the HTML field read-only, and the
 * history views of a banner and a zone list the changes. All data is invented. The banner editor needs the sample
 * variables MB_SAMPLE_BANNER_EDITOR_EMAIL and MB_SAMPLE_BANNER_EDITOR_PASSWORD; the editor needs MB_SAMPLE_EDITOR_*.
 */
test.describe.configure({ mode: "serial" });

const ZONE_ALIAS = "us6-zone";
const ZONE_NAME = "Permissions test zone";
const HTML_BANNER = "Permissions html banner";
const HTML = "<p>Invented permissions html</p>";

let admin: Page;
let htmlBannerPath = "";

test.beforeAll(async ({ browser }) => {
  admin = await browser.newPage();
  await loginAsAdmin(admin);
});

test.afterAll(async () => {
  await admin.close();
});

function toast(page: Page, text: string) {
  return page.getByRole("alert").filter({ hasText: text });
}

test("scenario 12: the sample editor sees no Banners section", async ({ browser }) => {
  const credentials = getEditorCredentials();
  test.skip(!credentials, "MB_SAMPLE_EDITOR_EMAIL and MB_SAMPLE_EDITOR_PASSWORD are not set");
  const editor = await browser.newPage();
  try {
    await loginAs(editor, credentials!);
    await expect(editor.getByRole("tab", { name: "Content" })).toBeVisible();
    await expect(editor.getByRole("tab", { name: "Banners" })).toHaveCount(0);

    await editor.goto("/umbraco/section/meresoft-banners/banners");
    await expect(editor.getByRole("heading", { name: "Banners" })).toHaveCount(0);
  } finally {
    await editor.close();
  }
});

test("US6 scenario 3: the administrator sees the Banners section", async () => {
  await admin.goto("/umbraco");
  await expect(admin.getByRole("tab", { name: "Banners" })).toBeVisible();
});

test("an administrator creates a zone and an Html banner (the type choice and the HTML field are editable)", async () => {
  await openBannersView(admin, "zones");
  await expect(admin.getByRole("heading", { name: "Zones" })).toBeVisible();
  // A rerun on the same database finds the zone of the last run.
  if ((await admin.getByRole("row", { name: new RegExp(ZONE_ALIAS) }).count()) === 0) {
    await admin.getByRole("button", { name: "Create zone" }).first().click();
    await admin.getByLabel("Alias").fill(ZONE_ALIAS);
    await admin.getByLabel("Name", { exact: true }).fill(ZONE_NAME);
    await admin.getByLabel("Banners shown at once").fill("1");
    await admin.getByRole("button", { name: "Save" }).click();
    await expect(toast(admin, "Zone saved")).toBeVisible();
  }

  await openBannersView(admin, "banners");
  await admin.getByRole("button", { name: "Create banner" }).click();
  await admin.getByLabel("Name", { exact: true }).fill(HTML_BANNER);
  await admin.locator("uui-radio", { hasText: "HTML" }).click();
  const html = admin.getByLabel("HTML code");
  await expect(html).toBeEditable();
  await html.fill(HTML);
  await admin.locator("uui-checkbox", { hasText: ZONE_NAME }).click();
  await admin.getByRole("button", { name: "Save" }).click();

  await expect(toast(admin, "Banner saved")).toBeVisible();
  await expect(admin).toHaveURL(/\/banners\/edit\//);
  htmlBannerPath = new URL(admin.url()).pathname;
  await expect(admin.getByLabel("HTML code")).toHaveValue(HTML);
});

test("a group with the section but without the HTML permission sees the HTML field read-only", async ({ browser }) => {
  const credentials = getBannerEditorCredentials();
  test.skip(!credentials, "MB_SAMPLE_BANNER_EDITOR_EMAIL and MB_SAMPLE_BANNER_EDITOR_PASSWORD are not set");
  const editor = await browser.newPage();
  try {
    await loginAs(editor, credentials!);
    await editor.goto(htmlBannerPath);
    const field = editor.getByLabel("HTML code");
    await expect(field).toHaveValue(HTML);
    await expect(field).toHaveAttribute("readonly", /.*/);
    await expect(editor.getByText("You need the HTML banner permission to change this code.")).toBeVisible();

    // The other fields stay editable.
    await editor.getByLabel("Name", { exact: true }).fill(HTML_BANNER + " renamed");
    await editor.getByRole("button", { name: "Save" }).click();
    await expect(toast(editor, "Banner saved")).toBeVisible();

    // The create form offers no Html type.
    await openBannersView(editor, "banners");
    await editor.getByRole("button", { name: "Create banner" }).click();
    await expect(editor.locator("uui-radio", { hasText: "Image" })).toBeVisible();
    await expect(editor.locator("uui-radio", { hasText: "HTML" })).toHaveCount(0);
  } finally {
    await editor.close();
  }
});

test("the history view of the banner lists the changes", async () => {
  await admin.goto(htmlBannerPath);
  await admin.getByRole("tab", { name: "History" }).click();

  const history = admin.getByRole("table", { name: "History" });
  await expect(history.getByRole("row", { name: /Created/ })).toBeVisible();
  await expect(history.getByRole("row", { name: /Saved/ }).first()).toBeVisible();
});

test("the history view of the zone lists the changes", async () => {
  await openBannersView(admin, "zones");
  await admin.getByRole("row", { name: new RegExp(ZONE_ALIAS) }).getByRole("link").first().click();
  await admin.getByRole("tab", { name: "History" }).click();

  const history = admin.getByRole("table", { name: "History" });
  await expect(history.getByRole("row", { name: /Created/ })).toBeVisible();
});

test("cleanup: the test banner and zone are deleted", async () => {
  await admin.goto(htmlBannerPath);
  await admin.getByRole("button", { name: "Delete" }).click();
  await admin.locator("umb-backoffice-modal-container").getByRole("button", { name: "Delete" }).click();
  await expect(toast(admin, "Banner deleted")).toBeVisible();

  await openBannersView(admin, "zones");
  await admin.getByRole("row", { name: new RegExp(ZONE_ALIAS) }).getByRole("link").first().click();
  await admin.getByRole("button", { name: "Delete" }).click();
  await admin.locator("umb-backoffice-modal-container").getByRole("button", { name: "Delete" }).click();
  await expect(toast(admin, "Zone deleted")).toBeVisible();
});
