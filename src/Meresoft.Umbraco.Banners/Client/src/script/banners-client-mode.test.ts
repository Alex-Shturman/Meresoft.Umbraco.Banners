import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * The package script in client mode (US4), run against a small fake DOM: the delivery call, the elements it builds
 * (always through setAttribute, never by string concatenation), the fallback that stays when nothing comes back,
 * and the Html banner scripts that are re-created so the browser runs them.
 */

class FakeElement {
  attributes = new Map<string, string>();
  children: FakeElement[] = [];
  dataset: Record<string, string> = {};
  textContent = "";
  parent: FakeElement | null = null;
  /** True once the test (the fake parser) made this element from html text; false for elements the script created. */
  parsed = false;
  htmlSetCount = 0;
  private html = "";

  constructor(public tag: string) {}

  setAttribute(name: string, value: string) {
    this.attributes.set(name, value);
    // The browser mirrors data-* attributes into dataset.
    if (name.startsWith("data-")) {
      this.dataset[name.slice(5).replace(/-(\w)/g, (_, c: string) => c.toUpperCase())] = value;
    }
  }
  getAttribute(name: string) {
    return this.attributes.get(name) ?? null;
  }
  getAttributeNames() {
    return [...this.attributes.keys()];
  }
  append(...nodes: FakeElement[]) {
    nodes.forEach((n) => {
      n.parent = this;
      this.children.push(n);
    });
  }
  replaceChildren(...nodes: FakeElement[]) {
    this.children = [];
    this.append(...nodes);
  }
  replaceWith(node: FakeElement) {
    const siblings = this.parent!.children;
    siblings[siblings.indexOf(this)] = node;
    node.parent = this.parent;
  }
  /** The fake parser: only `<script ...>text</script>` is understood, which is all the tests need. */
  set innerHTML(value: string) {
    this.html = value;
    this.htmlSetCount++;
    this.children = [];
    for (const match of value.matchAll(/<script([^>]*)>([\s\S]*?)<\/script>/g)) {
      const script = new FakeElement("script");
      script.parsed = true;
      for (const attr of match[1].matchAll(/(\w+)(?:="([^"]*)")?/g)) {
        script.setAttribute(attr[1], attr[2] ?? "");
      }
      script.textContent = match[2];
      this.append(script);
    }
  }
  get innerHTML() {
    return this.html;
  }
  querySelectorAll(selector: string): FakeElement[] {
    return this.children.filter((c) => c.tag === selector);
  }
}

let observerCallback: (entries: Array<{ target: FakeElement; isIntersecting: boolean; intersectionRatio: number }>) => void;
let observed: FakeElement[];
let sent: Array<{ url: string; body: string }>;
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

function placeholder(alias: string, base = "/_banners") {
  const zone = new FakeElement("div");
  zone.dataset = { mbz: alias, mbBase: base, mbzMode: "client" };
  const fallback = new FakeElement("p");
  zone.append(fallback);
  return { zone, fallback };
}

async function runScript(zones: FakeElement[], answer: () => Promise<Response>, times = 1) {
  vi.stubGlobal("IntersectionObserver", FakeIntersectionObserver);
  vi.stubGlobal("window", { IntersectionObserver: FakeIntersectionObserver, setTimeout: (f: () => void, ms: number) => globalThis.setTimeout(f, ms) });
  vi.stubGlobal("document", {
    visibilityState: "visible",
    createElement: (tag: string) => new FakeElement(tag),
    querySelectorAll: (selector: string) => (selector.includes("client") ? zones : []),
    addEventListener: () => {},
  });
  vi.stubGlobal("addEventListener", () => {});
  vi.stubGlobal("navigator", {
    sendBeacon: (url: string, body: string) => {
      sent.push({ url, body });
      return true;
    },
  });
  vi.stubGlobal("fetch", (url: string, init: RequestInit) => {
    fetched.push({ url, init });
    return answer();
  });
  for (let run = 0; run < times; run++) {
    vi.resetModules();
    await import("../../script/banners");
  }
  await vi.advanceTimersByTimeAsync(0);
}

const json = (body: unknown) => () => Promise.resolve(new Response(JSON.stringify(body), { status: 200, headers: { "content-type": "application/json" } }));

const imageBanner = {
  id: 12,
  type: "image",
  clickUrl: "/_banners/c/12?z=3",
  newWindow: true,
  imageUrl: "/media/abc/banner.png?width=728&height=90",
  width: 728,
  height: 90,
  alt: "Invented summer promo 2027",
  title: null,
};

beforeEach(() => {
  vi.useFakeTimers();
  observed = [];
  sent = [];
  fetched = [];
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("the package script in client mode", () => {
  it("asks the delivery endpoint of the zone, without credentials", async () => {
    const { zone } = placeholder("search-top", "/ads-x");

    await runScript([zone], json({ zone: "search-top", banners: [] }));

    expect(fetched).toHaveLength(1);
    expect(fetched[0].url).toBe("/ads-x/zone/search-top");
    expect(fetched[0].init).toMatchObject({ credentials: "omit" });
  });

  it("builds an image banner through the DOM and replaces the fallback", async () => {
    const { zone, fallback } = placeholder("search-top");

    await runScript([zone], json({ zone: "search-top", token: "tok", banners: [imageBanner] }));

    expect(zone.children).toHaveLength(1);
    expect(zone.children).not.toContain(fallback);
    const box = zone.children[0];
    expect(box.attributes.get("class")).toBe("mb-banner");
    expect(box.attributes.get("data-mbb")).toBe("12");
    const link = box.children[0];
    expect(link.tag).toBe("a");
    expect(Object.fromEntries(link.attributes)).toEqual({ href: "/_banners/c/12?z=3", rel: "sponsored noopener", target: "_blank" });
    const image = link.children[0];
    expect(Object.fromEntries(image.attributes)).toEqual({
      src: "/media/abc/banner.png?width=728&height=90",
      alt: "Invented summer promo 2027",
      width: "728",
      height: "90",
      loading: "lazy",
      decoding: "async",
    });
    expect(zone.dataset.mbzToken).toBe("tok");
  });

  it("writes a title and leaves out target when the banner has a title and stays in the window", async () => {
    const { zone } = placeholder("z");

    await runScript([zone], json({ zone: "z", token: "t", banners: [{ ...imageBanner, newWindow: false, title: "Invented title", width: null, height: null }] }));

    const link = zone.children[0].children[0];
    expect(link.attributes.get("title")).toBe("Invented title");
    expect(link.attributes.has("target")).toBe(false);
    expect(link.children[0].attributes.has("width")).toBe(false);
  });

  it("keeps values as attribute values: markup in an alt text is never parsed", async () => {
    const { zone } = placeholder("z");
    const hostile = '"><script>alert(1)</script>';

    await runScript([zone], json({ zone: "z", token: "t", banners: [{ ...imageBanner, alt: hostile, title: hostile }] }));

    const link = zone.children[0].children[0];
    expect(link.children[0].attributes.get("alt")).toBe(hostile);
    expect(link.attributes.get("title")).toBe(hostile);
    expect(zone.children[0].htmlSetCount).toBe(0);
    expect(link.htmlSetCount).toBe(0);
  });

  it("observes each rendered banner, and a seen report then carries the token of the response", async () => {
    const { zone } = placeholder("z");

    await runScript([zone], json({ zone: "z", token: "tok-9", banners: [imageBanner, { id: 15, type: "html", html: "<b>x</b>" }] }));

    expect(observed.map((e) => e.attributes.get("data-mbb"))).toEqual(["12", "15"]);
    // The banner element finds its zone through closest(); the fake wires it as the browser would.
    const first = observed[0] as FakeElement & { closest?: () => FakeElement };
    first.closest = () => zone;
    observerCallback([{ target: first, isIntersecting: true, intersectionRatio: 1 }]);
    vi.advanceTimersByTime(1_000);
    vi.advanceTimersByTime(2_000);

    expect(sent).toEqual([{ url: "/_banners/seen", body: "tok-9 12" }]);
  });

  it.each([
    ["an address on another site", "https://evil.example/c/12?z=3"],
    ["a protocol relative address", "//evil.example/c/12"],
    ["a javascript address", "javascript:alert(1)"],
    ["a path outside the click endpoint", "/_banners/other/12"],
    ["a click path of another base", "/ads-x/c/12?z=3"],
  ])("skips an image banner whose click address is %s, and renders the others", async (_name, clickUrl) => {
    const { zone } = placeholder("z");

    await runScript([zone], json({ zone: "z", token: "t", banners: [{ ...imageBanner, clickUrl }, { ...imageBanner, id: 13, clickUrl: "/_banners/c/13?z=3" }] }));

    expect(zone.children.map((c) => c.attributes.get("data-mbb"))).toEqual(["13"]);
    expect(observed.map((e) => e.attributes.get("data-mbb"))).toEqual(["13"]);
  });

  it("keeps the fallback when every banner was skipped for its click address", async () => {
    const { zone, fallback } = placeholder("z");

    await runScript([zone], json({ zone: "z", token: "t", banners: [{ ...imageBanner, clickUrl: "https://evil.example/" }] }));

    expect(zone.children).toEqual([fallback]);
    expect(zone.dataset.mbzToken).toBeUndefined();
  });

  it("keeps the fallback when no banner came back", async () => {
    const { zone, fallback } = placeholder("z");

    await runScript([zone], json({ zone: "z", banners: [] }));

    expect(zone.children).toEqual([fallback]);
    expect(observed).toHaveLength(0);
  });

  it.each([
    ["a server error", () => Promise.resolve(new Response("nope", { status: 500 }))],
    ["a 404", () => Promise.resolve(new Response("", { status: 404 }))],
    ["a network failure", () => Promise.reject(new TypeError("offline"))],
    ["text that is not JSON", () => Promise.resolve(new Response("<html>", { status: 200 }))],
  ])("keeps the fallback and throws nothing after %s", async (_name, answer) => {
    const { zone, fallback } = placeholder("z");

    await runScript([zone], answer);

    expect(zone.children).toEqual([fallback]);
    expect(observed).toHaveLength(0);
  });

  it("re-creates the script elements of an Html banner so the browser runs them", async () => {
    const { zone } = placeholder("z");
    const html = '<div>offer</div><script src="https://example.org/ad.js" async>window.x = 1;</script>';

    await runScript([zone], json({ zone: "z", token: "t", banners: [{ id: 15, type: "html", html }] }));

    const box = zone.children[0];
    expect(box.attributes.get("class")).toBe("mb-banner mb-html");
    expect(box.attributes.get("data-mbb")).toBe("15");
    const script = box.children.find((c) => c.tag === "script")!;
    expect(script.parsed, "a script made by the parser is inert, the script must be a fresh element").toBe(false);
    expect(script.attributes.get("src")).toBe("https://example.org/ad.js");
    expect(script.attributes.has("async")).toBe(true);
    expect(script.textContent).toBe("window.x = 1;");
  });

  it.each([
    ["no endpoint base", { mbz: "z" }],
    ["no zone alias", { mbBase: "/_banners" }],
  ])("skips a placeholder with %s, and makes no request", async (_name, dataset) => {
    const { zone, fallback } = placeholder("z");
    zone.dataset = { ...dataset, mbzMode: "client" };

    await runScript([zone], json({ zone: "z", token: "t", banners: [imageBanner] }));

    expect(fetched).toHaveLength(0);
    expect(zone.children).toEqual([fallback]);
  });

  it("loads a placeholder once when the script runs twice, so a banner is not observed or counted twice", async () => {
    const { zone } = placeholder("z");

    await runScript([zone], json({ zone: "z", token: "tok", banners: [imageBanner] }), 2);

    expect(fetched).toHaveLength(1);
    expect(observed.map((e) => e.attributes.get("data-mbb"))).toEqual(["12"]);
  });

  it("starts the requests of several placeholders independently", async () => {
    const a = placeholder("a");
    const b = placeholder("b", "/ads-x");

    await runScript([a.zone, b.zone], json({ zone: "x", banners: [] }));

    expect(fetched.map((f) => f.url)).toEqual(["/_banners/zone/a", "/ads-x/zone/b"]);
  });
});
