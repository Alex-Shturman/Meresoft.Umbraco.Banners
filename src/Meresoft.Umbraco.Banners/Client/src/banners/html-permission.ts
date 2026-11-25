import { HTML_PERMISSION_VERB } from "../permissions/manifests.js";

/**
 * Whether the signed in user may create and change HTML: any of the user's groups has the verb as a fallback
 * permission, which is what the `Umb.Condition.UserPermission.Fallback` condition checks too. The server enforces
 * the same rule on every write; this only decides what the editor lets a person type.
 */
export function canEditHtml(fallbackPermissions: readonly string[] | undefined): boolean {
  return fallbackPermissions?.includes(HTML_PERMISSION_VERB) ?? false;
}

/** How the HTML code field behaves: read-only with an explaining note without the permission. */
export function htmlFieldAccess(canEdit: boolean): { readonly: boolean; noteKey: string } {
  return canEdit
    ? { readonly: false, noteKey: "meresoftBanners_bannerHtmlDescription" }
    : { readonly: true, noteKey: "meresoftBanners_bannerHtmlLocked" };
}

/** The banner types the create form offers. */
export function offeredBannerTypes(canEdit: boolean): Array<"Image" | "Html"> {
  return canEdit ? ["Image", "Html"] : ["Image"];
}
