import { client } from "../api/client.gen.js";

export { client };

/** The problem details the package API returns (RFC 9457), with the extensions the views use. */
export interface ApiProblem {
  type?: string | null;
  title?: string | null;
  detail?: string | null;
  /** Validation errors: a list with one object per field, keyed by JSON path such as "$.alias". */
  errors?: Array<Record<string, string[]>>;
  bannerKeys?: string[];
}

export interface ApiResult<T> {
  data?: T;
  status: number;
  problem?: ApiProblem;
  /** The Location header of a 201 response. */
  location?: string;
}

interface HeyApiResult {
  data?: unknown;
  error?: unknown;
  response?: Response;
}

/** Runs a generated SDK call and turns the outcome, including a failed request, into a plain result. */
export async function send<T>(request: Promise<HeyApiResult>): Promise<ApiResult<T>> {
  try {
    const result = await request;
    const status = result.response?.status ?? 0;
    const ok = status >= 200 && status < 300;
    return {
      data: ok ? (result.data as T) : undefined,
      status,
      problem: ok ? undefined : (result.error as ApiProblem | undefined),
      location: result.response?.headers.get("Location") ?? undefined,
    };
  } catch {
    return { status: 0 };
  }
}

/** The key at the end of a Location header. */
export function keyFromLocation(location: string | undefined): string | undefined {
  return location?.split("?")[0].replace(/\/+$/, "").split("/").pop();
}

/** Flattens the validation errors of a problem into field name to first message ("$.alias" becomes "alias"). */
export function fieldErrors(problem: ApiProblem | undefined): Record<string, string> {
  const result: Record<string, string> = {};
  for (const entry of problem?.errors ?? []) {
    for (const [path, messages] of Object.entries(entry)) {
      result[path.replace(/^\$\./, "")] = messages[0] ?? "";
    }
  }
  return result;
}

/** Moves to a path inside the back office (relative to the back office base) without a page reload. */
export function navigate(path: string): void {
  const url = new URL(path, document.baseURI);
  history.pushState({}, "", url.pathname + url.search);
}
