import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { ArrowRight, Loader2, Search } from 'lucide-react';
import { useAdminCourses, type CourseListItemDto } from '../../hooks/useAdminCourseCatalog';
import type { AddCourseRequest } from '../../types/curriculumManagement';
import ClassificationSection from '../../pages/curriculum-management/course-form/ClassificationSection';
import CurriculumSpecificSection from '../../pages/curriculum-management/course-form/CurriculumSpecificSection';
import FlagsSection from '../../pages/curriculum-management/course-form/FlagsSection';
import GroupSection from '../../pages/curriculum-management/course-form/GroupSection';
import DecisionSection from '../../pages/curriculum-management/course-form/DecisionSection';
import IntibakSection from '../../pages/curriculum-management/course-form/IntibakSection';
import type {
  CourseClassificationState,
  CurriculumSpecificState,
  CourseFlagsState,
  CourseDecisionState,
  CourseIntibakState,
} from '../../pages/curriculum-management/course-form/types';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Full payload — selected course id + curriculum-specific fields (Faz 2/3 dahil) */
  onAdd: (req: AddCourseRequest) => void;
  isPending: boolean;
  existingCourseIds: Set<number>;
  catalogRoute?: string;
}

function defaultCurriculumSpecific(): CurriculumSpecificState {
  return {
    suggestedSemester: 1,
    isCompulsory: true,
    includeInGpa: true,
    showOnTranscript: true,
    categoryId: null,
    openingReasonId: null,
    groupCode: '',
    curriculumDescription: '',
  };
}

function defaultClassification(): CourseClassificationState {
  return {
    language: 'Turkish',
    teachingMethod: 'FaceToFace',
    examType: null,
    isNonGpa: false,
    status: 'Active',
    level: 'Undergraduate',
    openType: null,
  };
}

function defaultFlags(): CourseFlagsState {
  return {
    isMainGroupCourse: false,
    isFormationCourse: false,
    excludedFromCreditTotal: false,
    excludedFromTranscriptTotal: false,
    autoAddOnNewRegistration: false,
    showAtTakenCurriculum: false,
    showInstructorAtRegistration: true,
    hasCustomExamCurriculum: false,
  };
}

function defaultDecision(): CourseDecisionState {
  return { decisionDate: '', decisionNo: '', decisionNote: '' };
}

function defaultIntibak(): CourseIntibakState {
  return { previousCourseId: null, previousCourseCode: null, previousCourseName: null };
}

export default function AddCourseDialog({ open, onClose, onAdd, isPending, existingCourseIds, catalogRoute }: Props) {
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<CourseListItemDto | null>(null);

  // Curriculum-specific state (Phase 2/3 dahil)
  const [curriculumSpecific, setCurriculumSpecific] = useState<CurriculumSpecificState>(defaultCurriculumSpecific());
  const [classification, setClassification] = useState<CourseClassificationState>(defaultClassification());
  const [flags, setFlags] = useState<CourseFlagsState>(defaultFlags());
  const [decision, setDecision] = useState<CourseDecisionState>(defaultDecision());
  const [intibak, setIntibak] = useState<CourseIntibakState>(defaultIntibak());

  const { data: courses, isLoading } = useAdminCourses({ search: search.length >= 2 ? search : undefined });
  const filtered = (courses ?? []).filter(c => !existingCourseIds.has(c.id));

  function reset() {
    setSearch('');
    setSelected(null);
    setCurriculumSpecific(defaultCurriculumSpecific());
    setClassification(defaultClassification());
    setFlags(defaultFlags());
    setDecision(defaultDecision());
    setIntibak(defaultIntibak());
  }

  function handleClose() {
    reset();
    onClose();
  }

  function handleAdd() {
    if (!selected) return;
    onAdd({
      courseId: selected.id,
      suggestedSemester: curriculumSpecific.suggestedSemester,
      isCompulsory: curriculumSpecific.isCompulsory,
      includeInGpa: curriculumSpecific.includeInGpa,
      showOnTranscript: curriculumSpecific.showOnTranscript,
      categoryId: curriculumSpecific.categoryId ?? undefined,
      openingReasonId: curriculumSpecific.openingReasonId ?? undefined,
      groupCode: curriculumSpecific.groupCode || undefined,
      // Phase 2 — flags
      isMainGroupCourse: flags.isMainGroupCourse,
      isFormationCourse: flags.isFormationCourse,
      excludedFromCreditTotal: flags.excludedFromCreditTotal,
      excludedFromTranscriptTotal: flags.excludedFromTranscriptTotal,
      autoAddOnNewRegistration: flags.autoAddOnNewRegistration,
      showAtTakenCurriculum: flags.showAtTakenCurriculum,
      showInstructorAtRegistration: flags.showInstructorAtRegistration,
      hasCustomExamCurriculum: flags.hasCustomExamCurriculum,
      // Phase 2 — decision
      decisionDate: decision.decisionDate || undefined,
      decisionNo: decision.decisionNo || undefined,
      decisionNote: decision.decisionNote || undefined,
      // Phase 2 — curriculum description
      curriculumDescription: curriculumSpecific.curriculumDescription || undefined,
      // Phase 3 — intibak
      previousCourseId: intibak.previousCourseId ?? undefined,
    });
  }

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) handleClose(); }}>
      <ResizableDialogContent
        defaultWidth={selected ? 720 : 520}
        minWidth={400}
        minHeight={400}
        className="flex flex-col p-0 gap-0"
      >
        <DialogHeader className="px-6 pt-6 pb-3 shrink-0 border-b">
          <DialogTitle>Müfredata Ders Ekle</DialogTitle>
        </DialogHeader>

        {!selected ? (
          // ── Aşama 1: Katalog'dan ders seç ──
          <div className="flex flex-col flex-1 min-h-0 px-6 pt-4 pb-2 gap-3">
            <div className="relative shrink-0">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Ders kodu veya adı ile arayın..."
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="pl-9"
              />
            </div>
            <div className="flex-1 min-h-0 overflow-y-auto border rounded-md">
              {isLoading ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                </div>
              ) : filtered.length === 0 ? (
                <div className="text-center py-8 space-y-2">
                  <p className="text-sm text-muted-foreground">
                    {search.length < 2 ? 'En az 2 karakter girin' : 'Sonuç bulunamadı'}
                  </p>
                  {search.length >= 2 && catalogRoute && (
                    <p className="text-xs text-muted-foreground">
                      Aradığınız ders bulunamadı mı?{' '}
                      <Link
                        to={catalogRoute}
                        onClick={handleClose}
                        className="text-primary hover:underline inline-flex items-center gap-0.5"
                      >
                        Ders Kataloğu'na Git
                        <ArrowRight className="h-3 w-3" />
                      </Link>
                    </p>
                  )}
                </div>
              ) : (
                filtered.map(c => (
                  <button
                    key={c.id}
                    onClick={() => setSelected(c)}
                    className="w-full text-left px-3 py-2 hover:bg-muted/60 border-b last:border-b-0 transition-colors"
                  >
                    <span className="font-medium text-sm">{c.code}</span>
                    <span className="text-sm text-muted-foreground ml-2">{c.name}</span>
                    <span className="text-xs text-muted-foreground ml-2">({c.ects} AKTS)</span>
                  </button>
                ))
              )}
            </div>
          </div>
        ) : (
          // ── Aşama 2: Müfredat-özel detayları doldur ──
          <div className="flex-1 overflow-y-auto px-6 py-4 space-y-5">
            {/* Course özet (read-only) */}
            <div className="p-3 rounded-md bg-muted/40 border">
              <p className="font-medium text-sm">{selected.code} — {selected.name}</p>
              <p className="text-xs text-muted-foreground mt-1">
                {selected.ects} AKTS · {selected.theoryHours}T + {selected.labHours}L
                {selected.departmentName ? ` · ${selected.departmentName}` : ''}
              </p>
              <p className="text-[11px] text-muted-foreground mt-1">
                Course-seviyesi alanları (Diğer Adı, Uygulama Saat, Açma Tipi vb.) düzenlemek
                için <strong>Ders Kataloğu</strong> sayfasını kullanın.
              </p>
            </div>

            {/* Müfredat-özel alanlar */}
            <CurriculumSpecificSection
              value={curriculumSpecific}
              onChange={setCurriculumSpecific}
            />

            {/* Açma Nedeni + Kategori (curriculum-specific kısım) */}
            <ClassificationSection
              classification={classification}
              onClassificationChange={setClassification}
              curriculumSpecific={curriculumSpecific}
              onCurriculumSpecificChange={setCurriculumSpecific}
              hideCourseFields
            />

            <FlagsSection value={flags} onChange={setFlags} />

            <GroupSection
              groupCode={curriculumSpecific.groupCode}
              onGroupCodeChange={(g) => setCurriculumSpecific({ ...curriculumSpecific, groupCode: g })}
            />

            <DecisionSection value={decision} onChange={setDecision} />

            <IntibakSection
              value={intibak}
              onChange={setIntibak}
              currentCourseId={selected.id}
              departmentId={selected.departmentId}
            />
          </div>
        )}

        <DialogFooter className="shrink-0 px-6 py-4 border-t bg-background">
          {selected && (
            <Button variant="outline" onClick={() => setSelected(null)} disabled={isPending}>
              Geri
            </Button>
          )}
          <Button variant="outline" onClick={handleClose} disabled={isPending}>
            İptal
          </Button>
          {selected && (
            <Button onClick={handleAdd} disabled={isPending}>
              {isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              Ekle
            </Button>
          )}
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}
