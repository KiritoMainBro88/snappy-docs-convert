import { expect, test } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { discordUrl, portableUrl, releaseTagUrl } from "../src/content";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, "..", "..");
const outputRoot = path.join(repoRoot, "artifacts", "demo", "website");
const videoRoot = path.join(outputRoot, "video");

test("website demo screenshots and video", async ({ browser }) => {
  fs.mkdirSync(outputRoot, { recursive: true });
  fs.mkdirSync(videoRoot, { recursive: true });

  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    recordVideo: {
      dir: videoRoot,
      size: { width: 1280, height: 720 },
    },
  });
  const page = await context.newPage();

  await page.goto("/");
  await expect(page.getByText("kmb file tools").first()).toBeVisible();
  await expect(page.locator(`a[href="${discordUrl}"]`).first()).toBeVisible();
  await expect(page.locator(`a[href="${releaseTagUrl}"]`).first()).toBeVisible();
  await expect(page.locator(`a[href="${portableUrl}"]`).first()).toBeVisible();

  await page.screenshot({ path: path.join(outputRoot, "home-light-en.png"), fullPage: true });

  await page.getByRole("button", { name: "Dark" }).click();
  await page.screenshot({ path: path.join(outputRoot, "home-dark-en.png"), fullPage: true });

  await page.getByRole("button", { name: "Light" }).click();
  await page.getByRole("button", { name: "VI" }).click();
  await expect(page.getByText("Chạy cục bộ", { exact: false }).first()).toBeVisible();
  await page.screenshot({ path: path.join(outputRoot, "home-light-vi.png"), fullPage: true });

  await page.locator("#features").scrollIntoViewIfNeeded();
  await expect(page.locator("#features")).toBeVisible();
  await page.locator("#download").scrollIntoViewIfNeeded();
  await expect(page.locator("#download")).toBeVisible();

  const video = page.video();
  await context.close();
  if (video) {
    await video.saveAs(path.join(videoRoot, "website-demo.webm"));
  }

  const mobileContext = await browser.newContext({
    viewport: { width: 390, height: 844 },
    isMobile: true,
  });
  const mobilePage = await mobileContext.newPage();
  await mobilePage.goto("/");
  await mobilePage.getByRole("button", { name: "VI" }).click();
  await mobilePage.screenshot({ path: path.join(outputRoot, "mobile-vi.png"), fullPage: true });
  await mobileContext.close();
});
