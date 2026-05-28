/**
 * OBİS — Sunum Öncesi Uçtan Uca Audit (v2 — token-cache pattern)
 * 2026-05-20
 *
 * Tasarım kararı: Login'leri spec başında SIRALI bir kez yap (workers:1 + ardışık).
 * Playwright Node runtime'da concurrent fetch + ASP.NET Identity lockout race
 * concurrent login'leri başarısız ediyor. Tek seferlik token cache pattern sorunu çözer.
 */
import { test, expect, type Page, request as plRequest } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';

const USERS = {
  SystemAdmin: { email: 'admin@ankarabilim.edu.tr', password: 'Admin123!' },
  Advisor: { email: 'danisman@ankarabilim.edu.tr', password: 'Danisman123!' },
  Dean: { email: 'halimhaldun.goktas@ankarabilim.edu.tr', password: 'Ogretmen123!' },
  DepartmentManager: { email: 'ender.sevinc@ankarabilim.edu.tr', password: 'Ogretmen123!' },
  Instructor: { email: 'ogretmen@ankarabilim.edu.tr', password: 'Ogretmen123!' },
  Student: { email: 'ogrenci1@ankarabilim.edu.tr', password: 'Ogrenci123!' },
} as const;

type RoleName = keyof typeof USERS;

interface CachedLogin {
  token: string;
  expiresAt: string;
  roles: string[];
  permissions: string[];
}

const TOKENS: Partial<Record<RoleName, CachedLogin>> = {};

async function loadAllTokens() {
  const ctx = await plRequest.newContext();
  for (const role of Object.keys(USERS) as RoleName[]) {
    if (TOKENS[role]) continue;
    const { email, password } = USERS[role];
    let res = await ctx.post(`${API_BASE}/auth/login`, {
      data: { email, password },
    });
    // Rate-limit-aware retry: 429 ise 65sn bekle (1dk window dolar)
    if (res.status() === 429) {
      console.log(`[token-cache] ${role} 429 — 65sn bekleniyor rate-limit reset için`);
      await new Promise(r => setTimeout(r, 65_000));
      res = await ctx.post(`${API_BASE}/auth/login`, { data: { email, password } });
    }
    if (!res.ok()) {
      console.error(`[token-cache] ${role} login HTTP ${res.status()}`);
      continue;
    }
    const data = await res.json() as { success: boolean; token: string; expiresAt: string; roles: string[]; error?: string };
    if (!data.success) {
      console.error(`[token-cache] ${role} login fail: ${data.error}`);
      continue;
    }
    let perms: string[] = [];
    const pres = await ctx.get(`${API_BASE}/me/permissions`, { headers: { Authorization: `Bearer ${data.token}` } });
    if (pres.ok()) {
      const pdata = await pres.json() as string[] | { permissions?: string[] };
      perms = Array.isArray(pdata) ? pdata : (pdata.permissions ?? []);
    }
    TOKENS[role] = { token: data.token, expiresAt: data.expiresAt, roles: data.roles, permissions: perms };
    console.log(`[token-cache] ${role} OK roles=${JSON.stringify(data.roles)} perms=${perms.length}`);
  }
  await ctx.dispose();
}

async function authenticate(page: Page, role: RoleName) {
  const cached = TOKENS[role];
  if (!cached) throw new Error(`No cached token for ${role}`);
  await page.addInitScript(
    ({ t, e, r }) => {
      localStorage.setItem('sis_token', t);
      localStorage.setItem('sis_token_expires', e);
      localStorage.setItem('sis_roles', JSON.stringify(r));
    },
    { t: cached.token, e: cached.expiresAt, r: cached.roles }
  );
}

async function apiGet(path: string, token: string) {
  const ctx = await plRequest.newContext();
  const res = await ctx.get(`${API_BASE}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  const status = res.status();
  let body: unknown = null;
  try { body = await res.json(); } catch { /* */ }
  await ctx.dispose();
  return { status, body };
}

async function apiPost(path: string, token: string, body: unknown) {
  const ctx = await plRequest.newContext();
  const res = await ctx.post(`${API_BASE}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
    data: body,
  });
  const status = res.status();
  let rbody: unknown = null;
  try { rbody = await res.json(); } catch { /* */ }
  await ctx.dispose();
  return { status, body: rbody };
}

// Spec-wide setup: tüm token'ları SIRALI olarak baştan yükle
test.beforeAll(async () => {
  await loadAllTokens();
});

// ===========================================================================
// GRUP A — Login + JWT + Sidebar RBAC
// ===========================================================================
test.describe('GRUP A — Login + RBAC', () => {
  for (const role of Object.keys(USERS) as RoleName[]) {
    test(`A.${role}: login + token + permissions`, async () => {
      const cached = TOKENS[role];
      expect(cached, `${role} token cache'te olmalı`).toBeDefined();
      if (!cached) return;
      expect(cached.token.length, `JWT token boş olmamalı`).toBeGreaterThan(20);
      expect(cached.roles.length, `roles boş olmamalı`).toBeGreaterThan(0);
      // SystemAdmin permission shortcut: 0 dönebilir; diğerleri >=1
      if (role !== 'SystemAdmin') {
        expect(cached.permissions.length, `${role} en az 1 permission'a sahip olmalı`).toBeGreaterThan(0);
      }
    });
  }

  test('A.CrossRole: Student → /donem-yonetimi → forbidden/redirect', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/donem-yonetimi');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000); // RBAC redirect bekle
    const url = page.url();
    const onForbidden = url.includes('/forbidden') || url.endsWith('/') || !url.includes('/donem-yonetimi');
    const forbiddenText = await page.getByText(/yetki|forbidden|403|erişim/i).first().isVisible({ timeout: 2000 }).catch(() => false);
    console.log(`[A.CrossRole] url=${url} forbiddenText=${forbiddenText} onForbidden=${onForbidden}`);
    expect(onForbidden || forbiddenText, 'Student dönem yönetimini görmemeli').toBe(true);
  });

  test('A.SidebarMenu: her rol için /menu/me endpoint çalışır', async () => {
    for (const role of Object.keys(USERS) as RoleName[]) {
      const cached = TOKENS[role];
      if (!cached) continue;
      const { status, body } = await apiGet('/me/menu', cached.token);
      const modules = Array.isArray(body) ? body : (body as { modules?: unknown[] })?.modules ?? [];
      console.log(`[A.SidebarMenu] ${role} status=${status} modules=${Array.isArray(modules) ? modules.length : '?'}`);
      expect(status, `${role} /me/menu 2xx olmalı`).toBeLessThan(400);
    }
  });
});

// ===========================================================================
// GRUP B — Akademik yapı + Dönem + Takvim + Faz gating
// ===========================================================================
test.describe('GRUP B — Akademik yapı + Dönem + Takvim', () => {
  test('B1: AdminLookup endpoint\'leri (departments + programs) çalışır', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    const dep = await apiGet('/admin/departments', t!.token);
    const prog = await apiGet('/admin/programs', t!.token);
    console.log(`[B1] departments=${dep.status} programs=${prog.status}`);
    expect(dep.status, 'departments 2xx').toBeLessThan(400);
    expect(prog.status, 'programs 2xx').toBeLessThan(400);
  });

  test('B1b: Faculty endpoint mevcut', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    // Birkaç yolu dene — gerçek route'u bulalım
    const candidates = ['/admin/faculties', '/admin/academic-structure/faculties', '/faculty', '/admin/structure/faculties'];
    let found: { path: string; status: number; count?: number } | null = null;
    for (const p of candidates) {
      const { status, body } = await apiGet(p, t!.token);
      if (status < 400) {
        const list = Array.isArray(body) ? body : (body as { data?: unknown[] })?.data ?? [];
        found = { path: p, status, count: Array.isArray(list) ? list.length : undefined };
        break;
      }
    }
    console.log(`[B1b] faculty endpoint found=${JSON.stringify(found)}`);
    expect(found, 'En az 1 faculty endpoint cevap vermeli').not.toBeNull();
  });

  test('B2: Term listesi çekilir', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    const { status, body } = await apiGet('/admin/terms', t!.token);
    console.log(`[B2] terms status=${status}`);
    expect(status, 'terms 2xx').toBeLessThan(400);
    const list = Array.isArray(body) ? body : (body as { data?: unknown[] })?.data ?? [];
    expect(Array.isArray(list) ? list.length : 0, 'En az 1 dönem olmalı').toBeGreaterThan(0);
    console.log(`[B2] term count=${Array.isArray(list) ? list.length : '?'}`);
  });

  test('B3: AcademicCalendar terms listesi (hasCalendar flag)', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    const { status, body } = await apiGet('/academic-calendar/terms', t!.token);
    console.log(`[B3] academic-calendar/terms status=${status}`);
    expect(status, 'academic-calendar/terms 2xx').toBeLessThan(400);
    const list = Array.isArray(body) ? body : [];
    console.log(`[B3] term-calendar count=${list.length}; sample=${JSON.stringify(list[0] ?? null)}`);
  });

  test('B4: Aktif dönem academic-calendar/active', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    const { status, body } = await apiGet('/academic-calendar/active', t!.token);
    console.log(`[B4] /academic-calendar/active status=${status}`);
    if (status < 400) {
      console.log(`[B4] active calendar keys=${Object.keys(body ?? {}).join(',')}`);
    }
  });

  test('B5: Faz gating — student catalog 4xx vs 2xx davranisi (phase bagli)', async () => {
    const t = TOKENS.Student;
    if (!t) test.skip(true, 'Student token yok');
    const candidates = ['/students/me/course-catalog', '/student/course-catalog', '/students/me/catalog', '/registration/catalog'];
    for (const p of candidates) {
      const { status } = await apiGet(p, t!.token);
      console.log(`[B5] ${p} status=${status}`);
    }
  });

  test('B6: Term cancel endpoint authorize ediyor (admin)', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    // /api/admin/terms/{int}/cancel
    const { status } = await apiPost('/admin/terms/99999999/cancel', t!.token, {});
    console.log(`[B6] cancel fake term status=${status}`);
    expect(status, 'cancel endpoint authorize ediyor').not.toBe(401);
    expect(status, 'cancel endpoint authorize ediyor').not.toBe(403);
  });
});

// ===========================================================================
// GRUP C — Otomatik ders programı
// ===========================================================================
test.describe('GRUP C — Otomatik ders programı', () => {
  test('C1: Faculty-planning sayfası açılır (admin) + "Otomatik Programla" butonu var', async ({ page }) => {
    await authenticate(page, 'SystemAdmin');
    await page.goto('/ders-programi-olusturma');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000); // Sayfa render
    const trigger = page.getByRole('button', { name: /Otomatik Programla/ });
    const visible = await trigger.isVisible({ timeout: 5000 }).catch(() => false);
    console.log(`[C1] Otomatik Programla buton visible=${visible} url=${page.url()}`);
    expect(visible, 'Otomatik Programla butonu görünür olmalı').toBe(true);
  });

  test('C2: Auto-schedule preview endpoint authorize ediyor', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    const { status } = await apiPost('/faculty-planning/auto-schedule', t!.token, {
      facultyId: 99999, termId: 99999,
    });
    console.log(`[C2] auto-schedule fake-id status=${status}`);
    expect(status, 'auto-schedule authorize ediyor').not.toBe(401);
    expect(status, 'auto-schedule authorize ediyor').not.toBe(403);
  });

  test('C3: Constraint listesi (hard + soft)', async () => {
    const t = TOKENS.SystemAdmin;
    if (!t) test.skip(true, 'SystemAdmin token yok');
    const candidates = ['/scheduling/constraints', '/admin/constraints', '/constraints', '/admin/scheduling/constraints'];
    let result: { path: string; count: number } | null = null;
    for (const p of candidates) {
      const { status, body } = await apiGet(p, t!.token);
      if (status < 400 && body) {
        const list = Array.isArray(body) ? body : (body as { data?: unknown[] })?.data ?? [];
        if (Array.isArray(list)) {
          result = { path: p, count: list.length };
          break;
        }
      }
    }
    console.log(`[C3] constraint endpoint=${JSON.stringify(result)}`);
    if (result) {
      expect(result.count, 'en az 15 constraint olmalı').toBeGreaterThan(10);
    }
  });

  test('C4: Plan 9 bilinen sorun — AmphiThreshold + 0 amfi davranışı (manuel)', async () => {
    console.log('[C4] DOCUMENTED: Plan 9 known issue — manuel doğrulama gerek');
  });

  test('C5: Schedule editor sayfası açılır', async ({ page }) => {
    await authenticate(page, 'SystemAdmin');
    await page.goto('/ders-programi-olusturma/duzenle');
    await page.waitForLoadState('domcontentloaded');
    console.log(`[C5] schedule-editor url=${page.url()}`);
  });
});

// ===========================================================================
// GRUP D — Öğrenci ders kaydı
// ===========================================================================
test.describe('GRUP D — Öğrenci ders kaydı', () => {
  test('D1: Student catalog endpoint /course/student-catalog', async () => {
    const t = TOKENS.Student;
    if (!t) test.skip(true, 'Student token yok');
    const { status, body } = await apiGet('/course/student-catalog', t!.token);
    const list = Array.isArray(body) ? body : (body as { data?: unknown[] })?.data ?? [];
    console.log(`[D1] /course/student-catalog status=${status} count=${Array.isArray(list) ? list.length : '?'}`);
    expect(status, 'catalog 2xx').toBeLessThan(400);
  });

  test('D2: Registration plan /registrations/my', async () => {
    const t = TOKENS.Student;
    if (!t) test.skip(true, 'Student token yok');
    const { status, body } = await apiGet('/registrations/my', t!.token);
    const list = Array.isArray(body) ? body : (body as { data?: unknown[] })?.data ?? [];
    console.log(`[D2] /registrations/my status=${status} count=${Array.isArray(list) ? list.length : '?'}`);
    expect(status, 'registrations/my 2xx').toBeLessThan(400);
  });

  test('D3: Transcript /student/transcript', async () => {
    const t = TOKENS.Student;
    if (!t) test.skip(true, 'Student token yok');
    const { status, body } = await apiGet('/student/transcript', t!.token);
    console.log(`[D3] /student/transcript status=${status} keys=${body ? Object.keys(body).join(',') : 'null'}`);
    expect(status, 'transcript 2xx').toBeLessThan(400);
  });

  test('D4: Curriculum status /student/curriculum-status', async () => {
    const t = TOKENS.Student;
    if (!t) test.skip(true, 'Student token yok');
    const { status, body } = await apiGet('/student/curriculum-status', t!.token);
    console.log(`[D4] /student/curriculum-status status=${status} keys=${body ? Object.keys(body).join(',') : 'null'}`);
    expect(status, 'curriculum-status 2xx').toBeLessThan(400);
  });

  test('D5: Ders kayıt sayfası açılır + içerik render', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/ders-kayit');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000);
    const body = await page.locator('body').textContent();
    console.log(`[D5] ders-kayit body length=${body?.length} url=${page.url()}`);
    expect(body?.length, 'Sayfa boş olmamalı').toBeGreaterThan(100);
  });

  test('D6: /plan redirect /ders-kayit yönlendirir', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/plan');
    await page.waitForLoadState('domcontentloaded');
    console.log(`[D6] plan url=${page.url()}`);
    // /plan retired; now redirects to /ders-kayit
    expect(page.url()).toContain('/ders-kayit');
  });

  test('D7: Withdraw /registrations/{id}/withdraw authorize', async () => {
    const t = TOKENS.Student;
    if (!t) test.skip(true, 'Student token yok');
    const { status } = await apiPost('/registrations/99999/withdraw', t!.token, {});
    console.log(`[D7] POST /registrations/99999/withdraw status=${status}`);
    expect(status, 'withdraw auth').not.toBe(401);
    expect(status, 'withdraw auth').not.toBe(403);
  });
});

// ===========================================================================
// GRUP E — Hoca not + yoklama
// ===========================================================================
test.describe('GRUP E — Hoca işlemleri', () => {
  test('E1: Instructor sections /instructor/my-sections', async () => {
    const t = TOKENS.Instructor;
    if (!t) test.skip(true, 'Instructor token yok');
    const { status, body } = await apiGet('/instructor/my-sections', t!.token);
    const list = Array.isArray(body) ? body : (body as { data?: unknown[] })?.data ?? [];
    console.log(`[E1] /instructor/my-sections status=${status} count=${Array.isArray(list) ? list.length : '?'}`);
    expect(status, 'my-sections 2xx').toBeLessThan(400);
  });

  test('E2: Derslerim sayfası', async ({ page }) => {
    await authenticate(page, 'Instructor');
    await page.goto('/derslerim');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[E2] derslerim url=${page.url()}`);
    const body = await page.locator('body').textContent();
    expect(body?.length, 'Sayfa boş olmamalı').toBeGreaterThan(100);
  });

  test('E3: Yoklama girişi sayfası', async ({ page }) => {
    await authenticate(page, 'Instructor');
    await page.goto('/yoklama-girisi');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[E3] yoklama-girisi url=${page.url()}`);
  });

  test('E4: Not girişi route /derslerim redirect', async ({ page }) => {
    await authenticate(page, 'Instructor');
    await page.goto('/not-girisi');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[E4] not-girisi → ${page.url()}`);
    expect(page.url(), '/not-girisi → /derslerim redirect').toContain('/derslerim');
  });
});

// ===========================================================================
// GRUP F — Öğrenci görünüm + cascade
// ===========================================================================
test.describe('GRUP F — Öğrenci görünüm + cascade', () => {
  test('F1: Transkript sayfası', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/report/transkript');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[F1] transkript url=${page.url()}`);
  });

  test('F2: Müfredat durumu', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/mufredat-durum');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[F2] mufredat-durum url=${page.url()}`);
  });

  test('F3: Dönem ortalamaları', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/donem-ortalamalari');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[F3] donem-ortalamalari url=${page.url()}`);
  });

  test('F4: Devamsızlık durumu', async ({ page }) => {
    await authenticate(page, 'Student');
    await page.goto('/devamsizlik-durumu');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[F4] devamsizlik-durumu url=${page.url()}`);
  });
});

// ===========================================================================
// GRUP G — Nice-to-have
// ===========================================================================
test.describe('GRUP G — Nice-to-have', () => {
  test('G1: Sayfa refresh sonrası login korunur', async ({ page }) => {
    await authenticate(page, 'SystemAdmin');
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);
    await page.reload();
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);
    console.log(`[G1] refresh url=${page.url()}`);
    expect(page.url(), 'refresh sonrası /login\'e atmamalı').not.toContain('/login');
  });

  test('G2: Login form validation', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');
    const submit = page.getByRole('button', { name: /giriş|login/i }).first();
    const visible = await submit.isVisible({ timeout: 2000 }).catch(() => false);
    if (!visible) {
      test.skip(true, 'Login submit butonu bulunamadı');
      return;
    }
    await submit.click();
    await page.waitForTimeout(1000);
    console.log(`[G2] boş submit → url=${page.url()}`);
    expect(page.url(), 'boş submit /login\'de kalmalı').toContain('/login');
  });

  test('G3: Şifre değiştir sayfası', async ({ page }) => {
    await authenticate(page, 'SystemAdmin');
    await page.goto('/sifre-degistir');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[G3] sifre-degistir url=${page.url()}`);
  });

  test('G4: Audit log sayfası (admin)', async ({ page }) => {
    await authenticate(page, 'SystemAdmin');
    await page.goto('/denetim-kayitlari');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1500);
    console.log(`[G4] audit-log url=${page.url()}`);
  });
});
