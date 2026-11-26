export type Preset = "today" | "last7" | "last30" | "thisMonth" | "lastMonth" | "custom";
const DAY_MS = 86_400_000;

/** A date as the UTC day text the API takes, for example 2027-03-01. */
function dayText(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** The first and last UTC day of a preset. Statistics days are UTC days, so the browser time zone plays no part. */
export function presetRange(preset: Exclude<Preset, "custom">, now: Date = new Date()): { from: string; to: string } {
  const today = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  switch (preset) {
    case "today":
      return { from: dayText(today), to: dayText(today) };
    case "last7":
      return { from: dayText(new Date(today.getTime() - 6 * DAY_MS)), to: dayText(today) };
    case "last30":
      return { from: dayText(new Date(today.getTime() - 29 * DAY_MS)), to: dayText(today) };
    case "thisMonth":
      return { from: dayText(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1))), to: dayText(today) };
    case "lastMonth":
      return {
        from: dayText(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() - 1, 1))),
        to: dayText(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 0))),
      };
  }
}
