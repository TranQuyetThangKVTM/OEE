import { chromium } from "playwright-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifacts = path.join(root, "artifacts", "uat");
const signalrScript = path.join(root, "tools", "ui-verification", "node_modules", "@microsoft", "signalr", "dist", "browser", "signalr.js");
const xlsxPath = path.join(root, "artifacts", "ui-verification", "uat-fault-mapping.xlsx");
const executablePath = process.env.OEE_BROWSER_PATH
  ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const run = new Date().toISOString().replace(/\D/g, "").slice(4, 14);
const productCode = `UAT-${run}`;
const viewerName = `uatviewer${run}`.toLowerCase();
const viewerPassword = `Uat!${run}Aa1`;
const evidence = { generatedAt: new Date().toISOString(), baseUrl, run, checks: [], errors: [] };

function record(id, pass, actual, details = {}) {
  evidence.checks.push({ id, status: pass ? "pass" : "fail", actual, ...details });
  if (!pass) process.exitCode = 1;
}

async function settle(page, delay = 700) {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForSelector("body", { state: "visible" });
  await page.waitForTimeout(delay);
}

async function login(page, username, password) {
  await page.goto(`${baseUrl}/Account/Login`);
  await settle(page);
  await page.locator("#userName").fill(username);
  await page.locator("#password").fill(password);
  await Promise.all([
    page.waitForURL(url => !url.pathname.toLowerCase().includes("account/login")),
    page.getByRole("button", { name: "Đăng nhập", exact: true }).click(),
  ]);
  await settle(page);
}

async function currentDialog(page) {
  const dialog = page.locator(".mud-dialog").last();
  await dialog.waitFor({ state: "visible" });
  return dialog;
}

async function checkSignalR(page) {
  await page.goto(`${baseUrl}/live-oee`);
  await settle(page);
  await page.addScriptTag({ path: signalrScript });
  const result = await page.evaluate(async () => {
    const received = new Promise(resolve => {
      const timer = setTimeout(() => resolve(null), 10000);
      window.__uatResolve = value => { clearTimeout(timer); resolve(value); };
    });
    const connection = new window.signalR.HubConnectionBuilder()
      .withUrl("/hubs/oee-live")
      .withAutomaticReconnect()
      .build();
    connection.on("LiveOeeUpdated", value => window.__uatResolve(value));
    await connection.start();
    const payload = await received;
    const state = connection.state;
    await connection.stop();
    return { state, received: payload !== null, updatedAt: payload?.updatedAt ?? null };
  });
  record("UAT-SIGNALR", result.state === "Connected" && result.received, `state=${result.state}; initialPayload=${result.received}`, result);
}

async function createEditAndRejectDuplicate(page) {
  await page.goto(`${baseUrl}/master/products`);
  await settle(page);
  await page.getByRole("button", { name: /Thêm sản phẩm/i }).click();
  let dialog = await currentDialog(page);
  await dialog.getByLabel("Mã sản phẩm", { exact: true }).fill(productCode);
  await dialog.getByLabel("Tên sản phẩm", { exact: true }).fill(`${productCode} sản phẩm UAT`);
  await dialog.getByLabel("Mô tả", { exact: true }).fill("UAT create");
  await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
  await dialog.waitFor({ state: "detached", timeout: 15000 });

  const filter = page.getByLabel("Mã, tên, đơn vị hoặc mô tả", { exact: true });
  await filter.fill(productCode);
  await page.getByRole("button", { name: "Lọc", exact: true }).click();
  const row = page.locator("tr", { hasText: productCode }).first();
  await row.waitFor({ state: "visible", timeout: 15000 });
  await row.getByRole("button", { name: "Sửa sản phẩm" }).click();
  await page.getByLabel("Mô tả", { exact: true }).fill("UAT updated");
  await page.getByRole("button", { name: "Lưu", exact: true }).click();
  await page.getByText("UAT updated", { exact: true }).waitFor({ state: "visible", timeout: 15000 });
  record("UAT-DATA-CREATE-EDIT", true, `${productCode} created and updated through UI`);

  await page.getByRole("button", { name: /Thêm sản phẩm/i }).click();
  dialog = await currentDialog(page);
  await dialog.getByLabel("Mã sản phẩm", { exact: true }).fill(productCode);
  await dialog.getByLabel("Tên sản phẩm", { exact: true }).fill(`${productCode} duplicate`);
  await page.keyboard.press("Tab");
  await page.waitForTimeout(400);
  await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
  const error = dialog.getByText(/mã có thể trùng|already exists|duplicate/i).first();
  await error.waitFor({ state: "visible", timeout: 15000 });
  const stillOpen = await dialog.isVisible();
  record("UAT-NO-DOUBLE-INSERT", stillOpen, `duplicate rejected; dialogOpen=${stillOpen}`);
  await page.keyboard.press("Escape");
}

async function exportPdf(page) {
  await page.goto(`${baseUrl}/reports/daily-oee`);
  await settle(page, 1000);
  const href = await page.getByRole("link", { name: /Xuất PDF/i }).getAttribute("href");
  const response = await page.request.get(new URL(href, baseUrl).toString());
  const bytes = await response.body();
  const pdf = bytes.subarray(0, 4).toString("ascii") === "%PDF";
  await fs.writeFile(path.join(artifacts, "oee-report.pdf"), bytes);
  record("UAT-PDF", response.ok() && pdf && response.headers()["content-type"]?.includes("application/pdf"), `status=${response.status()}; bytes=${bytes.length}; signature=${pdf}`);
}

async function importXlsx(page) {
  const template = await page.request.get(`${baseUrl}/api/v1/plc/fault-mappings/import/template`);
  const templateBytes = await template.body();
  await fs.writeFile(path.join(artifacts, "fault-mappings-v1.xlsx"), templateBytes);
  const templateOk = template.ok()
    && template.headers()["content-type"]?.includes("spreadsheetml")
    && templateBytes.length > 1000;

  await page.goto(`${baseUrl}/plc/faults`);
  await settle(page);
  await page.getByRole("button", { name: "Import XLSX", exact: true }).click();
  const dialog = await currentDialog(page);
  await dialog.locator('input[type="file"]').setInputFiles(xlsxPath);
  await dialog.getByText(/Dry-run:\s*1 dòng.*1 hợp lệ/i).waitFor({ state: "visible", timeout: 20000 });
  await dialog.getByRole("button", { name: "Import transaction", exact: true }).click();
  await dialog.waitFor({ state: "detached", timeout: 20000 });

  await page.getByLabel("Mã lỗi, reason hoặc mô tả", { exact: true }).fill("UAT-14001");
  await page.getByRole("button", { name: "Lọc", exact: true }).click();
  await page.getByText("UAT-14001", { exact: true }).first().waitFor({ state: "visible", timeout: 15000 });
  record("UAT-XLSX", templateOk, `template=${template.status()}; dry-run valid; transaction persisted UAT-14001`);
}

async function createViewer(page) {
  await page.goto(`${baseUrl}/users`);
  await settle(page);
  await page.getByRole("button", { name: "Tạo người dùng", exact: true }).click();
  await page.getByText("Tạo người dùng", { exact: true }).last().waitFor({ state: "visible" });
  await page.getByLabel("Tên đăng nhập", { exact: true }).fill(viewerName);
  await page.getByLabel("Tên hiển thị", { exact: true }).fill(`UAT Viewer ${run}`);
  await page.getByLabel("Email", { exact: true }).fill(`${viewerName}@example.test`);
  await page.getByLabel("Mật khẩu ban đầu", { exact: true }).fill(viewerPassword);
  await page.getByLabel("Xác nhận mật khẩu", { exact: true }).fill(viewerPassword);
  await page.getByRole("button", { name: "Lưu người dùng", exact: true }).click();
  await page.getByText(viewerName, { exact: true }).first().waitFor({ state: "visible", timeout: 20000 });
  record("UAT-USER-CREATE", true, `${viewerName} created with Viewer role`);
}

async function checkAudit(page) {
  await page.goto(`${baseUrl}/audit`);
  await settle(page);
  await page.getByLabel("Loại entity", { exact: true }).fill("Product");
  await page.getByRole("button", { name: "Áp dụng", exact: true }).click();
  const auditRow = page.locator("tr", { hasText: productCode }).first();
  await auditRow.waitFor({ state: "visible", timeout: 15000 });
  const details = auditRow.getByRole("button", { name: "Xem JSON diff" });
  await details.click();
  const dialog = await currentDialog(page);
  const hasJson = await dialog.getByText(/Changes JSON|Trước|Sau|JSON/i).count() > 0;
  record("UAT-AUDIT", hasJson, `${productCode} audit found; JSON diff dialog opened`);
  await page.keyboard.press("Escape");
}

async function activateFakeMapping(page) {
  await page.goto(`${baseUrl}/plc/mappings`);
  await settle(page);
  await page.getByLabel("Thiết bị PLC, WorkUnit hoặc connection", { exact: true }).fill("UIV-OPC");
  await page.getByRole("button", { name: "Lọc", exact: true }).click();
  const row = page.locator("tr", { hasText: "UIV-OPC" }).first();
  await row.waitFor({ state: "visible", timeout: 15000 });
  if ((await row.innerText()).includes("Paused")) {
    await row.getByRole("button", { name: "Bật hoặc tạm dừng mapping" }).click();
  }
  await page.waitForTimeout(12000);
  await page.getByRole("button", { name: "Làm mới mapping" }).click();
  await page.waitForTimeout(1200);
  const text = await page.locator("tr", { hasText: "UIV-OPC" }).first().innerText();
  const connected = text.includes("Connected") || text.includes("NoData");
  record("UAT-FAKE-OPC", connected, `mapping active; connectivity=${text.includes("Connected") ? "Connected" : text.includes("NoData") ? "NoData" : "unknown"}`);
}

async function checkViewer(browser) {
  const context = await browser.newContext({ viewport: { width: 1366, height: 768 }, locale: "vi-VN" });
  const page = await context.newPage();
  await login(page, viewerName, viewerPassword);
  await page.goto(`${baseUrl}/master/products`);
  await settle(page);
  const noCreate = await page.getByRole("button", { name: /Thêm sản phẩm/i }).count() === 0;
  await page.goto(`${baseUrl}/users`);
  await settle(page);
  const denied = (await page.locator("body").innerText()).includes("Không có quyền truy cập");
  const forbidden = await page.request.get(`${baseUrl}/api/v1/plc/fault-mappings/import/template`);
  record("UAT-RBAC-VIEWER", noCreate && denied && forbidden.status() === 403, `masterCreateHidden=${noCreate}; usersDenied=${denied}; plcApi=${forbidden.status()}`);
  await page.screenshot({ path: path.join(artifacts, "viewer-access-denied.png"), fullPage: true });
  await context.close();
}

async function main() {
  await fs.mkdir(artifacts, { recursive: true });
  const browser = await chromium.launch({ executablePath, headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 1000 }, locale: "vi-VN" });
  const page = await context.newPage();
  page.on("console", message => { if (message.type() === "error") evidence.errors.push({ type: "console", url: page.url(), message: message.text() }); });
  page.on("pageerror", error => evidence.errors.push({ type: "page", url: page.url(), message: error.message }));
  try {
    await login(page, process.env.OEE_UI_USER ?? "admin", process.env.OEE_UI_PASSWORD);
    record("UAT-LOGIN", !page.url().toLowerCase().includes("account/login"), `authenticated as ${process.env.OEE_UI_USER ?? "admin"}`);
    const menuHrefs = await page.locator("nav a[href], .mud-navmenu a[href]").evaluateAll(nodes => [...new Set(nodes.map(x => x.getAttribute("href")))].filter(Boolean));
    const essential = ["/", "/production-plans", "/master/products", "/reports/daily-oee", "/live-oee", "/plc/status", "/users", "/health-ui"];
    record("UAT-MENU", essential.every(href => menuHrefs.includes(href)), `${menuHrefs.length} unique menu routes; all essential groups present`, { menuHrefs });
    await checkSignalR(page);
    await createEditAndRejectDuplicate(page);
    await exportPdf(page);
    await importXlsx(page);
    await createViewer(page);
    await checkAudit(page);
    await activateFakeMapping(page);
    await checkViewer(browser);
  } catch (error) {
    evidence.errors.push({ type: "fatal", url: page.url(), message: error.stack ?? error.message });
    record("UAT-RUN", false, error.message);
  } finally {
    await page.screenshot({ path: path.join(artifacts, "admin-final.png"), fullPage: true }).catch(() => {});
    await context.close();
    await browser.close();
    evidence.finishedAt = new Date().toISOString();
    await fs.writeFile(path.join(artifacts, "uat-results.json"), JSON.stringify(evidence, null, 2));
  }
  const passed = evidence.checks.filter(x => x.status === "pass").length;
  process.stdout.write(`UAT checks: ${passed}/${evidence.checks.length}; browserErrors=${evidence.errors.length}\n`);
}

await main();
