import type { BannerFlagsModel, BannerState, BannerType } from "../api/types.gen.js";

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
  if (flags.scheduled) {
    result.push({ key: "meresoftBanners_flagScheduled", color: "warning" });
  }
  if (flags.expired) {
    result.push({ key: "meresoftBanners_flagExpired", color: "danger" });
  }
  if (flags.limitReached) {
    result.push({ key: "meresoftBanners_flagLimitReached", color: "danger" });
  }
  return result;
}

/** Html banners are rendered as stored and their clicks are not counted, so only image banners have a click limit (FR-006). */
export function showsClickLimit(type: BannerType): boolean {
  return type !== "Html";
}

/** The reading of a limit field: no limit, a whole number of 1 or more, or not acceptable. */
export type LimitParse = { ok: true; value: number | null } | { ok: false };

/** Reads a limit field. An empty field is no limit; anything but a whole number from 1 up to the largest safe integer is refused. */
export function parseLimit(text: string): LimitParse {
  const trimmed = text.trim();
  if (trimmed === "") {
    return { ok: true, value: null };
  }
  if (!/^\d+$/.test(trimmed)) {
    return { ok: false };
  }
  const value = Number(trimmed);
  return value >= 1 && Number.isSafeInteger(value) ? { ok: true, value } : { ok: false };
}

/** The text of a limit field for a stored limit. */
export function limitToText(limit: number | null | undefined): string {
  return limit == null ? "" : String(limit);
}
