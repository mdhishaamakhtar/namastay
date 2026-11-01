// Draws the share image from scripts/og.html. Link previews get the small file, and
// the README gets a double-size one, so it stays sharp on high-resolution screens.
// Run it after changing the design: start the dev server, then `node scripts/og.mjs`.
import { chromium } from "playwright";
import { readFile } from "node:fs/promises";

const draw = async (browser, scale, path) => {
  const context = await browser.newContext({
    viewport: { width: 1200, height: 630 },
    deviceScaleFactor: scale,
  });

  const page = await context.newPage();
  await page.goto("http://localhost:5173/");
  await page.setContent(await readFile("scripts/og.html", "utf8"), { waitUntil: "load" });
  await page.addStyleTag({ url: "http://localhost:5173/styles.css" });
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(500);
  await page.screenshot({ path });
  await context.close();
};

const browser = await chromium.launch();
await draw(browser, 1, "public/og.png");
await draw(browser, 2, "../../.github/assets/namastay-preview.png");
await browser.close();
