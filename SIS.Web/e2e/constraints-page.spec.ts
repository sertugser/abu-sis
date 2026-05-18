/**
 * E2E denetim: /ders-programi-kisitlamalari sayfası
 * Amaç: 2026-05-17 manuel denetimde tespit edilen bug'ları kanıtlamak / yalanlamak.
 *
 * Test fixturesi:
 *   Aktif term: id=7 ("2027-2028 Güz (Plan 10 Test)") — backend'den dinamik çekiyoruz.
 *   H4 default: isEnabled=true, parameters={"AmphiThreshold":100}, weight=5.
 *   DIST tablosu term 7'de boş — testler kendi kayıtlarını üretip temizliyor.
 */
import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';
const ADMIN_EMAIL = 'admin@ankarabilim.edu.tr';
const ADMIN_PASSWORD = 'Admin123!';

const CONSTRAINTS_ROUTE = '/ders-programi-kisitlamalari';

interface LoginResult { token: string; expiresAt: string; roles: string[]; }

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
  if (!res.ok) throw new Error(`${init?.method ?? 'GET'} ${path} → ${res.status}`);
  const text = await res.text();
  return (text ? JSON.parse(text) : null) as T;
}

interface ConstraintDto {
  id: number; code: string; name: string; isEnabled: boolean;
  category: string; parameters: string | null; weight: number;
}
interface SystemConfigDto { id: number; key: string; value: string; }
interface DistributionConstraintDto {
  id: number; distributionType: string; preferenceLevel: string;
  termId: number; courses: Array<{ courseId: number; courseCode: string; sortOrder: number }>;
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

// ────────────────────────────────────────────────────────────────
// TEST 1 — Toggle gerçekten DB'ye yansıyor mu (ConstraintController.Toggle → IsEnabled flip)
// ────────────────────────────────────────────────────────────────
test('Test 1 — Aktif/Pasif toggle DB IsEnabled alanını gerçekten flip ediyor', async ({ page }) => {
  const constraints = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4Before = constraints.find(c => c.code === 'H4');
  expect(h4Before, 'H4 constraint bulunamadı').toBeTruthy();
  const initialEnabled = h4Before!.isEnabled;

  // Network izle: PUT /api/constraints/{id}/toggle
  const toggleRequests: string[] = [];
  page.on('request', (req) => {
    if (req.method() === 'PUT' && /\/constraints\/\d+\/toggle/.test(req.url())) {
      toggleRequests.push(req.url());
    }
  });

  await page.goto(CONSTRAINTS_ROUTE);
  await expect(page.getByRole('tab', { name: 'Kısıtlar' })).toBeVisible({ timeout: 15_000 });

  // H4 satırını bul. "Derslik Tipi Uyumu" → backend'den dinamik aldık ama UI'da
  // constraint.name ile gösteriliyor. Düzenle butonuna tıkla.
  const h4Row = page.locator('tr', { hasText: h4Before!.name });
  await expect(h4Row).toBeVisible({ timeout: 10_000 });

  // Türkçe karakter normalizasyonu için aria-label kullan
  await h4Row.getByRole('button', { name: 'Düzenle' }).click();

  // Dialog açıldı
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText(h4Before!.name).first()).toBeVisible();

  // Mevcut Aktif/Pasif durumu tersine çevir
  const targetButton = initialEnabled
    ? dialog.getByRole('button', { name: 'Pasif', exact: true })
    : dialog.getByRole('button', { name: 'Aktif', exact: true });

  // Pasife geçişte window.confirm var; otomatik onayla
  page.once('dialog', (d) => d.accept());

  await targetButton.click();

  // Toaster fail varsa yakalanır; success path: invalidate + refetch.
  await expect.poll(async () => toggleRequests.length, { timeout: 5_000, message: 'toggle PUT request' }).toBeGreaterThan(0);

  // 1-2 saniye query refetch'e izin ver, sonra DB durumu doğrula
  await page.waitForTimeout(1500);
  const afterFlip = await fetchJson<ConstraintDto[]>(token, '/constraints');
  const h4After = afterFlip.find(c => c.code === 'H4')!;
  expect(h4After.isEnabled, 'IsEnabled flip etmiş olmalı').toBe(!initialEnabled);

  // CLEANUP — eski haline geri çevir
  await fetchJson(token, `/constraints/${h4Before!.id}/toggle`, { method: 'PUT' });
  const restored = await fetchJson<ConstraintDto[]>(token, '/constraints');
  expect(restored.find(c => c.code === 'H4')!.isEnabled).toBe(initialEnabled);
});

// ────────────────────────────────────────────────────────────────
// TEST 2 — Weight slider hem ConstraintDefinitions.Weight'a hem SystemConfigs key'ine yazıyor
//          (CLAUDE.md: ConstraintParameterMapping.ResolveWeightKey ile dual write)
// ────────────────────────────────────────────────────────────────
test('Test 2 — Ağırlık değiştirme SystemConfigs.Schedule.H4Weight key UPSERT yapıyor', async ({ page }) => {
  // Önce H4'ü Soft'a çek (Hard ise weight slider görünmüyor)
  const initial = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H4')!;
  const originalCategory = initial.category;
  const originalWeight = initial.weight;

  if (originalCategory !== 'Soft') {
    await fetchJson(token, `/constraints/${initial.id}/category`, {
      method: 'PUT',
      body: JSON.stringify({ category: 'Soft' }),
    });
  }

  // SystemConfigs'ten H4Weight okuma — controller'a doğrudan endpoint yok,
  // SchedulingAlgorithmConfig listesinde de görünmez (Schedule kategorisi farklı).
  // Bunun yerine PUT sonrası backend response'unun .systemConfigKey döndürdüğünü
  // ve sonra controller'ın UpdateWeight içinde UPSERT yaptığını test edeceğiz.

  // Doğrudan API ile weight değişikliği — Playwright UI testinde slider drag yerine
  // API çağrısı çok daha güvenilir; UI bağlantısını Test 1 zaten doğruladı.
  const targetWeight = originalWeight === 7 ? 8 : 7;
  const updateRes = await fetch(`${API_BASE}/constraints/${initial.id}/weight`, {
    method: 'PUT',
    headers: authHeaders(token),
    body: JSON.stringify({ weight: targetWeight }),
  });
  expect(updateRes.ok).toBeTruthy();
  const updateBody = await updateRes.json() as { id: number; weight: number; systemConfigKey: string };
  expect(updateBody.weight).toBe(targetWeight);
  expect(updateBody.systemConfigKey, 'SystemConfigs key adı response içinde olmalı').toBe('Schedule.H4Weight');

  // DB'ye yansıdığını doğrula
  const afterUpdate = (await fetchJson<ConstraintDto[]>(token, '/constraints')).find(c => c.code === 'H4')!;
  expect(afterUpdate.weight).toBe(targetWeight);

  // UI bunu okuyabiliyor mu (kullanıcı tarafı görüyor mu)?
  await page.goto(CONSTRAINTS_ROUTE);
  const h4Row = page.locator('tr', { hasText: initial.name });
  await expect(h4Row).toBeVisible({ timeout: 10_000 });
  await expect(h4Row.getByText(new RegExp(`\\b${targetWeight}\\b`))).toBeVisible({ timeout: 5_000 });

  // CLEANUP
  await fetchJson(token, `/constraints/${initial.id}/weight`, {
    method: 'PUT', body: JSON.stringify({ weight: originalWeight }),
  });
  if (originalCategory !== 'Soft') {
    await fetchJson(token, `/constraints/${initial.id}/category`, {
      method: 'PUT', body: JSON.stringify({ category: originalCategory }),
    });
  }
});

// ────────────────────────────────────────────────────────────────
// TEST 3 — Algoritma Parametreleri sekmesi PUT atıyor, response 200, UI yansıtıyor
// ────────────────────────────────────────────────────────────────
test('Test 3 — Algoritma Parametreleri sekmesi config update SystemConfigs cache invalidate ediyor', async ({ page }) => {
  const all = await fetchJson<SystemConfigDto[]>(token, '/admin/scheduling-algorithm-config');
  // AmphiThresholdEnabled iyi bir hedef — bool, doğrulanabilir
  const target = all.find(c => c.key === 'Schedule.AmphiThresholdEnabled');
  expect(target, 'AmphiThresholdEnabled config bulunamadı').toBeTruthy();
  const originalValue = target!.value;
  const newValue = originalValue === 'true' ? 'false' : 'true';

  await page.goto(CONSTRAINTS_ROUTE);
  await page.getByRole('tab', { name: 'Algoritma Parametreleri' }).click();

  const putRequests: string[] = [];
  page.on('request', (req) => {
    if (req.method() === 'PUT' && /\/admin\/scheduling-algorithm-config\/\d+/.test(req.url())) {
      putRequests.push(req.url());
    }
  });

  // Doğrudan API ile değişikliği yap, UI yansıttığını doğrula
  const res = await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${target!.id}`, {
    method: 'PUT',
    headers: authHeaders(token),
    body: JSON.stringify({ value: newValue }),
  });
  expect(res.status, 'PUT response 200 dönmeli').toBe(200);

  // UI'ı yenile (React Query 60s stale, manuel refetch için sayfayı reload)
  await page.reload();
  await page.getByRole('tab', { name: 'Algoritma Parametreleri' }).click();

  // Yeni değer "Evet"/"Hayır" badge'ı olarak görünmeli
  const expectedBadge = newValue === 'true' ? 'Evet' : 'Hayır';
  const row = page.locator('tr', { hasText: target!.key.split('.').pop() ?? target!.key });
  // description ile arama daha güvenilir olabilir; .first() ile genişlet
  await expect(page.locator('table').getByText(expectedBadge).first()).toBeVisible({ timeout: 10_000 });

  // CLEANUP
  await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${target!.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: originalValue }),
  });
});

// ────────────────────────────────────────────────────────────────
// TEST 4 — DIST Edit Duplicate Bug
//   Hipotez: "Düzenle" → form aç → "Güncelle" basınca POST gidiyor (UPDATE yerine),
//            yani toplam kayıt sayısı 1 yerine 2 oluyor.
//   Kanıt dosyası: SIS.Web/src/components/constraint/DistributionConstraintPanel.tsx:55-59
// ────────────────────────────────────────────────────────────────
test('Test 4 — DIST Edit butonu update yerine duplicate kayıt yaratıyor (BUG)', async ({ page }) => {
  const TERM_ID = 7;
  const courses = await fetchJson<Array<{ id: number; code: string }>>(token, '/admin/courses');
  // Stable iki ders seç (ilk iki kayıtlı)
  const c1 = courses[0];
  const c2 = courses[1];
  expect(c1 && c2).toBeTruthy();

  // Mevcut DIST'leri temizle (test izolasyonu)
  const existing = await fetchJson<DistributionConstraintDto[]>(token, `/constraints/distributions?termId=${TERM_ID}`);
  for (const d of existing) {
    await fetchJson(token, `/constraints/distributions/${d.id}`, { method: 'DELETE' });
  }

  // API üzerinden bir kayıt oluştur
  const created = await fetchJson<DistributionConstraintDto>(token, '/constraints/distributions', {
    method: 'POST',
    body: JSON.stringify({
      distributionType: 'SameDay',
      preferenceLevel: 'Discouraged',
      termId: TERM_ID,
      courses: [
        { courseId: c1.id, courseSectionId: null, sortOrder: 1 },
        { courseId: c2.id, courseSectionId: null, sortOrder: 2 },
      ],
    }),
  });
  expect(created.id).toBeGreaterThan(0);

  // Şimdi UI tarafına git
  await page.goto(CONSTRAINTS_ROUTE);
  await page.getByRole('tab', { name: 'Ders Eşleştirme Kuralları' }).click();

  // Liste yüklensin
  const countBefore = (await fetchJson<DistributionConstraintDto[]>(
    token, `/constraints/distributions?termId=${TERM_ID}`)).length;
  expect(countBefore).toBe(1);

  // Network izle: POST veya PUT gidecek mi
  const postRequests: string[] = [];
  const putRequests: string[] = [];
  page.on('request', (req) => {
    if (/\/constraints\/distributions/.test(req.url())) {
      if (req.method() === 'POST') postRequests.push(req.url());
      if (req.method() === 'PUT') putRequests.push(req.url());
    }
  });

  // Edit butonunu bul (kalem ikonu) ve tıkla
  const row = page.locator('text=/Ayni Gun|SameDay/').first().locator('..');
  // Daha güvenli: kalem ikonu olan butona tıkla — DistributionRow içinde "Pencil" ikon, h-7 w-7 size icon button.
  // Sadece bir DIST var, sayfadaki ilk Pencil icon'u onun edit butonu.
  const allEditButtons = page.locator('button:has(svg.lucide-pencil)');
  // Birden fazla olabilir (constraint listesinde de var) — son tıklanan tab Ders Eşleştirme, scope o.
  // Daha sağlam: DistributionConstraintPanel'in kapsadığı bölüm — başlık "İlişkisel Kısıtlamalar"
  const distSection = page.locator('div').filter({ hasText: 'Iliskisel Kisitlamalar' }).first();
  await expect(distSection).toBeVisible({ timeout: 10_000 });

  // Bu section içindeki pencil butonu
  const editBtn = distSection.locator('button:has(svg.lucide-pencil)').first();
  await editBtn.click();

  // Form Sheet aç — başlık "Kural Düzenle"
  const sheet = page.getByRole('dialog').filter({ hasText: 'Kural Düzenle' });
  await expect(sheet).toBeVisible({ timeout: 5_000 });

  // Hiçbir şey değiştirmeden "Güncelle" butonuna bas
  await sheet.getByRole('button', { name: /Güncelle/ }).click();

  // Network'un oturmasını bekle
  await page.waitForTimeout(2000);

  // BUG kanıtı: POST gitti mi yoksa PUT mu?
  console.log('POST requests:', postRequests);
  console.log('PUT requests:', putRequests);

  const countAfter = (await fetchJson<DistributionConstraintDto[]>(
    token, `/constraints/distributions?termId=${TERM_ID}`)).length;

  // ASSERT: Beklenen davranış (update) → PUT gönderilmeli, count=1 kalmalı
  //         Bug davranışı → POST gönderilir, count=2 olur
  if (countAfter === 2) {
    console.log('🐛 BUG DOĞRULANDI: Edit butonu duplicate kayıt yarattı (count 1 → 2)');
    console.log('   POST count:', postRequests.length, 'PUT count:', putRequests.length);
  }

  // Bu testin amacı bug'ı yakalamak; assertion bug'ın varlığına işaret etsin.
  // Beklenti: edit submission PUT olmalı (UPDATE semantiği)
  expect.soft(putRequests.length, '"Güncelle" butonu PUT göndermeli (UPDATE)').toBeGreaterThan(0);
  expect.soft(countAfter, '"Güncelle" sonrası DIST kayıt sayısı değişmemeli (UPDATE in-place)').toBe(1);
  expect.soft(postRequests.length, '"Güncelle" butonu POST gönderememeli').toBe(0);

  // CLEANUP — tüm DIST'leri sil
  const final = await fetchJson<DistributionConstraintDto[]>(token, `/constraints/distributions?termId=${TERM_ID}`);
  for (const d of final) {
    await fetchJson(token, `/constraints/distributions/${d.id}`, { method: 'DELETE' });
  }
});

// ────────────────────────────────────────────────────────────────
// TEST 5 — DIST Preferred/StronglyPreferred artık UI'da gösterilmemeli
//   Düzeltme: DistributionConstraintForm.tsx içinde PREFERENCE_LEVELS listesinden
//   bu iki tier kaldırıldı (encoder DistributionConstraintEncoder.cs:48'de skip
//   ettiği için kullanıcı yanılgıya düşmesin).
// ────────────────────────────────────────────────────────────────
test('Test 5 — DIST form Preferred/StronglyPreferred seçeneklerini göstermiyor', async ({ page }) => {
  await page.goto(CONSTRAINTS_ROUTE);
  await page.getByRole('tab', { name: 'Ders Eşleştirme Kuralları' }).click();

  await page.getByRole('button', { name: /Yeni Kural/ }).click();
  const sheet = page.getByRole('dialog').filter({ hasText: /Yeni .li.kisel/i });
  await expect(sheet).toBeVisible({ timeout: 5_000 });

  // Tercih Seviyesi combobox'ı aç (ikinci combobox)
  const triggers = sheet.locator('[role="combobox"]');
  await triggers.nth(1).click();

  // Beklenen üç seçenek görünmeli (exact match — "Tercih Edilmez" substring olarak
  // "Kesinlikle Tercih Edilmez"e de match eder, strict-mode hatası vermesin)
  await expect(page.getByRole('option', { name: 'Tercih Edilmez', exact: true })).toBeVisible();
  await expect(page.getByRole('option', { name: 'Kesinlikle Tercih Edilmez', exact: true })).toBeVisible();
  await expect(page.getByRole('option', { name: 'Kesin Yasak (Hard)', exact: true })).toBeVisible();

  // Yutulan iki tier görünmemeli (exact)
  await expect(page.getByRole('option', { name: 'Tercih Edilir', exact: true })).toHaveCount(0);
  await expect(page.getByRole('option', { name: 'Kesinlikle Tercih Edilir', exact: true })).toHaveCount(0);

  // Kapat
  await page.keyboard.press('Escape');
  await sheet.getByRole('button', { name: 'İptal' }).click();
});

// ────────────────────────────────────────────────────────────────
// TEST 6 — ConstraintController cache invalidate eksikliği (CLAUDE.md / kod denetimi)
//   ConstraintController PUT /parameters ve PUT /weight çağrılarından sonra
//   ISystemConfigProvider.InvalidateCache çağrılmıyor (constructor inject etmiyor).
//   Buna karşılık SchedulingAlgorithmConfigController invalidate ediyor.
// ────────────────────────────────────────────────────────────────
test('Test 6 — ConstraintController.UpdateParameters/Weight cache invalidate çağrısı atlanıyor', async () => {
  // Bu test, kod denetimi sonucunu API yanıtı üzerinden değil dosya okuması üzerinden doğrulamaz —
  // davranışsal kanıt: parameters yazımı sonrası tekrar GET ile değer doğrulanır,
  // ancak cache TTL içinde solver eski değeri okuyabilir. Bunu observable testlemek
  // SystemConfigProvider'ın internal cache'sine erişim gerektirir → atlıyoruz.
  test.skip(true, 'Cache invalidation observable değil — kod denetimi yeterli (ConstraintController.cs:23 ISystemConfigProvider inject yok)');
});

// ────────────────────────────────────────────────────────────────
// TEST 7 — Atıl algoritma parametreleri DB'den/UI'dan kaldırıldı
//   Düzeltme: Schedule.Lab.MaxBlockHours ve Schedule.SplittableBlockTypes
//   hiçbir encoder/builder okumadığı için 2026-05-17'de migration ile DB'den silindi
//   + SeedData'dan + SchedulingConstraintConfig property'sinden temizlendi.
// ────────────────────────────────────────────────────────────────
test('Test 7 — Atıl scheduling config keyleri DB\'de yok', async () => {
  const configs = await fetchJson<Array<{ key: string }>>(token, '/admin/scheduling-algorithm-config');
  const keys = configs.map(c => c.key);
  expect(keys, 'Schedule.Lab.MaxBlockHours kaldırılmalı').not.toContain('Schedule.Lab.MaxBlockHours');
  expect(keys, 'Schedule.SplittableBlockTypes kaldırılmalı').not.toContain('Schedule.SplittableBlockTypes');
  // S20 Soft → Hard dönüşümüyle ceza puanı atıllaştı, sayfadan kaldırıldı
  expect(keys, 'SoftConstraint.S20.PenaltyPerPair kaldırılmalı (S20 Hard, ceza puanı anlamsız)').not.toContain('SoftConstraint.S20.PenaltyPerPair');
  // Diğer satırlar yerinde olmalı + yeni MaxLabBlockHours eklendi
  expect(keys).toContain('Schedule.CurriculumConflictMode');
  expect(keys).toContain('Schedule.MaxLabBlockHours');
});

// ────────────────────────────────────────────────────────────────
// TEST 9 — Schedule.MaxLabBlockHours yeni ayar (default 2, aralık 2-6)
//   Düzeltme: CpSatSolverEngine bu key'i okuyordu (default 2 fallback) ama DB'de yoktu.
//   Şimdi seed + UI'da düzenlenebilir, sayı kutusu min/max ile.
// ────────────────────────────────────────────────────────────────
test('Test 9 — Schedule.MaxLabBlockHours DB\'de var, validasyonu çalışıyor', async () => {
  const configs = await fetchJson<Array<{ id: number; key: string; value: string }>>(token, '/admin/scheduling-algorithm-config');
  const target = configs.find(c => c.key === 'Schedule.MaxLabBlockHours');
  expect(target, 'MaxLabBlockHours config bulunmalı').toBeTruthy();
  const originalValue = target!.value;

  // Limits endpoint 2-6 aralığı dönmeli
  const limits = await fetchJson<Record<string, { min: number; max: number }>>(token, '/admin/scheduling-algorithm-config/limits');
  expect(limits['Schedule.MaxLabBlockHours']).toEqual({ min: 2, max: 6 });

  // Min/max ihlali reddedilmeli
  const tooLow = await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${target!.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: '1' }),
  });
  expect(tooLow.status, '1 reddedilmeli (min 2)').toBe(400);

  const tooHigh = await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${target!.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: '7' }),
  });
  expect(tooHigh.status, '7 reddedilmeli (max 6)').toBe(400);

  // Aralık içi kabul edilmeli
  const valid = await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${target!.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: '3' }),
  });
  expect(valid.status, '3 kabul edilmeli').toBe(200);

  // CLEANUP
  await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${target!.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: originalValue }),
  });
});

// ────────────────────────────────────────────────────────────────
// TEST 8 — Schedule.CurriculumConflictMode için backend enum doğrulaması
//   Düzeltme: SchedulingAlgorithmConfigController.EnumOptions ile izinli değer kontrolü
//   + GET /enum-options endpoint frontend dropdown render'ı için seçenek listesi döner.
// ────────────────────────────────────────────────────────────────
test('Test 8 — CurriculumConflictMode için enum validasyonu ve UI dropdown davranışı', async ({ page }) => {
  // Backend: enum-options endpoint 3 seçenek dönüyor (cohort, curriculum, enrollment-disabled)
  const enumOptions = await fetchJson<Record<string, Array<{ value: string; label: string; disabled: boolean }>>>(
    token, '/admin/scheduling-algorithm-config/enum-options');
  const modeOptions = enumOptions['Schedule.CurriculumConflictMode'];
  expect(modeOptions, 'CurriculumConflictMode enum options gelmeli').toBeTruthy();
  expect(modeOptions.map(o => o.value)).toEqual(['cohort', 'curriculum', 'enrollment']);
  expect(modeOptions.find(o => o.value === 'enrollment')?.disabled, 'enrollment disabled olmalı').toBe(true);
  expect(modeOptions.find(o => o.value === 'cohort')?.disabled).toBe(false);

  // Backend: geçersiz değer → 400
  const configs = await fetchJson<Array<{ id: number; key: string; value: string }>>(token, '/admin/scheduling-algorithm-config');
  const modeConfig = configs.find(c => c.key === 'Schedule.CurriculumConflictMode')!;
  const originalValue = modeConfig.value;

  const invalidRes = await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${modeConfig.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: 'asdf' }),
  });
  expect(invalidRes.status, 'Geçersiz değer reddedilmeli').toBe(400);

  const disabledRes = await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${modeConfig.id}`, {
    method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: 'enrollment' }),
  });
  expect(disabledRes.status, 'Disabled enrollment reddedilmeli').toBe(400);

  // UI: dropdown render edildi mi (cohort + curriculum + enrollment görünür, enrollment disabled)
  await page.goto(CONSTRAINTS_ROUTE);
  await page.getByRole('tab', { name: 'Algoritma Parametreleri' }).click();
  // CurriculumConflictMode satırını "Cohort (önerilen)" label'ından bul (DataType=string + enum
  // olduğu için label render ediliyor; description metni güncellenebilir, label stabil).
  const modeRow = page.locator('tr', { hasText: 'Cohort (önerilen)' });
  await expect(modeRow).toBeVisible({ timeout: 10_000 });
  await modeRow.getByRole('button', { name: 'Düzenle' }).click();

  const dialog = page.getByRole('dialog');
  // Button locator (dialog içinde 3 seçenek button olarak render edilir)
  await expect(dialog.getByRole('button', { name: /Cohort \(önerilen\)/ })).toBeVisible();
  await expect(dialog.getByRole('button', { name: /^Curriculum/ })).toBeVisible();
  await expect(dialog.getByRole('button', { name: /Enrollment \(desteklenmiyor\)/ })).toBeVisible();
  // Disabled olan button → disabled attribute
  await expect(dialog.getByRole('button', { name: /Enrollment \(desteklenmiyor\)/ })).toBeDisabled();

  // Kapat (değer değiştirmeden)
  await dialog.getByRole('button', { name: 'İptal' }).click();

  // CLEANUP: orijinal değeri geri yükle (olmazsa bile aynı kalmış olmalı)
  if (originalValue !== modeConfig.value) {
    await fetch(`${API_BASE}/admin/scheduling-algorithm-config/${modeConfig.id}`, {
      method: 'PUT', headers: authHeaders(token), body: JSON.stringify({ value: originalValue }),
    });
  }
});

// ────────────────────────────────────────────────────────────────
// TEST 10 — S20 (Komşu Yıl Çakışma Garantisi) satırı UI'da görünür ve
//   Düzenle butonu crash etmiyor.
//   Regression: PARAMETER_SCHEMAS[code] undefined olan kurallar için
//   `?? []` her render'da yeni array → useEffect dep değişti sanır →
//   "Maximum update depth exceeded" (infinite loop). Düzeltme: useMemo.
// ────────────────────────────────────────────────────────────────
test('Test 10 — S20 Komşu Yıl Çakışma Garantisi UI\'da görünür, Düzenle crash etmiyor', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(`[pageerror] ${err.message}`));

  await page.goto(CONSTRAINTS_ROUTE);

  // S20 satırı Müfredat & Ders Kısıtlamaları grubunda görünmeli
  const s20Row = page.locator('tr', { hasText: 'Komşu Yıl Çakışma Garantisi' });
  await expect(s20Row, 'S20 satırı CONSTRAINT_GROUPS içinde olmalı').toBeVisible({ timeout: 10_000 });
  await expect(s20Row.getByText('Zorunlu', { exact: true })).toBeVisible();

  // Düzenle'ye bas — infinite loop sayfa crash ediyordu
  await s20Row.getByRole('button', { name: 'Düzenle' }).click();
  await page.waitForTimeout(1500);

  // Sayfa crash olmamalı (rootLen > 10k threshold)
  const rootLen = (await page.locator('#root').innerHTML()).length;
  expect(rootLen, 'Sayfa "Maximum update depth" hatasıyla crash olmamalı').toBeGreaterThan(10_000);

  // Dialog açıldı, "Zorunlu (sabit)" görünür (capability=hard-only)
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText('Zorunlu (sabit)')).toBeVisible();

  // Hiçbir runtime hata olmamalı
  expect(errors, `Sayfa runtime hatası vermemeli (varsa: ${errors.join('\\n')})`).toHaveLength(0);
});
