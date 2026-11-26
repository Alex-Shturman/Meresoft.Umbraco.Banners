/** What the reports view shows below the filters. */
export type ReportBodyKind = "none" | "failed" | "loading" | "results";

/**
 * Decides what to render. A refused period shows only its error: no totals, no table and no empty-state text, because
 * an older report in memory belongs to a different period and "nothing was counted" would be untrue.
 */
export function reportBodyKind(state: { periodInvalid: boolean; failed: boolean; hasReport: boolean }): ReportBodyKind {
  if (state.periodInvalid) {
    return "none";
  }
  if (state.failed) {
    return "failed";
  }
  return state.hasReport ? "results" : "loading";
}
