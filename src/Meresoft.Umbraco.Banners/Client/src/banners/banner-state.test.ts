import { describe, expect, it } from "vitest";
import type { BannerFlagsModel } from "../api/types.gen.js";
import { activeFlags, limitToText, parseLimit, showsClickLimit } from "./banner-state.js";

const NO_FLAGS: BannerFlagsModel = { notPlaced: false, imageMissing: false, scheduled: false, expired: false, limitReached: false };

describe("activeFlags", () => {
  it("shows Limit reached, in danger color, when the limit is used up", () => {
    expect(activeFlags({ ...NO_FLAGS, limitReached: true })).toEqual([{ key: "meresoftBanners_flagLimitReached", color: "danger" }]);
  });

  it("shows nothing when no flag is on", () => {
    expect(activeFlags(NO_FLAGS)).toEqual([]);
  });
});

describe("showsClickLimit", () => {
  it("is on for image banners and off for Html banners, whose clicks are not counted", () => {
    expect(showsClickLimit("Image")).toBe(true);
    expect(showsClickLimit("Html")).toBe(false);
  });
});

describe("parseLimit", () => {
  it("is null for an empty field, so there is no limit", () => {
    expect(parseLimit("")).toBeNull();
    expect(parseLimit("   ")).toBeNull();
  });

  it("is the number otherwise", () => {
    expect(parseLimit("10000")).toBe(10000);
    expect(parseLimit(" 3 ")).toBe(3);
  });
});

describe("limitToText", () => {
  it("is empty without a limit and the number as text with one", () => {
    expect(limitToText(null)).toBe("");
    expect(limitToText(undefined)).toBe("");
    expect(limitToText(50)).toBe("50");
  });
});
