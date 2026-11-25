import { describe, expect, it } from "vitest";
import type { HistoryEntryModel, HistoryPageModel } from "../api/types.gen.js";
import type { ApiResult } from "./api.js";
import { HistoryPager, type HistoryLoader } from "./history-pager.js";

const entry = (n: number): HistoryEntryModel => ({ userName: `Invented user ${n}`, action: "save", date: "2027-03-01T10:00:00Z" });
const entries = (from: number, count: number) => Array.from({ length: count }, (_, i) => entry(from + i));
const ok = (items: HistoryEntryModel[], total: number): ApiResult<HistoryPageModel> => ({ status: 200, data: { items, total } });
const failed: ApiResult<HistoryPageModel> = { status: 500 };

/** A loader whose answers are released by the test, in any order. */
function controlledLoader() {
  const calls: Array<{ skip: number; take: number; resolve: (result: ApiResult<HistoryPageModel>) => void }> = [];
  const load: HistoryLoader = (skip, take) =>
    new Promise((resolve) => {
      calls.push({ skip, take, resolve });
    });
  return { load, calls };
}

describe("HistoryPager stale responses", () => {
  it("ignores the answer of an older request when a newer one was started", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 20);

    const first = pager.reload();
    const second = pager.reload();
    calls[1].resolve(ok(entries(100, 2), 2));
    await second;
    calls[0].resolve(ok(entries(1, 5), 5)); // the slow, older answer arrives last
    await first;

    expect(pager.entries.map((e) => e.userName)).toEqual(["Invented user 100", "Invented user 101"]);
    expect(pager.total).toBe(2);
    expect(pager.loading).toBe(false);
  });

  it("ignores a stale failure as well", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 20);

    const first = pager.reload();
    const second = pager.reload();
    calls[1].resolve(ok(entries(1, 1), 1));
    await second;
    calls[0].resolve(failed);
    await first;

    expect(pager.entries).toHaveLength(1);
    expect(pager.error).toBe("none");
  });

  it("does not append a page that was asked for before a reload", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 2);
    const initial = pager.reload();
    calls[0].resolve(ok(entries(1, 2), 5));
    await initial;

    const more = pager.more();
    const reload = pager.reload();
    calls[2].resolve(ok(entries(50, 2), 2));
    await reload;
    calls[1].resolve(ok(entries(3, 2), 5));
    await more;

    expect(pager.entries.map((e) => e.userName)).toEqual(["Invented user 50", "Invented user 51"]);
  });
});

describe("HistoryPager paging", () => {
  it("asks for the next page after the rows it holds", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 2);
    const initial = pager.reload();
    calls[0].resolve(ok(entries(1, 2), 3));
    await initial;

    const more = pager.more();
    expect(calls[1].skip).toBe(2);
    expect(calls[1].take).toBe(2);
    calls[1].resolve(ok(entries(3, 1), 3));
    await more;

    expect(pager.entries).toHaveLength(3);
    expect(pager.canLoadMore).toBe(false);
  });

  it("stops offering more at the cap, even when the total is higher", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 20, 40);
    const initial = pager.reload();
    calls[0].resolve(ok(entries(1, 20), 500));
    await initial;
    expect(pager.canLoadMore).toBe(true);

    const more = pager.more();
    calls[1].resolve(ok(entries(21, 20), 500));
    await more;

    expect(pager.entries).toHaveLength(40);
    expect(pager.canLoadMore).toBe(false);
  });

  it("keeps the loaded rows and reports an inline error when a later page fails", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 2);
    const initial = pager.reload();
    calls[0].resolve(ok(entries(1, 2), 6));
    await initial;
    expect(pager.error).toBe("none");

    const more = pager.more();
    calls[1].resolve(failed);
    await more;

    expect(pager.entries).toHaveLength(2);
    expect(pager.error).toBe("more");
    expect(pager.canLoadMore).toBe(true); // the editor can retry

    const retry = pager.more();
    calls[2].resolve(ok(entries(3, 2), 6));
    await retry;
    expect(pager.entries).toHaveLength(4);
    expect(pager.error).toBe("none");
  });

  it("reports a failure of the first page as a full error", async () => {
    const { load, calls } = controlledLoader();
    const pager = new HistoryPager(load, 2);
    const initial = pager.reload();
    calls[0].resolve(failed);
    await initial;

    expect(pager.entries).toHaveLength(0);
    expect(pager.error).toBe("first");
  });

  it("tells its owner when the state changes", async () => {
    const { load, calls } = controlledLoader();
    let changes = 0;
    const pager = new HistoryPager(load, 2, 10_000, () => changes++);

    const initial = pager.reload();
    calls[0].resolve(ok(entries(1, 1), 1));
    await initial;

    expect(changes).toBeGreaterThanOrEqual(2); // loading started, then the rows arrived
  });
});
