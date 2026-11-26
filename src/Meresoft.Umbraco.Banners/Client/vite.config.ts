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
    sourcemap: "hidden", // the maps are written but not referenced, and the project leaves them out of the package
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
