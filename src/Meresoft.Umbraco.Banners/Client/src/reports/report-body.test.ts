import { describe, expect, it } from "vitest";
import { reportBodyKind } from "./report-body.js";

describe("reportBodyKind", () => {
  it("shows only the error when the period was refused, even with an older report in memory", () => {
    expect(reportBodyKind({ periodInvalid: true, failed: false, hasReport: true })).toBe("none");
    expect(reportBodyKind({ periodInvalid: true, failed: false, hasReport: false })).toBe("none");
  });

  it("shows the load failure when loading failed", () => {
    expect(reportBodyKind({ periodInvalid: false, failed: true, hasReport: false })).toBe("failed");
  });

  it("shows the loader until a report arrives", () => {
    expect(reportBodyKind({ periodInvalid: false, failed: false, hasReport: false })).toBe("loading");
  });

  it("shows the results when a report is there", () => {
    expect(reportBodyKind({ periodInvalid: false, failed: false, hasReport: true })).toBe("results");
  });
});
