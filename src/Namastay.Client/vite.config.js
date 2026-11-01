import { defineConfig } from "vite";

const backend = process.env.NAMASTAY_API ?? "http://localhost:5080";

// The browser tests compile to their own folder, so they never reload a page open on the dev server.
const buildFolder = process.env.NAMASTAY_BUILD ?? "build";

const useBuildFolder = {
  name: "use-build-folder",
  transformIndexHtml: {
    order: "pre",
    handler: (html) => html.replace("/build/App.js", `/${buildFolder}/App.js`),
  },
};

export default defineConfig({
  plugins: [useBuildFolder],
  server: {
    host: "0.0.0.0",
    proxy: {
      "/api": backend,
      "/places": backend,
      "/media": backend,
      "/sitemap.xml": backend,
    },
  },
});
