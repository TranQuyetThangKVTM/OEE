// Only presentation attributes/styles are changed. Rows/order/content remain owned by Blazor.
const views = new Map();
let removalObserver;
let observedDocument;

function text(cell) {
    // textContent does not force browser layout like innerText.
    return (cell?.textContent ?? '').replace(/\r/g, '').trim();
}

function inspect(view) {
    const table = view.root.querySelector('table');
    if (!table) return [];
    const headers = [...(table.tHead?.rows[0]?.cells ?? [])];
    const occurrences = new Map();
    view.columns = headers.map((cell, index) => {
        const label = text(cell).replace(/\s+/g, ' ');
        const count = occurrences.get(label) ?? 0;
        occurrences.set(label, count + 1);
        const key = `${label}:${count}`;
        return { key, label, index, visible: !view.hidden.has(key) };
    }).filter(column => column.label);
    // Old preferences must never leave a changed table with every data column hidden.
    if (view.columns.length && view.columns.every(column => !column.visible)) {
        view.hidden.delete(view.columns[0].key);
        view.columns[0].visible = true;
    }
    const hiddenIndices = view.columns.filter(column => !column.visible).map(column => column.index);
    // Value-only updates need no row inspection. Even structural changes need no
    // per-row work when all columns are visible (the default for most tables).
    const rows = hiddenIndices.length || view.hadHidden ? [...(table.tBodies[0]?.rows ?? [])] : [];
    rows.forEach(row => {
        const heading = row.querySelector('.sample-machine-heading, .table-group-heading');
        const isData = !heading && row.cells.length === headers.length && ![...row.cells].some(cell => cell.colSpan > 1);
        if (row.hasAttribute('data-oee-data-row') !== isData) row.toggleAttribute('data-oee-data-row', isData);
        // Group headings span every currently visible data column, including selection columns.
        if (heading) {
            const cell = row.cells[row.cells.length - 1];
            const span = Math.max(1, headers.length - 1 - hiddenIndices.length);
            if (cell && cell.colSpan !== span) cell.colSpan = span;
        }
    });
    const css = hiddenIndices.map(index =>
        `#${view.root.id} thead tr > :nth-child(${index + 1}),` +
        `#${view.root.id} tbody tr[data-oee-data-row] > :nth-child(${index + 1}),` +
        `#${view.root.id} colgroup > col:nth-child(${index + 1}){display:none!important}`
    ).join('\n');
    if (view.style.textContent !== css) view.style.textContent = css;
    view.hadHidden = hiddenIndices.length > 0;
    return view.columns.map(({ key, label, visible }) => ({ key, label, visible }));
}

function affectsColumns(record) {
    const element = record.target.nodeType === 1 ? record.target : record.target.parentElement;
    if (element?.closest('thead')) return true;
    if (record.type !== 'childList') return false;
    return [...record.addedNodes, ...record.removedNodes].some(node => node.nodeType === 1
        && (node.matches('table,thead,tbody,tr,th,td,colgroup,col')
            || node.querySelector('table,thead,tbody,tr,th,td,colgroup,col')));
}

function watchRemovedTables() {
    if (removalObserver && observedDocument === document) return;
    removalObserver?.disconnect();
    observedDocument = document;
    removalObserver = new MutationObserver(records => {
        if (!records.some(record => record.removedNodes.length)) return;
        for (const [id, view] of views) if (!view.root.isConnected) disconnect(id);
    });
    removalObserver.observe(document.body, { childList: true, subtree: true });
}

function scheduleInspection(id, view) {
    if (view.frame !== null) return;
    const schedule = callback => globalThis.requestAnimationFrame
        ? globalThis.requestAnimationFrame(callback) : setTimeout(callback, 0);
    view.cancelFrame = handle => globalThis.cancelAnimationFrame
        ? globalThis.cancelAnimationFrame(handle) : clearTimeout(handle);
    view.frame = schedule(() => {
        view.frame = null;
        if (!view.root.isConnected) { disconnect(id); return; }
        const columns = inspect(view);
        const signature = JSON.stringify(columns);
        if (view.signature !== signature) {
            view.signature = signature;
            view.callback.invokeMethodAsync('UpdateColumns', columns).catch(() => {});
        }
    });
}

export function connect(id, title, callback) {
    const root = document.getElementById(id);
    if (!root) return null;
    const storageKey = `oee:columns:v1:${location.pathname}:${title}`;
    let view = views.get(id);
    if (view && (view.root !== root || view.storageKey !== storageKey)) { disconnect(id); view = null; }
    if (view) return view.columns.map(({ key, label, visible }) => ({ key, label, visible }));
    if (!view) {
        let saved = [];
        try { saved = JSON.parse(localStorage.getItem(storageKey) ?? '[]'); } catch { /* Storage may be disabled. */ }
        const style = document.createElement('style');
        document.head.append(style);
        view = { root, style, storageKey, callback, frame: null, hadHidden: false,
            hidden: new Set(Array.isArray(saved) ? saved.filter(value => typeof value === 'string') : []), columns: [] };
        view.observer = new MutationObserver(records => {
            if (records.some(affectsColumns)) scheduleInspection(id, view);
        });
        view.observer.observe(root, { childList: true, subtree: true, characterData: true });
        views.set(id, view);
        watchRemovedTables();
    }
    const columns = inspect(view);
    view.signature = JSON.stringify(columns);
    return columns;
}

function persist(view) {
    try { localStorage.setItem(view.storageKey, JSON.stringify([...view.hidden])); } catch { /* In-memory preferences still work. */ }
    const columns = inspect(view);
    view.signature = JSON.stringify(columns);
    return columns;
}

export function setVisible(id, key, visible) {
    const view = views.get(id);
    if (!view) return [];
    if (!visible && view.columns.filter(column => column.visible).length <= 1) return inspect(view);
    if (visible) view.hidden.delete(key); else view.hidden.add(key);
    return persist(view);
}

export function showAll(id) {
    const view = views.get(id);
    if (!view) return [];
    view.hidden.clear();
    return persist(view);
}

export function disconnect(id) {
    const view = views.get(id);
    if (view?.frame !== null && view?.frame !== undefined) view.cancelFrame(view.frame);
    view?.observer.disconnect();
    view?.style.remove();
    views.delete(id);
    if (views.size === 0) {
        removalObserver?.disconnect();
        removalObserver = null;
        observedDocument = null;
    }
}
