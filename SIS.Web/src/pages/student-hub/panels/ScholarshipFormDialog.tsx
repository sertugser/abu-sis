import { useEffect, useState } from 'react';
import {
  type ScholarshipDetail,
  type UpsertScholarship,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { toDateInput, toNullable } from '../panelUtils';
import { SCHOLARSHIP_TYPE_LABELS } from './scholarshipConstants';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const INPUT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

interface ScholarshipFormFields {
  type: string;
  ratePercent: string;
  source: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  note: string;
}

const EMPTY: ScholarshipFormFields = {
  type: 'OsymScholarship',
  ratePercent: '',
  source: '',
  startDate: '',
  endDate: '',
  isActive: true,
  note: '',
};

function seedFrom(record: ScholarshipDetail): ScholarshipFormFields {
  return {
    type: record.type,
    ratePercent: record.ratePercent != null ? String(record.ratePercent) : '',
    source: record.source ?? '',
    startDate: toDateInput(record.startDate),
    endDate: toDateInput(record.endDate),
    isActive: record.isActive,
    note: record.note ?? '',
  };
}

/** Parse a 0–100 rate string to a number, or null when empty/invalid. */
function parseRate(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

interface ScholarshipFormDialogProps {
  open: boolean;
  /** The record being edited, or null when adding a new one. */
  record: ScholarshipDetail | null;
  isSaving: boolean;
  error: string | null;
  onOpenChange: (open: boolean) => void;
  onSubmit: (dto: UpsertScholarship) => void;
}

export default function ScholarshipFormDialog({
  open,
  record,
  isSaving,
  error,
  onOpenChange,
  onSubmit,
}: ScholarshipFormDialogProps) {
  const [form, setForm] = useState<ScholarshipFormFields>(EMPTY);

  const [localError, setLocalError] = useState<string | null>(null);

  // Re-seed whenever the dialog opens (add vs edit a specific record). Keyed on
  // `record?.id` rather than the `record` reference so a fresh parent re-render
  // (e.g. a newly-created record object) does not reset the form mid-edit.
  // exhaustive-deps is intentionally suppressed: keying on the stable id is the
  // point — re-running on every new `record` reference is the bug we are fixing.
  /* eslint-disable react-hooks/set-state-in-effect, react-hooks/exhaustive-deps */
  useEffect(() => {
    if (open) {
      setForm(record ? seedFrom(record) : EMPTY);
      setLocalError(null);
    }
  }, [open, record?.id]);
  /* eslint-enable react-hooks/set-state-in-effect, react-hooks/exhaustive-deps */

  const setField = <K extends keyof ScholarshipFormFields>(
    key: K,
    value: ScholarshipFormFields[K],
  ) => setForm((prev) => ({ ...prev, [key]: value }));

  const handleSubmit = () => {
    const ratePercent = parseRate(form.ratePercent);
    // Immediate client-side feedback before the server round-trip; the backend
    // remains the authoritative validation gate.
    if (ratePercent != null && (ratePercent < 0 || ratePercent > 100)) {
      setLocalError('Burs oranı 0 ile 100 arasında olmalıdır.');
      return;
    }
    setLocalError(null);
    const dto: UpsertScholarship = {
      type: form.type,
      ratePercent,
      source: toNullable(form.source),
      startDate: toNullable(form.startDate),
      endDate: toNullable(form.endDate),
      isActive: form.isActive,
      note: toNullable(form.note),
    };
    onSubmit(dto);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{record ? 'Burs Kaydını Düzenle' : 'Burs Ekle'}</DialogTitle>
          <DialogDescription>
            Detaylı burs kaydı bilgilerini girin. Bu kayıt akademik kayıttaki burs işaretinden
            bağımsızdır.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-2 gap-x-4 gap-y-3">
          <div className="col-span-2">
            <label className={FIELD_LABEL} htmlFor="scholarship-type">
              Tür
            </label>
            <select
              id="scholarship-type"
              className={INPUT_CLASS}
              value={form.type}
              onChange={(e) => setField('type', e.target.value)}
            >
              {Object.entries(SCHOLARSHIP_TYPE_LABELS).map(([code, label]) => (
                <option key={code} value={code}>
                  {label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className={FIELD_LABEL} htmlFor="scholarship-rate">
              Oran (%)
            </label>
            <input
              id="scholarship-rate"
              type="number"
              min={0}
              max={100}
              className={INPUT_CLASS}
              value={form.ratePercent}
              onChange={(e) => setField('ratePercent', e.target.value)}
            />
          </div>

          <div>
            <label className={FIELD_LABEL} htmlFor="scholarship-source">
              Kaynak
            </label>
            <input
              id="scholarship-source"
              type="text"
              className={INPUT_CLASS}
              value={form.source}
              onChange={(e) => setField('source', e.target.value)}
            />
          </div>

          <div>
            <label className={FIELD_LABEL} htmlFor="scholarship-start">
              Başlangıç
            </label>
            <input
              id="scholarship-start"
              type="date"
              className={INPUT_CLASS}
              value={form.startDate}
              onChange={(e) => setField('startDate', e.target.value)}
            />
          </div>

          <div>
            <label className={FIELD_LABEL} htmlFor="scholarship-end">
              Bitiş
            </label>
            <input
              id="scholarship-end"
              type="date"
              className={INPUT_CLASS}
              value={form.endDate}
              onChange={(e) => setField('endDate', e.target.value)}
            />
          </div>

          <label className="col-span-2 flex items-center gap-2 text-sm" htmlFor="scholarship-active">
            <input
              id="scholarship-active"
              type="checkbox"
              className="h-4 w-4 rounded border-input"
              checked={form.isActive}
              onChange={(e) => setField('isActive', e.target.checked)}
            />
            Aktif
          </label>

          <div className="col-span-2">
            <label className={FIELD_LABEL} htmlFor="scholarship-note">
              Not
            </label>
            <textarea
              id="scholarship-note"
              rows={3}
              className={INPUT_CLASS}
              value={form.note}
              onChange={(e) => setField('note', e.target.value)}
            />
          </div>
        </div>

        {(localError || error) && (
          <p className="text-sm text-destructive">{localError || error}</p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isSaving}>
            Vazgeç
          </Button>
          <Button onClick={handleSubmit} disabled={isSaving}>
            {isSaving ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
