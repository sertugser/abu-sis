import { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import { useAssignAdvisor } from '../../../hooks/useAdminStudents';
import { useAdminAdvisors } from '../../../hooks/useAdminLookup';
import { Button } from '@/components/ui/button';

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const SELECT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

export default function AdvisorPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canAssign = hasPermission('menu.toplu-danisman-atama.assign');

  const assignAdvisor = useAssignAdvisor();
  const { data: advisors = [] } = useAdminAdvisors();

  // Hooks must be called unconditionally; seed below guards on context.
  const [advisorId, setAdvisorId] = useState<number | undefined>(context?.student.advisorId);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Re-seed when navigating between student cards (panel stays mounted).
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    if (!context) return;
    setAdvisorId(context.student.advisorId);
    setError(null);
    setSuccess(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [context?.student.id]);
  /* eslint-enable react-hooks/set-state-in-effect */

  if (!context) return null;
  const { student } = context;

  // Clear the lingering "güncellendi" message once the user starts editing again.
  const markDirty = () => {
    if (success) setSuccess(null);
  };

  const isUnchanged = advisorId === student.advisorId;
  const hasSelection = advisorId != null;

  const handleSave = async () => {
    setError(null);
    setSuccess(null);
    if (advisorId == null) return;
    try {
      await assignAdvisor.mutateAsync({ studentId: student.id, advisorId });
      setSuccess('Danışman güncellendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Danışman atanamadı.');
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      <div className="space-y-1">
        <span className={FIELD_LABEL}>Mevcut Danışman</span>
        <p className="text-sm">{student.advisorName ?? 'Atanmamış'}</p>
      </div>

      <div>
        <label className={FIELD_LABEL} htmlFor="field-advisor">
          Danışman
        </label>
        <select
          id="field-advisor"
          className={SELECT_CLASS}
          value={advisorId ?? ''}
          disabled={!canAssign}
          onChange={(e) => {
            markDirty();
            setAdvisorId(e.target.value ? Number(e.target.value) : undefined);
          }}
        >
          <option value="">Danışman seçiniz</option>
          {advisors.map((a) => (
            <option key={a.id} value={a.id}>
              {a.fullName} — {a.departmentName}
            </option>
          ))}
        </select>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {canAssign && (
        <Button
          onClick={() => void handleSave()}
          disabled={assignAdvisor.isPending || !hasSelection || isUnchanged}
        >
          {assignAdvisor.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      )}
    </div>
  );
}
