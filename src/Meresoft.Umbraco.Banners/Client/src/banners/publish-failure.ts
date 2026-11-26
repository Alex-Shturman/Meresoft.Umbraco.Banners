import { fieldErrors, type ApiProblem } from "../shared/api.js";

/** What the editor shows after a failed publish. */
export interface PublishFailureView {
  errors: Record<string, string>;
  problem: string;
  conflict: boolean;
  /** The banner is gone: the editor shows its not-found page. */
  missing: boolean;
}

/**
 * Decides what a failed publish shows: a field error, the server's detail, a conflict, or else the `generic` message so
 * the editor is never left without an explanation. On the edit page a 404 means the banner is gone; right after a
 * create (`afterCreate`) it does not, because the edit page loads the banner again, so it gets the generic message.
 */
export function publishFailure(
  status: number,
  problem: ApiProblem | undefined,
  generic: string,
  afterCreate: boolean,
): PublishFailureView {
  if (problem?.type === "no-zone-assigned") {
    return { errors: { zoneKeys: problem.detail ?? "" }, problem: "", conflict: false, missing: false };
  }
  const errors = status === 400 ? fieldErrors(problem) : {};
  const detail = status === 400 && Object.keys(errors).length === 0 ? (problem?.detail ?? "") : "";
  const conflict = problem?.type === "changed-by-another-user";
  const missing = status === 404 && !afterCreate;
  const nothingToShow = !missing && !conflict && !detail && Object.keys(errors).length === 0;
  return { errors, problem: nothingToShow ? generic : detail, conflict, missing };
}
