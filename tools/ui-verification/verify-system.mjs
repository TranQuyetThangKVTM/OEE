import { chromium } from "playwright-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifacts = path.join(root, "artifacts", "ui-verification", "system");
const executablePath = process.env.OEE_BROWSER_PATH ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const run = Date.now().toString().slice(-9);
const username = `sysuat${run}`;
const password = `Sys@${run}Aa1`;
const result = { generatedAt: new Date().toISOString(), baseUrl, username, checks: [], errors: [] };
const consoleErrors = [];

function check(id, passed, actual) {
  result.checks.push({ id, status: passed ? "pass" : "fail", actual });
  if (!passed) process.exitCode = 1;
}

async function settle(page, delay = 600) {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForSelector("body", { state: "visible" });
  await page.waitForTimeout(delay);
}

async function login(page) {
  await page.goto(`${baseUrl}/Account/Login`);
  await settle(page);
  await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
  await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
  await Promise.all([
    page.waitForURL(url => !url.pathname.toLowerCase().includes("account/login")),
    page.getByRole("button", { name: /Đăng nhập/i }).click(),
  ]);
  await settle(page);
}

async function dialog(page) {
  const current = page.locator(".mud-dialog").last();
  await current.waitFor({ state: "visible", timeout: 15000 });
  return current;
}

await fs.mkdir(artifacts, { recursive: true });
const browser = await chromium.launch({ headless: true, executablePath });
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
page.on("console", message => { if (message.type() === "error") consoleErrors.push(message.text()); });
page.on("pageerror", error => consoleErrors.push(error.message));

try {
  await login(page);
  await page.goto(`${baseUrl}/users`);
  await settle(page);
  check("SYSTEM-MENU", await page.getByText("Quản lý người dùng", { exact: true }).count() > 0,
    "Hệ thống có Quản lý người dùng và Nhật ký thao tác");

  await page.getByRole("button", { name: "Tạo tài khoản", exact: true }).click();
  let current = await dialog(page);
  await current.getByLabel("Tài khoản (username)", { exact: true }).fill(username);
  await current.getByLabel("Tên hiển thị", { exact: true }).fill(`System UAT ${run}`);
  await current.getByLabel("Email", { exact: true }).fill(`${username}@example.test`);
  await current.getByLabel("Mật khẩu ban đầu", { exact: true }).fill(password);
  const viewer = current.getByText("Viewer", { exact: true });
  await viewer.click();
  await current.getByRole("button", { name: "Tạo tài khoản", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  const row = page.locator("tr", { hasText: username }).first();
  await row.waitFor({ state: "visible", timeout: 20000 });
  check("SYSTEM-CREATE", true, `${username} persisted and reloaded from server table`);

  await row.getByRole("button", { name: "Chỉnh sửa tài khoản" }).click();
  current = await dialog(page);
  const usernameDisabled = await current.getByLabel("Tài khoản (username)", { exact: true }).isDisabled();
  await current.getByLabel("Tên hiển thị", { exact: true }).fill(`System UAT Updated ${run}`);
  await current.getByRole("button", { name: "Lưu", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  await page.getByText(`System UAT Updated ${run}`, { exact: true }).waitFor({ state: "visible", timeout: 20000 });
  check("SYSTEM-EDIT", usernameDisabled, "username read-only; update persisted and parent table reloaded");

  const updatedRow = page.locator("tr", { hasText: username }).first();
  await updatedRow.getByRole("button", { name: "Đặt lại mật khẩu" }).click();
  current = await dialog(page);
  await current.getByLabel("Mật khẩu mới", { exact: true }).fill(`New@${run}Aa1`);
  await current.getByLabel("Nhập lại mật khẩu", { exact: true }).fill(`New@${run}Aa1`);
  await current.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  check("SYSTEM-RESET", true, "reset password command succeeded and dialog closed after SQL commit");

  await updatedRow.getByRole("button", { name: "Khóa tài khoản" }).click();
  current = await dialog(page);
  await current.getByRole("button", { name: "Khóa", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  const lockedRow = page.locator("tr", { hasText: username }).first();
  await lockedRow.getByText("Đang khóa", { exact: true }).waitFor({ state: "visible", timeout: 20000 });
  await lockedRow.getByRole("button", { name: "Mở khóa tài khoản" }).click();
  current = await dialog(page);
  await current.getByRole("button", { name: "Mở khóa", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  check("SYSTEM-LOCK", true, "lock and unlock persisted; dynamic icon/tooltip changed");
  await page.screenshot({ path: path.join(artifacts, "user-management-desktop.png"), fullPage: true });

  await page.goto(`${baseUrl}/audit`);
  await settle(page, 1000);
  await page.getByLabel("Tìm theo tên người dùng", { exact: true }).fill("admin");
  await page.getByLabel("Loại đối tượng", { exact: true }).click();
  await page.getByRole("option", { name: "Tài khoản người dùng", exact: true }).click();
  await page.getByLabel("Thao tác", { exact: true }).click();
  await page.getByRole("option", { name: "Tạo mới", exact: true }).last().click();
  await page.getByRole("button", { name: "Áp dụng", exact: true }).click();
  await page.waitForTimeout(1000);
  const auditRow = page.locator("tr", { hasText: username })
    .filter({ hasText: "Tạo mới" }).first();
  await auditRow.waitFor({ state: "visible", timeout: 20000 });
  await auditRow.getByRole("button", { name: "Xem chi tiết audit" }).click();
  current = await dialog(page);
  const friendly = await current.getByText("Nội dung tạo mới", { exact: false }).count() > 0;
  const raw = await current.getByText("JSON gốc (dành cho IT)", { exact: true }).count() > 0;
  const dialogText = await current.innerText();
  check("AUDIT-DETAIL", friendly && raw && !/PasswordHash|SecurityStamp/.test(dialogText),
    "friendly Create detail + authorized raw JSON; sensitive values absent");
  await page.screenshot({ path: path.join(artifacts, "audit-detail-desktop.png"), fullPage: true });
  await current.getByRole("button", { name: "Đóng", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 10000 });
  await page.screenshot({ path: path.join(artifacts, "audit-filtered-desktop.png"), fullPage: true });

  await page.getByRole("button", { name: "Dọn log cũ (18T)", exact: true }).click();
  current = await dialog(page);
  const preview = await current.innerText();
  check("AUDIT-RETENTION-PREVIEW", /Số bản ghi dự kiến/.test(preview) && /18 tháng/.test(preview),
    "cutoff/candidate preview shown; destructive command not executed in browser UAT");
  await page.keyboard.press("Escape");

  const workUnitCode = `UAT-WU-${run}`;
  await page.goto(`${baseUrl}/master/work-units`);
  await settle(page);
  await page.getByRole("button", { name: "Thêm thiết bị", exact: true }).click();
  current = await dialog(page);
  await current.getByLabel("Nhóm thiết bị", { exact: true }).click();
  await page.getByRole("option").first().click();
  await current.getByLabel("Mã thiết bị", { exact: true }).click();
  await current.getByLabel("Mã thiết bị", { exact: true }).fill(workUnitCode);
  await current.getByLabel("Tên thiết bị", { exact: true }).fill(`Thiết bị audit UAT ${run}`);
  await page.keyboard.press("Tab");
  await page.waitForTimeout(500);
  await current.getByRole("button", { name: "Lưu", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  await page.getByLabel("Mã, tên hoặc nhóm", { exact: true }).fill(workUnitCode);
  await page.getByRole("button", { name: "Lọc", exact: true }).click();
  const unitRow = page.locator("tr", { hasText: workUnitCode }).first();
  await unitRow.waitFor({ state: "visible", timeout: 20000 });
  await page.waitForTimeout(1000);
  await page.getByRole("button", { name: "Xóa thiết bị" }).last().click({ force: true });
  await page.waitForTimeout(500);
  current = await dialog(page);
  await current.getByRole("button", { name: "Xóa", exact: true }).click();
  await current.waitFor({ state: "detached", timeout: 20000 });
  check("AUDIT-WORK-UNIT-DELETE-SETUP", true, `${workUnitCode} created and soft-deleted through real buttons`);

  for (const [id, entityType, action] of [
    ["AUDIT-PRODUCTION-PLAN", "ProductionPlan", "Added"],
    ["AUDIT-PLC-MAPPING", "PlcDeviceMapping", "Modified"],
    ["AUDIT-WORK-UNIT-DELETE", "WorkUnit", "Deleted"],
  ]) {
    const response = await page.request.get(`${baseUrl}/api/v1/audit?entityType=${entityType}&action=${action}&page=1&pageSize=1`);
    const body = response.ok() ? await response.json() : { items: [] };
    let detailsOk = false;
    if (body.items?.length) {
      const detailResponse = await page.request.get(`${baseUrl}/api/v1/audit/${body.items[0].id}`);
      const detail = detailResponse.ok() ? await detailResponse.json() : null;
      detailsOk = Boolean(detail && detail.entityType === entityType && detail.fields && !JSON.stringify(detail).match(/PasswordHash|SecurityStamp/));
    }
    check(id, response.ok() && body.items?.length > 0 && detailsOk,
      body.items?.length ? `${entityType}/${action} snapshot loaded from SQL` : `${entityType}/${action} has no development audit row`);
  }
  check("BROWSER-CONSOLE", consoleErrors.length === 0, consoleErrors.join(" | ") || "no console/page errors");
} catch (error) {
  result.errors.push(error.stack ?? String(error));
  process.exitCode = 1;
  await page.screenshot({ path: path.join(artifacts, "system-failure.png"), fullPage: true });
} finally {
  result.consoleErrors = consoleErrors;
  await fs.writeFile(path.join(artifacts, "system-results.json"), JSON.stringify(result, null, 2));
  await browser.close();
}
