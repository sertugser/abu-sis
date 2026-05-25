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
import { useStudentPersonalInfo } from '../../../hooks/useStudentCard';
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
import { cgpaColor } from '../utils';
import { toDateInput, toNullable } from '../panelUtils';
import { PanelLayout } from '../components/panel/PanelLayout';
import { PageSection } from '../components/panel/PageSection';
import { SummaryColumn } from '../components/panel/SummaryColumn';
import {
  FIELD_LABEL,
  INPUT_CLASS,
  SELECT_CLASS,
  SummaryItem,
  MockField,
} from '../components/panel/panelFields';

type ProgramType = 'Main' | 'DoubleMajor' | 'Minor';

const PROGRAM_TYPE_LABELS: Record<string, string> = {
  Main: 'Ana Dal',
  DoubleMajor: 'Çift Anadal',
  Minor: 'Yan Dal',
};

const GENDER_LABELS: Record<string, string> = {
  Male: 'Erkek',
  Female: 'Kadın',
};

const DESTRUCTIVE_STATUSES = ['Dismissed', 'Graduated', 'Suspended'];

function formatTrDate(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  return isNaN(d.getTime()) ? '—' : d.toLocaleDateString('tr-TR');
}

export default function AcademicInfoPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ogrenci-rehberi.edit-academic');

  const updateAcademic = useUpdateStudentAcademic();
  const { data: personalInfo } = useStudentPersonalInfo(context?.student.id ?? null);

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
  const [departureDate, setDepartureDate] = useState(toDateInput(context?.student.departureDate ?? null));

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
    setDepartureDate(toDateInput(student.departureDate ?? null));
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
      departureDate: toNullable(departureDate),
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

  const programName = student.programName ?? '';
  const educationType = programName.includes('Önlisans') ? 'Önlisans' : 'Lisans';
  const cgpaDisplay =
    student.transcriptAccessible === false
      ? 'Kapalı'
      : (student.cgpa?.toFixed(2) ?? '—');

  const aside = (
    <SummaryColumn>
      <SummaryItem label="T.C. Kimlik">
        <span className="font-mono">{student.nationalId ?? '—'}</span>
      </SummaryItem>
      <SummaryItem label="Doğum Tarihi">{formatTrDate(personalInfo?.birthDate)}</SummaryItem>
      <SummaryItem label="Cinsiyet">
        {GENDER_LABELS[personalInfo?.gender ?? ''] ?? '—'}
      </SummaryItem>
      <SummaryItem label="Öğrenci No">
        <span className="font-mono">{student.studentNumber}</span>
      </SummaryItem>
      {student.previousStudentNumber && (
        <SummaryItem label="Önceki No">
          <span className="font-mono">{student.previousStudentNumber}</span>
        </SummaryItem>
      )}
      <SummaryItem label="GNO">
        <span
          className={student.transcriptAccessible === false ? 'text-muted-foreground' : cgpaColor(student.cgpa)}
        >
          {cgpaDisplay}
        </span>
      </SummaryItem>
      <SummaryItem label="Sınıf">
        {student.classYear != null ? `${student.classYear}. Sınıf` : '—'}
      </SummaryItem>
      <SummaryItem label="Kayıt Tarihi">{formatTrDate(student.enrollmentDate)}</SummaryItem>
      <MockField label="YÖKSİS Güncelleme" />
    </SummaryColumn>
  );

  return (
    <PanelLayout aside={aside}>
      <div className="space-y-6">
        <PageSection title="Akademik Bilgiler">
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

          <div className="sm:col-span-2">
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
        </PageSection>

        <PageSection title="Kayıt Bilgileri">
          <div>
            <label className={FIELD_LABEL} htmlFor="field-entrance-type">
              Kayıt Nedeni
            </label>
            <input
              id="field-entrance-type"
              type="text"
              className={INPUT_CLASS}
              value={student.entranceTypeLabel ?? '—'}
              disabled
              readOnly
            />
            <p className="mt-1 text-xs text-muted-foreground">
              Yerleştirme Bilgileri&apos;nden düzenlenir.
            </p>
          </div>

          <div>
            <label className={FIELD_LABEL} htmlFor="field-departure-date">
              Ayrılış Tarihi
            </label>
            <input
              id="field-departure-date"
              type="date"
              className={INPUT_CLASS}
              value={departureDate}
              disabled={!canEdit}
              onChange={(e) => {
                markDirty();
                setDepartureDate(e.target.value);
              }}
            />
          </div>
        </PageSection>

        <PageSection title="Mali Bilgiler">
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
        </PageSection>

        <PageSection title="Diğer Bilgiler (bilgi amaçlı)">
          <MockField label="Dosya No" />
          <MockField label="Öğrenim Tipi" value={educationType} />
          <MockField label="Öğrenci Tipi" value="—" />
          <MockField label="YÖKSİS Not Barem" />
          <MockField label="Ort. Hesap Tipi" value="AKTS" />
          <MockField label="Ders Kayıt İşlem Türü" />
          <MockField label="A.P. Okuma Nedeni" />
          <MockField label="İlave Dönem" value={student.frozenSemesterCount ?? 0} />
          <MockField label="Diğer Danışmanı" />
          <MockField label="Ek Sınav Durumu" />
          <MockField label="Sınav Sayısı" />
        </PageSection>

        {error && <p className="text-sm text-destructive">{error}</p>}
        {success && <p className="text-sm text-emerald-600">{success}</p>}

        {canEdit && (
          <Button onClick={handleSave} disabled={updateAcademic.isPending || confirmOpen}>
            {updateAcademic.isPending ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        )}
      </div>

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
    </PanelLayout>
  );
}
