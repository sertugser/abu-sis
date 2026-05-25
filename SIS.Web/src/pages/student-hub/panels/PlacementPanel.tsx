import { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  usePlacement,
  useUpdatePlacement,
  type StudentPlacement,
  type UpdatePlacement,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import { toNullable } from '../panelUtils';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const INPUT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

const ENTRANCE_TYPE_LABELS: Record<string, string> = {
  Osym: 'ÖSYM/YKS',
  Dgs: 'DGS',
  HorizontalTransfer: 'Yatay Geçiş',
  InternationalStudent: 'Yabancı Uyruklu',
  SpecialTalent: 'Özel Yetenek',
  Other: 'Diğer',
};

interface PlacementFields {
  entranceType: string;
  scoreType: string;
  score: string;
  rankOrder: string;
  quotaType: string;
  placementYear: string;
  preferenceOrder: string;
  placedProgramName: string;
  note: string;
}

const EMPTY: PlacementFields = {
  entranceType: '',
  scoreType: '',
  score: '',
  rankOrder: '',
  quotaType: '',
  placementYear: '',
  preferenceOrder: '',
  placedProgramName: '',
  note: '',
};

function numToInput(value: number | null): string {
  return value == null ? '' : String(value);
}

function seedFrom(data: StudentPlacement): PlacementFields {
  return {
    entranceType: data.entranceType ?? '',
    scoreType: data.scoreType ?? '',
    score: numToInput(data.score),
    rankOrder: numToInput(data.rankOrder),
    quotaType: data.quotaType ?? '',
    placementYear: numToInput(data.placementYear),
    preferenceOrder: numToInput(data.preferenceOrder),
    placedProgramName: data.placedProgramName ?? '',
    note: data.note ?? '',
  };
}

/** Parse an input string into number|null (empty string → null). */
function parseNumber(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  const parsed = Number(trimmed);
  return Number.isNaN(parsed) ? null : parsed;
}

export default function PlacementPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-placement');

  const studentId = context?.student.id ?? null;
  const { data: loaded } = usePlacement(studentId);
  const update = useUpdatePlacement();

  const [form, setForm] = useState<PlacementFields>(EMPTY);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Re-seed when navigating between student cards AND when the query data arrives.
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    setForm(loaded ? seedFrom(loaded) : EMPTY);
    setError(null);
    setSuccess(null);
  }, [studentId, loaded]);
  /* eslint-enable react-hooks/set-state-in-effect */

  if (!context) return null;
  const { student } = context;

  const setField = (key: keyof PlacementFields) => (value: string) => {
    if (success) setSuccess(null);
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSave = async () => {
    setError(null);
    setSuccess(null);
    const dto: UpdatePlacement = {
      entranceType: form.entranceType === '' ? null : form.entranceType,
      scoreType: toNullable(form.scoreType),
      score: parseNumber(form.score),
      rankOrder: parseNumber(form.rankOrder),
      quotaType: toNullable(form.quotaType),
      placementYear: parseNumber(form.placementYear),
      preferenceOrder: parseNumber(form.preferenceOrder),
      placedProgramName: toNullable(form.placedProgramName),
      note: toNullable(form.note),
    };
    try {
      await update.mutateAsync({ studentId: student.id, dto });
      setSuccess('Yerleştirme bilgileri güncellendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Güncellenemedi.');
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      <div className="grid grid-cols-2 gap-x-6 gap-y-4">
        <div>
          <label className={FIELD_LABEL} htmlFor="field-entranceType">
            Giriş Türü
          </label>
          <select
            id="field-entranceType"
            className={INPUT_CLASS}
            value={form.entranceType}
            disabled={!canEdit}
            onChange={(e) => setField('entranceType')(e.target.value)}
          >
            <option value="">— Seçiniz —</option>
            {Object.entries(ENTRANCE_TYPE_LABELS).map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-scoreType">
            Puan Türü
          </label>
          <input
            id="field-scoreType"
            type="text"
            placeholder="SAY / SÖZ / EA"
            className={INPUT_CLASS}
            value={form.scoreType}
            disabled={!canEdit}
            onChange={(e) => setField('scoreType')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-score">
            Puan
          </label>
          <input
            id="field-score"
            type="number"
            step="any"
            className={INPUT_CLASS}
            value={form.score}
            disabled={!canEdit}
            onChange={(e) => setField('score')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-rankOrder">
            Başarı Sırası
          </label>
          <input
            id="field-rankOrder"
            type="number"
            step="1"
            className={INPUT_CLASS}
            value={form.rankOrder}
            disabled={!canEdit}
            onChange={(e) => setField('rankOrder')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-quotaType">
            Kontenjan Türü
          </label>
          <input
            id="field-quotaType"
            type="text"
            className={INPUT_CLASS}
            value={form.quotaType}
            disabled={!canEdit}
            onChange={(e) => setField('quotaType')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-placementYear">
            Yerleştirme Yılı
          </label>
          <input
            id="field-placementYear"
            type="number"
            step="1"
            className={INPUT_CLASS}
            value={form.placementYear}
            disabled={!canEdit}
            onChange={(e) => setField('placementYear')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-preferenceOrder">
            Tercih Sırası
          </label>
          <input
            id="field-preferenceOrder"
            type="number"
            step="1"
            className={INPUT_CLASS}
            value={form.preferenceOrder}
            disabled={!canEdit}
            onChange={(e) => setField('preferenceOrder')(e.target.value)}
          />
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-placedProgramName">
            Yerleştiği Program
          </label>
          <input
            id="field-placedProgramName"
            type="text"
            className={INPUT_CLASS}
            value={form.placedProgramName}
            disabled={!canEdit}
            onChange={(e) => setField('placedProgramName')(e.target.value)}
          />
        </div>

        <div className="col-span-2">
          <label className={FIELD_LABEL} htmlFor="field-note">
            Not
          </label>
          <textarea
            id="field-note"
            rows={3}
            className={INPUT_CLASS}
            value={form.note}
            disabled={!canEdit}
            onChange={(e) => setField('note')(e.target.value)}
          />
        </div>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {canEdit && (
        <Button onClick={() => void handleSave()} disabled={update.isPending}>
          {update.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      )}
    </div>
  );
}
