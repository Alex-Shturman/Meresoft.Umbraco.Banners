import { defineConfig } from "@hey-api/openapi-ts";

// Minimal declaration: the client project does not depend on @types/node.
declare const process: { env: Record<string, string | undefined> };

export default defineConfig({
  input:
    process.env.MB_OPENAPI_URL ??
    "http://localhost:5100/umbraco/swagger/meresoft-banners/swagger.json",
  output: "src/api",
  plugins: ["@hey-api/client-fetch", "@hey-api/typescript", "@hey-api/sdk"],
});
