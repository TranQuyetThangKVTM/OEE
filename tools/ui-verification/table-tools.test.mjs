import assert from 'node:assert/strict';
import { test } from 'node:test';
import { JSDOM } from 'jsdom';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../src/Oee.Web/wwwroot/table-tools.js', import.meta.url), 'utf8');
const tools = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
const tick = async () => {
    await new Promise(resolve => setTimeout(resolve, 0));
    await new Promise(resolve => setTimeout(resolve, 0));
};
const table = `<table><colgroup><col><col><col></colgroup><thead><tr><th>Mã</th><th>SKU</th><th>Thao tác</th></tr></thead>
    <tbody><tr><td>M01</td><td>SKU01</td><td><button>Sửa</button></td></tr></tbody></table>`;

function setup(markup = table) {
    const dom = new JSDOM(`<div id="table-test">${markup}</div>`, { url: 'http://localhost/master/products' });
    globalThis.document = dom.window.document;
    globalThis.location = dom.window.location;
    globalThis.localStorage = dom.window.localStorage;
    globalThis.MutationObserver = dom.window.MutationObserver;
    const changes = [];
    const callback = { invokeMethodAsync: async (_, columns) => { changes.push(columns); } };
    return { dom, changes, callback, connect: () => tools.connect('table-test', 'Dữ liệu', callback),
        close: () => { tools.disconnect('table-test'); dom.window.close(); } };
}

test('hidden columns retain cells, data and action buttons; cannot hide final column', () => {
    const view = setup();
    try {
        assert.equal(view.connect().length, 3);
        tools.setVisible('table-test', 'SKU:0', false);
        assert.match(document.head.textContent, /nth-child\(2\)/);
        assert.equal(document.querySelector('tbody td:nth-child(2)').textContent, 'SKU01');
        assert.equal(document.querySelectorAll('tbody button').length, 1);
        tools.setVisible('table-test', 'Mã:0', false);
        const columns = tools.setVisible('table-test', 'Thao tác:0', false);
        assert.equal(columns.filter(column => column.visible).length, 1);
        assert.ok(tools.showAll('table-test').every(column => column.visible));
        assert.equal(document.head.textContent, '');
    } finally { view.close(); }
});

test('group selection and expand columns stay outside preferences and headings resize correctly', () => {
    const view = setup(`<table><thead><tr><th></th><th><input type="checkbox"></th><th>SKU</th><th>Ca</th></tr></thead>
        <tbody><tr><td><button>Thu gọn</button></td><td colspan="3"><div class="sample-machine-heading">M01</div></td></tr>
        <tr><td></td><td><input type="checkbox"></td><td>SKU01</td><td>S01</td></tr></tbody></table>`);
    try {
        assert.deepEqual(view.connect().map(column => column.label), ['SKU', 'Ca']);
        tools.setVisible('table-test', 'SKU:0', false);
        const headingCell = document.querySelector('td[colspan]');
        assert.equal(headingCell.colSpan, 2);
        assert.equal(document.querySelectorAll('[data-oee-data-row]').length, 1);
        tools.showAll('table-test');
        assert.equal(headingCell.colSpan, 3);
    } finally { view.close(); }
});

test('asynchronous loading and schema changes notify column UI without losing matching preferences', async () => {
    const view = setup('');
    try {
        assert.deepEqual(view.connect(), []);
        document.getElementById('table-test').innerHTML = table;
        await tick();
        assert.equal(view.changes.at(-1).length, 3);
        tools.setVisible('table-test', 'SKU:0', false);
        document.querySelector('thead tr').insertAdjacentHTML('afterbegin', '<th>Máy</th>');
        document.querySelector('tbody tr').insertAdjacentHTML('afterbegin', '<td>M01</td>');
        await tick();
        assert.equal(view.changes.at(-1).find(column => column.label === 'SKU').visible, false);
        assert.match(document.head.textContent, /nth-child\(3\)/);
    } finally { view.close(); }
});

test('same-table reload restores preferences and disconnect removes generated styles and observers', async () => {
    const view = setup();
    try {
        view.connect();
        tools.setVisible('table-test', 'SKU:0', false);
        tools.disconnect('table-test');
        assert.equal(document.head.querySelectorAll('style').length, 0);
        assert.equal(view.connect().find(column => column.label === 'SKU').visible, false);
        tools.disconnect('table-test');
        const before = view.changes.length;
        document.querySelector('thead th').textContent = 'Đổi tên';
        await tick();
        assert.equal(view.changes.length, before);
    } finally { view.close(); }
});

test('malformed saved preferences and storage denial do not break table controls', () => {
    const view = setup();
    try {
        localStorage.setItem('oee:columns:v1:/master/products:Dữ liệu', '{broken');
        assert.ok(view.connect().every(column => column.visible));
        tools.disconnect('table-test');
        Object.defineProperty(globalThis, 'localStorage', { configurable: true, get: () => { throw new Error('denied'); } });
        assert.equal(view.connect().length, 3);
        assert.equal(tools.setVisible('table-test', 'SKU:0', false).find(column => column.label === 'SKU').visible, false);
    } finally {
        delete globalThis.localStorage;
        view.close();
    }
});

test('ten live value updates do not rescan rows, read layout or rewrite CSS', async () => {
    const view = setup();
    let styleObserver;
    try {
        view.connect();
        tools.setVisible('table-test', 'SKU:0', false);
        let writes = 0, rowScans = 0, layoutReads = 0;
        styleObserver = new MutationObserver(records => { writes += records.length; });
        styleObserver.observe(document.head, { childList: true, subtree: true, characterData: true });
        const row = document.querySelector('tbody tr');
        const query = row.querySelector.bind(row);
        row.querySelector = (...args) => { rowScans++; return query(...args); };
        for (const header of document.querySelectorAll('th'))
            Object.defineProperty(header, 'innerText', { get: () => { layoutReads++; return header.textContent; } });
        const value = row.cells[0].firstChild;
        for (let index = 0; index < 10; index++) {
            value.data = String(index);
            await tick();
        }
        assert.deepEqual({ writes, rowScans, layoutReads }, { writes: 0, rowScans: 0, layoutReads: 0 });
    } finally { styleObserver?.disconnect(); view.close(); }
});

test('twenty detached page tables release observers and styles without a server disposal round trip', async () => {
    const view = setup('');
    try {
        for (let index = 0; index < 20; index++) {
            const root = document.createElement('div');
            root.id = `page-table-${index}`;
            root.innerHTML = table;
            document.body.append(root);
            tools.connect(root.id, 'Dữ liệu', view.callback);
            root.remove();
        }
        await tick();
        assert.equal(document.head.querySelectorAll('style').length, 0);
    } finally {
        for (let index = 0; index < 20; index++) tools.disconnect(`page-table-${index}`);
        view.close();
    }
});
