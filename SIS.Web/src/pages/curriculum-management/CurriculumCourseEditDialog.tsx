import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Loader2 } from 'lucide-react';
import {
  useAdminCourseDetail,
  useUpdateCourse,
} from '../../hooks/useAdminCourseCatalog';
import { useUpdateCurriculumCourse } from '../../hooks/useCurriculumManagement';
import type { CurriculumCourseItemDto } from '../../types/curriculumManagement';
import BasicInfoSection from './course-form/BasicInfoSection';
import ScheduleSection from './course-form/ScheduleSection';
import ClassificationSection from './course-form/ClassificationSection';
import CurriculumSpecificSection from './course-form/CurriculumSpecificSection';
import GroupSection from './course-form/GroupSection';
import DescriptionSection from './course-form/DescriptionSection';
import type {
  CourseBasicState,
  CourseScheduleState,
  CourseClassificationState,
  CurriculumSpecificState,
  CourseDescriptionState,
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
    code: '', name: '', englishName: '',
  });
  const [schedule, setSchedule] = useState<CourseScheduleState>({
    ects: 0, nationalCredit: null, theoryHours: 0, labHours: 0,
  });
  const [classification, setClassification] = useState<CourseClassificationState>({
    language: 'Turkish',
    teachingMethod: 'FaceToFace',
    examType: null,
    isNonGpa: false,
    status: 'Active',
    level: 'Undergraduate',
  });
  const [curriculumSpecific, setCurriculumSpecific] = useState<CurriculumSpecificState>({
    suggestedSemester: 1,
    isCompulsory: true,
    includeInGpa: true,
    showOnTranscript: true,
    categoryId: null,
    openingReasonId: null,
    groupCode: '',
  });
  const [description, setDescription] = useState<CourseDescriptionState>({ description: '' });

  // ── Initialize on open ──
  useEffect(() => {
    if (!open || !courseDetail) return;

    setBasic({
      code: courseDetail.code,
      name: courseDetail.name,
      englishName: courseDetail.englishName ?? '',
    });
    setSchedule({
      ects: courseDetail.ects,
      nationalCredit: courseDetail.nationalCredit,
      theoryHours: courseDetail.theoryHours,
      labHours: courseDetail.labHours,
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
    });
    setCurriculumSpecific({
      suggestedSemester: curriculumCourse.suggestedSemester,
      isCompulsory: curriculumCourse.isCompulsory,
      includeInGpa: curriculumCourse.includeInGpa,
      showOnTranscript: curriculumCourse.showOnTranscript,
      categoryId: curriculumCourse.categoryId,
      openingReasonId: curriculumCourse.openingReasonId,
      groupCode: curriculumCourse.groupCode ?? '',
    });
    setDescription({ description: courseDetail.description ?? '' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, courseDetail?.id, curriculumCourse.courseId]);

  // ── Mutations ──
  const updateCourseMut = useUpdateCourse();
  const updateCurrMut = useUpdateCurriculumCourse();
  const isSaving = updateCourseMut.isPending || updateCurrMut.isPending;

  async function handleSave() {
    if (!courseDetail) return;

    const promises: Promise<unknown>[] = [];

    // Course-level update (shared fields)
    promises.push(
      updateCourseMut.mutateAsync({
        id: courseDetail.id,
        code: courseDetail.code,
        name: basic.name,
        englishName: basic.englishName || null,
        ects: schedule.ects,
        theoryHours: schedule.theoryHours,
        labHours: schedule.labHours,
        nationalCredit: schedule.nationalCredit,
        isNonGpa: courseDetail.isNonGpa,
        examType: classification.examType,
        departmentId: courseDetail.departmentId,
        language: classification.language,
        teachingMethod: classification.teachingMethod,
        status: classification.status,
        description: description.description || null,
        level: classification.level,
        minCreditsRequired: courseDetail.minCreditsRequired,
        minEctsRequired: courseDetail.minEctsRequired,
        requiresNoFailedLowerCourses: courseDetail.requiresNoFailedLowerCourses,
        canBeTakenStandalone: courseDetail.canBeTakenStandalone,
      }),
    );

    // CurriculumCourse-level update (curriculum-specific fields)
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
        groupCode: curriculumSpecific.groupCode || undefined,
        clearCategory: curriculumSpecific.categoryId === null,
        clearOpeningReason: curriculumSpecific.openingReasonId === null,
      }),
    );

    await Promise.all(promises);
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
              <GroupSection
                groupCode={curriculumSpecific.groupCode}
                onGroupCodeChange={(g) => setCurriculumSpecific({ ...curriculumSpecific, groupCode: g })}
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
    </Dialog>
  );
}
