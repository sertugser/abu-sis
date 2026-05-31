// Ortak yazdırma yardımcısı — bir tabloyu temiz bir yazdırma penceresinde basar.
// Sayfanın kendisini (sidebar/üst bar dahil) DEĞİL, yalnız verilen başlık + tabloyu basar.
// Mevcut sayfalardaki kopyalanmış window.open(...).document.write(...) desenini tek yere toplar.

export interface PrintTableColumn {
  header: string;
  align?: 'left' | 'right' | 'center';
}

export interface PrintTableOptions {
  /** Çıktı başlığı (h1 + pencere title). */
  title: string;
  /** Başlığın altındaki özet kutuları (Etiket → Değer). */
  meta?: { label: string; value: string }[];
  columns: PrintTableColumn[];
  /** Satırlar; her hücre string/number/null olabilir. Kolon sırasıyla aynı uzunlukta olmalı. */
  rows: ReadonlyArray<ReadonlyArray<string | number | null | undefined>>;
  /** A4 yatay (varsayılan) ya da dikey. */
  orientation?: 'landscape' | 'portrait';
}

function escapeHtml(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return '';
  return String(value).replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c] as string),
  );
}

function alignClass(align: PrintTableColumn['align']): string {
  if (align === 'right') return ' class="r"';
  if (align === 'center') return ' class="c"';
  return '';
}

/**
 * Temiz bir popup açar, başlık + meta + tabloyu yazar ve yazdırma diyaloğunu tetikler.
 * Popup engellenirse sessizce false döner (çağıran isterse kullanıcıyı uyarır).
 */
export function printHtmlTable(options: PrintTableOptions): boolean {
  const { title, meta = [], columns, rows, orientation = 'landscape' } = options;

  // NOT: 'noopener' VERİLMEZ — verilirse window.open null döner, içerik yazılamaz (boş about:blank).
  const win = window.open('', '_blank', 'width=1120,height=820');
  if (!win) return false;

  const metaHtml = meta.length > 0
    ? `<div class="meta">${meta
        .map((m) => `<div><span class="label">${escapeHtml(m.label)}</span>${escapeHtml(m.value)}</div>`)
        .join('')}</div>`
    : '';

  const headHtml = columns
    .map((c) => `<th${alignClass(c.align)}>${escapeHtml(c.header)}</th>`)
    .join('');

  const bodyHtml = rows
    .map(
      (row) =>
        `<tr>${row
          .map((cell, i) => `<td${alignClass(columns[i]?.align)}>${escapeHtml(cell)}</td>`)
          .join('')}</tr>`,
    )
    .join('');

  win.document.open();
  win.document.write(`
    <!doctype html>
    <html lang="tr">
      <head>
        <meta charset="utf-8" />
        <title>${escapeHtml(title)}</title>
        <style>
          @page { size: A4 ${orientation}; margin: 14mm; }
          * { box-sizing: border-box; }
          body { margin: 0; color: #111827; font-family: Arial, Helvetica, sans-serif; font-size: 11px; }
          h1 { margin: 0 0 8px; font-size: 20px; }
          .meta { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; margin: 12px 0 16px; }
          .meta div { border: 1px solid #d1d5db; border-radius: 6px; padding: 8px; }
          .label { display: block; margin-bottom: 3px; color: #6b7280; font-size: 9px; text-transform: uppercase; }
          table { width: 100%; border-collapse: collapse; }
          th, td { border: 1px solid #d1d5db; padding: 6px 7px; text-align: left; vertical-align: top; }
          th { background: #f3f4f6; font-size: 10px; }
          td.r, th.r { text-align: right; font-variant-numeric: tabular-nums; }
          td.c, th.c { text-align: center; }
          .footer { margin-top: 12px; color: #6b7280; font-size: 9px; }
        </style>
      </head>
      <body>
        <h1>${escapeHtml(title)}</h1>
        ${metaHtml}
        <table>
          <thead><tr>${headHtml}</tr></thead>
          <tbody>${bodyHtml}</tbody>
        </table>
        <div class="footer">Oluşturulma zamanı: ${escapeHtml(new Date().toLocaleString('tr-TR'))}</div>
        <script>
          window.addEventListener('load', () => {
            window.focus();
            setTimeout(() => window.print(), 150);
          });
        </script>
      </body>
    </html>
  `);
  win.document.close();
  return true;
}
