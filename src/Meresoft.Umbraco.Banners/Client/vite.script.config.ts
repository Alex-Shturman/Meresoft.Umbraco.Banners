import { defineConfig } from "vite";

export default defineConfig({
  publicDir: false,
  build: {
    lib: {
      entry: "script/banners.ts",
      formats: ["iife"],
      name: "MeresoftBanners",
      fileName: () => "banners.js",
    },
    outDir: "../wwwroot/meresoft-banners",
    emptyOutDir: true,
    minify: true,
    sourcemap: false,
  },
});
