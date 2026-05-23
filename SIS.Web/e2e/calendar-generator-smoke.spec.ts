/**
 * E2E smoke: /akademik-takvim-olusturma (AcademicCalendarGeneratorPage)
 *
 * Amaç: sayfanın hook/bileşen refactor'undan (useCalendarNotices /
 * useCalendarGeneratorForm / useCalendarDraftWorkspace + leaf bileşenler)
 * sonra davranışın korunduğunu canlı doğrulamak.
 *
 * NON-DESTRUCTIVE: yalnızca taslak üretir (30 gün TTL), "Onayla"ya BASMAZ
 * (gerçek akademik takvime yazmaz). Üretilen taslaklar afterAll'da API ile silinir.
 */
import { test, expect, type Page } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';
const ADMIN_EMAIL = 'admin@ankarabilim.edu.tr';
const ADMIN_PASSWORD = 'Admin123!';
const ROUTE = '/akademik-takvim-olusturma';

let token: string;
let expiresAt: string;
let roles: string[];
const preExistingDraftIds = new Set<number>();

async function login() {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
  });
  const data = await res.json() as { success: boolean; token: string; expiresAt: string; roles: string[]; error?: string };
  if (!data.success) throw new Error(`Login failed: ${data.error}`);
  return data;
}

function authHeaders() {
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
}

async function listDraftIds(): Promise<number[]> {
  const res = await fetch(`${API_BASE}/admin/calendar-generator/drafts?take=200`, { headers: authHeaders() });
  if (!res.ok) throw new Error(`GET drafts → ${res.status}`);
  const data = await res.json() as { id: number }[];
  return data.map(d => d.id);
}

async function authenticatePage(page: Page) {
  await page.addInitScript(({ t, e, r }) => {
    localStorage.setItem('sis_token', t);
    localStorage.setItem('sis_token_expires', e);
    localStorage.setItem('sis_roles', JSON.stringify(r));
  }, { t: token, e: expiresAt, r: roles });
}

test.beforeAll(async () => {
  const r = await login();
  token = r.token; expiresAt = r.expiresAt; roles = r.roles;
  for (const id of await listDraftIds())
    preExistingDraftIds.add(id);
});

test.beforeEach(async ({ page }) => {
  await authenticatePage(page);
});

test.afterAll(async () => {
  // Test sırasında üretilen taslakları temizle (Onayla'ya basılmadığı için DB'de yalnızca draft kayıtları kalır)
  for (const id of await listDraftIds()) {
    if (!preExistingDraftIds.has(id)) {
      await fetch(`${API_BASE}/admin/calendar-generator/drafts/${id}`, { method: 'DELETE', headers: authHeaders() });
    }
  }
});

// ────────────────────────────────────────────────────────────────
// TEST 1 — Sayfa + 3 sekme render (form hook + Tabs)
// ────────────────────────────────────────────────────────────────
test('Test 1 — Sayfa yüklenir, 3 sekme ve üret formu görünür', async ({ page }) => {
  await page.goto(ROUTE);

  await expect(page.getByRole('tab', { name: 'Kısıtlar & Üret' })).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole('tab', { name: 'Taslak & Zincirleme' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Tatil Tanımları' })).toBeVisible();

  // Form alanları (useCalendarGeneratorForm) render edildi
  await expect(page.getByPlaceholder('2025-2026')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Taslak Üret' })).toBeVisible();
  await expect(page.getByText('Yönetmelik Kontrolü')).toBeVisible();
});

// ────────────────────────────────────────────────────────────────
// TEST 2 — Tatil Tanımları sekmesi (HolidaysTab bileşeni) tatil kartları gösterir
// ────────────────────────────────────────────────────────────────
test('Test 2 — Tatil Tanımları sekmesi tatil kartlarını listeler', async ({ page }) => {
  await page.goto(ROUTE);
  await page.getByRole('tab', { name: 'Tatil Tanımları' }).click();

  await expect(page.getByRole('heading', { name: 'Tatil tanımları' })).toBeVisible({ timeout: 10_000 });
  // En az bir tatil kartı render edildi (her kartta "Süre: N gün")
  await expect(page.getByText(/Süre:\s*\d/).first()).toBeVisible({ timeout: 10_000 });
});

// ────────────────────────────────────────────────────────────────
// TEST 3 — Taslak Üret: ConfirmDialog → onay → Taslak sekmesi + NoticeToast
//          (useCalendarNotices + useCalendarDraftWorkspace.handleGenerate)
// ────────────────────────────────────────────────────────────────
test('Test 3 — Taslak üretme akışı çalışır ve taslak sekmesini açar', async ({ page }) => {
  await page.goto(ROUTE);

  await page.getByRole('button', { name: 'Taslak Üret' }).click();

  // ConfirmDialog (çıkarılan bileşen) açıldı
  await expect(page.getByRole('heading', { name: 'Taslak üretilecek' })).toBeVisible({ timeout: 10_000 });
  await page.getByRole('button', { name: 'Evet, üret' }).click();

  // handleGenerate → activeTab='draft' → "Taslak #N" başlığı görünür
  await expect(page.getByRole('heading', { name: /Taslak #\d+/ })).toBeVisible({ timeout: 30_000 });

  // TermSummaryCards (çıkarılan bileşen) — Dönem Özeti tablosu
  await expect(page.getByText('Dönem Özeti')).toBeVisible({ timeout: 10_000 });
  // WarningsPanel (çıkarılan bileşen)
  await expect(page.getByRole('heading', { name: 'Uyarılar' })).toBeVisible();
  // Takvim Görünümü (DraftMiniCalendar)
  await expect(page.getByText('Takvim Görünümü')).toBeVisible();
});

// ────────────────────────────────────────────────────────────────
// TEST 4 — Üretilen taslakta etkinlik seçme → Tarih Düzenleme paneli
//          (useCalendarDraftWorkspace.selectEvent + editForm)
// ────────────────────────────────────────────────────────────────
test('Test 4 — Taslak etkinliğine tıklayınca tarih düzenleme paneli dolar', async ({ page }) => {
  await page.goto(ROUTE);
  await page.getByRole('button', { name: 'Taslak Üret' }).click();
  await expect(page.getByRole('heading', { name: 'Taslak üretilecek' })).toBeVisible({ timeout: 10_000 });
  await page.getByRole('button', { name: 'Evet, üret' }).click();
  await expect(page.getByRole('heading', { name: /Taslak #\d+/ })).toBeVisible({ timeout: 30_000 });

  // Etkinlik listesindeki ilk satıra tıkla (aria-pressed butonları)
  const firstEvent = page.locator('button[aria-pressed]').first();
  await expect(firstEvent).toBeVisible({ timeout: 10_000 });
  await firstEvent.click();

  // Tarih Düzenleme paneli: düzenlenebilir etkinlikte "Başlık" inputu, tatilde uyarı
  await expect(page.getByText('Tarih Düzenleme')).toBeVisible();
  const editableTitle = page.getByText('Başlık', { exact: true });
  const holidayWarn = page.getByText(/tarih duzenleme ile degistirilemez/i);
  await expect(editableTitle.or(holidayWarn).first()).toBeVisible({ timeout: 10_000 });
});

// ────────────────────────────────────────────────────────────────
// TEST 5 — Taslak geçmişi (DraftHistoryDialog bileşeni) açılır ve taslak listeler
// ────────────────────────────────────────────────────────────────
test('Test 5 — Taslak geçmişi dialog\'u açılır ve üretilen taslağı gösterir', async ({ page }) => {
  await page.goto(ROUTE);
  // Önce bir taslak üret (liste boş olmasın)
  await page.getByRole('button', { name: 'Taslak Üret' }).click();
  await expect(page.getByRole('heading', { name: 'Taslak üretilecek' })).toBeVisible({ timeout: 10_000 });
  await page.getByRole('button', { name: 'Evet, üret' }).click();
  await expect(page.getByRole('heading', { name: /Taslak #\d+/ })).toBeVisible({ timeout: 30_000 });

  await page.getByRole('button', { name: 'Taslak geçmişi' }).click();

  await expect(page.getByRole('heading', { name: 'Taslak geçmişi' })).toBeVisible({ timeout: 10_000 });
  // En az bir taslak satırı (Aç + Sil butonları)
  await expect(page.getByRole('button', { name: 'Aç' }).first()).toBeVisible({ timeout: 10_000 });
});
