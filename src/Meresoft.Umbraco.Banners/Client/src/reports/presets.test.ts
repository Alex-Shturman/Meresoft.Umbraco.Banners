import { describe, expect, it } from "vitest";
import { presetRange } from "./presets.js";

// vitest.config.ts runs these in Europe/Berlin (UTC+1 in winter, UTC+2 in summer). Statistics days are UTC
// days (spec, Edge Cases: clock and time zones), so a preset follows the UTC calendar even when the user's
// local date is already one day ahead. The tests pin that on moments where the two calendars differ.

describe("presetRange", () => {
  it("today is the UTC day, also when the local date is already the next day", () => {
    // 23:30 UTC on 31 March is 01:30 on 1 April in Berlin.
    const now = new Date("2027-03-31T23:30:00Z");
    expect(now.getDate()).toBe(1);
    expect(presetRange("today", now)).toEqual({ from: "2027-03-31", to: "2027-03-31" });
  });

  it("last 7 and last 30 include today and cover exactly 7 and 30 days", () => {
    const now = new Date("2027-06-15T10:00:00Z");
    expect(presetRange("last7", now)).toEqual({ from: "2027-06-09", to: "2027-06-15" });
    expect(presetRange("last30", now)).toEqual({ from: "2027-05-17", to: "2027-06-15" });
  });

  it("last 7 and last 30 cross a month and a year end", () => {
    const now = new Date("2027-01-03T10:00:00Z");
    expect(presetRange("last7", now)).toEqual({ from: "2026-12-28", to: "2027-01-03" });
    expect(presetRange("last30", now)).toEqual({ from: "2026-12-05", to: "2027-01-03" });
  });

  it("this month starts on the 1st and ends today", () => {
    expect(presetRange("thisMonth", new Date("2027-02-10T10:00:00Z"))).toEqual({ from: "2027-02-01", to: "2027-02-10" });
  });

  it("this month on the 1st at 00:30 UTC is that one day, though Berlin is already at 02:30", () => {
    expect(presetRange("thisMonth", new Date("2027-05-01T00:30:00Z"))).toEqual({ from: "2027-05-01", to: "2027-05-01" });
  });

  it("last month is the whole previous month, including a leap February", () => {
    expect(presetRange("lastMonth", new Date("2027-03-10T10:00:00Z"))).toEqual({ from: "2027-02-01", to: "2027-02-28" });
    expect(presetRange("lastMonth", new Date("2028-03-10T10:00:00Z"))).toEqual({ from: "2028-02-01", to: "2028-02-29" });
  });

  it("last month on January 15 is December of the year before", () => {
    expect(presetRange("lastMonth", new Date("2027-01-15T10:00:00Z"))).toEqual({ from: "2026-12-01", to: "2026-12-31" });
  });

  it("last month on 1 January at 00:30 UTC (Berlin 01:30) is still December", () => {
    expect(presetRange("lastMonth", new Date("2027-01-01T00:30:00Z"))).toEqual({ from: "2026-12-01", to: "2026-12-31" });
  });

  it("a clock change inside the period does not shift a day (spring, 2027-03-28)", () => {
    // The Berlin day of 28 March has 23 hours; UTC days always have 24.
    const now = new Date("2027-03-30T10:00:00Z");
    expect(presetRange("last7", now)).toEqual({ from: "2027-03-24", to: "2027-03-30" });
    expect(presetRange("last30", now)).toEqual({ from: "2027-03-01", to: "2027-03-30" });
  });

  it("a clock change inside the period does not shift a day (autumn, 2027-10-31)", () => {
    // The Berlin day of 31 October has 25 hours.
    const now = new Date("2027-11-02T10:00:00Z");
    expect(presetRange("last7", now)).toEqual({ from: "2027-10-27", to: "2027-11-02" });
    expect(presetRange("last30", now)).toEqual({ from: "2027-10-04", to: "2027-11-02" });
  });

  it("on the day of a clock change today is that day", () => {
    expect(presetRange("today", new Date("2027-03-28T00:30:00Z"))).toEqual({ from: "2027-03-28", to: "2027-03-28" });
    expect(presetRange("today", new Date("2027-10-31T23:30:00Z"))).toEqual({ from: "2027-10-31", to: "2027-10-31" });
  });
});
