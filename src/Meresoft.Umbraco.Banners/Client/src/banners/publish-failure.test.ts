import { describe, expect, it } from "vitest";
import { publishFailure } from "./publish-failure.js";

const GENERIC = "Could not publish.";

describe("publishFailure", () => {
  it("puts a no-zone problem on the zone field", () => {
    const view = publishFailure(400, { type: "no-zone-assigned", detail: "Pick a zone." }, GENERIC, false);
    expect(view).toEqual({ errors: { zoneKeys: "Pick a zone." }, problem: "", conflict: false, missing: false });
  });

  it("shows the detail of a 400 that names no field", () => {
    const view = publishFailure(400, { detail: "Not allowed." }, GENERIC, false);
    expect(view.problem).toBe("Not allowed.");
  });

  it("falls back to the generic message for a failure with nothing to show", () => {
    expect(publishFailure(500, undefined, GENERIC, false).problem).toBe(GENERIC);
    expect(publishFailure(0, undefined, GENERIC, false).problem).toBe(GENERIC);
  });

  it("marks a conflict and shows no generic message", () => {
    const view = publishFailure(409, { type: "changed-by-another-user" }, GENERIC, false);
    expect(view).toMatchObject({ conflict: true, problem: "" });
  });

  it("on the edit page a 404 means the banner is gone, with no generic message", () => {
    expect(publishFailure(404, undefined, GENERIC, false)).toEqual({ errors: {}, problem: "", conflict: false, missing: true });
  });

  it("on a new banner a 404 is not a missing page: it gets the generic message to show on the edit page", () => {
    expect(publishFailure(404, undefined, GENERIC, true)).toEqual({ errors: {}, problem: GENERIC, conflict: false, missing: false });
  });
});
