import { test, expect, type Page } from '@playwright/test';

const API_BASE = 'http://localhost:5213/api';
const ADMIN_EMAIL = 'admin@ankarabilim.edu.tr';
const ADMIN_PASSWORD = 'Admin123!';

// Faculty Planning sayfası backend'den aktif term + ilk fakülte alır; route param yok.
const FACULTY_PLANNING_ROUTE = '/ders-programi-olusturma';

// Gerçek OR-Tools solver test verisinde (Güz 2024-25, MUH fakültesi, 72 section) tipik
// olarak 5-15s sürer. CI ya da yavaş makinelerde tampon bırakıyoruz.
const SOLVE_TIMEOUT_MS = 120_000;

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
  const data = await res.json() as {
    success: boolean; token: string; expiresAt: string; roles: string[]; error?: string;
  };
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

/**
 * "Otomatik Programla" butonu, aktif dönem "Active" değilse veya buton "Hesaplanıyor..."
 * durumundaysa disabled. Bunu test başında doğrulayıp, doğru ortamda olmadığımızda
 * testi atlatıyoruz (CI'da yanlış pozitif vermesin).
 */
async function ensureTriggerableState(page: Page) {
  const trigger = page.getByRole('button', { name: /Otomatik Programla/ });
  await trigger.waitFor({ state: 'visible', timeout: 15_000 });
  // Ardışık koşumda önceki testin tamamlanması, query refetch'leri vb. nedeniyle
  // buton kısa süreliğine disabled görünebiliyor. 5s tampon bırakıp gerçek
  // "ortam yanlış" durumunu (Active term yok / başka run sürüyor) skip ile ayırıyoruz.
  try {
    await expect(trigger).toBeEnabled({ timeout: 5_000 });
  } catch {
    test.skip(true, 'Otomatik Programla butonu enabled olmadı — Active term yok veya başka run sürüyor');
  }
  return trigger;
}

test.describe('Auto-schedule progress modal', () => {
  test.beforeEach(async ({ page }) => {
    await authenticate(page);
    page.on('pageerror', (err) => {
      console.error(`[browser-error] ${err.message}`);
    });
  });

  test('happy path: tetikle → progress modal → tamamlanır → sonuç paneli', async ({ page }) => {
    // Preview API çağrısının tetiklendiğini doğrulamak için ağı izle.
    // Endpoint: POST /api/faculty-planning/auto-schedule  (apply uçnoktasıyla karışmasın
    // diye trailing /apply'ı dışlıyoruz).
    const previewRequests: Array<{ url: string }> = [];
    page.on('request', (req) => {
      if (
        req.method() === 'POST' &&
        /\/api\/faculty-planning\/auto-schedule(\?|$)/.test(req.url())
      ) {
        previewRequests.push({ url: req.url() });
      }
    });

    await page.goto(FACULTY_PLANNING_ROUTE);

    const trigger = await ensureTriggerableState(page);
    await trigger.click();

    // Progress modal görünür: dialog with aria-labelledby="auto-schedule-progress-title".
    // Header metni "Otomatik Programlama" — ResultPanel'in "Programlama Tamamlandı"
    // başlığından farklı olduğu için iki mod birbirine karışmıyor.
    const progressModal = page.locator('[role="dialog"][aria-labelledby="auto-schedule-progress-title"]');
    await expect(progressModal).toBeVisible({ timeout: 10_000 });
    await expect(progressModal.getByText('Otomatik Programlama')).toBeVisible();

    // Preview mutation tetiklendi mi
    expect(previewRequests.length, 'preview endpoint çağrılmalı').toBeGreaterThan(0);

    // Stage etiketlerinden en az birinin görünür olmasını bekle. Header altyazısı + stage
    // indicator span'ları aynı string'i içerebildiği için `.first()` ile strict-mode hatasını
    // önlüyoruz; amaç sadece "modal stage rendering yapıyor mu" doğrulaması.
    await expect(
      progressModal.locator('text=/Veri Hazırlığı|Program Çözümü/').first(),
    ).toBeVisible({ timeout: 15_000 });

    // ResultPanel görünene kadar bekle: "Programlama Tamamlandı" başlığı.
    const resultModal = page.locator('[role="dialog"][aria-labelledby="auto-schedule-result-title"]');
    await expect(resultModal).toBeVisible({ timeout: SOLVE_TIMEOUT_MS });
    await expect(resultModal.getByText('Programlama Tamamlandı')).toBeVisible();

    // "X / Y şube yerleştirildi" formatı — assignments + failures toplamı.
    const summaryText = await resultModal.locator('p', { hasText: 'şube yerleştirildi' }).first().textContent();
    expect(summaryText, 'özet metni X / Y şube yerleştirildi formatında olmalı').toMatch(/\d+\s*\/\s*\d+\s*şube yerleştirildi/);

    // "Yerleştirilen" + "Yerleşemeyen" başlıkları render edildi (boş olabilir, sadece varlık kontrolü).
    await expect(resultModal.getByText('Yerleştirilen', { exact: false })).toBeVisible();
    await expect(resultModal.getByText('Yerleşemeyen', { exact: false })).toBeVisible();

    // "Tümünü Uygula (N)" butonu — assignments > 0 ise enabled, değilse disabled.
    // Burada apply'a basmıyoruz; canlı veri etkilensin istemiyoruz. Sadece butonun
    // doğru sayıyı yansıttığını doğruluyoruz (regression — "Tümünü Uygula (0)" gözükmezse bug).
    const applyButton = resultModal.getByRole('button', { name: /Tümünü Uygula/ });
    if (await applyButton.isVisible()) {
      const label = await applyButton.textContent();
      expect(label, 'Tümünü Uygula butonu yerleştirilen şube sayısını parantez içinde göstermeli')
        .toMatch(/Tümünü Uygula \(\d+\)/);
    }

    // Sağ üst X ile kapat — modal kaybolur. ResultPanel onDismiss handler'ı showResultModal=false yapar.
    await resultModal.getByRole('button', { name: 'Kapat' }).click();
    await expect(resultModal).toBeHidden({ timeout: 5_000 });
  });

  test('cancel flow: İptal Et → confirmation → Evet → modal kapanır', async ({ page }) => {
    await page.goto(FACULTY_PLANNING_ROUTE);

    const trigger = await ensureTriggerableState(page);
    await trigger.click();

    const progressModal = page.locator('[role="dialog"][aria-labelledby="auto-schedule-progress-title"]');
    await expect(progressModal).toBeVisible({ timeout: 10_000 });

    // İptal Et butonu — SignalR connectionStatus === 'connected' olunca enabled olur.
    // Hub auth handshake'i ortalama <1s sürer; 10s tampon bırakıyoruz.
    const cancelButton = progressModal.getByRole('button', { name: /^İptal Et$/ });
    await expect(cancelButton).toBeEnabled({ timeout: 10_000 });
    await cancelButton.click();

    // Confirmation AlertDialog — Radix kendi role="alertdialog" üretiyor.
    // "İşlemi iptal et?" başlığı handleCancelClick → setConfirmCancel(true) sonrası açılır.
    const confirmDialog = page.getByRole('alertdialog');
    await expect(confirmDialog).toBeVisible({ timeout: 5_000 });
    await expect(confirmDialog.getByText('İşlemi iptal et?')).toBeVisible();

    // "Evet, İptal Et" → handleConfirmCancel → Hub.invoke('Cancel') → previewMutation.reset()
    // → isPending=false ve previewData=null → modal'ın open koşulu false olur.
    await confirmDialog.getByRole('button', { name: /Evet, İptal Et/ }).click();

    // Hem progress modal hem result modal kapanmış olmalı (cancel'da sonuç paneli açılmaz).
    await expect(progressModal).toBeHidden({ timeout: 15_000 });
    await expect(
      page.locator('[role="dialog"][aria-labelledby="auto-schedule-result-title"]'),
    ).toBeHidden();

    // Otomatik Programla butonu yeniden enabled olmalı (autoIsPending=false).
    await expect(trigger).toBeEnabled({ timeout: 10_000 });
  });
});
