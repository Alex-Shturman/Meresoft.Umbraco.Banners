import type { HistoryEntryModel, HistoryPageModel } from "../api/types.gen.js";
import type { ApiResult } from "./api.js";

/** Loads one page of a history: the banner and the zone history views each pass their own endpoint. */
export type HistoryLoader = (skip: number, take: number) => Promise<ApiResult<HistoryPageModel>>;

/** What went wrong, if anything: the first page (nothing to show) or a later page (the loaded rows stay). */
export type HistoryError = "none" | "first" | "more";

/** The server refuses to skip more than this many entries, so the view stops offering "show more" here. */
export const MAX_HISTORY_ENTRIES = 10_000;

/**
 * The state of a history list that grows page by page. It has no DOM, so it can be tested alone. Every request gets
 * a number, and an answer that is not from the latest request is ignored, so a slow answer of an old request can
 * never overwrite or extend the list of a newer one.
 */
export class HistoryPager {
  entries: HistoryEntryModel[] = [];
  total = 0;
  loading = false;
  error: HistoryError = "none";

  #request = 0;

  constructor(
    private readonly load: HistoryLoader,
    private readonly pageSize = 20,
    private readonly maxEntries = MAX_HISTORY_ENTRIES,
    private readonly changed: () => void = () => undefined,
  ) {}

  /** True while more entries exist and the cap is not reached; stays true after a failed page so it can be retried. */
  get canLoadMore(): boolean {
    return this.entries.length > 0 && this.entries.length < this.total && this.entries.length < this.maxEntries;
  }

  /** Starts again from the newest entry. */
  async reload(): Promise<void> {
    this.entries = [];
    this.total = 0;
    this.error = "none";
    await this.more();
  }

  /** Loads the page after the rows already held; a failure keeps those rows and sets `error` to "more". */
  async more(): Promise<void> {
    const request = ++this.#request;
    const skip = this.entries.length;
    this.loading = true;
    this.error = "none";
    this.changed();

    const result = await this.load(skip, this.pageSize);
    if (request !== this.#request) {
      return; // A newer request started meanwhile; its answer decides what is shown.
    }

    if (result.data) {
      this.entries = [...this.entries, ...result.data.items];
      this.total = result.data.total;
    } else {
      this.error = skip === 0 ? "first" : "more";
    }
    this.loading = false;
    this.changed();
  }
}
