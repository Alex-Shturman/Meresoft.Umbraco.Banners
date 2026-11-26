import { SECTION_ALIAS } from "../section/manifests.js";

// The manifest type of a section route does not declare conditions, but the extension system
// applies them to every manifest; the condition keeps the route inside the Banners section.
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "sectionRoute",
    alias: "Meresoft.Banners.SectionRoute.Zones",
    name: "Meresoft Banners Zones Route",
    element: () => import("./zones-section.element.js"),
    api: () => import("../section/section-route.api.js"),
    meta: { path: "zones" },
    conditions: [{ alias: "Umb.Condition.SectionAlias", match: SECTION_ALIAS }],
  } as unknown as UmbExtensionManifest,
];
