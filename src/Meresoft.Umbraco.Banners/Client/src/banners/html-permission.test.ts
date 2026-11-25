import { describe, expect, it } from "vitest";
import { canEditHtml, htmlFieldAccess, offeredBannerTypes } from "./html-permission.js";

const VERB = "Meresoft.Banners.Html";

describe("canEditHtml", () => {
  it("is true only when the fallback permissions hold the HTML verb", () => {
    expect(canEditHtml([VERB])).toBe(true);
    expect(canEditHtml(["Umb.Document.Read", VERB])).toBe(true);
  });

  it("is false without the verb, with no permissions, and before the permissions are known", () => {
    expect(canEditHtml(["Umb.Document.Read"])).toBe(false);
    expect(canEditHtml([])).toBe(false);
    expect(canEditHtml(undefined)).toBe(false);
  });

  it("does not match a verb that only looks similar", () => {
    expect(canEditHtml(["Meresoft.Banners.Html.Extra", "meresoft.banners.html"])).toBe(false);
  });
});

describe("htmlFieldAccess", () => {
  it("makes the field read-only and explains why when the user lacks the permission", () => {
    expect(htmlFieldAccess(false)).toEqual({ readonly: true, noteKey: "meresoftBanners_bannerHtmlLocked" });
  });

  it("makes the field editable with the normal hint when the user has the permission", () => {
    expect(htmlFieldAccess(true)).toEqual({ readonly: false, noteKey: "meresoftBanners_bannerHtmlDescription" });
  });
});

describe("offeredBannerTypes", () => {
  it("offers HTML on create only with the permission", () => {
    expect(offeredBannerTypes(true)).toEqual(["Image", "Html"]);
    expect(offeredBannerTypes(false)).toEqual(["Image"]);
  });
});
