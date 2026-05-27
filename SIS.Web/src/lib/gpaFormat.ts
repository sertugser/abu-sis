// ANO/AGNO gösterimi için tek kaynak: 2 ondalık, Türkçe virgül; veri yoksa "—".
// Hem Dönem Ortalamaları sayfası hem Öğrenci Rehberi paneli bunu kullanır.
export function formatGpa(value: number | null | undefined): string {
  if (value == null) return '—';
  return value.toFixed(2).replace('.', ',');
}
