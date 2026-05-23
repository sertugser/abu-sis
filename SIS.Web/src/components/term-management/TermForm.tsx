import type React from 'react';
import { Input } from '@/components/ui/input';
import { TERM_TYPE_OPTIONS } from '@/lib/termTypes';

const YOKSIS_STATUS_OPTIONS = [
  { value: 'Inactive', label: 'Pasif' },
  { value: 'Active', label: 'Aktif' },
] as const;

export interface TermFormState {
  name: string;
  shortName: string;
  englishName: string;
  shortEnglishName: string;
  otherName: string;
  year: number;
  termType: string;
  yoksisStatus: 'Inactive' | 'Active';
  termStart: string;
  termEnd: string;
}

export function TermForm({
  form, setForm, showYearTermType, formError,
}: {
  form: TermFormState;
  setForm: React.Dispatch<React.SetStateAction<TermFormState>>;
  showYearTermType: boolean;
  formError: string;
}) {
  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <label className="text-sm font-medium">Dönem Adı *</label>
        <Input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="örn. 2024-2025 Güz Dönemi" />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <label className="text-sm font-medium">Kısa Ad</label>
          <Input value={form.shortName} onChange={e => setForm(f => ({ ...f, shortName: e.target.value }))} placeholder="örn. 24-25 Güz" />
        </div>
        <div className="space-y-1">
          <label className="text-sm font-medium">İngilizce Ad</label>
          <Input value={form.englishName} onChange={e => setForm(f => ({ ...f, englishName: e.target.value }))} placeholder="örn. Fall 2024-2025" />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <label className="text-sm font-medium">Kısa İngilizce Ad *</label>
          <Input value={form.shortEnglishName} onChange={e => setForm(f => ({ ...f, shortEnglishName: e.target.value }))} placeholder="örn. Fall 24-25" />
        </div>
        <div className="space-y-1">
          <label className="text-sm font-medium">Diğer Ad</label>
          <Input value={form.otherName} onChange={e => setForm(f => ({ ...f, otherName: e.target.value }))} placeholder="Opsiyonel" />
        </div>
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="space-y-1">
          <label className="text-sm font-medium">Yıl *</label>
          <Input
            type="number"
            value={form.year}
            disabled={!showYearTermType}
            title={!showYearTermType ? 'Yıl dönem oluşturulduktan sonra değiştirilemez' : undefined}
            onChange={e => setForm(f => ({ ...f, year: Number(e.target.value) }))}
            min={2000}
            max={2100}
          />
        </div>
        <div className="space-y-1">
          <label className="text-sm font-medium">Tür *</label>
          <select
            value={form.termType}
            onChange={e => setForm(f => ({ ...f, termType: e.target.value }))}
            className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground"
          >
            {TERM_TYPE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </div>
        <div className="space-y-1">
          <label className="text-sm font-medium">YÖKSİS Durumu</label>
          <select
            value={form.yoksisStatus}
            onChange={e => setForm(f => ({ ...f, yoksisStatus: e.target.value as 'Inactive' | 'Active' }))}
            className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background text-foreground"
          >
            {YOKSIS_STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </div>
      </div>

      <div className="border-t border-border pt-3 space-y-2">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Dönem Tarihleri</p>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-sm font-medium">Başlangıç *</label>
            <Input type="date" value={form.termStart} onChange={e => setForm(f => ({ ...f, termStart: e.target.value }))} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Bitiş *</label>
            <Input type="date" value={form.termEnd} onChange={e => setForm(f => ({ ...f, termEnd: e.target.value }))} />
          </div>
        </div>
      </div>

      {formError && <p className="text-sm text-destructive">{formError}</p>}
    </div>
  );
}
