import { SECTION_ALIAS } from "../section/manifests.js";

// The reports view and its own localization entry (the Umbraco localization registry merges entries of one culture).
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "sectionRoute",
    alias: "Meresoft.Banners.SectionRoute.Reports",
    name: "Meresoft Banners Reports Route",
    element: () => import("./reports-view.element.js"),
    meta: { path: "reports" },
    conditions: [{ alias: "Umb.Condition.SectionAlias", match: SECTION_ALIAS }],
  } as unknown as UmbExtensionManifest,
  {
    type: "localization",
    alias: "Meresoft.Banners.Localization.Reports.En",
    name: "English Meresoft Banners Reports Localization",
    weight: 100,
    meta: {
      culture: "en",
    },
    js: () => import("./reports-lang-en.js"),
  },
];
