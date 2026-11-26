import type { UmbRouteEntry } from "@umbraco-cms/backoffice/router";

// A sectionRoute is an element-and-api extension: the section view creates an api for every route and
// logs an error when there is none. This one has nothing to do, so the route path comes from `meta.path`.
export class MeresoftBannersSectionRouteApi implements UmbRouteEntry {
  destroy(): void {}
}

export { MeresoftBannersSectionRouteApi as api };
