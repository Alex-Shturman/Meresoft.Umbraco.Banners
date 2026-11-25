/**
 * Times in the editor are shown and typed in the user's time zone and stored in UTC. A
 * `datetime-local` input holds "YYYY-MM-DDTHH:mm" with no zone, so these helpers convert at the edge.
 */

const pad = (n: number) => String(n).padStart(2, "0");

/** The name of the time zone the times are shown in, for example "Europe/Berlin". */
export function userTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

/** A UTC moment (ISO text) as a `datetime-local` value in the user's time zone; empty when there is none. */
export function utcToLocalInput(utc: string | null | undefined): string {
  if (!utc) {
    return "";
  }
  // The server sends UTC without an offset on some paths; treat such text as UTC.
  const date = new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(utc) ? utc : `${utc}Z`);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** A `datetime-local` value typed in the user's time zone as an ISO UTC text; null when empty or not a time. */
export function localInputToUtc(local: string): string | null {
  if (!local) {
    return null;
  }
  const date = new Date(local); // No zone in the text, so the browser reads it in the user's zone.
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/**
 * The UTC value to send. When the input still shows what the stored value shows, the stored value is
 * kept as it is, so saving other fields never rounds its seconds away.
 */
export function resolveUtc(local: string, storedUtc: string | null | undefined): string | null {
  return local === utcToLocalInput(storedUtc) ? (storedUtc ?? null) : localInputToUtc(local);
}
