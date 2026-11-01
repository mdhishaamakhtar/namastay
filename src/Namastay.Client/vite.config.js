import { defineConfig } from "vite";

const backend = process.env.NAMASTAY_API ?? "http://localhost:5080";

export default defineConfig({
  server: {
    host: "0.0.0.0",
    proxy: {
      "/api": backend,
      "/places": backend,
      "/media": backend,
      "/fonts": backend,
      "/sitemap.xml": backend,
    },
  },
});
