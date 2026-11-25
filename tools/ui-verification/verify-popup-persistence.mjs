import { chromium } from "playwright-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifactDir = path.join(root, "artifacts", "ui-verification");
const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const runId = `UIV-${new Date().toISOString().replace(/\D/g, "").slice(4, 14)}`;
const result = { runId, startedAt: new Date().toISOString(), actions: [], errors: [] };

const browser = await chromium.launch({
  executablePath: process.env.OEE_BROWSER_PATH ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  headless: true,
});
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, locale: "vi-VN" });
page.on("console", message => { if (message.type() === "error") result.errors.push({ type: "console", url: page.url(), message: message.text() }); });
page.on("pageerror", error => result.errors.push({ type: "page", url: page.url(), message: error.message }));

async function settle() {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForSelector("body", { state: "visible" });
  await page.waitForTimeout(650);
}

async function login() {
  await page.goto(`${baseUrl}/Account/Login`);
  await settle();
  await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
  await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
  await Promise.all([
    page.waitForURL(url => !url.pathname.toLowerCase().includes("account/login")),
    page.getByRole("button", { name: "Đăng nhập", exact: true }).click(),
  ]);
  await settle();
}

function currentDialog() {
  return page.locator(".mud-dialog").last();
}

async function fill(label, value) {
  await currentDialog().getByLabel(label, { exact: true }).fill(value);
}

async function selectOne(label, match) {
  await currentDialog().getByRole("combobox", { name: label, exact: true }).click();
  const option = match
    ? page.getByRole("option").filter({ hasText: match }).first()
    : page.getByRole("option").first();
  await option.waitFor({ state: "visible" });
  await option.click();
  await page.waitForTimeout(250);
}

async function selectMany(label, matches) {
  await currentDialog().getByRole("combobox", { name: label, exact: true }).click();
  for (const match of matches) {
    const option = page.getByRole("option").filter({ hasText: match }).first();
    await option.waitFor({ state: "visible" });
    await option.click();
  }
  await page.keyboard.press("Escape");
  await page.waitForTimeout(250);
}

async function saveDialog(buttonName, expectedText, id, validateWithTab = true) {
  const dialog = currentDialog();
  if (validateWithTab) {
    await page.keyboard.press("Tab");
    await page.waitForTimeout(250);
  }
  await dialog.getByRole("button", { name: buttonName, exact: true }).click();
  await dialog.waitFor({ state: "detached", timeout: 20000 });
  if (expectedText) {
    await page.getByText(expectedText, { exact: false }).first().waitFor({ state: "visible", timeout: 15000 });
  }
  result.actions.push({ id, status: "pass", persistedText: expectedText ?? "DialogResult.Ok; parent reloaded" });
}

async function openMaster(route, opener) {
  await page.goto(`${baseUrl}${route}`);
  await settle();
  await page.getByRole("button", { name: new RegExp(opener, "i") }).first().click();
  await currentDialog().waitFor({ state: "visible" });
  await page.waitForTimeout(250);
}

async function masterPopups() {
  await openMaster("/master/products", "Thêm sản phẩm");
  await fill("Mã sản phẩm", `${runId}-SKU`);
  await fill("Tên sản phẩm", `${runId} Acceptance SKU`);
  await fill("Mô tả", "Tạo bởi kiểm tra UI Prompt 13");
  await saveDialog("Lưu", `${runId}-SKU`, "POPUP_004");

  await openMaster("/master/work-unit-standards", "Thêm phiên bản");
  await selectOne("Thiết bị / dây chuyền", "M-011");
  await selectOne("Sản phẩm (SKU)", `${runId}-SKU`);
  await fill("Người phê duyệt", "UI Acceptance");
  await saveDialog("Lưu", `${runId}-SKU`, "POPUP_005");

  await openMaster("/master/shifts", "Thêm ca");
  await selectOne("Nhà máy");
  await fill("Mã ca", `${runId}-S`);
  await fill("Tên ca", `${runId} Acceptance Shift`);
  await saveDialog("Lưu", `${runId}-S`, "POPUP_006");

  await openMaster("/master/downtime-reasons", "Thêm lý do");
  await fill("Mã lý do", `${runId}-DR`);
  await fill("Tên lý do", `${runId} Acceptance Reason`);
  await fill("Mô tả", "Tạo bởi kiểm tra UI Prompt 13");
  await saveDialog("Lưu", `${runId}-DR`, "POPUP_007");

  await openMaster("/master/work-units", "Thêm thiết bị");
  await selectOne("Nhóm thiết bị");
  await fill("Mã thiết bị", `${runId}-M`);
  await fill("Tên thiết bị", `${runId} Acceptance Machine`);
  await saveDialog("Lưu", `${runId}-M`, "POPUP_008");

  await openMaster("/master/work-unit-groups", "Thêm nhóm");
  await selectOne("Công đoạn");
  await fill("Mã nhóm", `${runId}-G`);
  await fill("Tên nhóm", `${runId} Acceptance Group`);
  await saveDialog("Lưu", `${runId}-G`, "POPUP_009");

  await openMaster("/master/processes", "Thêm công đoạn");
  await selectOne("Nhà máy");
  await fill("Mã công đoạn", `${runId}-P`);
  await fill("Tên công đoạn", `${runId} Acceptance Process`);
  await saveDialog("Lưu", `${runId}-P`, "POPUP_010");

  await openMaster("/equipment/enterprises", "Thêm doanh nghiệp");
  await fill("Mã doanh nghiệp", `${runId}-E`);
  await fill("Tên doanh nghiệp", `${runId} Acceptance Enterprise`);
  await fill("Mô tả", "Tạo bởi kiểm tra UI Prompt 13");
  await saveDialog("Lưu", `${runId}-E`, "POPUP_011");

  await openMaster("/equipment/sites", "Thêm nhà máy");
  await selectOne("Doanh nghiệp", `${runId}-E`);
  await fill("Mã nhà máy", `${runId}-SITE`);
  await fill("Tên nhà máy", `${runId} Acceptance Site`);
  await currentDialog().getByLabel("Múi giờ (Windows ID)", { exact: true }).click();
  await page.getByRole("option").filter({ hasText: "SE Asia Standard Time" }).first().click();
  await page.waitForTimeout(250);
  await saveDialog("Lưu", `${runId}-SITE`, "POPUP_012", false);
}

async function createSampleAndLine() {
  await page.goto(`${baseUrl}/production/plan-templates`);
  await settle();
  await page.getByRole("button", { name: /Thêm mẫu/i }).click();
  await currentDialog().waitFor({ state: "visible" });
  await fill("Tên kế hoạch mẫu", `${runId} Acceptance Sample`);
  await fill("Mô tả", "2 thiết bị × 3 ca qua MudBlazor");
  await selectMany("Thiết bị / dây chuyền", ["M-001", "M-002"]);
  await selectMany("Ca làm việc", ["S1", "S2", "S3"]);
  await selectOne("Sản phẩm (SKU)", `${runId}-SKU`);
  await fill("Ghi chú", "Kiểm tra transaction cha và 6 dòng");
  await currentDialog().getByText("6 dòng", { exact: false }).waitFor({ state: "visible" });
  await saveDialog("Tạo mẫu", `${runId} Acceptance Sample`, "POPUP_002");

  const row = page.getByRole("row").filter({ hasText: `${runId} Acceptance Sample` }).first();
  const detailHref = await row.getByLabel("Xem mẫu").getAttribute("href");
  if (!detailHref) throw new Error("Mẫu đã lưu nhưng không có link chi tiết");
  result.sampleDetail = detailHref;
  await page.goto(`${baseUrl}${detailHref}`);
  await settle();
  await page.getByRole("button", { name: /Thêm dòng/i }).click();
  await currentDialog().waitFor({ state: "visible" });
  await selectMany("Thiết bị / dây chuyền", ["M-003"]);
  await selectMany("Ca làm việc", ["S1"]);
  await selectOne("Sản phẩm (SKU)", `${runId}-SKU`);
  await currentDialog().getByRole("button", { name: "Xem trước dòng mới", exact: true }).click();
  await currentDialog().getByText("1 dòng mới", { exact: false }).waitFor({ state: "visible" });
  await saveDialog("Thêm dòng", null, "POPUP_003");
  await page.getByRole("row").filter({ hasText: "M-003" }).filter({ hasText: `${runId}-SKU` }).first().waitFor({ state: "visible" });
}

async function createPlans() {
  await page.goto(`${baseUrl}/production/plans`);
  await settle();
  await page.getByRole("button", { name: "Tạo kế hoạch", exact: true }).click();
  await currentDialog().waitFor({ state: "visible" });
  await selectMany("Thiết bị / dây chuyền", [`${runId}-M`]);
  await selectMany("Ca làm việc", [`${runId}-S`]);
  await selectOne("Sản phẩm (SKU)", `${runId}-SKU`);
  await currentDialog().getByRole("button", { name: "Xem trước tổ hợp", exact: true }).click();
  await currentDialog().getByText("1 hợp lệ", { exact: false }).waitFor({ state: "visible", timeout: 15000 });
  await currentDialog().getByRole("button", { name: "Tạo kế hoạch", exact: true }).click();
  await currentDialog().waitFor({ state: "detached", timeout: 20000 });
  result.actions.push({ id: "POPUP_001_DIRECT", status: "pass", requested: 1 });

  await page.getByRole("button", { name: "Từ kế hoạch mẫu", exact: true }).click();
  await currentDialog().waitFor({ state: "visible" });
  const sampleInput = currentDialog().getByLabel("Kế hoạch mẫu", { exact: true });
  await sampleInput.fill(`${runId} Acceptance Sample`);
  const sampleOption = page.getByRole("option").filter({ hasText: `${runId} Acceptance Sample` }).first();
  await sampleOption.waitFor({ state: "visible", timeout: 10000 });
  await sampleOption.click();
  await currentDialog().getByText("7 hợp lệ", { exact: false }).waitFor({ state: "visible", timeout: 20000 });
  await currentDialog().getByRole("button", { name: "Tạo kế hoạch", exact: true }).click();
  await currentDialog().waitFor({ state: "detached", timeout: 20000 });
  result.actions.push({ id: "POPUP_001_FROM_SAMPLE", status: "pass", requested: 7 });
}

try {
  await fs.mkdir(artifactDir, { recursive: true });
  await login();
  await masterPopups();
  await createSampleAndLine();
  await createPlans();
} catch (error) {
  result.errors.push({ type: "script", url: page.url(), message: error.message, stack: error.stack });
} finally {
  result.finishedAt = new Date().toISOString();
  result.status = result.errors.length === 0 && result.actions.length === 13 ? "pass" : "fail";
  await page.screenshot({ path: path.join(artifactDir, "popup-persistence-final.png"), fullPage: true }).catch(() => {});
  await fs.writeFile(path.join(artifactDir, "popup-persistence-results.json"), JSON.stringify(result, null, 2));
  await browser.close();
}

process.stdout.write(`runId=${runId}; actions=${result.actions.length}/13; errors=${result.errors.length}; status=${result.status}\n`);
process.exitCode = result.status === "pass" ? 0 : 1;
