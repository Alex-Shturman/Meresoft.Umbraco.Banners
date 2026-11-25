import type { BannerFlagsModel, BannerState } from "../api/types.gen.js";

/** Localization key and tag color of a publish state. */
export const STATE_DISPLAY: Record<BannerState, { key: string; color: "default" | "positive" | "warning" | "danger" }> = {
  Draft: { key: "meresoftBanners_stateDraft", color: "default" },
  Published: { key: "meresoftBanners_statePublished", color: "positive" },
  PublishedWithChanges: { key: "meresoftBanners_statePublishedWithChanges", color: "warning" },
  Unpublished: { key: "meresoftBanners_stateUnpublished", color: "danger" },
};

/** The flags that are on, as localization keys with a tag color. */
export function activeFlags(flags: BannerFlagsModel): Array<{ key: string; color: "warning" | "danger" }> {
  const result: Array<{ key: string; color: "warning" | "danger" }> = [];
  if (flags.notPlaced) {
    result.push({ key: "meresoftBanners_flagNotPlaced", color: "warning" });
  }
  if (flags.imageMissing) {
    result.push({ key: "meresoftBanners_flagImageMissing", color: "danger" });
  }
  return result;
}
