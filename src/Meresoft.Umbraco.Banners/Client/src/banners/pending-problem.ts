/** What the edit page of a banner shows after Publish on a new banner created it but could not publish it. */
export interface PendingProblem {
  key: string;
  errors: Record<string, string>;
  problem: string;
  conflict: boolean;
}

/** How long a problem waits for the edit page to load; a longer wait means the editor went elsewhere. */
const TIME_TO_LIVE_MS = 10_000;

/**
 * Hands one problem from the editor that created a banner to the editor that loads its edit address (the router may
 * build a new one). It is taken once, only by that banner, and only while fresh, so an abandoned failure is never
 * shown later. It has no DOM.
 */
export class PendingProblemStore {
  #pending?: { problem: PendingProblem; expires: number };

  constructor(
    private readonly timeToLiveMs = TIME_TO_LIVE_MS,
    private readonly now: () => number = () => Date.now(),
  ) {}

  set(problem: PendingProblem): void {
    this.#pending = { problem, expires: this.now() + this.timeToLiveMs };
  }

  /** The problem of this banner if it is still fresh; any load of any banner clears what was waiting. */
  take(key: string | undefined): PendingProblem | undefined {
    const pending = this.#pending;
    this.#pending = undefined;
    return pending && pending.problem.key === key && this.now() <= pending.expires ? pending.problem : undefined;
  }
}
