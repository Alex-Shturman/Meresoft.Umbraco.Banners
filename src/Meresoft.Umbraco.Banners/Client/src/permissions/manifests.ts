/** Verb stored in the fallback permissions of a user group; the server checks the same string. */
export const HTML_PERMISSION_VERB = "Meresoft.Banners.Html";

/** Entity type of a banner, used by the user group editor to group the permission. */
export const BANNER_ENTITY_TYPE = "meresoft-banner";

// The user group editor lists the permission under the heading of the localization key
// user_permissionsEntityGroup_meresoft-banner (see lang/en.ts).
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "entityUserPermission",
    alias: "Meresoft.Banners.EntityUserPermission.Html",
    name: "Meresoft Banners HTML Permission",
    forEntityTypes: [BANNER_ENTITY_TYPE],
    meta: {
      verbs: [HTML_PERMISSION_VERB],
      label: "#meresoftBanners_htmlPermissionLabel",
      description: "#meresoftBanners_htmlPermissionDescription",
    },
  } as unknown as UmbExtensionManifest,
];
