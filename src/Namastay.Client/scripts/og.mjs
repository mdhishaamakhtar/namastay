// Draws public/og.png from scripts/og.html. Run it after changing the design:
// start the dev server, then `node scripts/og.mjs`.
import { chromium } from "playwright";
import { readFile } from "node:fs/promises";

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1200, height: 630 } });
await page.goto("http://localhost:5173/");
await page.setContent(await readFile("scripts/og.html", "utf8"), { waitUntil: "load" });
await page.addStyleTag({ url: "http://localhost:5173/styles.css" });
await page.evaluate(() => document.fonts.ready);
await page.waitForTimeout(500);
await page.screenshot({ path: "public/og.png" });
await browser.close();
