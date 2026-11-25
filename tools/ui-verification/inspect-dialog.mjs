import { chromium } from "playwright-core";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const browser = await chromium.launch({
  executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, locale: "vi-VN" });
await page.goto("http://localhost:5052/Account/Login");
await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
await Promise.all([
  page.waitForURL(url => !url.pathname.toLowerCase().includes("account/login")),
  page.getByRole("button", { name: "Đăng nhập", exact: true }).click(),
]);
await page.goto("http://localhost:5052/production/plan-templates");
await page.waitForTimeout(900);
await page.getByRole("button", { name: /Thêm mẫu/i }).click();
await page.waitForTimeout(500);
const description = await page.locator(".mud-dialog").last().evaluate(dialog => ({
  text: dialog.innerText,
  inputs: [...dialog.querySelectorAll("input")].map(input => ({ id: input.id, type: input.type, value: input.value, aria: input.getAttribute("aria-label"), labelledby: input.getAttribute("aria-labelledby"), role: input.getAttribute("role"), placeholder: input.placeholder })),
  labels: [...dialog.querySelectorAll("label")].map(label => ({ text: label.textContent?.trim(), for: label.htmlFor })),
  buttons: [...dialog.querySelectorAll("button")].map(button => ({ text: button.innerText.trim(), aria: button.getAttribute("aria-label"), disabled: button.disabled, classes: button.className })),
}));
await page.getByRole("combobox", { name: "Thiết bị / dây chuyền" }).click();
await page.waitForTimeout(200);
description.openOptions = await page.locator('[role="option"], .mud-list-item').evaluateAll(options => options.map(option => ({
  text: option.textContent?.trim(),
  role: option.getAttribute("role"),
  ariaSelected: option.getAttribute("aria-selected"),
  classes: option.className,
})).filter(option => option.text));
process.stdout.write(JSON.stringify(description, null, 2));
await browser.close();
