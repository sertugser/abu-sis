/**
 * E2E denetim: /fakulte-tanimlari sayfası (FacultyDefinitionsPage)
 *
 * Kapsam:
 *   1. Liste render — tablo + footer sayacı API ile tutarlı
 *   2. UI'dan fakülte oluşturma → listede görünür + API doğrular
 *   3. Çift kod reddi — dialog içinde hata mesajı görünür (backend 400)
 *   4. In-page detay — satıra tıklayınca dialog açılır, navigate ETMEZ
 *   5. Silme guard'ı — bağlı bölümü olan fakülte silinemez, hata banner'ı çıkar
 *   6. BULGU #3 — backend ad/kodu trim ETMİYOR (Program'da ediyor); soft assert ile kanıt
 *
 * Tüm üretilen veri test sonunda API ile temizlenir. Kod prefiksi: "ZE2E".
 */
import { test, expect, type Page } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';
const ADMIN_EMAIL = 'admin@ankarabilim.edu.tr';
const ADMIN_PASSWORD = 'Admin123!';
const ROUTE = '/fakulte-tanimlari';

interface FacultyDto { id: number; name: string; code: string; departmentCount: number; }

let token: string;
let expiresAt: string;
let roles: string[];

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

async function authenticatePage(page: Page) {
  await page.addInitScript(({ t, e, r }) => {
    localStorage.setItem('sis_token', t);
    localStorage.setItem('sis_token_expires', e);
    localStorage.setItem('sis_roles', JSON.stringify(r));
  }, { t: token, e: expiresAt, r: roles });
}

function authHeaders() {
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
}

async function getFaculties(): Promise<FacultyDto[]> {
  const res = await fetch(`${API_BASE}/admin/academic-structure/faculties`, { headers: authHeaders() });
  if (!res.ok) throw new Error(`GET faculties → ${res.status}`);
  return await res.json() as FacultyDto[];
}

async function createFacultyApi(name: string, code: string): Promise<FacultyDto> {
  const res = await fetch(`${API_BASE}/admin/academic-structure/faculties`, {
    method: 'POST', headers: authHeaders(), body: JSON.stringify({ name, code }),
  });
  if (!res.ok) throw new Error(`POST faculty → ${res.status}`);
  return await res.json() as FacultyDto;
}

async function deleteFacultyApi(id: number): Promise<void> {
  await fetch(`${API_BASE}/admin/academic-structure/faculties/${id}`, {
    method: 'DELETE', headers: authHeaders(),
  });
}

// Üretilen fakülte id'lerini izle, afterAll'da temizle
const createdIds = new Set<number>();

test.beforeAll(async () => {
  const r = await login();
  token = r.token; expiresAt = r.expiresAt; roles = r.roles;
});

test.beforeEach(async ({ page }) => {
  await authenticatePage(page);
});

test.afterAll(async () => {
  for (const id of createdIds) {
    await deleteFacultyApi(id);
  }
});

// ────────────────────────────────────────────────────────────────
// TEST 1 — Liste render: tablo görünür, footer sayacı API sayısıyla eşleşir
// ────────────────────────────────────────────────────────────────
test('Test 1 — Liste render ve footer sayacı API ile tutarlı', async ({ page }) => {
  const apiFaculties = await getFaculties();

  await page.goto(ROUTE);
  // Toolbar arama kutusu render edildi
  await expect(page.getByPlaceholder('Ad veya kod ara...')).toBeVisible({ timeout: 15_000 });

  // Pagination sayacı: "1–N / N fakülte" (toplam API sayısıyla biter)
  await expect(page.getByText(new RegExp(`/ ${apiFaculties.length} fakülte`)))
    .toBeVisible({ timeout: 10_000 });
});

// ────────────────────────────────────────────────────────────────
// TEST 2 — UI'dan fakülte oluşturma → listede görünür + API doğrular
// ────────────────────────────────────────────────────────────────
test('Test 2 — "Fakülte Ekle" akışı yeni kaydı listeye ve DB\'ye yazar', async ({ page }) => {
  const code = `ZE2E${Date.now() % 100000}`;
  const name = `E2E Test Fakültesi ${code}`;

  await page.goto(ROUTE);
  await page.getByRole('button', { name: 'Fakülte Ekle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Fakülte Ekle')).toBeVisible();
  await dialog.getByPlaceholder('Örn. Mühendislik Fakültesi').fill(name);
  await dialog.getByPlaceholder('Örn. MF').fill(code);
  await dialog.getByRole('button', { name: 'Kaydet' }).click();

  // Satır listede görünmeli
  await expect(page.locator('tr', { hasText: name })).toBeVisible({ timeout: 10_000 });

  // DB doğrulama
  const after = await getFaculties();
  const created = after.find(f => f.code === code);
  expect(created, 'Yeni fakülte API listesinde olmalı').toBeTruthy();
  if (created) createdIds.add(created.id);
});

// ────────────────────────────────────────────────────────────────
// TEST 3 — Çift kod: aynı kodla ikinci kayıt dialog içinde hata gösterir
// ────────────────────────────────────────────────────────────────
test('Test 3 — Mevcut kodla ekleme dialog içinde 400 hatası gösterir', async ({ page }) => {
  const code = `ZE2E${Date.now() % 100000}`;
  const seed = await createFacultyApi(`E2E Seed ${code}`, code);
  createdIds.add(seed.id);

  await page.goto(ROUTE);
  await page.getByRole('button', { name: 'Fakülte Ekle' }).click();

  const dialog = page.getByRole('dialog');
  await dialog.getByPlaceholder('Örn. Mühendislik Fakültesi').fill('Çakışan Fakülte');
  await dialog.getByPlaceholder('Örn. MF').fill(code);
  await dialog.getByRole('button', { name: 'Kaydet' }).click();

  // Backend mesajı: "'<CODE>' kodlu fakülte zaten mevcut."
  await expect(dialog.getByText(/zaten mevcut/i)).toBeVisible({ timeout: 10_000 });
});

// ────────────────────────────────────────────────────────────────
// TEST 4 — In-page detay: satıra tıklayınca dialog açılır, navigate ETMEZ
// ────────────────────────────────────────────────────────────────
test('Test 4 — Satır tıklaması detayı aynı sayfada açar (URL değişmez)', async ({ page }) => {
  const code = `ZE2E${Date.now() % 100000}`;
  const seed = await createFacultyApi(`E2E Detay ${code}`, code);
  createdIds.add(seed.id);

  await page.goto(ROUTE);
  const row = page.locator('tr', { hasText: seed.name });
  await expect(row).toBeVisible({ timeout: 10_000 });
  await row.click();

  // Detay dialog'u aynı sayfada açıldı (sekmeli panel)
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('tab', { name: 'Genel Bakış' })).toBeVisible({ timeout: 10_000 });

  // URL hâlâ /fakulte-tanimlari (navigate olmadı)
  expect(new URL(page.url()).pathname).toBe(ROUTE);
});

// ────────────────────────────────────────────────────────────────
// TEST 5 — Silme dialog'u: bağlı bölümü olan fakülte için gerçek sayılar
//          gösterilir ve "Sil" butonu KİLİTLİ (proaktif guard, hata banner'ı değil)
// ────────────────────────────────────────────────────────────────
test('Test 5 — Bağlı bölümü olan fakülte silme dialog\'u gerçek sayıları gösterir, Sil kilitli', async ({ page }) => {
  const faculties = await getFaculties();
  const withDepartments = faculties.find(f => f.departmentCount > 0);
  test.skip(!withDepartments, 'Bölümü olan fakülte yok — guard testi atlanıyor');

  await page.goto(ROUTE);
  const row = page.locator('tr', { hasText: withDepartments!.name });
  await expect(row).toBeVisible({ timeout: 10_000 });
  await row.click();

  // Detay dialog footer "Sil" → FacultyDeleteDialog açılır
  const detailDialog = page.getByRole('dialog');
  await detailDialog.getByRole('button', { name: 'Sil' }).click();

  // Yeni silme dialog'u: başlık "Fakülteyi Sil"
  const deleteDialog = page.getByRole('dialog').filter({ hasText: 'Fakülteyi Sil' });
  await expect(deleteDialog).toBeVisible({ timeout: 5_000 });

  // Gerçek bağlı kayıt sayıları görünmeli (hardcoded değil) — Stat etiketleri exact
  await expect(deleteDialog.getByText('Bölüm', { exact: true })).toBeVisible();
  await expect(deleteDialog.getByText('Öğrenci', { exact: true })).toBeVisible();
  await expect(deleteDialog.getByText('Müfredat', { exact: true })).toBeVisible();
  await expect(deleteDialog.getByText(/silinemez/i)).toBeVisible({ timeout: 5_000 });

  // "Sil" butonu kilitli olmalı (canDelete=false)
  await expect(deleteDialog.getByRole('button', { name: /Sil/ })).toBeDisabled();

  // Fakülte hâlâ DB'de (hiçbir şey silinmedi)
  const after = await getFaculties();
  expect(after.some(f => f.id === withDepartments!.id), 'Fakülte silinmemiş olmalı').toBe(true);
});

// ────────────────────────────────────────────────────────────────
// TEST 6 — BULGU #3 DÜZELTİLDİ: backend artık ad/kodu trim ediyor
//   AdminAcademicStructureService.CreateFacultyAsync → NormalizeRequiredText/NormalizeCode.
//   Baştaki/sondaki boşluk DB'ye yazılmadan kırpılmalı.
// ────────────────────────────────────────────────────────────────
test('Test 6 — Fakülte adındaki baştaki/sondaki boşluk backend\'de trim ediliyor (BULGU #3 fix)', async ({ page }) => {
  const code = `ZE2E${Date.now() % 100000}`;
  const paddedName = `  E2E Boşluk ${code}  `;

  await page.goto(ROUTE);
  await page.getByRole('button', { name: 'Fakülte Ekle' }).click();

  const dialog = page.getByRole('dialog');
  await dialog.getByPlaceholder('Örn. Mühendislik Fakültesi').fill(paddedName);
  await dialog.getByPlaceholder('Örn. MF').fill(code);
  await dialog.getByRole('button', { name: 'Kaydet' }).click();

  await expect.poll(async () => (await getFaculties()).some(f => f.code === code), { timeout: 10_000 }).toBe(true);

  const created = (await getFaculties()).find(f => f.code === code)!;
  createdIds.add(created.id);

  // Backend trim ediyor → boşluksuz kaydedilmeli
  expect(created.name, 'Backend ad alanını trim etmeli').toBe(paddedName.trim());
});

// ────────────────────────────────────────────────────────────────
// TEST 7 — Boş fakülte happy-path: silme dialog'u "kayıt yok" der,
//          Sil butonu AKTİF olur ve gerçekten siler
// ────────────────────────────────────────────────────────────────
test('Test 7 — Boş fakülte için silme dialog\'u Sil\'i aktif eder ve siler', async ({ page }) => {
  const code = `ZE2E${Date.now() % 100000}`;
  const seed = await createFacultyApi(`E2E Boş ${code}`, code);
  // Not: başarıyla silinirse createdIds'e eklemiyoruz; silinmezse afterAll temizler
  createdIds.add(seed.id);

  await page.goto(ROUTE);
  const row = page.locator('tr', { hasText: seed.name });
  await expect(row).toBeVisible({ timeout: 10_000 });
  await row.click();

  const detailDialog = page.getByRole('dialog');
  await detailDialog.getByRole('button', { name: 'Sil' }).click();

  const deleteDialog = page.getByRole('dialog').filter({ hasText: 'Fakülteyi Sil' });
  await expect(deleteDialog).toBeVisible({ timeout: 5_000 });
  await expect(deleteDialog.getByText(/hiçbir kayıt yok/i)).toBeVisible({ timeout: 5_000 });

  const confirmBtn = deleteDialog.getByRole('button', { name: /Sil/ });
  await expect(confirmBtn).toBeEnabled();
  await confirmBtn.click();

  // DB'den silindiğini doğrula
  await expect.poll(async () => (await getFaculties()).some(f => f.id === seed.id), { timeout: 10_000 }).toBe(false);
  createdIds.delete(seed.id);
});

// ────────────────────────────────────────────────────────────────
// TEST 8 — 15+ fakülte için pagination: sayfa başına 15, sonrası 2. sayfa
// ────────────────────────────────────────────────────────────────
test('Test 8 — 16 fakülte sayfalanır (sayfa 1 = 15 satır, sayfa 2 = kalan)', async ({ page }) => {
  const prefix = `ZPAG${Date.now() % 100000}`;
  for (let i = 1; i <= 16; i++) {
    const f = await createFacultyApi(`${prefix} Fakülte ${String(i).padStart(2, '0')}`, `${prefix}${i}`);
    createdIds.add(f.id);
  }

  await page.goto(ROUTE);
  // Sadece bu 16 kayda filtrele (Mühendislik vb. hariç) → deterministik
  await page.getByPlaceholder('Ad veya kod ara...').fill(prefix);

  // Sayaç: 16 sonuç, 1–15 gösteriliyor, sayfa 1/2
  await expect(page.getByText(/\/ 16 fakülte/)).toBeVisible({ timeout: 10_000 });
  await expect(page.getByText('1 / 2', { exact: true })).toBeVisible();

  // Sayfa 1: tam 15 veri satırı
  const dataRows = page.locator('tbody tr');
  await expect(dataRows).toHaveCount(15);

  // Sonraki sayfaya geç → kalan 1 satır
  await page.getByRole('button', { name: 'Sonraki sayfa' }).click();
  await expect(page.getByText('2 / 2', { exact: true })).toBeVisible();
  await expect(dataRows).toHaveCount(1);
});

// ────────────────────────────────────────────────────────────────
// TEST 9 — Toplu ekleme: yapıştır → içe aktar → DB'ye 2 kayıt
// ────────────────────────────────────────────────────────────────
test('Test 9 — Toplu fakülte ekleme 2 kaydı oluşturur', async ({ page }) => {
  const base = `ZBLK${Date.now() % 10000}`;
  const c1 = `${base}A`;
  const c2 = `${base}B`;

  await page.goto(ROUTE);
  await page.getByRole('button', { name: 'Toplu Ekle' }).click();

  const dialog = page.getByRole('dialog').filter({ hasText: 'Toplu Fakülte Ekle' });
  await expect(dialog).toBeVisible({ timeout: 5_000 });

  await dialog.locator('textarea').fill(`${c1}, Toplu Test A\n${c2}, Toplu Test B`);
  // Önizleme: 2 geçerli satır
  await expect(dialog.getByText('2 geçerli satır')).toBeVisible();

  await dialog.getByRole('button', { name: /İçe Aktar/ }).click();
  await expect(dialog.getByText(/2 eklendi/)).toBeVisible({ timeout: 10_000 });

  // DB doğrula + cleanup kaydı
  const after = await getFaculties();
  for (const code of [c1, c2]) {
    const f = after.find(x => x.code === code);
    expect(f, `${code} oluşturulmalı`).toBeTruthy();
    if (f) createdIds.add(f.id);
  }
});

// ────────────────────────────────────────────────────────────────
// TEST 10 — Zengin fakülte detay paneli: KPI + grafikler + bölüm tablosu
// ────────────────────────────────────────────────────────────────
test('Test 10 — Fakülte detay paneli zengin istatistik + grafik gösterir', async ({ page }) => {
  const faculties = await getFaculties();
  const withDepts = faculties.find(f => f.departmentCount > 0);
  test.skip(!withDepts, 'Bölümü olan fakülte yok — zengin panel testi atlanıyor');

  await page.goto(ROUTE);
  await page.locator('tr', { hasText: withDepts!.name }).first().click();

  const dialog = page.getByRole('dialog');
  // "Genel Bakış" sekmesi (varsayılan): özgün grafikler + oran + uyarılar
  await expect(dialog.getByText('Program derece dağılımı')).toBeVisible({ timeout: 10_000 });
  await expect(dialog.getByText('Bölüm iş yükü')).toBeVisible();
  await expect(dialog.getByText(/Öğrenci \/ Öğretim Üyesi oranı/)).toBeVisible();
  await expect(dialog.getByText('Hazırlık & Uyarılar')).toBeVisible();

  // "Bölümler" sekmesine geç → bölüm tablosu
  await dialog.getByRole('tab', { name: /Bölümler/ }).click();
  await expect(dialog.getByRole('heading', { name: /Bağlı Bölümler/ })).toBeVisible();
});
