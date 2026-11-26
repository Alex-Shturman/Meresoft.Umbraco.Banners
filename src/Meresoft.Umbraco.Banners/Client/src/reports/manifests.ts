import { SECTION_ALIAS } from "../section/manifests.js";

// The reports view. Its texts are in lang/en.ts with the others.
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "sectionRoute",
    alias: "Meresoft.Banners.SectionRoute.Reports",
    name: "Meresoft Banners Reports Route",
    element: () => import("./reports-view.element.js"),
    api: () => import("../section/section-route.api.js"),
    meta: { path: "reports" },
    conditions: [{ alias: "Umb.Condition.SectionAlias", match: SECTION_ALIAS }],
  } as unknown as UmbExtensionManifest,
];
