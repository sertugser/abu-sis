import { useEffect, useState, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  useCurriculumList,
  useCurriculumDetail,
  useCreateCurriculum,
  useUpdateCurriculum,
  useDeleteCurriculum,
  useActivateCurriculum,
  useAddCurriculumCourse,
  useRemoveCurriculumCourse,
  useCopyCurriculum,
  useCurriculumPools,
} from '../hooks/useCurriculumManagement';
import AddCourseDialog from '../components/curriculum-management/AddCourseDialog';
import AddPoolDialog from '../components/curriculum-management/AddPoolDialog';
import PoolDetailDialog from '../components/curriculum-management/PoolDetailDialog';
import CurriculumCourseEditDialog from './curriculum-management/CurriculumCourseEditDialog';
import CourseLinksDialog from '../components/curriculum-management/CourseLinksDialog';
import { CurriculumTermRulesDialog } from '../components/curriculum-management/CurriculumTermRulesDialog';
import ActivateCurriculumDialog from './curriculum-management/ActivateCurriculumDialog';
import DeleteCurriculumDialog from './curriculum-management/DeleteCurriculumDialog';
import CopyCurriculumDialog from './curriculum-management/CopyCurriculumDialog';
import CurriculumFormDialog, { type CurriculumFormSubmitPayload } from './curriculum-management/CurriculumFormDialog';
import CurriculumFilterBar, { type StatusFilter, type YearFilter } from './curriculum-management/CurriculumFilterBar';
import CurriculumListView from './curriculum-management/CurriculumListView';
import CurriculumCoursesView from './curriculum-management/CurriculumCoursesView';
import PromotionRulesDialog from './curriculum-management/PromotionRulesDialog';
import { getFromYearOptionsFromEcts } from './curriculum-management/promotionRulesUtils';
import type {
  CurriculumCourseItemDto,
  CurriculumElectivePoolDto,
  CurriculumListDto,
  CurriculumRow,
} from '../types/curriculumManagement';
import { ROUTES } from '@/lib/constants';

type ViewMode = 'list' | 'courses';

const PAGE_SIZE = 10;          // Müfredat liste sayfalama
const COURSES_PAGE_SIZE = 15;  // Müfredat detay → ders listesi sayfalama

export default function CurriculumManagementPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  // Deep-link: /mufredat-islemleri?curriculum=ID → o müfredat ders görünümünde
  // direkt açılır. ElectiveGroupDetailSheet "Bağlı Müfredatlar" tab'ından gelir.
  const initialCurriculumId = (() => {
    const raw = searchParams.get('curriculum');
    const num = raw !== null ? Number(raw) : NaN;
    return Number.isFinite(num) && num > 0 ? num : null;
  })();

  // ── Filter state ──
  const [facultyId, setFacultyId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [programId, setProgramId] = useState<number | undefined>();
  const [selectedCurriculumId, setSelectedCurriculumId] = useState<number | null>(initialCurriculumId);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [yearFilter, setYearFilter] = useState<YearFilter>('all');
  // Anahtar kelimeyle arama — fakülte / program / müfredat adı / yıl / program kodu içinde
  const [searchKeyword, setSearchKeyword] = useState('');

  // Sayfa görünüm modu: 'list' (müfredat listesi tablosu) | 'courses' (ders listesi)
  const [view, setView] = useState<ViewMode>(initialCurriculumId !== null ? 'courses' : 'list');

  // Query param'ı tek seferlik tüket — kullanıcı normal kullanıma dönünce
  // URL temizlenir; geri butonuyla deep-link tekrar tetiklenmez.
  useEffect(() => {
    if (searchParams.has('curriculum')) {
      const next = new URLSearchParams(searchParams);
      next.delete('curriculum');
      setSearchParams(next, { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  // Müfredat detay → ders listesi pagination (15 ders/sayfa)
  const [coursesPage, setCoursesPage] = useState(1);

  // Edit Dialog — mevcut müfredatı düzenleme
  const [editingCurriculumId, setEditingCurriculumId] = useState<number | null>(null);

  // Confirm Dialogları
  const [activateTarget, setActivateTarget] = useState<{ id: number; label: string } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: number; label: string } | null>(null);

  // Copy Dialog — müfredatı kopyala (kaynak + yeni yıl)
  const [copyTarget, setCopyTarget] = useState<{ id: number; label: string } | null>(null);

  // Promotion Rules Dialog — sınıf atlatma kuralları (in-page modal)
  const [promotionRulesTarget, setPromotionRulesTarget] = useState<
    { id: number; programId: number; label: string } | null
  >(null);

  // ── Dialogs ──
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [showAddCourse, setShowAddCourse] = useState(false);
  const [showAddPool, setShowAddPool] = useState(false);
  const [poolDetailTarget, setPoolDetailTarget] = useState<CurriculumElectivePoolDto | null>(null);
  const [showTermRulesDialog, setShowTermRulesDialog] = useState(false);
  // Hangi müfredat için dialog açıldı (listeden dişli menüsüyle tetikleniyor)
  const [termRulesTarget, setTermRulesTarget] = useState<{ id: number; label: string } | null>(null);

  // ── Tabs ──

  // ── Course detail sheet ──
  const [detailCourseId, setDetailCourseId] = useState<number | null>(null);
  const [showDetailSheet, setShowDetailSheet] = useState(false);

  // ── Search & table filters ──
  // Proliz tarzı: iki ayrı arama alanı (Ders Kodu | Ders Adı). AND mantığıyla birleşir.
  const [searchCode, setSearchCode] = useState('');
  const [searchName, setSearchName] = useState('');
  const [classFilter, setClassFilter] = useState<number | null>(null); // 1,2,3,4
  const [termTypeFilter, setTermTypeFilter] = useState<'all' | 'fall' | 'spring'>('all');
  // Gruplu Dersleri Göster — şimdilik UI-only, seçmeli havuzlar entegrasyonu için yer tutucu
  const [showGroupedCourses, setShowGroupedCourses] = useState(false);

  // ── Satır-içi dişli menü → Ön Koşul / İntibak dialog target ──
  const [courseLinksTarget, setCourseLinksTarget] = useState<
    { courseId: number; label: string; mode: 'prerequisite' | 'equivalency' } | null
  >(null);

  // ── Queries ──
  // Filtre barındaki cascade — Fakülte/Bölüm/Program hangi seviyeye kadar seçildiyse o seviyeden liste gelir
  const { data: curricula, isLoading: loadingCurricula } = useCurriculumList({
    programId, departmentId, facultyId,
  });
  const { data: detail, isLoading: loadingDetail } = useCurriculumDetail(selectedCurriculumId);

  // ── Mutations ──
  const createMut = useCreateCurriculum();
  const updateMut = useUpdateCurriculum();
  const deleteMut = useDeleteCurriculum();
  const activateMut = useActivateCurriculum();
  const addCourseMut = useAddCurriculumCourse();
  const removeCourseMut = useRemoveCurriculumCourse();
  const copyMut = useCopyCurriculum();

  // Faz B B5: müfredata bağlı grup slot'ları (pool'lar)
  const { data: poolsData } = useCurriculumPools(selectedCurriculumId);

  // ── Derived — düz ders listesi (grup yok) ──
  const flatCourses = useMemo(() => {
    if (!detail) return [] as CurriculumCourseItemDto[];
    const codeLower = searchCode.trim().toLowerCase();
    const nameLower = searchName.trim().toLowerCase();

    let filtered: CurriculumCourseItemDto[] = detail.courses;

    if (codeLower) {
      filtered = filtered.filter(c => c.courseCode.toLowerCase().includes(codeLower));
    }
    if (nameLower) {
      filtered = filtered.filter(c => c.courseName.toLowerCase().includes(nameLower));
    }

    // Sınıf filtresi: 1 → sem 1,2; 2 → sem 3,4; 3 → sem 5,6; 4 → sem 7,8
    if (classFilter !== null) {
      const minSem = (classFilter - 1) * 2 + 1;
      const maxSem = classFilter * 2;
      filtered = filtered.filter(c => c.suggestedSemester >= minSem && c.suggestedSemester <= maxSem);
    }

    // Dönem: fall → tek semesterlar (G), spring → çift semesterlar (B)
    if (termTypeFilter === 'fall') {
      filtered = filtered.filter(c => c.suggestedSemester % 2 !== 0);
    } else if (termTypeFilter === 'spring') {
      filtered = filtered.filter(c => c.suggestedSemester % 2 === 0);
    }

    // Varsayılan sıralama: sem asc + code asc (sort filtresi kaldırıldı — Proliz davranışı)
    return [...filtered].sort((a, b) =>
      a.suggestedSemester - b.suggestedSemester || a.courseCode.localeCompare(b.courseCode),
    );
  }, [detail, searchCode, searchName, classFilter, termTypeFilter]);

  // Faz B B5: filtrelenmiş pool listesi (course filtreleriyle aynı kuralları uygular)
  const filteredPools = useMemo(() => {
    if (!poolsData) return [] as CurriculumElectivePoolDto[];
    const codeLower = searchCode.trim().toLowerCase();
    const nameLower = searchName.trim().toLowerCase();

    let filtered = poolsData;

    if (codeLower) {
      filtered = filtered.filter(p => p.groupCode.toLowerCase().includes(codeLower));
    }
    if (nameLower) {
      filtered = filtered.filter(p =>
        p.groupName.toLowerCase().includes(nameLower) || p.slotName.toLowerCase().includes(nameLower),
      );
    }

    if (classFilter !== null) {
      const minSem = (classFilter - 1) * 2 + 1;
      const maxSem = classFilter * 2;
      filtered = filtered.filter(p => p.suggestedSemesterStart >= minSem && p.suggestedSemesterStart <= maxSem);
    }

    if (termTypeFilter === 'fall') {
      filtered = filtered.filter(p => p.suggestedSemesterStart % 2 !== 0);
    } else if (termTypeFilter === 'spring') {
      filtered = filtered.filter(p => p.suggestedSemesterStart % 2 === 0);
    }

    return filtered;
  }, [poolsData, searchCode, searchName, classFilter, termTypeFilter]);

  // Faz B B5: course + pool karma satır listesi (yarıyıl asc, eşitlikte course'lar önce)
  const flatRows = useMemo<CurriculumRow[]>(() => {
    const rows: CurriculumRow[] = [
      ...flatCourses.map(c => ({ kind: 'course' as const, course: c })),
      ...filteredPools.map(p => ({ kind: 'pool' as const, pool: p })),
    ];
    return rows.sort((a, b) => {
      const aSem = a.kind === 'course' ? a.course.suggestedSemester : a.pool.suggestedSemesterStart;
      const bSem = b.kind === 'course' ? b.course.suggestedSemester : b.pool.suggestedSemesterStart;
      if (aSem !== bSem) return aSem - bSem;
      // Eşit yarıyılda önce course'lar, sonra pool'lar
      if (a.kind !== b.kind) return a.kind === 'course' ? -1 : 1;
      // Aynı kind içinde alfabetik
      const aKey = a.kind === 'course' ? a.course.courseCode : `${a.pool.groupCode}-${a.pool.slotName}`;
      const bKey = b.kind === 'course' ? b.course.courseCode : `${b.pool.groupCode}-${b.pool.slotName}`;
      return aKey.localeCompare(bKey);
    });
  }, [flatCourses, filteredPools]);

  const existingCourseIds = useMemo(
    () => new Set(detail?.courses?.map(c => c.courseId) ?? []),
    [detail],
  );

  // Ders + pool listesi sayfalama (15 satır/sayfa)
  const coursesTotalPages = Math.max(1, Math.ceil(flatRows.length / COURSES_PAGE_SIZE));
  const pagedRows = useMemo(
    () => flatRows.slice((coursesPage - 1) * COURSES_PAGE_SIZE, coursesPage * COURSES_PAGE_SIZE),
    [flatRows, coursesPage],
  );
  // Alt summary bar — sadece course'lardan toplam (pool'lar derslerin AKTS'ini tekrar saymasın diye dışarıda)
  const flatCoursesCredit = useMemo(
    () => flatCourses.reduce((s, c) => s + c.nationalCredit, 0),
    [flatCourses],
  );
  const flatCoursesEcts = useMemo(
    () => flatCourses.reduce((s, c) => s + c.ects, 0),
    [flatCourses],
  );
  // Filtre değişince ders sayfası 1'e dön — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const courseFilterKey = `${searchCode}|${searchName}|${classFilter}|${termTypeFilter}|${selectedCurriculumId}`;
  const [prevCourseFilterKey, setPrevCourseFilterKey] = useState(courseFilterKey);
  if (prevCourseFilterKey !== courseFilterKey) {
    setPrevCourseFilterKey(courseFilterKey);
    setCoursesPage(1);
  }
  // Toplam sayfa küçüldüğünde mevcut sayfa dışarı taşarsa düzelt
  if (coursesPage > coursesTotalPages) {
    setCoursesPage(coursesTotalPages);
  }



  // Mevcut müfredat yıllarını distinct listele (Müfredat Yılı dropdown'ı için)
  const availableYears = useMemo(() => {
    if (!curricula) return [] as string[];
    const set = new Set<string>();
    for (const c of curricula) set.add(c.version);
    return Array.from(set).sort((a, b) => b.localeCompare(a));
  }, [curricula]);

  // Durum + Müfredat Yılı + Anahtar kelime filtreleri birlikte uygulanır (tablo satırlarını süzer)
  const filteredCurricula = useMemo(() => {
    if (!curricula) return [];
    let out = curricula;
    if (statusFilter === 'active') out = out.filter(c => c.isActive);
    else if (statusFilter === 'inactive') out = out.filter(c => !c.isActive && c.status !== 2);
    else if (statusFilter === 'archived') out = out.filter(c => c.status === 2);
    if (yearFilter !== 'all') out = out.filter(c => c.version === yearFilter);
    const kw = searchKeyword.trim().toLowerCase();
    if (kw) {
      out = out.filter(c =>
        c.version.toLowerCase().includes(kw)
        || c.facultyName.toLowerCase().includes(kw)
        || c.programName.toLowerCase().includes(kw)
        || c.programCode.toLowerCase().includes(kw)
        || (c.name?.toLowerCase().includes(kw) ?? false),
      );
    }
    return out;
  }, [curricula, statusFilter, yearFilter, searchKeyword]);

  // Sayfalama
  const totalPages = Math.max(1, Math.ceil(filteredCurricula.length / PAGE_SIZE));
  const pagedCurricula = useMemo(
    () => filteredCurricula.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [filteredCurricula, currentPage],
  );
  // Filtre değişince sayfa 1'e dön — adjust-state-during-render pattern
  const currFilterKey = `${statusFilter}|${yearFilter}|${facultyId}|${departmentId}|${programId}|${searchKeyword}`;
  const [prevCurrFilterKey, setPrevCurrFilterKey] = useState(currFilterKey);
  if (prevCurrFilterKey !== currFilterKey) {
    setPrevCurrFilterKey(currFilterKey);
    setCurrentPage(1);
  }
  // totalPages küçüldüğünde currentPage dışarı taşarsa düzelt
  if (currentPage > totalPages) {
    setCurrentPage(totalPages);
  }

  // Edit dialog'unda gösterilecek mevcut müfredat bilgisi (Fakülte/Program read-only info bar için)
  const editingCurr = useMemo(
    () => (editingCurriculumId !== null ? curricula?.find(c => c.id === editingCurriculumId) : undefined),
    [editingCurriculumId, curricula],
  );

  // Detay ekranı breadcrumb'ı için aktif müfredat (tablodaki bütün alanlarıyla — facultyName vs)
  const selectedCurr = useMemo(
    () => (selectedCurriculumId !== null ? curricula?.find(c => c.id === selectedCurriculumId) : undefined),
    [selectedCurriculumId, curricula],
  );

  // Get the full curriculum course row for the detail/edit sheet
  const detailCurriculumCourse = useMemo(() => {
    if (!detailCourseId || !detail) return undefined;
    return detail.courses.find(c => c.courseId === detailCourseId);
  }, [detailCourseId, detail]);

  // ── Handlers ──
  function handleFacultyChange(val: string) {
    setFacultyId(Number(val));
    setDepartmentId(undefined);
    setProgramId(undefined);
    setSelectedCurriculumId(null);
  }
  function handleDepartmentChange(val: string) {
    setDepartmentId(Number(val));
    setProgramId(undefined);
    setSelectedCurriculumId(null);
  }
  function handleProgramChange(val: string) {
    setProgramId(Number(val));
    setSelectedCurriculumId(null);
  }

  function handleOpenCreateDialog() {
    setEditingCurriculumId(null);
    setShowCreateDialog(true);
  }

  // Satırdaki "Düzenle" ikonundan gelen — dialog'u edit mode'da aç
  function handleOpenEditDialog(c: CurriculumListDto) {
    setEditingCurriculumId(c.id);
    setShowCreateDialog(true);
  }

  function handleCloseFormDialog() {
    setShowCreateDialog(false);
    setEditingCurriculumId(null);
  }

  // "Müfredat Dersi Tanımla" — dişli menüsünden — ders listesi görünümüne geç
  function handleViewCourses(curriculumId: number) {
    setSelectedCurriculumId(curriculumId);
    setView('courses');
  }

  function handleBackToList() {
    setView('list');
    setSelectedCurriculumId(null);
  }

  function handleConfirmActivate() {
    if (!activateTarget) return;
    activateMut.mutate(activateTarget.id, {
      onSuccess: () => setActivateTarget(null),
    });
  }

  function handleConfirmDelete() {
    if (!deleteTarget) return;
    deleteMut.mutate(deleteTarget.id, {
      onSuccess: () => {
        setDeleteTarget(null);
        // Eğer ders listesi modundaysak ve silinen müfredat açıksa geri dön
        if (selectedCurriculumId === deleteTarget.id) handleBackToList();
      },
    });
  }

  function handleConfirmCopy(newVersion: string) {
    if (!copyTarget || !newVersion.trim()) return;
    copyMut.mutate(
      { sourceId: copyTarget.id, newVersion: newVersion.trim() },
      {
        onSuccess: () => setCopyTarget(null),
      },
    );
  }

  function handleSubmitForm(payload: CurriculumFormSubmitPayload) {
    if (payload.mode === 'edit') {
      updateMut.mutate(
        {
          id: payload.id,
          version: payload.values.version,
          isActive: payload.isActive,
          name: payload.values.name,
          shortName: payload.values.shortName,
          englishName: payload.values.englishName,
          startDate: payload.values.startDate,
          endDate: payload.values.endDate,
          decisionDate: payload.values.decisionDate,
          decisionNo: payload.values.decisionNo,
          decisionNote: payload.values.decisionNote,
          gradingSystem: payload.values.gradingSystem,
          internshipDuration: payload.values.internshipDuration,
          description: payload.values.description,
        },
        { onSuccess: handleCloseFormDialog },
      );
      return;
    }

    if (payload.mode === 'create-copy') {
      copyMut.mutate(
        { sourceId: payload.sourceId, newVersion: payload.values.version },
        {
          onSuccess: (data) => {
            handleCloseFormDialog();
            setSelectedCurriculumId(data.id);
          },
        },
      );
      return;
    }

    // create-empty
    createMut.mutate(
      {
        programId: payload.programId,
        version: payload.values.version,
        name: payload.values.name,
        shortName: payload.values.shortName,
        englishName: payload.values.englishName,
        startDate: payload.values.startDate,
        endDate: payload.values.endDate,
        decisionDate: payload.values.decisionDate,
        decisionNo: payload.values.decisionNo,
        decisionNote: payload.values.decisionNote,
        gradingSystem: payload.values.gradingSystem,
        internshipDuration: payload.values.internshipDuration,
        description: payload.values.description,
      },
      { onSuccess: handleCloseFormDialog },
    );
  }

  function handleAddCourse(req: import('../types/curriculumManagement').AddCourseRequest) {
    if (!selectedCurriculumId) return;
    addCourseMut.mutate(
      { curriculumId: selectedCurriculumId, ...req },
      { onSuccess: () => setShowAddCourse(false) },
    );
  }

  function handleShowCourseDetail(courseId: number) {
    setDetailCourseId(courseId);
    setShowDetailSheet(true);
  }

  return (
    <div className="space-y-4">
      {/* ── Filtre Barı (3 kolon × 2 satır, Ara satırının yanında Müfredat Ekle) ── */}
      {view === 'list' && (
        <CurriculumFilterBar
          facultyId={facultyId}
          departmentId={departmentId}
          programId={programId}
          yearFilter={yearFilter}
          statusFilter={statusFilter}
          searchKeyword={searchKeyword}
          availableYears={availableYears}
          onFacultyChange={handleFacultyChange}
          onDepartmentChange={handleDepartmentChange}
          onProgramChange={handleProgramChange}
          onYearFilterChange={setYearFilter}
          onStatusFilterChange={setStatusFilter}
          onSearchKeywordChange={setSearchKeyword}
          onOpenCreateDialog={handleOpenCreateDialog}
        />
      )}

      {/* ── LISTE VIEW: Müfredat tablosu ── */}
      {view === 'list' && (
        <CurriculumListView
          hasFilter={!!(facultyId || departmentId || programId)}
          isLoading={loadingCurricula}
          filteredCount={filteredCurricula.length}
          pagedCurricula={pagedCurricula}
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={setCurrentPage}
          onOpenCreateDialog={handleOpenCreateDialog}
          onOpenEditDialog={handleOpenEditDialog}
          onViewCourses={handleViewCourses}
          onOpenTermRules={(target) => {
            setTermRulesTarget(target);
            setShowTermRulesDialog(true);
          }}
          onCopy={setCopyTarget}
          onActivate={setActivateTarget}
          onDelete={setDeleteTarget}
          onOpenPromotionRules={setPromotionRulesTarget}
        />
      )}

      {/* ── COURSES VIEW: Seçili müfredatın ders listesi ── */}
      {view === 'courses' && (
        <CurriculumCoursesView
          detail={detail}
          selectedCurr={selectedCurr}
          selectedCurriculumId={selectedCurriculumId}
          isLoadingDetail={loadingDetail}
          searchCode={searchCode}
          searchName={searchName}
          classFilter={classFilter}
          termTypeFilter={termTypeFilter}
          showGroupedCourses={showGroupedCourses}
          onSearchCodeChange={setSearchCode}
          onSearchNameChange={setSearchName}
          onClassFilterChange={setClassFilter}
          onTermTypeFilterChange={setTermTypeFilter}
          onShowGroupedCoursesChange={setShowGroupedCourses}
          pagedRows={pagedRows}
          flatRowsCount={flatRows.length}
          flatCoursesCount={flatCourses.length}
          flatCoursesCredit={flatCoursesCredit}
          flatCoursesEcts={flatCoursesEcts}
          coursesPage={coursesPage}
          coursesTotalPages={coursesTotalPages}
          onCoursesPageChange={setCoursesPage}
          onBack={handleBackToList}
          onOpenEditDialog={handleOpenEditDialog}
          onCopy={setCopyTarget}
          onActivate={setActivateTarget}
          onDelete={setDeleteTarget}
          onAddCourse={() => setShowAddCourse(true)}
          onAddPool={() => setShowAddPool(true)}
          onShowPoolDetail={(pool) => setPoolDetailTarget(pool)}
          onShowCourseDetail={handleShowCourseDetail}
          onOpenCourseLinks={setCourseLinksTarget}
          onRemoveCourse={(courseId) => removeCourseMut.mutate({ curriculumId: selectedCurriculumId!, courseId })}
          isRemovingCourse={removeCourseMut.isPending}
        />
      )}

      {/* ── Create / Edit Curriculum Dialog ── */}
      <CurriculumFormDialog
        open={showCreateDialog}
        editingCurriculum={editingCurr}
        defaultFacultyId={facultyId}
        defaultDepartmentId={departmentId}
        defaultProgramId={programId}
        onClose={handleCloseFormDialog}
        onSubmit={handleSubmitForm}
        isSubmitting={createMut.isPending || updateMut.isPending || copyMut.isPending}
      />

      {/* ── Add Course Dialog ── */}
      <AddCourseDialog
        open={showAddCourse}
        onClose={() => setShowAddCourse(false)}
        onAdd={handleAddCourse}
        isPending={addCourseMut.isPending}
        existingCourseIds={existingCourseIds}
        catalogRoute={ROUTES.COURSE_CATALOG_ADMIN}
      />

      {/* ── Add Pool Dialog (Faz B B4) ── */}
      {selectedCurriculumId !== null && (
        <AddPoolDialog
          curriculumId={selectedCurriculumId}
          open={showAddPool}
          onOpenChange={setShowAddPool}
        />
      )}

      {/* ── Pool Detail Dialog (Faz B B5) ── */}
      {selectedCurriculumId !== null && poolDetailTarget !== null && (
        <PoolDetailDialog
          key={poolDetailTarget.id}
          curriculumId={selectedCurriculumId}
          pool={poolDetailTarget}
          open={poolDetailTarget !== null}
          onOpenChange={(o) => { if (!o) setPoolDetailTarget(null); }}
        />
      )}

      {/* ── Course Edit Dialog (Proliz B style, edit-only) ── */}
      {detailCourseId !== null && selectedCurriculumId !== null && detailCurriculumCourse && (
        <CurriculumCourseEditDialog
          key={`${detailCourseId}-${selectedCurriculumId}`}
          open={showDetailSheet}
          onClose={() => { setShowDetailSheet(false); setDetailCourseId(null); }}
          curriculumId={selectedCurriculumId}
          courseId={detailCourseId}
          curriculumCourse={detailCurriculumCourse}
        />
      )}

      {/* ── Müfredat Dönem Kuralları Dialog (Proliz tarzı, 3 panel) ── */}
      {showTermRulesDialog && termRulesTarget && (
        <CurriculumTermRulesDialog
          curriculumId={termRulesTarget.id}
          curriculumLabel={termRulesTarget.label}
          open={showTermRulesDialog}
          onClose={() => {
            setShowTermRulesDialog(false);
            setTermRulesTarget(null);
          }}
        />
      )}

      {/* ── Ders Ön Koşul / İntibak Dialog ── */}
      <CourseLinksDialog
        mode={courseLinksTarget?.mode ?? 'prerequisite'}
        open={courseLinksTarget !== null}
        courseId={courseLinksTarget?.courseId ?? null}
        courseLabel={courseLinksTarget?.label ?? ''}
        onClose={() => setCourseLinksTarget(null)}
      />

      <ActivateCurriculumDialog
        target={activateTarget}
        onCancel={() => setActivateTarget(null)}
        onConfirm={handleConfirmActivate}
        isPending={activateMut.isPending}
      />

      <DeleteCurriculumDialog
        target={deleteTarget}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={handleConfirmDelete}
        isPending={deleteMut.isPending}
      />

      <CopyCurriculumDialog
        target={copyTarget}
        onCancel={() => setCopyTarget(null)}
        onConfirm={handleConfirmCopy}
        isPending={copyMut.isPending}
      />

      {promotionRulesTarget && (
        <PromotionRulesDialog
          open
          onClose={() => setPromotionRulesTarget(null)}
          curriculumId={promotionRulesTarget.id}
          programId={promotionRulesTarget.programId}
          curriculumLabel={promotionRulesTarget.label}
          fromYearOptions={getFromYearOptionsFromEcts(undefined)}
        />
      )}
    </div>
  );
}
