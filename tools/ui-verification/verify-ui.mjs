import { chromium } from "playwright-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

if (!process.env.OEE_UI_PASSWORD) throw new Error("Set OEE_UI_PASSWORD for the test account.");

const baseUrl = process.env.OEE_BASE_URL ?? "http://localhost:5052";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const artifactDir = path.join(root, "artifacts", "ui-verification");
const executablePath = process.env.OEE_BROWSER_PATH
  ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";

const screens = [
  ["001", "/Account/Login", "Chào mừng trở lại", false],
  ["002", "/", "Bảng điều khiển", true],
  ["003", "/", "Trạng thái", true],
  ["004", "/production/plans", "Kế hoạch sản xuất", true],
  ["005", "/production/plan-templates", "Kế hoạch sản xuất mẫu", true],
  ["006", "/production/plan-templates", "Sinh kế hoạch theo ngày/ca", true, "sample-detail"],
  ["008", "/downtime", "Phân loại", true],
  ["010", "/master/products", "Sản phẩm", true],
  ["011", "/master/work-unit-standards", "Định mức tốc độ", true],
  ["012", "/master/shifts", "Ca làm việc", true],
  ["013", "/master/downtime-reasons", "Lý do dừng máy", true],
  ["014", "/master/work-units", "Thiết bị", true],
  ["015", "/master/work-unit-groups", "Nhóm thiết bị", true],
  ["016", "/master/processes", "Công đoạn", true],
  ["017", "/equipment/enterprises", "Doanh nghiệp", true],
  ["018", "/equipment/sites", "Nhà máy", true],
  ["022", "/reports/device-detail", "Chi tiết OEE", true],
  ["023", "/reports/shift-kpi", "KPI theo ca", true],
  ["024", "/reports/daily-oee", "KPI theo ngày", true],
  ["025", "/reports/weekly-kpi", "KPI theo tuần ISO", true],
  ["026", "/reports/monthly-kpi", "KPI theo tháng", true],
  ["029", "/reports/downtime-events", "Danh sách sự kiện downtime", true],
  ["030", "/reports/quality-loss", "Tổn thất chất lượng", true],
  ["031", "/reports/reliability", "tin cậy", true],
  ["033", "/production/reconciliation", "Đối soát", true],
  ["034", "/alerts", "Cảnh báo", true],
  ["035", "/alerts/targets", "mục tiêu", true],
  ["036", "/live-oee", "OEE realtime", true],
  ["037", "/andon", "ANDON", true],
  ["038", "/plc/status", "PLC", true],
  ["039", "/plc/mappings", "Ánh xạ thiết bị", true],
  ["040", "/plc/mappings", "Cấu hình OPC UA", true, "opc-dialog"],
  ["041", "/plc/faults", "Ánh xạ mã lỗi", true],
  ["042", "/plc/events", "Nhật ký sự kiện", true],
  ["043", "/users", "Quản lý người dùng", true],
  ["044", "/audit", "Nhật ký audit", true],
  ["045", "/", "Phiên bảo mật", true, "user-popover"],
];

const popupOpeners = [
  ["001", "/production/plans", "Tạo kế hoạch", "Thêm kế hoạch sản xuất"],
  ["002", "/production/plan-templates", "Thêm mẫu", "Thêm kế hoạch mẫu"],
  ["003", "/production/plan-templates", "Thêm dòng", "Thêm dòng vào mẫu", "sample-detail"],
  ["004", "/master/products", "Thêm sản phẩm", "Thêm sản phẩm mới"],
  ["005", "/master/work-unit-standards", "Thêm định mức", "Thêm định mức tốc độ"],
  ["006", "/master/shifts", "Thêm ca", "Thêm ca làm việc"],
  ["007", "/master/downtime-reasons", "Thêm lý do", "Thêm lý do dừng máy"],
  ["008", "/master/work-units", "Thêm thiết bị", "Thêm thiết bị"],
  ["009", "/master/work-unit-groups", "Thêm nhóm", "Thêm nhóm thiết bị"],
  ["010", "/master/processes", "Thêm công đoạn", "Thêm công đoạn"],
  ["011", "/equipment/enterprises", "Thêm doanh nghiệp", "Thêm doanh nghiệp"],
  ["012", "/equipment/sites", "Thêm nhà máy", "Thêm nhà máy"],
];

async function waitForBlazor(page) {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForSelector("body", { state: "visible" });
  await page.waitForTimeout(900);
}

async function login(page) {
  await page.goto(`${baseUrl}/Account/Login`);
  await waitForBlazor(page);
  await page.locator("#userName").fill(process.env.OEE_UI_USER ?? "admin");
  await page.locator("#password").fill(process.env.OEE_UI_PASSWORD);
  await Promise.all([
    page.waitForURL(url => !url.pathname.toLowerCase().includes("account/login")),
    page.getByRole("button", { name: "Đăng nhập", exact: true }).click(),
  ]);
  await waitForBlazor(page);
}

async function resolveSampleDetail(page) {
  await page.goto(`${baseUrl}/production/plan-templates`);
  await waitForBlazor(page);
  const href = await page.locator('a[href^="/production/plan-templates/"]').first().getAttribute("href").catch(() => null);
  return href && !href.endsWith("/edit") ? href : null;
}

async function openSpecial(page, special, sampleDetail) {
  if (special === "sample-detail") {
    if (!sampleDetail) throw new Error("Không tìm thấy kế hoạch mẫu thật để mở trang chi tiết");
    await page.goto(`${baseUrl}${sampleDetail}`);
    await waitForBlazor(page);
  } else if (special === "opc-dialog") {
    const edit = page.getByRole("button", { name: /Sửa cấu hình|Thêm cấu hình|Cấu hình OPC/i }).first();
    await edit.click();
    await page.getByText("Cấu hình OPC UA", { exact: false }).last().waitFor({ state: "visible" });
  } else if (special === "user-popover") {
    const avatar = page.getByLabel("Mở menu người dùng");
    await avatar.click();
    await page.getByText("Phiên bảo mật", { exact: true }).waitFor({ state: "visible" });
  }
}

async function run() {
  await fs.mkdir(artifactDir, { recursive: true });
  const browser = await chromium.launch({ executablePath, headless: true });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 }, locale: "vi-VN" });
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  const failedResponses = [];
  page.on("console", message => { if (message.type() === "error") consoleErrors.push({ url: page.url(), message: message.text() }); });
  page.on("pageerror", error => pageErrors.push({ url: page.url(), message: error.message }));
  page.on("response", response => { if (response.status() >= 400 && !response.url().includes("favicon")) failedResponses.push({ url: response.url(), status: response.status() }); });

  const results = [];
  await page.goto(`${baseUrl}/Account/Login`);
  await waitForBlazor(page);
  await page.screenshot({ path: path.join(artifactDir, "OEE_001-desktop.png"), fullPage: true });
  results.push({ id: "OEE_001", route: "/Account/Login", expected: "Chào mừng trở lại", status: (await page.getByText("Chào mừng trở lại").count()) ? "pass" : "fail" });
  await login(page);
  const sampleDetail = await resolveSampleDetail(page);

  for (const [id, route, expected, authenticated, special] of screens.slice(1)) {
    const beforeConsole = consoleErrors.length;
    const beforePage = pageErrors.length;
    const beforeHttp = failedResponses.length;
    try {
      await page.goto(`${baseUrl}${route}`);
      await waitForBlazor(page);
      if (authenticated && page.url().toLowerCase().includes("account/login")) throw new Error("Bị chuyển về login");
      await openSpecial(page, special, sampleDetail);
      const bodyText = await page.locator("body").innerText();
      const found = bodyText.toLocaleLowerCase("vi").includes(expected.toLocaleLowerCase("vi"));
      const layout = await page.evaluate(() => ({ width: document.documentElement.clientWidth, scrollWidth: document.documentElement.scrollWidth }));
      await page.screenshot({ path: path.join(artifactDir, `OEE_${id}-desktop.png`), fullPage: true });
      results.push({ id: `OEE_${id}`, route: special === "sample-detail" ? sampleDetail : route, expected, status: found ? "pass" : "fail", horizontalOverflow: layout.scrollWidth > layout.width + 2, consoleErrors: consoleErrors.length - beforeConsole, pageErrors: pageErrors.length - beforePage, httpErrors: failedResponses.length - beforeHttp });
      if (special === "opc-dialog" || special === "user-popover") await page.keyboard.press("Escape").catch(() => {});
    } catch (error) {
      results.push({ id: `OEE_${id}`, route, expected, status: "fail", error: error.message, consoleErrors: consoleErrors.length - beforeConsole, pageErrors: pageErrors.length - beforePage, httpErrors: failedResponses.length - beforeHttp });
    }
  }

  const popupResults = [];
  for (const [id, route, opener, expected, special] of popupOpeners) {
    try {
      await page.goto(`${baseUrl}${route}`);
      await waitForBlazor(page);
      if (special === "sample-detail") await openSpecial(page, special, sampleDetail);
      const effectiveOpener = id === "005" ? "Thêm phiên bản" : opener;
      await page.getByRole("button", { name: new RegExp(effectiveOpener, "i") }).first().click();
      await page.getByText(expected, { exact: false }).last().waitFor({ state: "visible" });
      const dialog = page.locator(".mud-dialog").last();
      const hasForm = await dialog.locator("form, .mud-form").count() > 0;
      await page.screenshot({ path: path.join(artifactDir, `POPUP_${id}-desktop.png`), fullPage: true });
      popupResults.push({ id: `POPUP_${id}`, route: special === "sample-detail" ? sampleDetail : route, expected, status: "pass", hasForm });
      await page.keyboard.press("Escape");
      await page.waitForTimeout(250);
    } catch (error) {
      popupResults.push({ id: `POPUP_${id}`, route, expected, status: "fail", error: error.message });
      await page.keyboard.press("Escape").catch(() => {});
    }
  }

  const tablet = await context.newPage();
  await tablet.setViewportSize({ width: 1024, height: 768 });
  for (const [id, route, expected, authenticated, special] of screens.filter(([, , , , sp]) => !sp)) {
    try {
      await tablet.goto(`${baseUrl}${route}`);
      await waitForBlazor(tablet);
      if (authenticated && tablet.url().toLowerCase().includes("account/login")) continue;
      const layout = await tablet.evaluate(() => ({ width: document.documentElement.clientWidth, scrollWidth: document.documentElement.scrollWidth }));
      await tablet.screenshot({ path: path.join(artifactDir, `OEE_${id}-tablet.png`), fullPage: true });
      const result = results.find(x => x.id === `OEE_${id}`);
      if (result) result.tabletHorizontalOverflow = layout.scrollWidth > layout.width + 2;
    } catch { /* desktop result keeps the primary failure evidence */ }
  }

  await fs.writeFile(path.join(artifactDir, "ui-verification-results.json"), JSON.stringify({ generatedAt: new Date().toISOString(), baseUrl, sampleDetail, results, popupResults, consoleErrors, pageErrors, failedResponses }, null, 2));
  await browser.close();
  const failures = results.filter(x => x.status !== "pass").length + popupResults.filter(x => x.status !== "pass").length;
  process.stdout.write(`Screens: ${results.length - results.filter(x => x.status !== "pass").length}/${results.length}; popups: ${popupResults.length - popupResults.filter(x => x.status !== "pass").length}/${popupResults.length}; failures=${failures}\n`);
  process.exitCode = failures ? 1 : 0;
}

await run();
