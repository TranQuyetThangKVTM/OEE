import { chromium } from "playwright-core";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const route = process.argv[2] ?? "/";
const browser = await chromium.launch({
  executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, locale: "vi-VN" });
const errors = [];
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
page.on("pageerror", error => errors.push(error.message));
await page.goto(`${baseUrl}/Account/Login`);
await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
for (let attempt = 0; attempt < 30 && page.url().toLowerCase().includes("account/login"); attempt++) {
  await page.waitForTimeout(500);
}
if (page.url().toLowerCase().includes("account/login")) {
  const loginText = await page.locator("body").innerText();
  process.stdout.write(JSON.stringify({ route, url: page.url(), errors, loginFailed: true, text: loginText.slice(-1000) }, null, 2));
  await browser.close();
  process.exit(1);
}
await page.goto(`${baseUrl}${route}`);
await page.waitForTimeout(1200);
const body = await page.locator("body").innerText();
process.stdout.write(JSON.stringify({
  route,
  url: page.url(),
  errors,
  hasErrorAlert: body.includes("Không thể tải"),
  costingMetrics: {
    downtime: body.includes("Chi phí downtime ước tính"),
    quality: body.includes("Chi phí lỗi ước tính"),
    total: body.includes("Tổng tổn thất quy tiền"),
  },
  text: body.slice(0, 1500),
}, null, 2));
await browser.close();
