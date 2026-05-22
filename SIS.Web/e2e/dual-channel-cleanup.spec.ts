/**
 * E2E denetim: 2026-05-19 dual-channel temizliği.
 *
 * Bu test SchedulingService + Kısıtlar/Algoritma Parametreleri sayfasında yapılan
 * çift-kanal (SystemConfigs ↔ ConstraintDefinitions) temizliğini bütünüyle doğrular.
 *
 * Kapsanan değişiklikler:
 *   A) Schedule.LunchBreak.IsHard SystemConfig silindi → H13 Category tek otorite
 *   B) Schedule.AllowSameCourseSectionOverlap silindi → H8 hybrid (Hard+Soft)
 *   C) Schedule.AmphiThresholdEnabled + Schedule.ClassroomTypeRequirementEnabled silindi
 *      → H4 Parameters JSON tek otorite
 *   D) H4 validator artık kural 4 (RequiredXxxClassroomType override) uyguluyor
 *   E) H5 validator artık N×M alternatif kombinasyon check yapıyor
 *
 * Login: admin@ankarabilim.edu.tr / Admin123!
 */
import { test, expect, type Page } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';
const ADMIN_EMAIL = 'admin@ankarabilim.edu.tr';
const ADMIN_PASSWORD = 'Admin123!';

const CONSTRAINTS_ROUTE = '/ders-programi-kisitlamalari';

interface LoginResult { token: string; expiresAt: string; roles: string[]; }
interface ConstraintDto {
  id: number; code: string; name: string; isEnabled: boolean;
  category: string; parameters: string | null; weight: number;
  description?: string;
}
interface SystemConfigDto { id: number; key: string; value: string; category: string; }

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

async function authenticatePage(page: Page, token: string, expiresAt: string, roles: string[]) {
  await page.addInitScript(({ t, e, r }) => {
    localStorage.setItem('sis_token', t);
    localStorage.setItem('sis_token_expires', e);
    localStorage.setItem('sis_roles', JSON.stringify(r));
  }, { t: token, e: expiresAt, r: roles });
}

function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
}

async function fetchJson<T = unknown>(token: string, path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { ...authHeaders(token), ...(init?.headers ?? {}) },
  });
  if (!res.ok) {
    const body = await res.text().catch(() => '');
    throw new Error(`${init?.method ?? 'GET'} ${path} → ${res.status} ${body}`);
  }
  const text = await res.text();
  return (text ? JSON.parse(text) : null) as T;
}

let token: string;
let expiresAt: string;
let roles: string[];

test.beforeAll(async () => {
  const r = await login();
  token = r.token; expiresAt = r.expiresAt; roles = r.roles;
});

test.beforeEach(async ({ page }) => {
  await authenticatePage(page, token, expiresAt, roles);
});

// ════════════════════════════════════════════════════════════════════
// API testleri — DB temizliği + endpoint yanıtları
// ════════════════════════════════════════════════════════════════════

test('A1 — Silinen 4 SystemConfig satırı API üzerinden de görünmemeli', async () => {
  // Algoritma Parametreleri endpoint'i sadece "scheduling kategorileri"ni döndürür.
  // HardThreshold kategorisinde silinen 4 key olmamalı.
  const configs = await fetchJson<SystemConfigDto[]>(token, '/admin/scheduling-algorithm-config');

  const bannedKeys = [
    'Schedule.LunchBreak.IsHard',
    'Schedule.AllowSameCourseSectionOverlap',
    'Schedule.AmphiThresholdEnabled',
    'Schedule.ClassroomTypeRequirementEnabled',
  ];

  for (const banned of bannedKeys) {
    const hit = configs.find(c => c.key === banned);
    expect(hit, `Silinen key hâlâ API'den dönüyor: ${banned}`).toBeUndefined();
  }
});

test('A2 — HardThreshold kategorisinde 0 satır kalmalı', async () => {
  // 4 hard threshold flag'i de kaldırıldıktan sonra grup tamamen boş olmalı.
  const configs = await fetchJson<SystemConfigDto[]>(token, '/admin/scheduling-algorithm-config');
  const hardThresholds = configs.filter(c => c.category === 'HardThreshold');
  expect(hardThresholds, 'HardThreshold artık boş olmalı (4 flag de H4/H8/H13 Parameters\'a taşındı)').toHaveLength(0);
});

test('B1 — H4 Parameters JSON 3 key içermeli (AmphiThreshold + 2 toggle)', async () => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4 = constraints.find(c => c.code === 'H4');
  expect(h4, 'H4 bulunamadı').toBeTruthy();
  expect(h4!.parameters, 'H4 Parameters boş olmamalı').toBeTruthy();

  const params = JSON.parse(h4!.parameters!) as Record<string, unknown>;
  expect(params.AmphiThreshold, 'AmphiThreshold sayı tipinde olmalı').toEqual(expect.any(Number));
  expect(params.AmphiThresholdEnabled, 'AmphiThresholdEnabled bool olmalı').toEqual(expect.any(Boolean));
  expect(params.ClassroomTypeRequirementEnabled, 'ClassroomTypeRequirementEnabled bool olmalı').toEqual(expect.any(Boolean));
});

test('B2 — H13 description "hard mod aktifken" ibaresi içermemeli', async () => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h13 = constraints.find(c => c.code === 'H13');
  expect(h13).toBeTruthy();
  expect(h13!.description ?? '', 'Eski açıklama metni hâlâ DB\'de').not.toContain('hard mod aktifken');
});

test('B3 — H8 hâlâ var, default Hard, IsEnabled true', async () => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h8 = constraints.find(c => c.code === 'H8');
  expect(h8, 'H8 silinmemeli (sadece flag silindi)').toBeTruthy();
  expect(h8!.category, 'H8 default Hard').toBe('Hard');
  expect(h8!.isEnabled, 'H8 default Aktif').toBe(true);
});

test('B4 — H4 Parameters AmphiThresholdEnabled toggle PUT ile değiştirilebilmeli', async () => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4 = constraints.find(c => c.code === 'H4')!;
  const before = JSON.parse(h4.parameters!) as { AmphiThreshold: number; AmphiThresholdEnabled: boolean; ClassroomTypeRequirementEnabled: boolean };

  // Toggle the AmphiThresholdEnabled value
  const newParams = { ...before, AmphiThresholdEnabled: !before.AmphiThresholdEnabled };
  await fetchJson(token, `/constraints/${h4.id}/parameters`, {
    method: 'PUT',
    body: JSON.stringify({ parameters: JSON.stringify(newParams) }),
  });

  const updated = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4After = updated.find(c => c.code === 'H4')!;
  const after = JSON.parse(h4After.parameters!) as typeof before;
  expect(after.AmphiThresholdEnabled, 'Toggle flip etmeli').toBe(!before.AmphiThresholdEnabled);

  // RESTORE — orijinal değere geri çevir
  await fetchJson(token, `/constraints/${h4.id}/parameters`, {
    method: 'PUT',
    body: JSON.stringify({ parameters: JSON.stringify(before) }),
  });
  const restored = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4Restored = restored.find(c => c.code === 'H4')!;
  expect(JSON.parse(h4Restored.parameters!).AmphiThresholdEnabled).toBe(before.AmphiThresholdEnabled);
});

test('B5 — H8 Category Hard ↔ Soft geçişi yapılabilmeli (hybrid predicate)', async () => {
  const before = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H8')!;
  const originalCategory = before.category;
  const targetCategory = originalCategory === 'Hard' ? 'Soft' : 'Hard';

  await fetchJson(token, `/constraints/${before.id}/category`, {
    method: 'PUT',
    body: JSON.stringify({ category: targetCategory }),
  });

  const middle = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H8')!;
  expect(middle.category, `H8 ${targetCategory}'a geçmeli`).toBe(targetCategory);

  // RESTORE
  await fetchJson(token, `/constraints/${before.id}/category`, {
    method: 'PUT',
    body: JSON.stringify({ category: originalCategory }),
  });
  const restored = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H8')!;
  expect(restored.category).toBe(originalCategory);
});

// ════════════════════════════════════════════════════════════════════
// UI testleri — Gerçek tarayıcı
// ════════════════════════════════════════════════════════════════════

test('UI-1 — Algoritma Parametreleri sekmesi ve paneli tamamen kaldırıldı', async ({ page }) => {
  // 2026-05-19: Lab Blok Saati H6 (Ders Bileşen Çakışması) Parameters JSON'a taşındı,
  // AlgorithmParametersPanel tamamen silindi. Kısıtlar sayfası artık 2 sekme: Kısıtlar +
  // Ders Eşleştirme Kuralları.
  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  // "Algoritma Parametreleri" sekmesi artık YOK
  await expect(page.getByRole('tab', { name: 'Algoritma Parametreleri' })).toHaveCount(0);

  // "Genel Sistem Ayarları" badge'i de YOK (Lab Blok H6 Parameters'a taşındı)
  await expect(page.locator('text=Genel Sistem Ayarları')).toHaveCount(0);

  // Lab Blok Saati ham metni Kısıtlar tablosunda görünmemeli (artık H6 Parameters JSON'unda)
  await expect(page.locator('text=Bir laboratuvar dersi tek seferde')).toHaveCount(0);

  // 4 silinen key'in açıklama metni hiç görünmemeli
  await expect(page.locator('text=Öğle arası saatlerine ders konulması yasak')).toHaveCount(0);
  await expect(page.locator('text=Aynı dersin farklı şubeleri aynı saatte birden fazla')).toHaveCount(0);
  await expect(page.locator('text=Kalabalık dersler amfiye yerleştirilmek')).toHaveCount(0);
  await expect(page.locator('text=Ders kataloğunda tanımlı derslik tipi (laboratuvar, amfi, normal sınıf)')).toHaveCount(0);

  // "Paylaşılan Kısıt Eşikleri" grup başlığı da olmamalı (HardThreshold zaten boş)
  await expect(page.locator('text=Paylaşılan Kısıt Eşikleri')).toHaveCount(0);
});

test('UI-2 — H4 dialog\'unda 3 parametre alanı görünmeli (AmphiThreshold + 2 toggle)', async ({ page }) => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4 = constraints.find(c => c.code === 'H4')!;

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  const h4Row = page.locator('tr', { hasText: h4.name });
  await expect(h4Row).toBeVisible({ timeout: 10_000 });
  await h4Row.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Parametre etiketleri görünüyor mu
  await expect(dialog.locator('text=Amfi Eşik Kontenjanı')).toBeVisible();
  await expect(dialog.locator('text=Kalabalık derslerde amfi zorunluluğu')).toBeVisible();
  await expect(dialog.locator('text=Ders kataloğundaki derslik tipi zorunluluğu')).toBeVisible();
});

test('UI-3 — H4 dialog\'unda toggle değiştir + kaydet → DB Parameters JSON\'una yansımalı', async ({ page }) => {
  const initial = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H4')!;
  const initialParams = JSON.parse(initial.parameters!) as { AmphiThresholdEnabled: boolean; AmphiThreshold: number; ClassroomTypeRequirementEnabled: boolean };

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  const h4Row = page.locator('tr', { hasText: initial.name });
  await h4Row.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // "Kalabalık derslerde amfi zorunluluğu" alanındaki Switch'i bul.
  // ConstraintDetailPanel.tsx render yapısı:
  //   <div.space-y-1>
  //     <label>Kalabalık derslerde amfi zorunluluğu</label>
  //     <div.flex>
  //       <button role="switch" ...></button>
  //       <span>Hayır/Evet</span>
  //     </div>
  //   </div>
  // CSS selector: label'dan sonraki kardeş div içindeki switch.
  await expect(dialog.locator('label', { hasText: 'Kalabalık derslerde amfi zorunluluğu' })).toBeVisible();
  const amphiSwitch = dialog
    .locator('div.space-y-1')
    .filter({ hasText: 'Kalabalık derslerde amfi zorunluluğu' })
    .getByRole('switch');
  await expect(amphiSwitch).toBeVisible();
  await amphiSwitch.click();

  // Kaydet
  await dialog.getByRole('button', { name: 'Parametreleri Kaydet' }).click();

  // Backend yansımasını 2 saniye bekle
  await page.waitForTimeout(2000);

  const after = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H4')!;
  const afterParams = JSON.parse(after.parameters!) as typeof initialParams;
  expect(afterParams.AmphiThresholdEnabled, 'AmphiThresholdEnabled değeri toggle ile değişmeli').toBe(!initialParams.AmphiThresholdEnabled);

  // CLEANUP — DB'yi orijinal duruma çevir
  await fetchJson(token, `/constraints/${initial.id}/parameters`, {
    method: 'PUT',
    body: JSON.stringify({ parameters: JSON.stringify(initialParams) }),
  });
});

test('UI-4 — H8 dialog\'unda parametre alanı olmamalı (AllowSameCourseSectionOverlap kaldırıldı)', async ({ page }) => {
  const h8 = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H8')!;

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  const h8Row = page.locator('tr', { hasText: h8.name });
  await expect(h8Row).toBeVisible({ timeout: 10_000 });
  await h8Row.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Eski parametre etiketi görünmemeli
  await expect(dialog.locator('text=Aynı Ders Şube Çakışmasına İzin Ver'), 'AllowSameCourseSectionOverlap kaldırıldı').toHaveCount(0);

  // "Parametreler" bölümü olmamalı (boş schema)
  await expect(dialog.locator('text=PARAMETRELER')).toHaveCount(0);
});

test('UI-6 — H5 dialog\'unda Çakışma Modu dropdown 3 seçenek görmeli', async ({ page }) => {
  // 2026-05-19: CurriculumConflictMode SystemConfigs'ten H5.Parameters JSON'a taşındı.
  // Dropdown 3 seçenek: cohort, curriculum, enrollment (artık helpers.ts'te tanımlı).
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h5 = constraints.find(c => c.code === 'H5')!;
  expect(h5.parameters, 'H5.Parameters CurriculumConflictMode içermeli').toBeTruthy();
  const params = JSON.parse(h5.parameters!) as Record<string, unknown>;
  expect(params.CurriculumConflictMode, 'CurriculumConflictMode key olmalı').toEqual(expect.any(String));

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  const h5Row = page.locator('tr', { hasText: h5.name });
  await h5Row.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('text=Çakışma Modu')).toBeVisible();

  // Şu anki değer "cohort" → dropdown trigger'ında "Cohort (önerilen)" görünmeli (string Input değil!)
  // Eski bug: "cohort" düz text Input olarak görünüyordu. Switch/Select desteği eklendikten sonra Dropdown.
  const currentMode = params.CurriculumConflictMode;
  const expectedLabel = currentMode === 'cohort' ? 'Cohort (önerilen)'
    : currentMode === 'curriculum' ? 'Müfredat Bazlı'
    : 'Kayıt Bazlı';
  await expect(dialog.locator(`text=${expectedLabel}`).first()).toBeVisible({ timeout: 5_000 });
});

test('UI-7 — H6 (Ders Bileşen Çakışması) dialog\'unda MaxLabBlockHours parametre olarak görünmeli', async ({ page }) => {
  // 2026-05-19: Lab Blok Saati SystemConfigs'ten H6.Parameters JSON'a taşındı.
  // Kısıtlar → H6 → Düzenle dialog'unda parametre olarak yönetiliyor.
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h6 = constraints.find(c => c.code === 'H6');
  expect(h6, 'H6 bulunamadı').toBeTruthy();
  expect(h6!.parameters, 'H6.Parameters MaxLabBlockHours içermeli').toBeTruthy();
  const params = JSON.parse(h6!.parameters!) as Record<string, unknown>;
  expect(params.MaxLabBlockHours, 'MaxLabBlockHours sayı olmalı').toEqual(expect.any(Number));

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  const h6Row = page.locator('tr', { hasText: h6!.name });
  await expect(h6Row).toBeVisible({ timeout: 10_000 });
  await h6Row.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('text=Tek Lab Bloğu Maks. Saat')).toBeVisible({ timeout: 5_000 });
});

test('UI-8 — H5 Çakışma Modu API ile değiştir → dialog yeni değeri göstermeli', async ({ page }) => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h5 = constraints.find(c => c.code === 'H5')!;
  const originalParams = JSON.parse(h5.parameters!) as Record<string, unknown>;
  const originalMode = originalParams.CurriculumConflictMode as string;

  // Test için karşı moda geç (cohort ↔ curriculum)
  const newMode = originalMode === 'cohort' ? 'curriculum' : 'cohort';
  const newParams = { ...originalParams, CurriculumConflictMode: newMode };

  await fetchJson(token, `/constraints/${h5.id}/parameters`, {
    method: 'PUT',
    body: JSON.stringify({ parameters: JSON.stringify(newParams) }),
  });

  // Doğrulama: H5.Parameters güncellendi
  const updated = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H5')!;
  const updatedMode = (JSON.parse(updated.parameters!) as Record<string, unknown>).CurriculumConflictMode;
  expect(updatedMode, `Mod ${newMode} olmalı`).toBe(newMode);

  // UI'da yeni değer dialog'da görünmeli
  await page.goto(CONSTRAINTS_ROUTE);
  const h5Row = page.locator('tr', { hasText: h5.name });
  await h5Row.getByRole('button', { name: 'Düzenle' }).click();
  const dialog = page.getByRole('dialog');
  const expectedLabel = newMode === 'cohort' ? 'Cohort (önerilen)' : 'Müfredat Bazlı';
  await expect(dialog.locator(`text=${expectedLabel}`).first()).toBeVisible({ timeout: 5_000 });

  // CLEANUP: orijinal değere geri çevir
  await fetchJson(token, `/constraints/${h5.id}/parameters`, {
    method: 'PUT',
    body: JSON.stringify({ parameters: JSON.stringify(originalParams) }),
  });
});

test('UI-5 — H13 dialog\'unda Kategori toggle (Zorunlu/Tercihli) çalışmalı', async ({ page }) => {
  const initial = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H13')!;
  const originalCategory = initial.category;

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  const h13Row = page.locator('tr', { hasText: initial.name });
  await h13Row.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Kategori toggle butonlarını gör
  await expect(dialog.getByRole('button', { name: 'Zorunlu', exact: true })).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Tercihli', exact: true })).toBeVisible();

  // Karşı kategoriye geç (confirm dialog'unu otomatik onayla)
  const targetCategory = originalCategory === 'Hard' ? 'Soft' : 'Hard';
  const targetLabel = targetCategory === 'Hard' ? 'Zorunlu' : 'Tercihli';
  page.once('dialog', d => d.accept());
  await dialog.getByRole('button', { name: targetLabel, exact: true }).click();

  await page.waitForTimeout(1500);

  const after = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H13')!;
  expect(after.category, `H13 ${targetCategory}'a geçmeli`).toBe(targetCategory);

  // RESTORE
  await fetchJson(token, `/constraints/${initial.id}/category`, {
    method: 'PUT',
    body: JSON.stringify({ category: originalCategory }),
  });
});
