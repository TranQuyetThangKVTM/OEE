import { chromium } from "playwright-core";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, locale: "vi-VN" });
await page.goto("http://localhost:5052/Account/Login");
await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
await page.waitForTimeout(800);
const avatar = page.getByLabel("Mở menu người dùng");
const tree = await avatar.evaluate(element => ({ self: element.outerHTML, parent: element.parentElement?.outerHTML, grandParent: element.parentElement?.parentElement?.outerHTML }));
await avatar.click();
await page.waitForTimeout(400);
const afterAvatar = (await page.locator("body").innerText()).includes("Phiên bảo mật");
if (!afterAvatar) {
  const activator = avatar.locator("xpath=ancestor::*[@tabindex][1]");
  await activator.focus();
  await activator.press("Enter").catch(() => {});
  await page.waitForTimeout(400);
}
const result = { tree, afterAvatar, afterAncestor: (await page.locator("body").innerText()).includes("Phiên bảo mật") };
process.stdout.write(JSON.stringify(result, null, 2));
await browser.close();
