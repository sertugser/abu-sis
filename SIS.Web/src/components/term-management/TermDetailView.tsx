import type React from 'react';
import { Button } from '@/components/ui/button';
import type { AdminTermDto } from '@/hooks/useAdminTerms';
import { fmt } from './termDate';

export function TermDetailView({ term, onEdit }: { term: AdminTermDto; onEdit: () => void }) {
  const today = new Date();
  const effectiveStart = new Date(term.effectiveStart);
  const effectiveEnd = new Date(term.effectiveEnd);
  const withinEffectiveDates = today >= effectiveStart && today <= effectiveEnd;

  // "Neden Aktif değil?" açıklaması — kullanıcı sık karşılaştığı için görünür yap
  const inactiveReason =
    term.status === 'Active' ? null
    : term.isCancelled ? 'Bu dönem iptal edilmiş. Aktif olabilmesi için önce iptal geri alınmalı.'
    : !term.isActivated ? 'Bu dönem kullanıma kapalı. Aktif dönem olarak kullanılabilmesi için önce kullanıma açılmalı.'
    : !withinEffectiveDates ? `Bugün (${fmt(today.toISOString())}) dönem tarih aralığında değil. Aktif olabilmesi için bugünün ${fmt(term.effectiveStart)} ve ${fmt(term.effectiveEnd)} arasında olması gerekir.`
    : null;

  return (
    <div className="space-y-3 py-2 text-sm">
      <DetailRow label="Ad" value={term.name} />
      <DetailRow label="Kısa Ad" value={term.shortName || '—'} />
      <DetailRow label="İngilizce Ad" value={term.englishName || '—'} />
      <DetailRow label="Kısa İngilizce Ad" value={term.shortEnglishName || '—'} />
      <DetailRow label="Diğer Ad" value={term.otherName || '—'} />
      <DetailRow label="Yıl" value={String(term.year)} />
      <DetailRow label="Tür" value={term.termTypeLabel} />
      <DetailRow label="Kullanım" value={term.isActivated ? 'Kullanıma açık' : 'Kullanıma kapalı'} />
      <DetailRow label="YÖKSİS Durumu" value={term.yoksisStatus === 'Active' ? 'Aktif' : 'Pasif'} />
      <DetailRow
        label="Durum"
        value={
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border ${
            term.status === 'Active' ? 'bg-emerald-100 text-emerald-700 border-emerald-300' :
            term.status === 'Cancelled' ? 'bg-red-100 text-red-700 border-red-300' :
            'bg-slate-100 text-slate-700 border-slate-300'
          }`}>
            {term.statusLabel || term.status}
          </span>
        }
      />
      <DetailRow label="Başlangıç" value={fmt(term.termStart)} />
      <DetailRow label="Bitiş" value={fmt(term.termEnd)} />

      <DetailRow
        label="Akademik Takvim"
        value={term.hasAcademicCalendar ? 'Tanımlı' : 'Eksik'}
      />

      {inactiveReason && (
        <div className="mt-4 p-3 rounded-lg bg-amber-50 border border-amber-200 text-xs text-amber-900">
          <p className="font-semibold mb-1">Neden Aktif değil?</p>
          <p className="mb-2">{inactiveReason}</p>
          {!term.isCancelled && (
            <div className="flex items-center gap-2 pt-1 border-t border-amber-200/60">
              <Button size="sm" variant="outline" onClick={onEdit} className="h-7 text-xs">
                Tarihleri Düzenle
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[140px_1fr] gap-3 items-center">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <span className="text-sm text-foreground">{value}</span>
    </div>
  );
}
