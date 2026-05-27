// Dönem formları/görünümleri için ortak tarih dönüştürücüleri.

export function toDateInput(isoString: string): string {
  if (!isoString) return '';
  return isoString.substring(0, 10);
}

export function toIsoString(dateInput: string): string {
  if (!dateInput) return '';
  return new Date(dateInput).toISOString();
}

export function fmt(iso: string): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' });
}
