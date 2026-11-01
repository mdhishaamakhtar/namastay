// Saves every public page as finished HTML, so search engines and link previews
// get real content. Needs the built site in dist and a running API.
import { copyFile, mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, extname, join } from "node:path";
import { chromium } from "playwright";

const dist = process.env.PRERENDER_DIST ?? "dist";
const site = process.env.SITE_URL.replace(/\/$/, "");
const api = process.env.API_URL.replace(/\/$/, "");
const atOnce = 6;

const types = {
  ".html": "text/html",
  ".js": "text/javascript",
  ".css": "text/css",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".woff2": "font/woff2",
};

const getJson = async (path) => {
  const response = await fetch(api + path);
  if (!response.ok) throw new Error(`${path} answered ${response.status}`);
  return response.json();
};

const getGuides = async () => {
  const response = await fetch(`${api}/sitemap.xml`);
  const text = await response.text();
  return [...text.matchAll(/\/places\/([^<]+)<\/loc>/g)].map((found) => `/places/${found[1]}`);
};

const appPages = async () => {
  const [regions, feelings, food] = await Promise.all([
    getJson("/api/directory"),
    getJson("/api/feelings"),
    getJson("/api/food"),
  ]);

  const dishes = food.regions.flatMap((region) => region.dishes.map((dish) => `/dishes/${dish.slug}`));

  return [
    "/",
    "/explore",
    "/food",
    ...regions.map((region) => `/explore/${region.slug}`),
    ...food.regions.map((region) => `/food/${region.slug}`),
    ...feelings.map((feeling) => `/feelings/${feeling.slug}`),
    ...new Set(dishes),
  ];
};

const save = async (path, html) => {
  const file = join(dist, path, "index.html");
  await mkdir(dirname(file), { recursive: true });
  await writeFile(file, html);
};

/// The page is served as if from the real address, so the API accepts its calls.
const serveSite = (context) =>
  context.route(`${site}/**`, async (route) => {
    const path = new URL(route.request().url()).pathname;
    const type = types[extname(path)];
    const body = await readFile(join(dist, type ? path : "app.html"));
    await route.fulfill({ body, contentType: type ?? "text/html" });
  });

/// Keeps a page's data inside it, so the app starts from what's already on screen.
const withSeed = (html, answers) => {
  const seed = JSON.stringify(answers).replace(/</g, "\\u003c");
  const script = `<script>window.__seed=${seed};setTimeout(()=>document.documentElement.removeAttribute("data-seeded"),1500)</script>`;
  return html.replace("<html", "<html data-seeded").replace("</head>", `${script}</head>`);
};

const renderApp = async (context, path) => {
  const page = await context.newPage();
  const problems = [];
  const answers = {};

  page.on("requestfailed", (request) => problems.push(request.url()));
  page.on("response", (response) => {
    // Crawlers are signed out, so being refused a personal page is expected.
    const signedOut = response.status() === 401;
    if (response.status() >= 400 && !signedOut) problems.push(`${response.status()} ${response.url()}`);
  });

  page.on("response", async (response) => {
    const address = new URL(response.url());
    if (address.origin === api && address.pathname.startsWith("/api/") && response.ok()) {
      answers[address.pathname] = await response.text();
    }
  });

  await page.goto(site + path);

  await page.waitForFunction(
    (wanted) => new URL(document.querySelector('link[rel="canonical"]')?.href ?? location.origin).pathname === wanted,
    path,
    { timeout: 20000 },
  ).catch(async (error) => {
    console.error(`${path} never finished: ${await page.title()}`);
    throw error;
  });

  await page.waitForLoadState("networkidle");
  if (problems.length > 0) throw new Error(`${path} didn't load fully: ${problems.join(", ")}`);
  await save(path, withSeed(await page.content(), answers));
  await page.close();
};

const writeShell = async (file, html) => {
  const target = join(dist, file);
  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, html);
};

/// Personal pages can't be saved with content, so each is saved as its own skeleton.
/// Holding the API back leaves the page exactly as it looks while loading.
const renderShell = async (context, path, file) => {
  const page = await context.newPage();
  await page.route(`${api}/**`, () => {});
  await page.goto(site + path);
  await page.waitForFunction(() => document.getElementById("app").childElementCount > 0);
  await page.waitForFunction(() => document.querySelector('meta[name="robots"]')?.content === "noindex");
  await writeShell(file, await page.content());
  await page.close();
};

/// Sign-in has no private data, so it's saved whole, with its options.
const renderSignIn = async (context) => {
  const page = await context.newPage();
  const answers = {};
  const path = "/api/auth/options";

  page.on("response", async (response) => {
    if (new URL(response.url()).pathname === path && response.ok()) answers[path] = await response.text();
  });

  await page.goto(`${site}/sign-in`, { waitUntil: "networkidle" });
  await writeShell("sign-in/index.html", withSeed(await page.content(), answers));
  await page.close();
};

const renderGuide = async (path) => {
  const response = await fetch(api + path);
  if (!response.ok) throw new Error(`${path} answered ${response.status}`);
  const html = await response.text();
  await save(path, html.replace(/(src|href)="\/media\//g, `$1="${api}/media/`));
};

const inBatches = async (items, work) => {
  for (let start = 0; start < items.length; start += atOnce) {
    await Promise.all(items.slice(start, start + atOnce).map(work));
  }
};

const sitemap = (paths) => {
  const today = new Date().toISOString().slice(0, 10);
  const urls = paths.map((path) => `<url><loc>${site}${path}</loc><lastmod>${today}</lastmod></url>`);
  return `<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${urls.join("")}</urlset>`;
};

const robots = `User-agent: *
Allow: /
Disallow: /my
Disallow: /sign-in
Disallow: /journey
Disallow: /trip

Sitemap: ${site}/sitemap.xml
`;

await copyFile(join(dist, "index.html"), join(dist, "app.html"));

const [pages, guides] = await Promise.all([appPages(), getGuides()]);
if (guides.length === 0) throw new Error("The API listed no guides.");

// Local runs call an API on localhost, which Chrome otherwise blocks.
const browser = await chromium.launch({ args: ["--disable-features=LocalNetworkAccessChecks"] });
const context = await browser.newContext();
await serveSite(context);
await inBatches(pages, (path) => renderApp(context, path));
await renderSignIn(context);
await inBatches(
  [
    ["/journey", "journey/index.html"],
    ["/my", "my/index.html"],
    ["/trip/shell", "shells/trip.html"],
    ["/my/shell", "shells/my-trip.html"],
    ["/my/shell/edit", "shells/edit.html"],
  ],
  ([path, file]) => renderShell(context, path, file),
);
await browser.close();

await inBatches(guides, renderGuide);
await writeFile(join(dist, "sitemap.xml"), sitemap([...pages, ...guides]));
await writeFile(join(dist, "robots.txt"), robots);
console.log(`Pre-rendered ${pages.length} pages and ${guides.length} guides.`);
