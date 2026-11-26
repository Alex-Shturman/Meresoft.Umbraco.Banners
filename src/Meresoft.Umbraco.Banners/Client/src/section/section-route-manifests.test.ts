import { describe, expect, it } from "vitest";
import { manifests as banners } from "../banners/manifests.js";
import { manifests as reports } from "../reports/manifests.js";
import { manifests as zones } from "../zones/manifests.js";

// Umbraco's section view (default-section.element.js) calls createExtensionApi for every sectionRoute
// without a fallback. A manifest with neither `api` nor `js` logs "did not succeed creating an API class
// instance" in the console, because ManifestSectionRoute is an element-and-api manifest.
type Route = Record<string, unknown> & { alias: string; meta: { path: string } };
const routes = [...banners, ...reports, ...zones].filter((m) => m.type === "sectionRoute") as unknown as Route[];

describe("section route manifests", () => {
  it("covers the three routes", () => {
    expect(routes.map((r) => r.meta.path).sort()).toEqual(["banners", "reports", "zones"]);
  });

  it.each(routes.map((r) => [r.alias, r] as const))("%s has an element and an api", async (_alias, route) => {
    expect(route.element).toBeTypeOf("function");
    expect(route.api).toBeTypeOf("function");
    const loaded = (await (route.api as () => Promise<{ api?: unknown; default?: unknown }>)());
    expect(loaded.api ?? loaded.default).toBeTypeOf("function");
  });
});
