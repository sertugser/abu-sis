import { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useStudentPersonalInfo,
  useUpdateStudentPersonalInfo,
  type StudentPersonalInfo,
  type UpdateStudentPersonalInfo,
} from '../../../hooks/useStudentCard';
import { Button } from '@/components/ui/button';
import { toDateInput, toNullable } from '../panelUtils';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const INPUT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

const MILITARY_STATUS_LABELS: Record<string, string> = {
  NotApplicable: 'Tabi Değil',
  Liable: 'Tabi',
  Deferred: 'Tecilli',
  Exempt: 'Muaf',
  Completed: 'Yapıldı',
};

interface MilitaryFields {
  militaryStatus: string;
  militaryDeferralUntil: string;
  militaryOffice: string;
}

const EMPTY: MilitaryFields = {
  militaryStatus: 'NotApplicable',
  militaryDeferralUntil: '',
  militaryOffice: '',
};

function seedFrom(data: StudentPersonalInfo): MilitaryFields {
  return {
    militaryStatus: data.militaryStatus ?? 'NotApplicable',
    militaryDeferralUntil: toDateInput(data.militaryDeferralUntil),
    militaryOffice: data.militaryOffice ?? '',
  };
}

export default function MilitaryInfoPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-personal');

  const studentId = context?.student.id ?? null;
  const { data: loaded } = useStudentPersonalInfo(studentId);
  const update = useUpdateStudentPersonalInfo();

  const [form, setForm] = useState<MilitaryFields>(EMPTY);
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

  const setField = (key: keyof MilitaryFields) => (value: string) => {
    if (success) setSuccess(null);
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSave = async () => {
    setError(null);
    setSuccess(null);
    if (!loaded) return;
    // Edit only this panel's slice but submit the FULL object so other sections aren't wiped.
    const dto: UpdateStudentPersonalInfo = {
      ...loaded,
      militaryStatus: form.militaryStatus,
      militaryDeferralUntil: toNullable(form.militaryDeferralUntil),
      militaryOffice: toNullable(form.militaryOffice),
    };
    try {
      await update.mutateAsync({ studentId: student.id, dto });
      setSuccess('Askerlik bilgileri güncellendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Güncellenemedi.');
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      <div className="grid grid-cols-2 gap-x-6 gap-y-4">
        <div>
          <label className={FIELD_LABEL} htmlFor="field-militaryStatus">
            Askerlik Durumu
          </label>
          <select
            id="field-militaryStatus"
            className={INPUT_CLASS}
            value={form.militaryStatus}
            disabled={!canEdit}
            onChange={(e) => setField('militaryStatus')(e.target.value)}
          >
            {Object.entries(MILITARY_STATUS_LABELS).map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-militaryDeferralUntil">
            Tecil Tarihi
          </label>
          <input
            id="field-militaryDeferralUntil"
            type="date"
            className={INPUT_CLASS}
            value={form.militaryDeferralUntil}
            disabled={!canEdit}
            onChange={(e) => setField('militaryDeferralUntil')(e.target.value)}
          />
        </div>

        <div className="col-span-2">
          <label className={FIELD_LABEL} htmlFor="field-militaryOffice">
            Askerlik Şubesi
          </label>
          <input
            id="field-militaryOffice"
            type="text"
            className={INPUT_CLASS}
            value={form.militaryOffice}
            disabled={!canEdit}
            onChange={(e) => setField('militaryOffice')(e.target.value)}
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
