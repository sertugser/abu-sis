import { test, expect, type Page } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';
const ADMIN_EMAIL = 'admin@ankarabilim.edu.tr';
const ADMIN_PASSWORD = 'Admin123!';
const FACULTY_ID = 1;
const TERM_ID = 7;

interface LoginResult {
  token: string;
  expiresAt: string;
  roles: string[];
}

async function login(): Promise<LoginResult> {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
  });
  const data = await res.json() as { success: boolean; token: string; expiresAt: string; roles: string[]; error?: string };
  if (!data.success) throw new Error(`Login failed: ${data.error}`);
  return { token: data.token, expiresAt: data.expiresAt, roles: data.roles };
}

async function authenticate(page: Page) {
  const { token, expiresAt, roles } = await login();
  await page.addInitScript(({ t, e, r }) => {
    localStorage.setItem('sis_token', t);
    localStorage.setItem('sis_token_expires', e);
    localStorage.setItem('sis_roles', JSON.stringify(r));
  }, { t: token, e: expiresAt, r: roles });
}

test.describe('Scheduling Editor — drag-drop', () => {
  test.beforeEach(async ({ page }) => {
    await authenticate(page);
    page.on('pageerror', (err) => {
      // eslint-disable-next-line no-console
      console.error(`[browser-error] ${err.message}`);
    });
  });

  test('dragging an assigned card to a different empty cell triggers an assign request', async ({ page }) => {
    // Track POST /api/faculty-planning/assign — this is the canonical signal that the drag
    // succeeded (over was non-null, drop target was the cell, drop handler fired the mutation).
    const assignRequests: Array<{ url: string; body: unknown }> = [];
    page.on('request', (req) => {
      if (req.method() === 'POST' && req.url().endsWith('/api/faculty-planning/assign')) {
        let body: unknown = null;
        try { body = JSON.parse(req.postData() ?? 'null'); } catch { /* ignore */ }
        assignRequests.push({ url: req.url(), body });
      }
    });

    await page.goto(`/ders-programi-olusturma/duzenle?facultyId=${FACULTY_ID}&termId=${TERM_ID}`);
    await page.locator('[data-section-id]').first().waitFor({ state: 'visible', timeout: 15_000 });

    const sourceCard = page.locator('[data-section-id]').first();
    const srcBox = await sourceCard.boundingBox();
    if (!srcBox) throw new Error('Source card has no bounding box');

    // Empty drop-target cell: a <td> that contains the CalendarCell wrapper (aria-hidden absolute
    // overlay) AND no [data-section-id] descendant. Stay within viewport to keep the simulated
    // pointer over a real droppable.
    const candidates = await page.locator('td:has(> div[aria-hidden="true"])').evaluateAll((tds) =>
      tds.map((td) => {
        const hasCard = td.querySelector('[data-section-id]') !== null;
        const r = td.getBoundingClientRect();
        return { hasCard, x: r.x + r.width / 2, y: r.y + r.height / 2, w: r.width, h: r.height };
      }),
    );
    const empty = candidates.filter((b) => !b.hasCard && b.w > 50 && b.h > 30 && b.y > 100 && b.y < 800);
    if (empty.length === 0) throw new Error('No visible empty drop-target cell');
    const dst = empty[0];

    const startX = srcBox.x + srcBox.width / 2;
    const startY = srcBox.y + srcBox.height / 2;

    await page.mouse.move(startX, startY);
    await page.mouse.down();
    await page.mouse.move(startX + 12, startY + 4, { steps: 4 });
    await page.mouse.move(dst.x, dst.y, { steps: 25 });
    await page.waitForTimeout(150);
    await page.mouse.up();

    // Wait for the mutation to fire
    await page.waitForTimeout(1500);

    expect(
      assignRequests.length,
      'drag should fire POST /api/faculty-planning/assign after the React 19 ref fix',
    ).toBeGreaterThan(0);

    // Sanity-check the payload shape
    const last = assignRequests.at(-1)!;
    expect(last.body).toMatchObject({
      sectionId: expect.any(Number),
      scheduleSlots: expect.arrayContaining([
        expect.objectContaining({
          day: expect.any(Number),
          startTime: expect.any(String),
          endTime: expect.any(String),
        }),
      ]),
    });
  });

  test('dragging an assigned card to the unassigned pool fires an unassign request', async ({ page }) => {
    const unassignRequests: string[] = [];
    page.on('request', (req) => {
      if (req.method() === 'DELETE' && /\/api\/faculty-planning\/unassign\/\d+$/.test(req.url())) {
        unassignRequests.push(req.url());
      }
    });

    await page.goto(`/ders-programi-olusturma/duzenle?facultyId=${FACULTY_ID}&termId=${TERM_ID}`);
    await page.locator('[data-section-id]').first().waitFor({ state: 'visible', timeout: 15_000 });

    const sourceCard = page.locator('[data-section-id]').first();
    const srcBox = await sourceCard.boundingBox();
    if (!srcBox) throw new Error('Source card has no bounding box');

    const pool = page.locator('aside').filter({ hasText: 'Atanmamış' }).first();
    await pool.waitFor({ state: 'visible', timeout: 10_000 });
    const dstBox = await pool.boundingBox();
    if (!dstBox) throw new Error('Pool has no bounding box');

    const startX = srcBox.x + srcBox.width / 2;
    const startY = srcBox.y + srcBox.height / 2;
    const endX = dstBox.x + dstBox.width / 2;
    const endY = dstBox.y + dstBox.height / 2;

    await page.mouse.move(startX, startY);
    await page.mouse.down();
    await page.mouse.move(startX - 12, startY, { steps: 4 });
    await page.mouse.move(endX, endY, { steps: 25 });
    await page.waitForTimeout(150);
    await page.mouse.up();
    await page.waitForTimeout(1500);

    expect(
      unassignRequests.length,
      'drag onto the pool should fire DELETE /api/faculty-planning/unassign/{id}',
    ).toBeGreaterThan(0);
  });
});
