import { defineConfig } from "vite";

export default defineConfig({
  publicDir: "public",
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: "bundle.manifests",
    },
    outDir: "../wwwroot/App_Plugins/MeresoftBanners",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
