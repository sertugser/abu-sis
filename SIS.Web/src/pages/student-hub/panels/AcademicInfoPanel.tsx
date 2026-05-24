import { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { usePermissions } from '../../../context/PermissionContext';
import {
  useUpdateStudentAcademic,
  type UpdateStudentAcademic,
} from '../../../hooks/useAdminStudents';
import {
  useAdminPrograms,
  useAdminCurricula,
  useAdminAdvisors,
} from '../../../hooks/useAdminLookup';
import { STUDENT_STATUS_LABELS } from '../../../lib/constants';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
} from '@/components/ui/alert-dialog';

type ProgramType = 'Main' | 'DoubleMajor' | 'Minor';

const PROGRAM_TYPE_LABELS: Record<string, string> = {
  Main: 'Ana Dal',
  DoubleMajor: 'Çift Anadal',
  Minor: 'Yan Dal',
};

const DESTRUCTIVE_STATUSES = ['Dismissed', 'Graduated', 'Suspended'];

const FIELD_LABEL = 'text-xs text-muted-foreground font-medium uppercase tracking-wide';
const SELECT_CLASS =
  'mt-1 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60';

export default function AcademicInfoPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-academic');

  const updateAcademic = useUpdateStudentAcademic();

  // Hooks must be called unconditionally; seed below guards on context.
  const [status, setStatus] = useState(context?.student.status ?? 'Active');
  const [advisorId, setAdvisorId] = useState<number | undefined>(context?.student.advisorId);
  const [programId, setProgramId] = useState<number | undefined>(
    context?.student.programId ?? undefined,
  );
  const [curriculumId, setCurriculumId] = useState<number | undefined>(
    context?.student.curriculumId ?? undefined,
  );
  const [programType, setProgramType] = useState<ProgramType>(
    (context?.student.programType as ProgramType) || 'Main',
  );
  const [isScholarshipStudent, setIsScholarshipStudent] = useState(
    context?.student.isScholarshipStudent ?? false,
  );
  const [hasFinancialBlock, setHasFinancialBlock] = useState(
    context?.student.hasFinancialBlock ?? false,
  );

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const { data: programs = [] } = useAdminPrograms();
  const { data: curricula = [] } = useAdminCurricula(programId);
  const { data: advisors = [] } = useAdminAdvisors();

  // Re-seed all form fields when navigating between student cards (panel stays mounted).
  // Intentional id-keyed reseed: a single synchronous pass keyed on student.id, not a render loop.
  /* eslint-disable react-hooks/set-state-in-effect */
  useEffect(() => {
    if (!context) return;
    const { student } = context;
    setStatus(student.status);
    setAdvisorId(student.advisorId);
    setProgramId(student.programId ?? undefined);
    setCurriculumId(student.curriculumId ?? undefined);
    setProgramType((student.programType as ProgramType) || 'Main');
    setIsScholarshipStudent(student.isScholarshipStudent);
    setHasFinancialBlock(student.hasFinancialBlock);
    setError(null);
    setSuccess(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [context?.student.id]);
  /* eslint-enable react-hooks/set-state-in-effect */

  if (!context) return null;
  const { student } = context;

  const resetMessages = () => {
    setError(null);
    setSuccess(null);
  };

  // Clear the lingering "güncellendi" message once the user starts editing again.
  const markDirty = () => {
    if (success) setSuccess(null);
  };

  const performUpdate = async () => {
    if (programId == null || curriculumId == null) {
      setError('Program ve müfredat seçilmelidir.');
      return;
    }
    const dto: UpdateStudentAcademic = {
      status,
      advisorId,
      programId,
      curriculumId,
      programType,
      isScholarshipStudent,
      hasFinancialBlock,
    };
    try {
      await updateAcademic.mutateAsync({ studentId: student.id, dto });
      setSuccess('Akademik bilgiler güncellendi.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Güncellenemedi.');
    }
  };

  const handleSave = () => {
    resetMessages();
    if (programId == null || curriculumId == null) {
      setError('Program ve müfredat seçilmelidir.');
      return;
    }
    const isDestructiveChange =
      DESTRUCTIVE_STATUSES.includes(status) && status !== student.status;
    if (isDestructiveChange) {
      setConfirmOpen(true);
      return;
    }
    void performUpdate();
  };

  const handleProgramChange = (value: string) => {
    markDirty();
    const next = value ? Number(value) : undefined;
    setProgramId(next);
    setCurriculumId(undefined); // stale curriculum guard
  };

  return (
    <div className="max-w-2xl space-y-6">
      <div className="grid grid-cols-2 gap-x-6 gap-y-4">
        <div>
          <label className={FIELD_LABEL} htmlFor="field-status">
            Durum
          </label>
          <select
            id="field-status"
            className={SELECT_CLASS}
            value={status}
            disabled={!canEdit}
            onChange={(e) => {
              markDirty();
              setStatus(e.target.value);
            }}
          >
            {Object.entries(STUDENT_STATUS_LABELS).map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-program-type">
            Program Tipi
          </label>
          <select
            id="field-program-type"
            className={SELECT_CLASS}
            value={programType}
            disabled={!canEdit}
            onChange={(e) => {
              markDirty();
              setProgramType(e.target.value as ProgramType);
            }}
          >
            {Object.entries(PROGRAM_TYPE_LABELS).map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-program">
            Program
          </label>
          <select
            id="field-program"
            className={SELECT_CLASS}
            value={programId ?? ''}
            disabled={!canEdit}
            onChange={(e) => handleProgramChange(e.target.value)}
          >
            <option value="">Seçiniz</option>
            {programs.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className={FIELD_LABEL} htmlFor="field-curriculum">
            Müfredat
          </label>
          <select
            id="field-curriculum"
            className={SELECT_CLASS}
            value={curriculumId ?? ''}
            disabled={!canEdit || programId == null}
            onChange={(e) => {
              markDirty();
              setCurriculumId(e.target.value ? Number(e.target.value) : undefined);
            }}
          >
            <option value="">Seçiniz</option>
            {curricula.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </div>

        <div className="col-span-2">
          <label className={FIELD_LABEL} htmlFor="field-advisor">
            Danışman
          </label>
          <select
            id="field-advisor"
            className={SELECT_CLASS}
            value={advisorId ?? ''}
            disabled={!canEdit}
            onChange={(e) => {
              markDirty();
              setAdvisorId(e.target.value ? Number(e.target.value) : undefined);
            }}
          >
            <option value="">Danışman yok</option>
            {advisors.map((a) => (
              <option key={a.id} value={a.id}>
                {a.fullName} — {a.departmentName}
              </option>
            ))}
          </select>
        </div>

        <label className="flex items-center gap-2 text-sm" htmlFor="field-scholarship">
          <input
            id="field-scholarship"
            type="checkbox"
            className="h-4 w-4 rounded border-input"
            checked={isScholarshipStudent}
            disabled={!canEdit}
            onChange={(e) => {
              markDirty();
              setIsScholarshipStudent(e.target.checked);
            }}
          />
          Burslu
        </label>

        <label className="flex items-center gap-2 text-sm" htmlFor="field-financial-block">
          <input
            id="field-financial-block"
            type="checkbox"
            className="h-4 w-4 rounded border-input"
            checked={hasFinancialBlock}
            disabled={!canEdit}
            onChange={(e) => {
              markDirty();
              setHasFinancialBlock(e.target.checked);
            }}
          />
          Finansal Blok
        </label>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {success && <p className="text-sm text-emerald-600">{success}</p>}

      {canEdit && (
        <Button onClick={handleSave} disabled={updateAcademic.isPending || confirmOpen}>
          {updateAcademic.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      )}

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Durum değişikliği onayı</AlertDialogTitle>
            <AlertDialogDescription>
              Öğrenciyi &apos;{STUDENT_STATUS_LABELS[status] ?? status}&apos; durumuna almak
              üzeresiniz. Onaylıyor musunuz?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction onClick={() => void performUpdate()}>Onayla</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
