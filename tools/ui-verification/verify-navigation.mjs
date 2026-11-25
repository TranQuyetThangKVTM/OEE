import { chromium } from "playwright-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifacts = path.join(root, "artifacts", "ui-verification");
const browser = await chromium.launch({
  executablePath: process.env.OEE_BROWSER_PATH ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, locale: "vi-VN" });
const errors = [];
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
page.on("pageerror", error => errors.push(error.message));

await page.goto(`${baseUrl}/Account/Login`);
await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
await page.waitForURL(
  url => !url.pathname.toLowerCase().includes("account/login"),
  { waitUntil: "commit" },
);
await page.goto(`${baseUrl}/production/plans`);
await page.waitForTimeout(1000);

const production = page.locator("button.oee-nav-group").filter({ hasText: "Sản xuất" });
const master = page.locator("button.oee-nav-group").filter({ hasText: "Dữ liệu nền" });
const activeLink = page.locator(".oee-navigation a.active");
const initial = {
  productionExpanded: await production.getAttribute("aria-expanded"),
  activeHref: await activeLink.getAttribute("href"),
  activeText: (await activeLink.innerText()).trim(),
  activeGroup: (await page.locator(".oee-nav-group--active").innerText()).trim(),
  expandedCount: await page.locator("button.oee-nav-group[aria-expanded='true']").count(),
};
await page.screenshot({ path: path.join(artifacts, "navigation-active-desktop.png"), fullPage: true });

await master.click();
await page.waitForTimeout(400);
const afterToggle = {
  productionExpanded: await production.getAttribute("aria-expanded"),
  masterExpanded: await master.getAttribute("aria-expanded"),
  expandedCount: await page.locator("button.oee-nav-group[aria-expanded='true']").count(),
};
await page.screenshot({ path: path.join(artifacts, "navigation-expanded-desktop.png"), fullPage: true });

await page.setViewportSize({ width: 1024, height: 768 });
await page.goto(`${baseUrl}/reports/daily-oee`);
await page.waitForTimeout(1000);
const tablet = {
  reportExpanded: await page.locator("button.oee-nav-group").filter({ hasText: "Báo cáo" }).getAttribute("aria-expanded"),
  activeHref: await page.locator(".oee-navigation a.active").getAttribute("href"),
  overflow: await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2),
};
await page.screenshot({ path: path.join(artifacts, "navigation-tablet.png"), fullPage: true });

const result = {
  generatedAt: new Date().toISOString(),
  initial,
  afterToggle,
  tablet,
  errors,
};
await fs.writeFile(path.join(artifacts, "navigation-results.json"), JSON.stringify(result, null, 2));
await browser.close();

const pass = initial.productionExpanded === "true"
  && initial.activeHref === "/production/plans"
  && initial.expandedCount === 1
  && afterToggle.productionExpanded === "false"
  && afterToggle.masterExpanded === "true"
  && afterToggle.expandedCount === 1
  && tablet.reportExpanded === "true"
  && tablet.activeHref === "/reports/daily-oee"
  && !tablet.overflow
  && errors.length === 0;
process.stdout.write(`Navigation verification: ${pass ? "pass" : "fail"}\n${JSON.stringify(result, null, 2)}\n`);
process.exitCode = pass ? 0 : 1;
