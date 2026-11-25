/** Host names of this machine; the e2e run may delete the banners and zones of a site on one of them. */
const LOCAL_HOSTS = new Set(["localhost", "127.0.0.1", "[::1]"]);

/**
 * The e2e run starts by deleting every banner and zone of the site (global-reset.ts). Throws unless the site is on
 * this machine, so a wrong MB_SAMPLE_BASE_URL can never wipe a real site. A remote sample site needs the explicit
 * override MB_E2E_ALLOW_REMOTE_RESET=1.
 */
export function assertResetAllowed(baseURL: string | undefined): void {
  let hostname: string;
  try {
    hostname = new URL(baseURL ?? "").hostname;
  } catch {
    throw new Error(`The e2e run needs a valid MB_SAMPLE_BASE_URL, got "${baseURL ?? ""}".`);
  }

  if (!LOCAL_HOSTS.has(hostname) && process.env.MB_E2E_ALLOW_REMOTE_RESET !== "1") {
    throw new Error(
      `Refusing to run: ${baseURL} is not on this machine, and the e2e run deletes every banner and zone of the site first. ` +
        "Set MB_E2E_ALLOW_REMOTE_RESET=1 to allow a remote sample site.",
    );
  }
}
