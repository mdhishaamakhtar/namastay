import { defineConfig } from "vite";

export default defineConfig({
  server: {
    host: "0.0.0.0",
    proxy: {
      "/api": "http://localhost:5000",
      "/places": "http://localhost:5000",
      "/sitemap.xml": "http://localhost:5000",
    },
  },
});
