import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/** The package script run against a small fake of the browser: banners that become visible, timers, and the beacon. */

interface FakeElement {
  dataset: Record<string, string>;
  closest: () => { dataset: Record<string, string> };
}

let observerCallback: (entries: Array<{ target: FakeElement; isIntersecting: boolean; intersectionRatio: number }>) => void;
let observed: FakeElement[];
let sent: Array<{ url: string; lines: string[] }>;
let beaconWorks: boolean;
let fetched: Array<{ url: string; init: RequestInit }>;

class FakeIntersectionObserver {
  constructor(callback: typeof observerCallback) {
    observerCallback = callback;
  }
  observe(element: FakeElement) {
    observed.push(element);
  }
  unobserve() {}
}

function zone(base: string, token: string) {
  return { dataset: { mbBase: base, mbzToken: token } };
}

function banners(count: number, owner: { dataset: Record<string, string> }, firstId = 1): FakeElement[] {
  return Array.from({ length: count }, (_, i) => ({ dataset: { mbb: String(firstId + i) }, closest: () => owner }));
}

async function runScriptWith(elements: FakeElement[]) {
  vi.resetModules();
  vi.stubGlobal("IntersectionObserver", FakeIntersectionObserver);
  vi.stubGlobal("window", { IntersectionObserver: FakeIntersectionObserver, setTimeout: (f: () => void, ms: number) => globalThis.setTimeout(f, ms) });
  vi.stubGlobal("document", {
    visibilityState: "visible",
    querySelectorAll: () => elements,
    addEventListener: () => {},
  });
  vi.stubGlobal("addEventListener", () => {});
  vi.stubGlobal("navigator", {
    sendBeacon: (url: string, body: string) => {
      if (beaconWorks) {
        sent.push({ url, lines: body.split("\n") });
      }
      return beaconWorks;
    },
  });
  vi.stubGlobal("fetch", (url: string, init: RequestInit) => {
    fetched.push({ url, init });
    return Promise.resolve(new Response(null, { status: 204 }));
  });
  await import("../../script/banners");
}

/** Makes every observed banner visible, waits the hold time, then waits for the send timer. */
function seeEverything() {
  observerCallback(observed.map((target) => ({ target, isIntersecting: true, intersectionRatio: 1 })));
  vi.advanceTimersByTime(1_000);
  vi.advanceTimersByTime(2_000);
}

beforeEach(() => {
  vi.useFakeTimers();
  observed = [];
  sent = [];
  fetched = [];
  beaconWorks = true;
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("the package script", () => {
  it("sends the reports of a zone in requests of at most 50 lines, as the server reads at most 50", async () => {
    const owner = zone("/_banners", "tok");
    await runScriptWith(banners(120, owner));

    seeEverything();

    expect(sent.map((s) => s.lines.length)).toEqual([50, 50, 20]);
    expect(new Set(sent.map((s) => s.url))).toEqual(new Set(["/_banners/seen"]));
    expect(sent.flatMap((s) => s.lines)).toHaveLength(120);
    expect(sent[0].lines[0]).toBe("tok 1");
    expect(sent[2].lines[19]).toBe("tok 120");
  });

  it("sends one request when there are exactly 50 lines, and one when there are fewer", async () => {
    await runScriptWith(banners(50, zone("/_banners", "tok")));

    seeEverything();

    expect(sent.map((s) => s.lines.length)).toEqual([50]);
  });

  it("keeps the endpoint of each zone apart", async () => {
    const first = zone("/_banners", "a");
    const second = zone("/ads-x", "b");
    await runScriptWith([...banners(2, first, 1), ...banners(3, second, 10)]);

    seeEverything();

    expect(sent.map((s) => [s.url, s.lines.length])).toEqual([
      ["/_banners/seen", 2],
      ["/ads-x/seen", 3],
    ]);
  });

  it("falls back to fetch with keepalive and no credentials when the beacon is refused, also in chunks", async () => {
    beaconWorks = false;
    await runScriptWith(banners(60, zone("/_banners", "tok")));

    seeEverything();

    expect(fetched).toHaveLength(2);
    expect(fetched[0].init).toMatchObject({ method: "POST", keepalive: true, credentials: "omit" });
    expect((fetched[0].init.body as string).split("\n")).toHaveLength(50);
    expect((fetched[1].init.body as string).split("\n")).toHaveLength(10);
  });

  it("counts nothing for a banner that leaves the view before a second has passed", async () => {
    await runScriptWith(banners(1, zone("/_banners", "tok")));
    observerCallback(observed.map((target) => ({ target, isIntersecting: true, intersectionRatio: 1 })));

    vi.advanceTimersByTime(999);
    observerCallback(observed.map((target) => ({ target, isIntersecting: false, intersectionRatio: 0 })));
    vi.advanceTimersByTime(10_000);

    expect(sent).toHaveLength(0);
  });
});
