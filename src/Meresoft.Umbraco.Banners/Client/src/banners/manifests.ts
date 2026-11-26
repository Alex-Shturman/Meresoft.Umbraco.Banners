import { SECTION_ALIAS } from "../section/manifests.js";

// The manifest type of a section route does not declare conditions, but the extension system
// applies them to every manifest; the condition keeps the route inside the Banners section.
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "sectionRoute",
    alias: "Meresoft.Banners.SectionRoute.Banners",
    name: "Meresoft Banners Banners Route",
    element: () => import("./banners-section.element.js"),
    api: () => import("../section/section-route.api.js"),
    meta: { path: "banners" },
    conditions: [{ alias: "Umb.Condition.SectionAlias", match: SECTION_ALIAS }],
  } as unknown as UmbExtensionManifest,
];
