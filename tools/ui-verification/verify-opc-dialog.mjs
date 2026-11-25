import { chromium } from "playwright-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifactDir = path.join(root, "artifacts", "ui-verification");
const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const runId = `UIV-OPC-${new Date().toISOString().replace(/\D/g, "").slice(4, 14)}`;
const result = { runId, startedAt: new Date().toISOString(), errors: [] };
const browser = await chromium.launch({
  executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 }, locale: "vi-VN" });
page.on("console", message => { if (message.type() === "error") result.errors.push({ type: "console", message: message.text() }); });
page.on("pageerror", error => result.errors.push({ type: "page", message: error.message }));

async function settle() {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForTimeout(700);
}

async function login() {
  await page.goto(`${baseUrl}/Account/Login`);
  await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
  await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForTimeout(900);
  if (page.url().toLowerCase().includes("account/login")) throw new Error("Đăng nhập acceptance OPC thất bại");
}

async function selectDialog(label, text) {
  const dialog = page.locator(".mud-dialog").last();
  await dialog.getByRole("combobox", { name: label, exact: true }).click();
  await page.getByRole("option").filter({ hasText: text }).first().click();
  await page.waitForTimeout(200);
}

try {
  await fs.mkdir(artifactDir, { recursive: true });
  await login();
  await page.goto(`${baseUrl}/plc/connections`);
  await settle();
  await page.getByRole("button", { name: "Thêm kết nối", exact: true }).click();
  await page.getByLabel("Mã kết nối", { exact: true }).fill(runId);
  await page.getByLabel("Tên kết nối", { exact: true }).fill(`${runId} Acceptance`);
  await page.getByLabel("Endpoint opc.tcp://", { exact: true }).fill("opc.tcp://127.0.0.1:4840");
  await page.getByRole("button", { name: "Lưu", exact: true }).click();
  await page.getByRole("row").filter({ hasText: runId }).first().waitFor({ state: "visible", timeout: 15000 });
  result.connectionPersisted = true;

  await page.goto(`${baseUrl}/plc/mappings`);
  await settle();
  await page.getByRole("button", { name: "Thêm mapping", exact: true }).click();
  const dialog = page.locator(".mud-dialog").last();
  await dialog.waitFor({ state: "visible" });
  await selectDialog("Kết nối OPC UA", runId);
  await selectDialog("WorkUnit", "M-010");
  await dialog.getByLabel("Mã thiết bị PLC", { exact: true }).fill(`${runId}-DEVICE`);
  const runTag = dialog.locator(".opc-tag-row").filter({ hasText: "RunState" }).first();
  const counterTag = dialog.locator(".opc-tag-row").filter({ hasText: "TotalCounter" }).first();
  await runTag.getByLabel("NodeId", { exact: true }).fill("ns=2;s=RunState");
  await counterTag.getByLabel("NodeId", { exact: true }).fill("ns=2;s=TotalCounter");
  const active = dialog.getByLabel("Mapping active", { exact: true });
  if (await active.isChecked()) await active.uncheck();
  await page.screenshot({ path: path.join(artifactDir, "OEE_040-desktop.png"), fullPage: true });
  await dialog.getByRole("button", { name: "LƯU", exact: true }).click();
  await dialog.waitFor({ state: "detached", timeout: 20000 });
  await page.getByRole("row").filter({ hasText: `${runId}-DEVICE` }).first().waitFor({ state: "visible", timeout: 15000 });
  result.mappingPersisted = true;

  await page.getByLabel("Sửa cấu hình OPC UA").first().click();
  await page.locator(".mud-dialog").last().getByText("RunState", { exact: true }).waitFor({ state: "visible" });
  result.reopenedFromPersistedRow = true;
  await page.screenshot({ path: path.join(artifactDir, "OEE_040-edit-desktop.png"), fullPage: true });
} catch (error) {
  result.errors.push({ type: "script", url: page.url(), message: error.message, stack: error.stack });
} finally {
  result.finishedAt = new Date().toISOString();
  result.status = result.connectionPersisted && result.mappingPersisted && result.reopenedFromPersistedRow && result.errors.length === 0 ? "pass" : "fail";
  await fs.writeFile(path.join(artifactDir, "opc-dialog-results.json"), JSON.stringify(result, null, 2));
  await browser.close();
}

process.stdout.write(`runId=${runId}; connection=${Boolean(result.connectionPersisted)}; mapping=${Boolean(result.mappingPersisted)}; reopen=${Boolean(result.reopenedFromPersistedRow)}; errors=${result.errors.length}; status=${result.status}\n`);
process.exitCode = result.status === "pass" ? 0 : 1;
