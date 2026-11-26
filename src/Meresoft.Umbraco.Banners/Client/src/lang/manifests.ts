import type {} from "@umbraco-cms/backoffice/localization";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "localization",
    alias: "Meresoft.Banners.Localization.En",
    name: "English Meresoft Banners Localization",
    weight: 100,
    meta: {
      culture: "en",
    },
    js: () => import("./en.js"),
  },
];
