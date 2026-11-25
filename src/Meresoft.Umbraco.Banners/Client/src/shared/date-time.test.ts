import { describe, expect, it } from "vitest";
import { localInputToUtc, resolveUtc, utcToLocalInput } from "./date-time.js";

// vitest.config.ts runs these in Europe/Berlin: UTC+1 in winter, UTC+2 in summer.
// In 2027 the clocks go forward on 2027-03-28 (02:00 becomes 03:00, so 02:00 to 02:59 does not exist) and
// back on 2027-10-31 (03:00 becomes 02:00, so 02:00 to 02:59 happens twice).

describe("time zone of the test run", () => {
  it("is Europe/Berlin", () => {
    expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe("Europe/Berlin");
  });
});

describe("utcToLocalInput", () => {
  it("shows a winter and a summer moment in local time", () => {
    expect(utcToLocalInput("2027-01-15T08:00:00Z")).toBe("2027-01-15T09:00");
    expect(utcToLocalInput("2027-06-01T08:00:00Z")).toBe("2027-06-01T10:00");
  });

  it("reads text without an offset as UTC", () => {
    expect(utcToLocalInput("2027-06-01T08:00:00")).toBe("2027-06-01T10:00");
  });

  it("is empty for nothing or for text that is not a time", () => {
    expect(utcToLocalInput(null)).toBe("");
    expect(utcToLocalInput(undefined)).toBe("");
    expect(utcToLocalInput("not a time")).toBe("");
  });

  it("jumps from 01:59 to 03:00 over the spring change", () => {
    expect(utcToLocalInput("2027-03-28T00:59:00Z")).toBe("2027-03-28T01:59");
    expect(utcToLocalInput("2027-03-28T01:00:00Z")).toBe("2027-03-28T03:00");
  });

  it("shows 02:30 twice over the autumn change", () => {
    expect(utcToLocalInput("2027-10-31T00:30:00Z")).toBe("2027-10-31T02:30"); // summer time, first
    expect(utcToLocalInput("2027-10-31T01:30:00Z")).toBe("2027-10-31T02:30"); // winter time, second
  });
});

describe("localInputToUtc", () => {
  it("converts winter and summer times", () => {
    expect(localInputToUtc("2027-01-15T09:00")).toBe("2027-01-15T08:00:00.000Z");
    expect(localInputToUtc("2027-06-01T10:00")).toBe("2027-06-01T08:00:00.000Z");
  });

  it("is null for empty text or text that is not a time", () => {
    expect(localInputToUtc("")).toBeNull();
    expect(localInputToUtc("nonsense")).toBeNull();
  });

  it("round trips ordinary times", () => {
    for (const utc of ["2027-01-15T08:00:00.000Z", "2027-06-01T08:00:00.000Z", "2027-12-31T23:59:00.000Z"]) {
      expect(localInputToUtc(utcToLocalInput(utc))).toBe(utc);
    }
  });

  it("moves a time in the spring gap forward: 02:30 does not exist, so it becomes 03:30 summer time", () => {
    expect(localInputToUtc("2027-03-28T02:30")).toBe("2027-03-28T01:30:00.000Z");
    expect(utcToLocalInput("2027-03-28T01:30:00.000Z")).toBe("2027-03-28T03:30");
  });

  it("takes the first occurrence of a time in the autumn overlap (summer time)", () => {
    expect(localInputToUtc("2027-10-31T02:30")).toBe("2027-10-31T00:30:00.000Z");
  });
});

describe("resolveUtc", () => {
  it("converts what was typed when it differs from the stored value", () => {
    expect(resolveUtc("2027-06-01T12:00", "2027-06-01T08:00:00Z")).toBe("2027-06-01T10:00:00.000Z");
  });

  it("keeps the stored value, seconds included, when the input still shows it", () => {
    expect(resolveUtc("2027-06-01T10:00", "2027-06-01T08:00:30.250Z")).toBe("2027-06-01T08:00:30.250Z");
  });

  it("keeps the second 02:30 of the autumn overlap instead of moving it an hour back", () => {
    const stored = "2027-10-31T01:30:00Z"; // 02:30 winter time, the second one
    expect(utcToLocalInput(stored)).toBe("2027-10-31T02:30");
    expect(resolveUtc("2027-10-31T02:30", stored)).toBe(stored);
  });

  it("keeps a stored time that the spring gap would shift, as long as the input shows it", () => {
    const stored = "2027-03-28T01:30:00Z"; // 03:30 summer time
    expect(resolveUtc(utcToLocalInput(stored), stored)).toBe(stored);
  });

  it("is null when the input is emptied", () => {
    expect(resolveUtc("", "2027-06-01T08:00:00Z")).toBeNull();
    expect(resolveUtc("", null)).toBeNull();
  });

  it("converts a new value when nothing was stored", () => {
    expect(resolveUtc("2027-06-01T10:00", null)).toBe("2027-06-01T08:00:00.000Z");
  });
});
