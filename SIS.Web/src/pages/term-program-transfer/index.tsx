import { useState, useMemo, useCallback } from 'react';
import { AlertCircle, Copy } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useAcademicOperationsContext } from '../../hooks/useAcademicOperations';
import { useActiveTerm } from '../../hooks/useActiveTerm';
import { useAdminTerms } from '../../hooks/useAdminTerms';
import { useAdminProgramsByStructure } from '../../hooks/useAdminAcademicStructure';
import {
  usePreviousTermCourses,
  useTransferFromTerm,
  useDepartmentInstructors,
  useTransferDefaults,
} from '../../hooks/useCourseOpening';
import type {
  PreviousTermCourseDto,
  TransferFromTermResultDto,
  TransferCourseItem,
} from '../../types/courseOpening';
import type { CourseOverride } from './types';
import { TransferFilterBar } from './components/TransferFilterBar';
import { SourcePanelHeader } from './components/SourcePanelHeader';
import { TransferTable } from './components/TransferTable';
import { TransferConfirmDialog } from './components/TransferConfirmDialog';
import { TransferResultBanner } from './components/TransferResultBanner';
import { TargetTermPanel } from './components/TargetTermPanel';
import { RowEditSheet } from './components/RowEditSheet';
import { SectionManagementDialog } from './components/SectionManagementDialog';
import { CalendarClock } from 'lucide-react';
import type { OpenedCourseSectionDto } from '../../types/courseOpening';

export default function TermProgramTransferPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: allTerms } = useAdminTerms();
  const { data: defaults } = useTransferDefaults();
  const departments = contextQuery.data?.accessibleDepartments ?? [];

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [sourceTermId, setSourceTermId] = useState<number | null>(null);
  const [programId, setProgramId] = useState<number | null>(null);
  const [classLevel, setClassLevel] = useState<number | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [transferResult, setTransferResult] = useState<TransferFromTermResultDto | null>(null);
  const [transferException, setTransferException] = useState<string | null>(null);
  const [overrides, setOverrides] = useState<Record<number, CourseOverride>>({});
  const [searchCode, setSearchCode] = useState('');
  const [searchName, setSearchName] = useState('');
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [includeSchedule, setIncludeSchedule] = useState(false);
  const [editingCourseId, setEditingCourseId] = useState<number | null>(null);
  const [manageSection, setManageSection] = useState<OpenedCourseSectionDto | null>(null);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const targetTermId = activeTerm?.id ?? null;
  const termStatus = activeTerm?.status;
  const isTermSuitable = termStatus === 'Active';
  const sourceTerms = useMemo(
    () => (allTerms ?? []).filter((t) => t.id !== targetTermId),
    [allTerms, targetTermId],
  );
  const selectedSourceTerm = sourceTerms.find((t) => t.id === sourceTermId);

  const coursesQuery = usePreviousTermCourses(deptId, sourceTermId, targetTermId, { programId, classLevel });
  const transferMutation = useTransferFromTerm(deptId ?? 0, targetTermId ?? 0);
  const instructorsQuery = useDepartmentInstructors(deptId, targetTermId);
  const programsQuery = useAdminProgramsByStructure(deptId ?? undefined);
  const programs = useMemo(
    () => (programsQuery.data ?? []).map((p) => ({ id: p.id, name: p.name })),
    [programsQuery.data],
  );
  const instructors = useMemo(() => instructorsQuery.data ?? [], [instructorsQuery.data]);
  const courses = useMemo(() => coursesQuery.data ?? [], [coursesQuery.data]);

  const filteredCourses = useMemo(() => {
    let result = courses;
    if (searchCode.trim()) {
      const q = searchCode.trim().toLowerCase();
      result = result.filter((c) => c.courseCode.toLowerCase().includes(q));
    }
    if (searchName.trim()) {
      const q = searchName.trim().toLowerCase();
      result = result.filter((c) => c.courseName.toLowerCase().includes(q));
    }
    return result;
  }, [courses, searchCode, searchName]);

  const fallbackCapacity = defaults?.defaultCapacityPerSection ?? 40;
  const maxSections = defaults?.maxSectionsPerCourse ?? 20;

  const buildDefaultOverride = useCallback(
    (c: PreviousTermCourseDto): CourseOverride => ({
      sectionCount: c.sectionCount,
      capacityPerSection:
        c.sectionCount > 0 ? Math.round(c.totalCapacity / c.sectionCount) : fallbackCapacity,
      instructorId: c.instructorId,
      internalQuota: c.sourceInternalQuota,
      externalQuota: c.sourceExternalQuota,
      accessibleDepartmentIds: c.sourceAccessibleDepartmentIds,
      includeCrossLists: c.hasCrossLists,
    }),
    [fallbackCapacity],
  );

  const getOverride = useCallback(
    (c: PreviousTermCourseDto): CourseOverride => overrides[c.courseId] ?? buildDefaultOverride(c),
    [overrides, buildDefaultOverride],
  );

  function updateOverride<K extends keyof CourseOverride>(
    courseId: number,
    field: K,
    value: CourseOverride[K],
  ) {
    setOverrides((prev) => {
      const course = courses.find((c) => c.courseId === courseId);
      if (!course) return prev;
      const current = prev[courseId] ?? buildDefaultOverride(course);
      return { ...prev, [courseId]: { ...current, [field]: value } };
    });
  }

  const toggle = (c: PreviousTermCourseDto) => {
    if (c.alreadyOpened) return;
    setSelectedIds((prev) => {
      const n = new Set(prev);
      if (n.has(c.courseId)) n.delete(c.courseId);
      else n.add(c.courseId);
      return n;
    });
  };

  const selectedCourses = courses.filter((c) => selectedIds.has(c.courseId));
  const totalCredits = selectedCourses.reduce((sum, c) => sum + c.credits, 0);
  const totalEcts = selectedCourses.reduce((sum, c) => sum + c.ects, 0);

  // ── Filtrelenenleri Seç (header checkbox) ───────────────────────────────────
  // 'none': hiçbiri seçili değil · 'some': bazısı · 'all': filtrelenen tüm
  // aktarılabilirler seçili. alreadyOpened=true olanlar her zaman atlanır.
  const sourceSelectAllState = useMemo<'none' | 'some' | 'all'>(() => {
    const transferableFiltered = filteredCourses.filter((c) => !c.alreadyOpened);
    if (transferableFiltered.length === 0) return 'none';
    const sel = transferableFiltered.filter((c) => selectedIds.has(c.courseId)).length;
    if (sel === 0) return 'none';
    if (sel === transferableFiltered.length) return 'all';
    return 'some';
  }, [filteredCourses, selectedIds]);

  const toggleSelectAllFiltered = useCallback(() => {
    const transferableFiltered = filteredCourses.filter((c) => !c.alreadyOpened);
    if (transferableFiltered.length === 0) return;
    const allSelected = transferableFiltered.every((c) => selectedIds.has(c.courseId));
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (allSelected) {
        for (const c of transferableFiltered) next.delete(c.courseId);
      } else {
        for (const c of transferableFiltered) next.add(c.courseId);
      }
      return next;
    });
  }, [filteredCourses, selectedIds]);

  // ── Kaynak panel CSV / Yazdır / Mail ────────────────────────────────────────
  const exportSourceCsv = useCallback(() => {
    if (filteredCourses.length === 0) return;
    const escape = (v: string | null | undefined) =>
      `"${(v ?? '').replace(/"/g, '""')}"`;
    const header = 'Kod,Ad,Z/S,Sınıf,Krd,AKTS,Şube,Kontenjan,Kayıtlı,Hoca,Hedefte';
    const rows = filteredCourses.map((c) =>
      [
        c.courseCode,
        escape(c.courseName),
        c.isCompulsory ? 'Z' : 'S',
        c.suggestedSemester > 0 ? Math.ceil(c.suggestedSemester / 2) : '',
        c.credits,
        c.ects,
        c.sectionCount,
        c.totalCapacity,
        c.totalEnrolled,
        escape(c.instructorName),
        c.alreadyOpened ? 'Evet' : 'Hayır',
      ].join(','),
    );
    const csv = '﻿' + header + '\n' + rows.join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `aktarim-kaynak-${selectedSourceTerm?.name ?? 'liste'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }, [filteredCourses, selectedSourceTerm]);

  const mailSourceList = useCallback(() => {
    const subject = `Aktarım Listesi: ${selectedSourceTerm?.name ?? '—'} → ${activeTerm?.name ?? '—'}`;
    const lines = [
      `Kaynak Dönem: ${selectedSourceTerm?.name ?? '—'}`,
      `Hedef Dönem: ${activeTerm?.name ?? '—'}`,
      `Bölüm: ${departments.find((d) => d.departmentId === deptId)?.departmentName ?? '—'}`,
      `Ders Sayısı: ${filteredCourses.length}`,
      '',
      'Kod\tAd\tKrd\tAKTS\tHoca',
      ...filteredCourses
        .slice(0, 100)
        .map((c) =>
          `${c.courseCode}\t${c.courseName}\t${c.credits}\t${c.ects}\t${c.instructorName ?? ''}`,
        ),
      filteredCourses.length > 100 ? `… (${filteredCourses.length - 100} ders daha)` : '',
    ].filter(Boolean);
    window.location.href = `mailto:?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(lines.join('\n'))}`;
  }, [filteredCourses, selectedSourceTerm, activeTerm, departments, deptId]);

  const printPage = useCallback(() => window.print(), []);

  const handleTransfer = async () => {
    if (!deptId || !sourceTermId || !targetTermId || selectedIds.size === 0) return;
    setShowConfirmDialog(false);
    setTransferResult(null);
    setTransferException(null);

    const courseItems: TransferCourseItem[] = selectedCourses.map((c) => {
      const ov = getOverride(c);
      return {
        courseId: c.courseId,
        sectionCount: ov.sectionCount,
        capacityPerSection: ov.capacityPerSection,
        instructorId: ov.instructorId,
        internalQuota: ov.internalQuota,
        externalQuota: ov.externalQuota,
        accessibleDepartmentIds: ov.accessibleDepartmentIds,
        includeCrossLists: ov.includeCrossLists,
        includeSchedule: includeSchedule && c.hasSchedule,
      };
    });

    try {
      const result = await transferMutation.mutateAsync({
        departmentId: deptId,
        sourceTermId,
        targetTermId,
        courses: courseItems,
      });
      setTransferResult(result);
      setSelectedIds(new Set());
    } catch (err) {
      const e = err as {
        response?: { data?: { error?: string; detail?: string; title?: string } };
        message?: string;
      };
      setTransferException(
        e?.response?.data?.error ??
          e?.response?.data?.detail ??
          e?.response?.data?.title ??
          e?.message ??
          'Aktarım sırasında beklenmeyen bir hata oluştu.',
      );
    }
  };

  const resetSelection = () => {
    setSelectedIds(new Set());
    setTransferResult(null);
    setTransferException(null);
    setOverrides({});
    setSearchCode('');
    setSearchName('');
  };

  const changeDept = (deptId: number) => {
    setSelectedDeptId(deptId);
    setProgramId(null);
    resetSelection();
  };
  const changeSource = (termId: number) => {
    setSourceTermId(termId);
    resetSelection();
  };

  if (contextQuery.isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 h-[calc(100vh-120px)] min-h-[480px]">
      <TransferFilterBar termStatus={termStatus} isTermSuitable={isTermSuitable} />

      {transferException && (
        <div className="shrink-0 rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3">
          <div className="flex items-start gap-2">
            <AlertCircle className="h-4 w-4 mt-0.5 text-destructive shrink-0" />
            <div className="flex-1 text-sm">
              <div className="font-semibold text-destructive">Aktarım yapılamadı</div>
              <div className="text-destructive/90">{transferException}</div>
            </div>
            <button
              type="button"
              className="text-xs text-destructive/70 hover:text-destructive"
              onClick={() => setTransferException(null)}
              aria-label="Kapat"
            >
              ✕
            </button>
          </div>
        </div>
      )}

      {transferResult && <TransferResultBanner result={transferResult} />}

      {/* Tek kart, içinde iki bölge — ortada belirgin ayraç. Sayfanın kalan yüksekliğini kaplar. */}
      <Card className="overflow-hidden flex-1 min-h-0 flex flex-col">
        <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_1px_minmax(0,1fr)] items-stretch">
          {/* SOL bölge: Kaynak (Referans) — outer Card stilini nötrle, iç davranış aynı */}
          <Card className="overflow-hidden border-0 shadow-none rounded-none flex flex-col min-h-0 max-h-full">
            <SourcePanelHeader
              departments={departments}
              departmentId={deptId}
              onDepartmentChange={changeDept}
              sourceTerms={sourceTerms}
              sourceTermId={sourceTermId}
              onSourceChange={changeSource}
              programs={programs}
              programId={programId}
              onProgramChange={setProgramId}
              classLevel={classLevel}
              onClassLevelChange={setClassLevel}
              searchCode={searchCode}
              onSearchCodeChange={setSearchCode}
              searchName={searchName}
              onSearchNameChange={setSearchName}
              totalCount={courses.length}
              onExportCsv={exportSourceCsv}
              onPrint={printPage}
              onMail={mailSourceList}
              selectedCount={selectedIds.size}
              onTransferClick={() => setShowConfirmDialog(true)}
              isPending={transferMutation.isPending}
              canTransfer={Boolean(targetTermId) && isTermSuitable}
              targetDepartmentLabel={departments.find((d) => d.departmentId === deptId)?.departmentName ?? null}
            />

            <CardContent className="p-0 flex-1 min-h-0 flex flex-col">
              {!sourceTermId ? (
                <div className="flex-1 flex flex-col items-center justify-center py-12">
                  <Copy className="h-8 w-8 text-muted-foreground/30 mb-2" />
                  <p className="text-xs text-muted-foreground">Kaynak dönem seçin</p>
                  <p className="text-[10px] text-muted-foreground/60 mt-1">
                    Üstteki filtreden bir önceki dönemi seçin.
                  </p>
                </div>
              ) : coursesQuery.isLoading ? (
                <div className="flex-1 flex items-center justify-center py-12 text-xs text-muted-foreground">
                  Yükleniyor...
                </div>
              ) : courses.length === 0 ? (
                <div className="flex-1 flex flex-col items-center justify-center py-12">
                  <AlertCircle className="h-8 w-8 text-muted-foreground/30 mb-2" />
                  <p className="text-xs text-muted-foreground">Ders bulunamadı</p>
                  <p className="text-[10px] text-muted-foreground/60 mt-1">
                    {selectedSourceTerm?.name} döneminde bu bölüme ait açılmış ders yok.
                  </p>
                </div>
              ) : (
                <TransferTable
                  courses={filteredCourses}
                  selectedIds={selectedIds}
                  onToggle={toggle}
                  onEdit={(c) => setEditingCourseId(c.courseId)}
                  onToggleSelectAll={toggleSelectAllFiltered}
                  selectAllState={sourceSelectAllState}
                  paginationExtraInfo={
                    <>
                      <label
                        htmlFor="include-schedule"
                        className="inline-flex items-center gap-1 cursor-pointer select-none"
                        title="Geçen dönemin gün/saat/derslik bilgisini de aktarır."
                      >
                        <input
                          id="include-schedule"
                          type="checkbox"
                          checked={includeSchedule}
                          onChange={(e) => setIncludeSchedule(e.target.checked)}
                          className="h-3 w-3 cursor-pointer"
                        />
                        <CalendarClock className="h-3 w-3" />
                        <span>Programı kopyala</span>
                      </label>
                      {selectedIds.size > 0 && (
                        <span className="whitespace-nowrap">
                          ·{' '}
                          <strong className="text-foreground">{selectedIds.size}</strong> seçili
                          {' · '}
                          <strong className="text-foreground">{totalCredits}</strong> Krd ·{' '}
                          <strong className="text-foreground">{totalEcts}</strong> AKTS
                        </span>
                      )}
                    </>
                  }
                />
              )}
            </CardContent>
          </Card>

          {/* Belirgin ayraç — desktop'ta dikey, mobilde yatay */}
          <div className="hidden lg:block bg-slate-200 self-stretch" aria-hidden />
          <div className="lg:hidden h-px bg-slate-200" aria-hidden />

          {/* SAĞ bölge: Hedef dönem — outer Card stilini nötrle, flex-col h-full propage et */}
          <div className="flex flex-col min-h-0 max-h-full [&>div]:border-0 [&>div]:shadow-none [&>div]:rounded-none [&>div]:flex [&>div]:flex-col [&>div]:min-h-0 [&>div]:flex-1">
            <TargetTermPanel
              departments={departments}
              departmentId={deptId}
              onDepartmentChange={changeDept}
              programId={programId}
              onProgramChange={setProgramId}
              defaultTermId={targetTermId}
              availableTerms={allTerms ?? []}
              onManage={setManageSection}
            />
          </div>
        </div>
      </Card>

      <TransferConfirmDialog
        open={showConfirmDialog}
        onOpenChange={setShowConfirmDialog}
        selectedCourses={selectedCourses}
        getOverride={getOverride}
        instructors={instructors}
        targetDepartmentName={departments.find((d) => d.departmentId === deptId)?.departmentName ?? null}
        targetTermName={activeTerm?.name ?? null}
        onConfirm={handleTransfer}
      />

      <RowEditSheet
        course={editingCourseId != null ? (courses.find((c) => c.courseId === editingCourseId) ?? null) : null}
        override={editingCourseId != null ? getOverride(courses.find((c) => c.courseId === editingCourseId)!) : null}
        instructors={instructors}
        maxSections={maxSections}
        open={editingCourseId != null}
        onOpenChange={(open) => !open && setEditingCourseId(null)}
        onUpdate={(field, value) => {
          if (editingCourseId != null) updateOverride(editingCourseId, field, value);
        }}
      />

      {/* Sağ panel — tek yönetim penceresi (çift tık ile açılır, içinde tüm aksiyonlar) */}
      <SectionManagementDialog
        section={manageSection}
        departmentId={deptId ?? 0}
        termId={targetTermId ?? 0}
        instructors={instructors}
        open={manageSection != null}
        onOpenChange={(o) => !o && setManageSection(null)}
      />
    </div>
  );
}
