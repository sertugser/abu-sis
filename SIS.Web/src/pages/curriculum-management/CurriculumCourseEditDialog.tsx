import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Loader2 } from 'lucide-react';
import {
  useAdminCourseDetail,
  useUpdateCourse,
  useCourseCurriculumUsage,
} from '../../hooks/useAdminCourseCatalog';
import { useUpdateCurriculumCourse } from '../../hooks/useCurriculumManagement';
import type { CurriculumCourseItemDto } from '../../types/curriculumManagement';
import BasicInfoSection from './course-form/BasicInfoSection';
import ScheduleSection from './course-form/ScheduleSection';
import ClassificationSection from './course-form/ClassificationSection';
import CurriculumSpecificSection from './course-form/CurriculumSpecificSection';
import DescriptionSection from './course-form/DescriptionSection';
import FlagsSection from './course-form/FlagsSection';
import DecisionSection from './course-form/DecisionSection';
import IntibakSection from './course-form/IntibakSection';
import SideEffectWarningDialog, { type FieldChange } from './course-form/SideEffectWarningDialog';
import type {
  CourseBasicState,
  CourseScheduleState,
  CourseClassificationState,
  CurriculumSpecificState,
  CourseDescriptionState,
  CourseFlagsState,
  CourseDecisionState,
  CourseIntibakState,
} from './course-form/types';

interface Props {
  open: boolean;
  onClose: () => void;
  curriculumId: number;
  /** The Course being edited (must already exist in catalog and be assigned to this curriculum) */
  courseId: number;
  /** The CurriculumCourse row (curriculum-specific values) */
  curriculumCourse: CurriculumCourseItemDto;
  onSaved?: () => void;
}

/**
 * Edit-only dialog for a course inside a curriculum.
 * Combines Course (shared) and CurriculumCourse (curriculum-specific) edits in one wide form,
 * Proliz B style. Adding a new course to a curriculum continues through AddCourseDialog
 * (search + minimal add) — that flow is untouched.
 */
export default function CurriculumCourseEditDialog({
  open, onClose, curriculumId, courseId, curriculumCourse, onSaved,
}: Props) {
  // Load Course catalog details for shared fields (Course table values)
  const { data: courseDetail, isLoading } = useAdminCourseDetail(open ? courseId : null);

  // ── Section state ──
  const [basic, setBasic] = useState<CourseBasicState>({
    code: '', name: '', englishName: '', alternativeName: '', showInInfoPackage: true,
  });
  const [schedule, setSchedule] = useState<CourseScheduleState>({
    ects: 0, nationalCredit: 0, theoryHours: 0, labHours: 0, applicationHours: null,
  });
  const [classification, setClassification] = useState<CourseClassificationState>({
    language: 'Turkish',
    teachingMethod: 'FaceToFace',
    examType: null,
    isNonGpa: false,
    status: 'Active',
    level: 'Undergraduate',
    openType: null,
  });
  const [curriculumSpecific, setCurriculumSpecific] = useState<CurriculumSpecificState>({
    suggestedSemester: 1,
    isCompulsory: true,
    includeInGpa: true,
    showOnTranscript: true,
    categoryId: null,
    openingReasonId: null,
    curriculumDescription: '',
  });
  const [description, setDescription] = useState<CourseDescriptionState>({ description: '' });
  const [flags, setFlags] = useState<CourseFlagsState>({
    isMainGroupCourse: false,
    isFormationCourse: false,
    excludedFromCreditTotal: false,
    excludedFromTranscriptTotal: false,
    autoAddOnNewRegistration: false,
    showAtTakenCurriculum: false,
    showInstructorAtRegistration: true,
    hasCustomExamCurriculum: false,
  });
  const [decision, setDecision] = useState<CourseDecisionState>({
    decisionDate: '',
    decisionNo: '',
    decisionNote: '',
  });
  const [intibak, setIntibak] = useState<CourseIntibakState>({
    previousCourseId: null,
    previousCourseCode: null,
    previousCourseName: null,
  });

  // ── Initialize on open ──
  useEffect(() => {
    if (!open || !courseDetail) return;

    setBasic({
      code: courseDetail.code,
      name: courseDetail.name,
      englishName: courseDetail.englishName ?? '',
      alternativeName: courseDetail.alternativeName ?? '',
      showInInfoPackage: courseDetail.showInInfoPackage,
    });
    setSchedule({
      ects: courseDetail.ects,
      nationalCredit: courseDetail.nationalCredit,
      theoryHours: courseDetail.theoryHours,
      labHours: courseDetail.labHours,
      applicationHours: courseDetail.applicationHours,
    });
    // CourseDetailDto types language/teachingMethod/status/level as number, but with
    // JsonStringEnumConverter the runtime values are strings ("Turkish", "FaceToFace", …).
    // Cast through unknown to align runtime with our string-literal state types.
    setClassification({
      language: courseDetail.language as unknown as CourseClassificationState['language'],
      teachingMethod: courseDetail.teachingMethod as unknown as CourseClassificationState['teachingMethod'],
      examType: courseDetail.examType,
      isNonGpa: courseDetail.isNonGpa,
      status: courseDetail.status as unknown as CourseClassificationState['status'],
      level: courseDetail.level as unknown as CourseClassificationState['level'],
      openType: courseDetail.openType,
    });
    setCurriculumSpecific({
      suggestedSemester: curriculumCourse.suggestedSemester,
      isCompulsory: curriculumCourse.isCompulsory,
      includeInGpa: curriculumCourse.includeInGpa,
      showOnTranscript: curriculumCourse.showOnTranscript,
      categoryId: curriculumCourse.categoryId,
      openingReasonId: curriculumCourse.openingReasonId,
      curriculumDescription: curriculumCourse.curriculumDescription ?? '',
    });
    setDescription({ description: courseDetail.description ?? '' });
    setFlags({
      isMainGroupCourse: curriculumCourse.isMainGroupCourse,
      isFormationCourse: curriculumCourse.isFormationCourse,
      excludedFromCreditTotal: curriculumCourse.excludedFromCreditTotal,
      excludedFromTranscriptTotal: curriculumCourse.excludedFromTranscriptTotal,
      autoAddOnNewRegistration: curriculumCourse.autoAddOnNewRegistration,
      showAtTakenCurriculum: curriculumCourse.showAtTakenCurriculum,
      showInstructorAtRegistration: curriculumCourse.showInstructorAtRegistration,
      hasCustomExamCurriculum: curriculumCourse.hasCustomExamCurriculum,
    });
    setDecision({
      decisionDate: curriculumCourse.decisionDate ? curriculumCourse.decisionDate.slice(0, 10) : '',
      decisionNo: curriculumCourse.decisionNo ?? '',
      decisionNote: curriculumCourse.decisionNote ?? '',
    });
    setIntibak({
      previousCourseId: curriculumCourse.previousCourseId,
      previousCourseCode: curriculumCourse.previousCourseCode,
      previousCourseName: curriculumCourse.previousCourseName,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, courseDetail?.id, curriculumCourse.courseId]);

  // ── Mutations ──
  const updateCourseMut = useUpdateCourse();
  const updateCurrMut = useUpdateCurriculumCourse();
  const isSaving = updateCourseMut.isPending || updateCurrMut.isPending;

  // ── Side-effect warning state (Faz 1C) ──
  const [pendingChanges, setPendingChanges] = useState<FieldChange[] | null>(null);
  const { data: curriculumUsage = [] } = useCourseCurriculumUsage(
    pendingChanges !== null ? courseId : null,
  );

  /** Detects which Course-table (shared) fields the user has modified. */
  function detectSharedChanges(): FieldChange[] {
    if (!courseDetail) return [];
    const c: FieldChange[] = [];
    if (basic.name !== courseDetail.name)
      c.push({ label: 'Ders Adı', oldValue: courseDetail.name, newValue: basic.name });
    if (basic.englishName !== (courseDetail.englishName ?? ''))
      c.push({ label: 'İngilizce Ad', oldValue: courseDetail.englishName ?? '—', newValue: basic.englishName || '—' });
    if (basic.alternativeName !== (courseDetail.alternativeName ?? ''))
      c.push({ label: 'Diğer Adı', oldValue: courseDetail.alternativeName ?? '—', newValue: basic.alternativeName || '—' });
    if (basic.showInInfoPackage !== courseDetail.showInInfoPackage)
      c.push({ label: 'Bilgi Paketinde Göster', oldValue: courseDetail.showInInfoPackage ? 'Evet' : 'Hayır', newValue: basic.showInInfoPackage ? 'Evet' : 'Hayır' });
    if (schedule.ects !== courseDetail.ects)
      c.push({ label: 'AKTS', oldValue: String(courseDetail.ects), newValue: String(schedule.ects) });
    if (schedule.theoryHours !== courseDetail.theoryHours)
      c.push({ label: 'Teori Saat', oldValue: String(courseDetail.theoryHours), newValue: String(schedule.theoryHours) });
    if (schedule.labHours !== courseDetail.labHours)
      c.push({ label: 'Lab Saat', oldValue: String(courseDetail.labHours), newValue: String(schedule.labHours) });
    if ((schedule.applicationHours ?? null) !== (courseDetail.applicationHours ?? null))
      c.push({
        label: 'Uygulama Saat',
        oldValue: courseDetail.applicationHours != null ? String(courseDetail.applicationHours) : '—',
        newValue: schedule.applicationHours != null ? String(schedule.applicationHours) : '—',
      });
    if (schedule.nationalCredit !== courseDetail.nationalCredit)
      c.push({
        label: 'Kredi',
        oldValue: String(courseDetail.nationalCredit),
        newValue: String(schedule.nationalCredit),
      });
    if (description.description !== (courseDetail.description ?? ''))
      c.push({ label: 'Açıklama', oldValue: '(önceki)', newValue: '(yeni)' });
    // language/teachingMethod/status/level use string-literal state (runtime strings via JsonStringEnumConverter);
    // courseDetail typed as number but holds strings at runtime — compare as unknown.
    if (classification.language !== (courseDetail.language as unknown as string))
      c.push({ label: 'Dil', oldValue: String(courseDetail.language), newValue: classification.language });
    if (classification.teachingMethod !== (courseDetail.teachingMethod as unknown as string))
      c.push({ label: 'Öğretim Şekli', oldValue: String(courseDetail.teachingMethod), newValue: classification.teachingMethod });
    if ((classification.examType ?? null) !== (courseDetail.examType ?? null))
      c.push({ label: 'Sınav Değer Tipi', oldValue: courseDetail.examType ?? '—', newValue: classification.examType ?? '—' });
    if ((classification.openType ?? null) !== (courseDetail.openType ?? null))
      c.push({
        label: 'Açma Tipi',
        oldValue: courseDetail.openType ?? '—',
        newValue: classification.openType ?? '—',
      });
    return c;
  }

  async function handleSave() {
    if (!courseDetail) return;

    const sharedChanges = detectSharedChanges();
    if (sharedChanges.length > 0) {
      // Defer save — show confirmation dialog first
      setPendingChanges(sharedChanges);
      return;
    }

    // No shared changes — save curriculum-specific only (no warning needed)
    await performSave();
  }

  /** Actually executes both Course + CurriculumCourse updates. Called either directly
   *  (when no shared changes) or after the user confirms the side-effect dialog. */
  async function performSave() {
    if (!courseDetail) return;

    const promises: Promise<unknown>[] = [];

    // Course-level update (shared fields) — only run if anything actually changed
    const sharedChanges = detectSharedChanges();
    if (sharedChanges.length > 0) {
      promises.push(
        updateCourseMut.mutateAsync({
          id: courseDetail.id,
          code: courseDetail.code,
          name: basic.name,
          englishName: basic.englishName || null,
          alternativeName: basic.alternativeName || null,
          showInInfoPackage: basic.showInInfoPackage,
          ects: schedule.ects,
          theoryHours: schedule.theoryHours,
          labHours: schedule.labHours,
          applicationHours: schedule.applicationHours,
          nationalCredit: schedule.nationalCredit,
          isNonGpa: courseDetail.isNonGpa,
          examType: classification.examType,
          departmentId: courseDetail.departmentId,
          language: classification.language,
          teachingMethod: classification.teachingMethod,
          status: classification.status,
          description: description.description || null,
          level: classification.level,
          openType: classification.openType,
          minCreditsRequired: courseDetail.minCreditsRequired,
          minEctsRequired: courseDetail.minEctsRequired,
          requiresNoFailedLowerCourses: courseDetail.requiresNoFailedLowerCourses,
          canBeTakenStandalone: courseDetail.canBeTakenStandalone,
        }),
      );
    }

    // CurriculumCourse-level update (curriculum-specific fields) — always
    promises.push(
      updateCurrMut.mutateAsync({
        curriculumId,
        courseId,
        suggestedSemester: curriculumSpecific.suggestedSemester,
        isCompulsory: curriculumSpecific.isCompulsory,
        includeInGpa: curriculumSpecific.includeInGpa,
        showOnTranscript: curriculumSpecific.showOnTranscript,
        categoryId: curriculumSpecific.categoryId ?? undefined,
        openingReasonId: curriculumSpecific.openingReasonId ?? undefined,
        clearCategory: curriculumSpecific.categoryId === null,
        clearOpeningReason: curriculumSpecific.openingReasonId === null,
        // Phase 2 — flags
        isMainGroupCourse: flags.isMainGroupCourse,
        isFormationCourse: flags.isFormationCourse,
        excludedFromCreditTotal: flags.excludedFromCreditTotal,
        excludedFromTranscriptTotal: flags.excludedFromTranscriptTotal,
        autoAddOnNewRegistration: flags.autoAddOnNewRegistration,
        showAtTakenCurriculum: flags.showAtTakenCurriculum,
        showInstructorAtRegistration: flags.showInstructorAtRegistration,
        hasCustomExamCurriculum: flags.hasCustomExamCurriculum,
        // Phase 2 — decision metadata (empty string maps to null via Clear flag)
        decisionDate: decision.decisionDate || undefined,
        clearDecisionDate: decision.decisionDate === '',
        decisionNo: decision.decisionNo,
        decisionNote: decision.decisionNote,
        // Phase 2 — curriculum-specific description
        curriculumDescription: curriculumSpecific.curriculumDescription,
        clearCurriculumDescription: curriculumSpecific.curriculumDescription === '',
        // Phase 3 — İntibak (null = clear)
        previousCourseId: intibak.previousCourseId ?? undefined,
        clearPreviousCourse: intibak.previousCourseId === null,
      }),
    );

    await Promise.all(promises);
    setPendingChanges(null);
    onSaved?.();
    onClose();
  }

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <ResizableDialogContent
        defaultWidth={780} minWidth={600} minHeight={500}
        className="flex flex-col p-0 gap-0"
      >
        <DialogHeader className="px-6 pt-5 pb-3 shrink-0 border-b">
          <DialogTitle>
            Müfredat Dersi Düzenleme — {basic.code} {basic.name}
          </DialogTitle>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-6">
          {isLoading ? (
            <div className="text-center py-8 text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin mx-auto" />
            </div>
          ) : (
            <>
              <BasicInfoSection value={basic} onChange={setBasic} codeReadonly />
              <ScheduleSection value={schedule} onChange={setSchedule} />
              <ClassificationSection
                classification={classification}
                onClassificationChange={setClassification}
                curriculumSpecific={curriculumSpecific}
                onCurriculumSpecificChange={setCurriculumSpecific}
              />
              <CurriculumSpecificSection
                value={curriculumSpecific}
                onChange={setCurriculumSpecific}
              />
              <FlagsSection value={flags} onChange={setFlags} />
              <DecisionSection value={decision} onChange={setDecision} />
              <IntibakSection
                value={intibak}
                onChange={setIntibak}
                currentCourseId={courseId}
                departmentId={courseDetail?.departmentId ?? null}
              />
              <DescriptionSection
                value={description.description}
                onChange={(d) => setDescription({ description: d })}
              />
            </>
          )}
        </div>

        <DialogFooter className="shrink-0 px-6 py-4 border-t bg-background">
          <Button variant="outline" onClick={onClose}>İptal</Button>
          <Button onClick={handleSave} disabled={isSaving}>
            {isSaving && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Kaydet
          </Button>
        </DialogFooter>
      </ResizableDialogContent>

      {/* Side-effect warning — shown when shared (Course-table) fields change */}
      <SideEffectWarningDialog
        open={pendingChanges !== null}
        changes={pendingChanges ?? []}
        curriculumUsage={curriculumUsage}
        isSaving={isSaving}
        onConfirm={performSave}
        onCancel={() => setPendingChanges(null)}
      />
    </Dialog>
  );
}
