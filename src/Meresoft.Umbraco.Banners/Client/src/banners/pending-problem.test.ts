import { describe, expect, it } from "vitest";
import { PendingProblemStore } from "./pending-problem.js";

const problem = (key: string) => ({ key, errors: { zoneKeys: "Pick a zone." }, problem: "", conflict: false });

describe("PendingProblemStore", () => {
  it("hands the problem to the editor that loads the banner, once", () => {
    const store = new PendingProblemStore();
    store.set(problem("a"));
    expect(store.take("a")).toEqual(problem("a"));
    expect(store.take("a")).toBeUndefined();
  });

  it("drops the problem when another banner loads first, so it is not shown later on the first banner", () => {
    const store = new PendingProblemStore();
    store.set(problem("a"));
    expect(store.take("b")).toBeUndefined();
    expect(store.take("a")).toBeUndefined();
  });

  it("drops a problem nobody took within the time to live", () => {
    let now = 1_000;
    const store = new PendingProblemStore(10_000, () => now);
    store.set(problem("a"));
    now += 10_001;
    expect(store.take("a")).toBeUndefined();
  });

  it("keeps a problem that is taken within the time to live", () => {
    let now = 1_000;
    const store = new PendingProblemStore(10_000, () => now);
    store.set(problem("a"));
    now += 9_999;
    expect(store.take("a")).toEqual(problem("a"));
  });

  it("keeps only the latest problem", () => {
    const store = new PendingProblemStore();
    store.set(problem("a"));
    store.set(problem("b"));
    expect(store.take("a")).toBeUndefined();
  });
});
