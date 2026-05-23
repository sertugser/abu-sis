// Term-type (dönem türü) tek kaynak — backend TermType enum'unun frontend karşılığı.
// Eskiden TermManagementPage (TERM_TYPE_OPTIONS) ve TopBar (TERM_TYPE_LABELS) bu listeyi
// ayrı ayrı tanımlıyordu; tek kaynağa indirildi. Backend tarafı: SIS.Infrastructure/Services/TermLabels.cs

export const TERM_TYPE_OPTIONS = [
  { value: 'Fall', label: 'Güz' },
  { value: 'Spring', label: 'Bahar' },
  { value: 'Summer', label: 'Yaz' },
  { value: 'FallResit', label: 'Güz Tekders' },
  { value: 'SpringResit', label: 'Bahar Tekders' },
  { value: 'SummerResit', label: 'Yaz Tekders' },
  { value: 'SpringOther', label: 'Bahar Diğer' },
  { value: 'SummerOther', label: 'Yaz Diğer' },
  { value: 'Exemption', label: 'Muafiyet' },
] as const;

export type TermTypeValue = (typeof TERM_TYPE_OPTIONS)[number]['value'];

export const TERM_TYPE_LABELS: Record<string, string> = Object.fromEntries(
  TERM_TYPE_OPTIONS.map(o => [o.value, o.label]),
);

export function getTermTypeLabel(value: string | null | undefined): string {
  if (!value) return '';
  return TERM_TYPE_LABELS[value] ?? value;
}
