import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["src/**/*.test.ts"],
    environment: "node",
    // The time zone tests need a zone with daylight saving time. Setting it here works on every operating
    // system (a TZ=... prefix in the npm script does not on Windows).
    env: { TZ: "Europe/Berlin" },
  },
});
