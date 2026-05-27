// Kayıt planı durumu için ortak etiket + Badge varyant haritaları.
// Component dosyasından ayrı tutulur (react-refresh/only-export-components).

export const PLAN_STATUS_LABELS: Record<string, string> = {
  Submitted: 'Onay Bekliyor',
  Approved: 'Onaylandı',
  Rejected: 'Reddedildi',
  None: 'Plan Yok',
  Draft: 'Taslak',
};

export const PLAN_STATUS_VARIANTS: Record<
  string,
  'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning'
> = {
  Submitted: 'warning',
  Approved: 'success',
  Rejected: 'destructive',
  None: 'secondary',
  Draft: 'secondary',
};
