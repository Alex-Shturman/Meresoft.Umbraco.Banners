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
  it("is no limit for an empty field", () => {
    expect(parseLimit("")).toEqual({ ok: true, value: null });
    expect(parseLimit("   ")).toEqual({ ok: true, value: null });
  });

  it("is the number for a whole number of 1 or more", () => {
    expect(parseLimit("10000")).toEqual({ ok: true, value: 10000 });
    expect(parseLimit(" 3 ")).toEqual({ ok: true, value: 3 });
    expect(parseLimit("1")).toEqual({ ok: true, value: 1 });
  });

  it.each(["abc", "2.5", "0", "-1", "1e3", "12abc", "9007199254740993"])("refuses %s", (text) => {
    expect(parseLimit(text)).toEqual({ ok: false });
  });
});

describe("limitToText", () => {
  it("is empty without a limit and the number as text with one", () => {
    expect(limitToText(null)).toBe("");
    expect(limitToText(undefined)).toBe("");
    expect(limitToText(50)).toBe("50");
  });
});
