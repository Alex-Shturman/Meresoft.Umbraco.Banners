import { describe, expect, it } from "vitest";
import { loadAllPages } from "./paging.js";

/** A fake list endpoint over `total` numbered items that records the pages asked for. */
function fakeEndpoint(total: number) {
  const calls: Array<{ skip: number; take: number }> = [];
  return {
    calls,
    fetchPage: async (skip: number, take: number) => {
      calls.push({ skip, take });
      const items = Array.from({ length: Math.max(0, Math.min(take, total - skip)) }, (_, i) => skip + i);
      return { items, total };
    },
  };
}

describe("loadAllPages", () => {
  it("pages until the total is reached", async () => {
    const endpoint = fakeEndpoint(250);

    const all = await loadAllPages(endpoint.fetchPage, 100);

    expect(all).toHaveLength(250);
    expect(all?.[249]).toBe(249);
    expect(endpoint.calls).toEqual([
      { skip: 0, take: 100 },
      { skip: 100, take: 100 },
      { skip: 200, take: 100 },
    ]);
  });

  it("asks once when everything fits in one page, and not again for a page that is exactly full", async () => {
    const endpoint = fakeEndpoint(100);

    const all = await loadAllPages(endpoint.fetchPage, 100);

    expect(all).toHaveLength(100);
    expect(endpoint.calls).toHaveLength(1);
  });

  it("gives an empty list for an empty endpoint", async () => {
    expect(await loadAllPages(fakeEndpoint(0).fetchPage, 100)).toEqual([]);
  });

  it("stops at an empty page even when the total promises more, instead of looping", async () => {
    const calls: number[] = [];
    const all = await loadAllPages(async (skip) => {
      calls.push(skip);
      return { items: skip === 0 ? [1, 2] : [], total: 999 };
    }, 2);

    expect(all).toEqual([1, 2]);
    expect(calls).toEqual([0, 2]);
  });

  it("gives undefined when a page cannot be loaded, so the caller can tell a failure from an empty list", async () => {
    let call = 0;
    const all = await loadAllPages(async () => (++call === 1 ? { items: [1, 2], total: 4 } : undefined), 2);

    expect(all).toBeUndefined();
  });

  it("stops after a bounded number of pages", async () => {
    const calls: number[] = [];
    const all = await loadAllPages(async (skip) => {
      calls.push(skip);
      return { items: [skip], total: Number.MAX_SAFE_INTEGER };
    }, 1);

    expect(all).toBeDefined();
    expect(calls.length).toBeLessThanOrEqual(100);
  });
});
