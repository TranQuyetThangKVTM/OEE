import { chromium } from "playwright-core";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 }, locale: "vi-VN" });
const events = [];
page.on("request", request => { if (request.url().includes("account")) events.push({ type: "request", method: request.method(), url: request.url() }); });
page.on("response", response => { if (response.url().includes("account")) events.push({ type: "response", status: response.status(), url: response.url() }); });
page.on("console", message => events.push({ type: `console-${message.type()}`, message: message.text() }));
page.on("pageerror", error => events.push({ type: "pageerror", message: error.message }));
await page.goto("http://localhost:5052/Account/Login");
await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
const button = page.getByRole("button", { name: "Đăng nhập", exact: true });
const info = await button.evaluate(element => ({ outerHtml: element.outerHTML, disabled: element.disabled, formAction: element.form?.action, formMethod: element.form?.method }));
await button.click();
await page.waitForTimeout(4000);
process.stdout.write(JSON.stringify({ info, url: page.url(), events, body: (await page.locator("body").innerText()).slice(-1200) }, null, 2));
await browser.close();
