import { useState, useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import {
  BookOpen, CheckCircle, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight,
  Copy, Edit, GraduationCap, Layers, Loader2, MoreHorizontal, Package, Plus, Printer, Scissors,
  Search, Settings, Trash2,
} from 'lucide-react';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
} from '../hooks/useAdminAcademicStructure';
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
} from '../hooks/useCurriculumManagement';
import AddCourseDialog from '../components/curriculum-management/AddCourseDialog';
import CourseDetailSheet from '../components/curriculum-management/CourseDetailSheet';
import CourseLinksDialog from '../components/curriculum-management/CourseLinksDialog';
import { CurriculumTermRulesDialog } from '../components/curriculum-management/CurriculumTermRulesDialog';
import ActivateCurriculumDialog from './curriculum-management/ActivateCurriculumDialog';
import DeleteCurriculumDialog from './curriculum-management/DeleteCurriculumDialog';
import CopyCurriculumDialog from './curriculum-management/CopyCurriculumDialog';
import type { CurriculumCourseItemDto, CurriculumListDto } from '../types/curriculumManagement';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/lib/constants';

type CreateMode = 'empty' | 'copy';
type StatusFilter = 'all' | 'active' | 'inactive' | 'archived';
type ViewMode = 'list' | 'courses';
type YearFilter = 'all' | string; // 'all' veya "2024" gibi yıl değeri

const PAGE_SIZE = 10;          // Müfredat liste sayfalama
const COURSES_PAGE_SIZE = 15;  // Müfredat detay → ders listesi sayfalama

export default function CurriculumManagementPage() {
  const navigate = useNavigate();
  // ── Filter state ──
  const [facultyId, setFacultyId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [programId, setProgramId] = useState<number | undefined>();
  const [selectedCurriculumId, setSelectedCurriculumId] = useState<number | null>(null);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [yearFilter, setYearFilter] = useState<YearFilter>('all');
  // Anahtar kelimeyle arama — fakülte / program / müfredat adı / yıl / program kodu içinde
  const [searchKeyword, setSearchKeyword] = useState('');

  // Sayfa görünüm modu: 'list' (müfredat listesi tablosu) | 'courses' (ders listesi)
  const [view, setView] = useState<ViewMode>('list');

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

  // Dialog içi seçimler — filtre barından bağımsız, dialog açıldığında filtre barından default alır
  const [dialogFacultyId, setDialogFacultyId] = useState<number | undefined>();
  const [dialogDepartmentId, setDialogDepartmentId] = useState<number | undefined>();
  const [dialogProgramId, setDialogProgramId] = useState<number | undefined>();

  // ── Dialogs ──
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [newVersion, setNewVersion] = useState('');
  const [newName, setNewName] = useState('');
  const [newShortName, setNewShortName] = useState('');
  const [newEnglishName, setNewEnglishName] = useState('');
  const [newStartDate, setNewStartDate] = useState('');
  const [newEndDate, setNewEndDate] = useState('');
  const [newDecisionDate, setNewDecisionDate] = useState('');
  const [newDecisionNo, setNewDecisionNo] = useState('');
  const [newDecisionNote, setNewDecisionNote] = useState('');
  const [newGradingSystem, setNewGradingSystem] = useState('Sınıf Geçme');
  const [newInternshipDuration, setNewInternshipDuration] = useState('');
  const [newDescription, setNewDescription] = useState('');
  // UI-only alanlar: backend henüz desteklemiyor, create request'e dahil edilmez
  const [newStatus, setNewStatus] = useState<'active' | 'inactive'>('active');
  const [newMufredatTipi, setNewMufredatTipi] = useState<string>('Programa Özel');
  const [newIncludeFormation, setNewIncludeFormation] = useState(false);
  const [newHideFromPackage, setNewHideFromPackage] = useState(false);
  const [createMode, setCreateMode] = useState<CreateMode>('empty');
  const [copySourceId, setCopySourceId] = useState<string>('');
  const [showAddCourse, setShowAddCourse] = useState(false);
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
  const { data: faculties, isLoading: loadingFaculties } = useAdminFaculties();
  const { data: departments, isLoading: loadingDepts } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs, isLoading: loadingPrograms } = useAdminProgramsByStructure(departmentId);
  // Filtre barındaki cascade — Fakülte/Bölüm/Program hangi seviyeye kadar seçildiyse o seviyeden liste gelir
  const { data: curricula, isLoading: loadingCurricula } = useCurriculumList({
    programId, departmentId, facultyId,
  });
  // Dialog cascade (ayrı sorgular — filtre barındaki seçimden bağımsız)
  const { data: dialogDepartments, isLoading: loadingDialogDepts } = useAdminDepartmentsByStructure(dialogFacultyId);
  const { data: dialogPrograms, isLoading: loadingDialogPrograms } = useAdminProgramsByStructure(dialogDepartmentId);
  const { data: dialogCurricula } = useCurriculumList({ programId: dialogProgramId });
  const { data: detail, isLoading: loadingDetail } = useCurriculumDetail(selectedCurriculumId);

  // ── Mutations ──
  const createMut = useCreateCurriculum();
  const updateMut = useUpdateCurriculum();
  const deleteMut = useDeleteCurriculum();
  const activateMut = useActivateCurriculum();
  const addCourseMut = useAddCurriculumCourse();
  const removeCourseMut = useRemoveCurriculumCourse();
  const copyMut = useCopyCurriculum();

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

  const existingCourseIds = useMemo(
    () => new Set(detail?.courses?.map(c => c.courseId) ?? []),
    [detail],
  );

  // Ders listesi sayfalama (15 ders/sayfa)
  const coursesTotalPages = Math.max(1, Math.ceil(flatCourses.length / COURSES_PAGE_SIZE));
  const pagedCourses = useMemo(
    () => flatCourses.slice((coursesPage - 1) * COURSES_PAGE_SIZE, coursesPage * COURSES_PAGE_SIZE),
    [flatCourses, coursesPage],
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

  // Dialog cascade handler'ları — filtre barından bağımsız
  function handleDialogFacultyChange(val: string) {
    setDialogFacultyId(Number(val));
    setDialogDepartmentId(undefined);
    setDialogProgramId(undefined);
  }
  function handleDialogDepartmentChange(val: string) {
    setDialogDepartmentId(Number(val));
    setDialogProgramId(undefined);
  }
  function handleDialogProgramChange(val: string) {
    setDialogProgramId(Number(val));
  }

  function handleOpenCreateDialog() {
    // Filtre barındaki seçimler varsa dialog'a default olarak taşı (kolaylık için)
    setEditingCurriculumId(null);
    setDialogFacultyId(facultyId);
    setDialogDepartmentId(departmentId);
    setDialogProgramId(programId);
    setShowCreateDialog(true);
  }

  // Satırdaki "Düzenle" ikonundan gelen — mevcut müfredatın alanlarını forma yükle
  function handleOpenEditDialog(c: CurriculumListDto) {
    setEditingCurriculumId(c.id);
    setDialogFacultyId(undefined); // Edit modunda readonly, sayım için hydrate etmiyoruz
    setDialogDepartmentId(undefined);
    setDialogProgramId(c.programId);
    setNewVersion(c.version);
    setNewName(c.name ?? '');
    setNewShortName('');
    setNewEnglishName('');
    setNewStartDate(c.startDate ? c.startDate.slice(0, 10) : '');
    setNewEndDate(c.endDate ? c.endDate.slice(0, 10) : '');
    setNewDecisionDate('');
    setNewDecisionNo('');
    setNewDecisionNote('');
    setNewGradingSystem('Sınıf Geçme');
    setNewInternshipDuration('');
    setNewDescription('');
    setNewStatus(c.isActive ? 'active' : 'inactive');
    setNewMufredatTipi('Programa Özel');
    setNewIncludeFormation(false);
    setNewHideFromPackage(false);
    setShowCreateDialog(true);
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

  function resetCreateForm() {
    setNewVersion('');
    setNewName('');
    setNewShortName('');
    setNewEnglishName('');
    setNewStartDate('');
    setNewEndDate('');
    setNewDecisionDate('');
    setNewDecisionNo('');
    setNewDecisionNote('');
    setNewGradingSystem('Sınıf Geçme');
    setNewInternshipDuration('');
    setNewDescription('');
    setNewStatus('active');
    setNewMufredatTipi('Programa Özel');
    setNewIncludeFormation(false);
    setNewHideFromPackage(false);
    setCreateMode('empty');
    setCopySourceId('');
    setDialogFacultyId(undefined);
    setDialogDepartmentId(undefined);
    setDialogProgramId(undefined);
    setEditingCurriculumId(null);
  }

  function handleCreate() {
    if (!newVersion.trim()) return;

    const extraFields = {
      name: newName.trim() || undefined,
      shortName: newShortName.trim() || undefined,
      englishName: newEnglishName.trim() || undefined,
      startDate: newStartDate || undefined,
      endDate: newEndDate || undefined,
      decisionDate: newDecisionDate.trim() || undefined,
      decisionNo: newDecisionNo.trim() || undefined,
      decisionNote: newDecisionNote.trim() || undefined,
      gradingSystem: newGradingSystem.trim() || undefined,
      internshipDuration: newInternshipDuration ? Number(newInternshipDuration) : undefined,
      description: newDescription.trim() || undefined,
    };

    if (editingCurriculumId !== null) {
      // Edit mode — PUT /api/curriculum/{id}; Durumu Aktif/Pasif backend'e gönderilir
      updateMut.mutate(
        {
          id: editingCurriculumId,
          version: newVersion.trim(),
          isActive: newStatus === 'active',
          ...extraFields,
        },
        {
          onSuccess: () => {
            setShowCreateDialog(false);
            resetCreateForm();
          },
        },
      );
      return;
    }

    // Create mode
    if (!dialogProgramId) return;

    if (createMode === 'copy' && copySourceId) {
      copyMut.mutate(
        { sourceId: Number(copySourceId), newVersion: newVersion.trim() },
        {
          onSuccess: (data) => {
            setShowCreateDialog(false);
            resetCreateForm();
            setSelectedCurriculumId(data.id);
          },
        },
      );
    } else {
      createMut.mutate({ programId: dialogProgramId, version: newVersion.trim(), ...extraFields }, {
        onSuccess: () => {
          setShowCreateDialog(false);
          resetCreateForm();
        },
      });
    }
  }

  function handleAddCourse(courseId: number, semester: number, isCompulsory: boolean) {
    if (!selectedCurriculumId) return;
    addCourseMut.mutate(
      { curriculumId: selectedCurriculumId, courseId, suggestedSemester: semester, isCompulsory },
      { onSuccess: () => setShowAddCourse(false) },
    );
  }

  function handleShowCourseDetail(courseId: number) {
    setDetailCourseId(courseId);
    setShowDetailSheet(true);
  }

  const isCreatePending = createMut.isPending || copyMut.isPending;

  return (
    <div className="space-y-4">
      {/* ── Filtre Barı (iki kolonlu Proliz düzeni) ── */}
      {view === 'list' && (
        <Card>
          <CardContent className="pt-4 pb-4 space-y-3">
            {/* Üst: sadece Müfredat Ekle (ince çizgi altta ayraç) */}
            <div className="flex items-center justify-end border-b pb-2">
              <Button size="sm" className="h-9" onClick={handleOpenCreateDialog} title="Yeni müfredat ekle">
                <Plus className="h-4 w-4 mr-1" /> Müfredat Ekle
              </Button>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-x-6 gap-y-2.5">
              {/* Sol: Fakülte */}
              <div className="grid grid-cols-[65px_1fr] md:grid-cols-[80px_1fr] items-center gap-4">
                <Label className="text-sm text-left">Fakülte</Label>
                <Select value={facultyId?.toString() ?? ''} onValueChange={handleFacultyChange}>
                  <SelectTrigger className="h-9 text-sm">
                    {loadingFaculties
                      ? <Loader2 className="h-4 w-4 animate-spin" />
                      : <SelectValue placeholder="Seçin" />}
                  </SelectTrigger>
                  <SelectContent>
                    {(faculties ?? []).map(f => (
                      <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Sağ: Müfredat Yılı */}
              <div className="grid grid-cols-[90px_1fr] md:grid-cols-[110px_1fr] items-center gap-4">
                <Label className="text-sm text-left">Müfredat Yılı</Label>
                <Select value={yearFilter} onValueChange={val => setYearFilter(val as YearFilter)}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tümü</SelectItem>
                    {availableYears.map(y => (
                      <SelectItem key={y} value={y}>{y}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Sol: Bölüm */}
              <div className="grid grid-cols-[65px_1fr] md:grid-cols-[80px_1fr] items-center gap-4">
                <Label className="text-sm text-left">Bölüm</Label>
                <Select
                  value={departmentId?.toString() ?? ''}
                  onValueChange={handleDepartmentChange}
                  disabled={!facultyId}
                >
                  <SelectTrigger className="h-9 text-sm">
                    {loadingDepts
                      ? <Loader2 className="h-4 w-4 animate-spin" />
                      : <SelectValue placeholder="Seçin" />}
                  </SelectTrigger>
                  <SelectContent>
                    {(departments ?? []).map(d => (
                      <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Sağ: Durum */}
              <div className="grid grid-cols-[90px_1fr] md:grid-cols-[110px_1fr] items-center gap-4">
                <Label className="text-sm text-left">Durum</Label>
                <Select value={statusFilter} onValueChange={val => setStatusFilter(val as StatusFilter)}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tümü</SelectItem>
                    <SelectItem value="active">Aktif</SelectItem>
                    <SelectItem value="inactive">Pasif</SelectItem>
                    <SelectItem value="archived">Arşiv</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {/* Sol: Program */}
              <div className="grid grid-cols-[65px_1fr] md:grid-cols-[80px_1fr] items-center gap-4">
                <Label className="text-sm text-left">Program</Label>
                <Select
                  value={programId?.toString() ?? ''}
                  onValueChange={handleProgramChange}
                  disabled={!departmentId}
                >
                  <SelectTrigger className="h-9 text-sm">
                    {loadingPrograms
                      ? <Loader2 className="h-4 w-4 animate-spin" />
                      : <SelectValue placeholder="Seçin" />}
                  </SelectTrigger>
                  <SelectContent>
                    {(programs ?? []).map(p => (
                      <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Sağ: Anahtar kelimeyle ara — fakülte / program / müfredat adı / yıl / program kodu içinde */}
              <div className="grid grid-cols-[90px_1fr] md:grid-cols-[110px_1fr] items-center gap-4">
                <Label className="text-sm text-left">Ara</Label>
                <div className="relative">
                  <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
                  <Input
                    placeholder="Fakülte, program, müfredat, yıl..."
                    className="h-9 text-sm pl-8"
                    value={searchKeyword}
                    onChange={e => setSearchKeyword(e.target.value)}
                  />
                </div>
              </div>

            </div>
          </CardContent>
        </Card>
      )}

      {/* ── LISTE VIEW: Müfredat tablosu ── */}
      {view === 'list' && !facultyId && !departmentId && !programId && (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            <BookOpen className="h-12 w-12 mx-auto mb-3 opacity-30" />
            <p>Müfredatları listelemek için en az bir fakülte seçin</p>
          </CardContent>
        </Card>
      )}

      {view === 'list' && (facultyId || departmentId || programId) && loadingCurricula && (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            <Loader2 className="h-6 w-6 animate-spin mx-auto mb-2" />
            Yükleniyor...
          </CardContent>
        </Card>
      )}

      {view === 'list' && (facultyId || departmentId || programId) && !loadingCurricula && filteredCurricula.length === 0 && (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            <p className="mb-2">Filtre kriterlerinize uygun müfredat bulunamadı.</p>
            <Button variant="outline" onClick={handleOpenCreateDialog}>
              <Plus className="h-4 w-4 mr-1" /> Yeni Müfredat Oluştur
            </Button>
          </CardContent>
        </Card>
      )}

      {view === 'list' && (facultyId || departmentId || programId) && !loadingCurricula && filteredCurricula.length > 0 && (
        <Card>
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-muted/50 border-b">
                  <tr className="text-left">
                    <th className="px-3 py-2.5 font-medium w-10"></th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">Yıl</th>
                    <th className="px-3 py-2.5 font-medium min-w-[200px]">Müfredat Adı</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">Başl.Tarihi</th>
                    <th className="px-3 py-2.5 font-medium whitespace-nowrap">Bit.Tarihi</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Aktif</th>
                    <th className="px-3 py-2.5 font-medium min-w-[180px]">Fakülte</th>
                    <th className="px-3 py-2.5 font-medium min-w-[200px]">Program</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.Kredi</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.AKTS</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.Ders</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Zor.Ders</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Seç.Ders</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Akt.Öğr.Say</th>
                    <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.Öğr.Say</th>
                    <th className="px-3 py-2.5 font-medium w-20 text-center">İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedCurricula.map(c => (
                    <tr key={c.id} className="border-b hover:bg-muted/30">
                      {/* Sol dişli menü */}
                      <td className="px-3 py-2">
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8" title="İşlemler">
                              <Settings className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="start" className="w-56">
                            <DropdownMenuItem onClick={() => handleViewCourses(c.id)}>
                              <BookOpen className="h-4 w-4 mr-2" /> Müfredat Dersleri
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => {
                              setTermRulesTarget({ id: c.id, label: `(${c.version}) ${c.name ?? c.programName}` });
                              setShowTermRulesDialog(true);
                            }}>
                              <Settings className="h-4 w-4 mr-2" /> Dönem Kuralları
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={() => navigate(ROUTES.PROMOTION_RULES)}
                              title="Müfredat Sınıf Dönem Tanımları sayfasını aç"
                            >
                              <GraduationCap className="h-4 w-4 mr-2" /> Sınıf Atlatma Kuralları
                            </DropdownMenuItem>
                            <DropdownMenuItem disabled title="Yakında">
                              <Scissors className="h-4 w-4 mr-2" /> Enstitü İlişik Kesme Kr.
                            </DropdownMenuItem>
                            <DropdownMenuItem disabled title="Yakında">
                              <Printer className="h-4 w-4 mr-2" /> Ders Listesi Yazdır
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap font-medium">{c.version}</td>
                      <td className="px-3 py-2">{c.name ?? '—'}</td>
                      <td className="px-3 py-2 whitespace-nowrap">{c.startDate ? c.startDate.slice(0, 10) : '—'}</td>
                      <td className="px-3 py-2 whitespace-nowrap">{c.endDate ? c.endDate.slice(0, 10) : '—'}</td>
                      <td className="px-3 py-2 text-center">
                        {c.isActive && <CheckCircle className="h-4 w-4 text-green-600 mx-auto" />}
                      </td>
                      <td className="px-3 py-2">{c.facultyName}</td>
                      <td className="px-3 py-2">{c.programName}</td>
                      <td className="px-3 py-2 text-center">{c.totalCredit}</td>
                      <td className="px-3 py-2 text-center">{c.totalEcts}</td>
                      <td className="px-3 py-2 text-center">{c.courseCount}</td>
                      <td className="px-3 py-2 text-center">{c.compulsoryCount}</td>
                      <td className="px-3 py-2 text-center">{c.electiveCount}</td>
                      <td className="px-3 py-2 text-center">{c.activeStudentCount}</td>
                      <td className="px-3 py-2 text-center">{c.totalStudentCount}</td>
                      {/* Sağ aksiyonlar — Düzenle (primary) + kebab (Kopyala / Aktif Et / Sil) */}
                      <td className="px-3 py-2">
                        <div className="flex items-center justify-center gap-0.5">
                          <Button
                            variant="ghost" size="icon" className="h-8 w-8"
                            title="Düzenle"
                            onClick={() => handleOpenEditDialog(c)}
                          >
                            <Edit className="h-4 w-4" />
                          </Button>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="h-8 w-8" title="Daha fazla">
                                <MoreHorizontal className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => {
                                setCopyTarget({ id: c.id, label: `${c.version} — ${c.name ?? c.programName}` });
                              }}>
                                <Copy className="h-4 w-4 mr-2" /> Kopyala
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                disabled={c.isActive}
                                onClick={() => setActivateTarget({ id: c.id, label: `${c.version} — ${c.name ?? c.programName}` })}
                              >
                                <CheckCircle className={`h-4 w-4 mr-2 ${c.isActive ? 'text-green-600' : ''}`} />
                                {c.isActive ? 'Zaten Aktif' : 'Aktif Et'}
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-destructive focus:text-destructive"
                                onClick={() => setDeleteTarget({ id: c.id, label: `${c.version} — ${c.name ?? c.programName}` })}
                              >
                                <Trash2 className="h-4 w-4 mr-2" /> Sil
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination — mobilde flex-wrap */}
            <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2.5 border-t bg-muted/20 text-sm">
              <span className="text-muted-foreground">
                {filteredCurricula.length} kayıt — Sayfa {currentPage}/{totalPages}
              </span>
              {totalPages > 1 && (
                <div className="flex items-center gap-1">
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === 1} onClick={() => setCurrentPage(1)}>
                    <ChevronsLeft className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === 1} onClick={() => setCurrentPage(p => Math.max(1, p - 1))}>
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <span className="px-2 min-w-[2rem] text-center">{currentPage}</span>
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === totalPages} onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}>
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === totalPages} onClick={() => setCurrentPage(totalPages)}>
                    <ChevronsRight className="h-4 w-4" />
                  </Button>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* ── COURSES VIEW: Seçili müfredatın ders listesi (semester groups) ── */}
      {view === 'courses' && (
        <>
          {/* Breadcrumb + geri butonu + müfredat aksiyonları */}
          <Card>
            <CardContent className="py-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <Button variant="ghost" size="sm" className="h-8" onClick={handleBackToList}>
                    <ChevronLeft className="h-4 w-4 mr-1" /> Müfredat Listesi
                  </Button>
                  <span className="text-muted-foreground">/</span>
                  <span className="font-medium">
                    {detail ? `${detail.programName} — ${detail.version}` : 'Yükleniyor...'}
                  </span>
                  {detail?.isActive && <Badge variant="default" className="text-xs">Aktif</Badge>}
                </div>
                {/* Müfredat seviyesi aksiyonlar — Düzenle (primary) + kebab (Kopyala / Aktif Et / Sil) */}
                {detail && selectedCurr && (
                  <div className="flex items-center gap-0.5">
                    <Button
                      variant="ghost" size="icon" className="h-8 w-8"
                      title="Düzenle"
                      onClick={() => handleOpenEditDialog(selectedCurr)}
                    >
                      <Edit className="h-4 w-4" />
                    </Button>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8" title="Daha fazla">
                          <MoreHorizontal className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => {
                          setCopyTarget({ id: selectedCurr.id, label: `${selectedCurr.version} — ${selectedCurr.name ?? selectedCurr.programName}` });
                        }}>
                          <Copy className="h-4 w-4 mr-2" /> Kopyala
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          disabled={detail.isActive}
                          onClick={() => setActivateTarget({ id: selectedCurr.id, label: `${selectedCurr.version} — ${selectedCurr.name ?? selectedCurr.programName}` })}
                        >
                          <CheckCircle className={`h-4 w-4 mr-2 ${detail.isActive ? 'text-green-600' : ''}`} />
                          {detail.isActive ? 'Zaten Aktif' : 'Aktif Et'}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          className="text-destructive focus:text-destructive"
                          onClick={() => setDeleteTarget({ id: selectedCurr.id, label: `${selectedCurr.version} — ${selectedCurr.name ?? selectedCurr.programName}` })}
                        >
                          <Trash2 className="h-4 w-4 mr-2" /> Sil
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          {!selectedCurriculumId ? null : (
            <>
          {/* ── Tek satır: Filtreler + Bilgi + Aksiyonlar ── */}
          {loadingDetail ? (
            <Skeleton className="h-16 w-full" />
          ) : detail && (
            <Card>
              <CardContent className="py-3">
                <div className="flex flex-wrap items-center gap-2">
                  {/* Proliz tarzı: iki ayrı arama alanı (Ders Kodu | Ders Adı) */}
                  <Input
                    placeholder="Ders Kodu"
                    className="h-9 w-full sm:w-32 text-sm"
                    value={searchCode}
                    onChange={e => setSearchCode(e.target.value)}
                  />
                  <Input
                    placeholder="Ders Adı"
                    className="h-9 w-full sm:w-44 text-sm"
                    value={searchName}
                    onChange={e => setSearchName(e.target.value)}
                  />

                  <Select
                    value={classFilter?.toString() ?? 'all'}
                    onValueChange={val => setClassFilter(val === 'all' ? null : Number(val))}
                  >
                    <SelectTrigger className="h-9 w-32 text-sm">
                      <SelectValue placeholder="Sınıf: Tümü" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">Sınıf: Tümü</SelectItem>
                      <SelectItem value="1">1. Sınıf</SelectItem>
                      <SelectItem value="2">2. Sınıf</SelectItem>
                      <SelectItem value="3">3. Sınıf</SelectItem>
                      <SelectItem value="4">4. Sınıf</SelectItem>
                    </SelectContent>
                  </Select>

                  <Select
                    value={termTypeFilter}
                    onValueChange={val => setTermTypeFilter(val as 'all' | 'fall' | 'spring')}
                  >
                    <SelectTrigger className="h-9 w-32 text-sm">
                      <SelectValue placeholder="Dönem: Tümü" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">Dönem: Tümü</SelectItem>
                      <SelectItem value="fall">Güz</SelectItem>
                      <SelectItem value="spring">Bahar</SelectItem>
                    </SelectContent>
                  </Select>

                  <span className="hidden md:inline text-muted-foreground">|</span>

                  <label className="flex items-center gap-1.5 text-sm cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={showGroupedCourses}
                      onChange={e => setShowGroupedCourses(e.target.checked)}
                      className="h-4 w-4 rounded border-input accent-primary cursor-pointer"
                    />
                    <span>Gruplu Dersleri Göster</span>
                  </label>

                  {/* Sağ aksiyonlar — Proliz: Ders Ekle | Grup Ekle | Ek İşlemler */}
                  <div className="ml-auto flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      className="h-9 text-sm"
                      onClick={() => setShowAddCourse(true)}
                    >
                      <Plus className="h-4 w-4 mr-1" /> Ders Ekle
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      className="h-9 text-sm"
                      disabled
                      title="Yakında"
                    >
                      <Plus className="h-4 w-4 mr-1" /> Grup Ekle
                    </Button>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button size="sm" variant="outline" className="h-9 text-sm">
                          <MoreHorizontal className="h-4 w-4 mr-1" />
                          Ek İşlemler
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem disabled title="Yakında">
                          <Printer className="h-4 w-4 mr-2" /> Ders Listesi Yazdır
                        </DropdownMenuItem>
                        <DropdownMenuItem disabled title="Yakında">
                          <Package className="h-4 w-4 mr-2" /> Bilgi Paketi
                        </DropdownMenuItem>
                        <DropdownMenuItem disabled title="Yakında">
                          <Layers className="h-4 w-4 mr-2" /> Toplu Gruplandırma
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {loadingDetail ? (
            <Skeleton className="h-64 w-full" />
          ) : !detail ? (
            <Card>
              <CardContent className="py-8 text-center text-muted-foreground">
                Veri yüklenemedi
              </CardContent>
            </Card>
          ) : flatCourses.length === 0 ? (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                {searchCode || searchName || classFilter !== null || termTypeFilter !== 'all'
                  ? 'Filtrenizle eşleşen ders bulunamadı'
                  : 'Henüz ders eklenmemiş'}
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-0">
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/50 border-b">
                      <tr className="text-left">
                        <th className="px-2 py-2.5 w-10"></th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">Ders Kodu</th>
                        <th className="px-3 py-2.5 font-medium min-w-[240px]">Ders Adı</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Z/S</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Krd</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">AKTS</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Teo</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Uyg</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Lab</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Sınıf</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Dönem</th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">Kategori</th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">Öğr.Elemanı</th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">İntibak</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Ön K</th>
                        <th className="px-3 py-2.5 font-medium text-center w-24">İşlem</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pagedCourses.map(c => {
                        const sinif = Math.ceil(c.suggestedSemester / 2);
                        const donem = c.suggestedSemester % 2 === 1 ? 'G' : 'B';
                        const courseLabel = `${c.courseCode} — ${c.courseName}`;
                        return (
                          <tr key={c.courseId} className="border-b hover:bg-muted/30">
                            <td className="px-2 py-2">
                              <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-7 w-7"
                                    title="Ders işlemleri"
                                  >
                                    <Settings className="h-4 w-4" />
                                  </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="start">
                                  <DropdownMenuItem disabled title="Yakında">
                                    <Package className="h-4 w-4 mr-2" /> Bilgi Paketi
                                  </DropdownMenuItem>
                                  <DropdownMenuItem
                                    onClick={() =>
                                      setCourseLinksTarget({
                                        courseId: c.courseId,
                                        label: courseLabel,
                                        mode: 'prerequisite',
                                      })
                                    }
                                  >
                                    <BookOpen className="h-4 w-4 mr-2" /> Ön Koşul Tanımları
                                  </DropdownMenuItem>
                                  <DropdownMenuItem disabled title="Yakında">
                                    <Layers className="h-4 w-4 mr-2" /> Gruplandırma İşlemleri
                                  </DropdownMenuItem>
                                  <DropdownMenuItem
                                    onClick={() =>
                                      setCourseLinksTarget({
                                        courseId: c.courseId,
                                        label: courseLabel,
                                        mode: 'equivalency',
                                      })
                                    }
                                  >
                                    <Scissors className="h-4 w-4 mr-2" /> İntibak İşlemleri
                                  </DropdownMenuItem>
                                </DropdownMenuContent>
                              </DropdownMenu>
                            </td>
                            <td className="px-3 py-2 whitespace-nowrap font-medium">{c.courseCode}</td>
                            <td className="px-3 py-2">
                              <button
                                type="button"
                                onClick={() => handleShowCourseDetail(c.courseId)}
                                className="text-left text-primary hover:underline"
                                title="Ders detayı"
                              >
                                {c.courseName}
                              </button>
                            </td>
                            <td className="px-3 py-2 text-center">
                              <Badge variant={c.isCompulsory ? 'default' : 'secondary'} className="text-xs">
                                {c.isCompulsory ? 'Z' : 'S'}
                              </Badge>
                            </td>
                            <td className="px-3 py-2 text-right text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-right">{c.ects}</td>
                            <td className="px-3 py-2 text-right">{c.theoryHours}</td>
                            <td className="px-3 py-2 text-right text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-right">{c.labHours}</td>
                            <td className="px-3 py-2 text-center">{sinif}</td>
                            <td className="px-3 py-2 text-center">{donem}</td>
                            <td className="px-3 py-2">{c.courseCategory ?? '—'}</td>
                            <td className="px-3 py-2 text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-center text-muted-foreground">—</td>
                            <td className="px-3 py-2">
                              <div className="flex items-center justify-center gap-0.5">
                                <Button
                                  variant="ghost" size="icon" className="h-8 w-8"
                                  title="Düzenle"
                                  onClick={() => handleShowCourseDetail(c.courseId)}
                                >
                                  <Edit className="h-4 w-4" />
                                </Button>
                                <Button
                                  variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive"
                                  title="Sil"
                                  onClick={() => removeCourseMut.mutate({ curriculumId: selectedCurriculumId!, courseId: c.courseId })}
                                  disabled={removeCourseMut.isPending}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Pagination — ders listesi (15 ders/sayfa) */}
                <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2.5 border-t bg-muted/20 text-sm">
                  <span className="text-muted-foreground">
                    {flatCourses.length} ders — Sayfa {coursesPage}/{coursesTotalPages}
                  </span>
                  {coursesTotalPages > 1 && (
                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === 1}
                        onClick={() => setCoursesPage(1)}
                        title="İlk sayfa"
                      >
                        <ChevronsLeft className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === 1}
                        onClick={() => setCoursesPage(p => Math.max(1, p - 1))}
                        title="Önceki"
                      >
                        <ChevronLeft className="h-4 w-4" />
                      </Button>
                      <span className="px-2 min-w-[2rem] text-center">{coursesPage}</span>
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === coursesTotalPages}
                        onClick={() => setCoursesPage(p => Math.min(coursesTotalPages, p + 1))}
                        title="Sonraki"
                      >
                        <ChevronRight className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === coursesTotalPages}
                        onClick={() => setCoursesPage(coursesTotalPages)}
                        title="Son sayfa"
                      >
                        <ChevronsRight className="h-4 w-4" />
                      </Button>
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          )}
            </>
          )}
        </>
      )}

      {/* ── Create / Edit Curriculum Dialog ── */}
      <Dialog open={showCreateDialog} onOpenChange={(open) => {
        setShowCreateDialog(open);
        if (!open) resetCreateForm();
      }}>
        <ResizableDialogContent defaultWidth={750} minWidth={500} minHeight={400} className="flex flex-col p-0 gap-0">
          <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
            <DialogTitle>
              {editingCurriculumId !== null ? 'Müfredat Düzenleniyor' : 'Müfredat (Yeni Ekleniyor)'}
            </DialogTitle>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto px-6 pb-2 space-y-4">
            {/* Yıl + Durumu */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Yıl *</Label>
                <Input
                  value={newVersion}
                  onChange={e => setNewVersion(e.target.value)}
                  placeholder="2024"
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Durumu</Label>
                <Select value={newStatus} onValueChange={(val) => setNewStatus(val as 'active' | 'inactive')}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="active">Aktif</SelectItem>
                    <SelectItem value="inactive">Pasif</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            {/* Fakülte / Bölüm / Program — Edit mode'da readonly info bar, Create mode'da seçilebilir dropdown'lar */}
            {editingCurriculumId !== null && editingCurr && (
              <div className="rounded-md border bg-muted/50 p-3 text-xs space-y-1">
                <div className="text-muted-foreground font-medium uppercase tracking-wide text-[10px]">
                  Müfredat Bilgisi (değiştirilemez)
                </div>
                <div><span className="text-muted-foreground">Fakülte:</span> <span className="font-medium">{editingCurr.facultyName}</span></div>
                <div><span className="text-muted-foreground">Bölüm:</span> <span className="font-medium">{editingCurr.departmentName}</span></div>
                <div><span className="text-muted-foreground">Program:</span> <span className="font-medium">{editingCurr.programName}</span></div>
              </div>
            )}
            {editingCurriculumId === null && (
              <>
            <div className="space-y-1">
              <Label className="text-xs">Fakülte *</Label>
              <Select
                value={dialogFacultyId?.toString() ?? ''}
                onValueChange={handleDialogFacultyChange}
              >
                <SelectTrigger className="h-9 text-sm">
                  {loadingFaculties
                    ? <Loader2 className="h-4 w-4 animate-spin" />
                    : <SelectValue placeholder="Fakülte seçin" />}
                </SelectTrigger>
                <SelectContent>
                  {(faculties ?? []).map(f => (
                    <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Bölüm *</Label>
              <Select
                value={dialogDepartmentId?.toString() ?? ''}
                onValueChange={handleDialogDepartmentChange}
                disabled={!dialogFacultyId}
              >
                <SelectTrigger className="h-9 text-sm">
                  {loadingDialogDepts
                    ? <Loader2 className="h-4 w-4 animate-spin" />
                    : <SelectValue placeholder="Bölüm seçin" />}
                </SelectTrigger>
                <SelectContent>
                  {(dialogDepartments ?? []).map(d => (
                    <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Program *</Label>
              <Select
                value={dialogProgramId?.toString() ?? ''}
                onValueChange={handleDialogProgramChange}
                disabled={!dialogDepartmentId}
              >
                <SelectTrigger className="h-9 text-sm">
                  {loadingDialogPrograms
                    ? <Loader2 className="h-4 w-4 animate-spin" />
                    : <SelectValue placeholder="Program seçin" />}
                </SelectTrigger>
                <SelectContent>
                  {(dialogPrograms ?? []).map(p => (
                    <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
              </>
            )}

            {/* Müfredat Tipi */}
            <div className="space-y-1">
              <Label className="text-xs">Müfredat Tipi</Label>
              <Select value={newMufredatTipi} onValueChange={setNewMufredatTipi}>
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Programa Özel">Programa Özel</SelectItem>
                  <SelectItem value="Ortak Müfredat">Ortak Müfredat</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Müfredat Ad */}
            <div className="space-y-1">
              <Label className="text-xs">Müfredat Ad</Label>
              <Input
                value={newName}
                onChange={e => setNewName(e.target.value)}
                placeholder="Bilgisayar Müh. 2024 Müfredatı"
              />
            </div>

            {/* Kısa Ad */}
            <div className="space-y-1">
              <Label className="text-xs">Kısa Ad</Label>
              <Input
                value={newShortName}
                onChange={e => setNewShortName(e.target.value)}
                placeholder="BM-2024"
              />
            </div>

            {/* İngilizce Ad */}
            <div className="space-y-1">
              <Label className="text-xs">İngilizce Ad</Label>
              <Input
                value={newEnglishName}
                onChange={e => setNewEnglishName(e.target.value)}
                placeholder="Computer Eng. 2024 Curriculum"
              />
            </div>

            {/* Başlangıç / Bitiş Tarihi */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Başlangıç Tarihi</Label>
                <Input type="date" value={newStartDate} onChange={e => setNewStartDate(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Bitiş Tarihi</Label>
                <Input type="date" value={newEndDate} onChange={e => setNewEndDate(e.target.value)} />
              </div>
            </div>

            {/* Karar Tarih / Karar No */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Karar Tarih</Label>
                <Input
                  value={newDecisionDate}
                  onChange={e => setNewDecisionDate(e.target.value)}
                  placeholder="01.01.2024"
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Karar No</Label>
                <Input
                  value={newDecisionNo}
                  onChange={e => setNewDecisionNo(e.target.value)}
                  placeholder="2024/01"
                />
              </div>
            </div>

            {/* Karar Açıklama */}
            <div className="space-y-1">
              <Label className="text-xs">Karar Açıklama</Label>
              <Input
                value={newDecisionNote}
                onChange={e => setNewDecisionNote(e.target.value)}
                placeholder="Senato kararı ile kabul edilmiştir"
              />
            </div>

            {/* Geçme Sistemi / Staj Süresi */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Geçme Sistemi</Label>
                <Select value={newGradingSystem} onValueChange={setNewGradingSystem}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Sınıf Geçme">Sınıf Geçme</SelectItem>
                    <SelectItem value="Ders Geçme">Ders Geçme</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Staj Süresi (gün)</Label>
                <Input
                  type="number"
                  min={0}
                  value={newInternshipDuration}
                  onChange={e => setNewInternshipDuration(e.target.value)}
                  placeholder="40"
                />
              </div>
            </div>

            {/* Açıklama */}
            <div className="space-y-1">
              <Label className="text-xs">Açıklama</Label>
              <Textarea
                value={newDescription}
                onChange={e => setNewDescription(e.target.value)}
                placeholder="Genel açıklama..."
                rows={2}
              />
            </div>

            {/* Checkbox'lar — UI-only, backend'e gitmez */}
            <div className="space-y-2 pt-1">
              <label className="flex items-start gap-2 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={newIncludeFormation}
                  onChange={e => setNewIncludeFormation(e.target.checked)}
                  className="mt-0.5 h-4 w-4 rounded border-input accent-primary cursor-pointer"
                />
                <span>Mezuniyette Formasyon Derslerini Toplam Kredi/AKTS ve Seçmeli/Zorunlu Ders Sayılarına Dahil Et</span>
              </label>
              <label className="flex items-start gap-2 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={newHideFromPackage}
                  onChange={e => setNewHideFromPackage(e.target.checked)}
                  className="mt-0.5 h-4 w-4 rounded border-input accent-primary cursor-pointer"
                />
                <span>Bilgi Paketinde Gösterme</span>
              </label>
            </div>

            {/* ── Kopyalama (en altta) — dialog'da seçili programa göre, sadece Create mode ── */}
            {editingCurriculumId === null && (dialogCurricula ?? []).length > 0 && (
              <fieldset className="space-y-3">
                <legend className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Kopyalama</legend>
                <div className="flex items-center gap-1 border rounded-md p-1">
                  <button
                    onClick={() => { setCreateMode('empty'); setCopySourceId(''); }}
                    className={`flex-1 text-xs px-2.5 py-1.5 rounded transition-colors ${
                      createMode === 'empty'
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                    }`}
                  >
                    Boş oluştur
                  </button>
                  <button
                    onClick={() => setCreateMode('copy')}
                    className={`flex-1 text-xs px-2.5 py-1.5 rounded transition-colors ${
                      createMode === 'copy'
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                    }`}
                  >
                    Mevcut versiyondan kopyala
                  </button>
                </div>
                {createMode === 'copy' && (
                  <Select value={copySourceId} onValueChange={setCopySourceId}>
                    <SelectTrigger className="h-8 text-sm">
                      <SelectValue placeholder="Kaynak versiyon seçin" />
                    </SelectTrigger>
                    <SelectContent>
                      {(dialogCurricula ?? []).map(c => (
                        <SelectItem key={c.id} value={c.id.toString()}>
                          {c.version} ({c.courseCount} ders, {c.totalEcts} AKTS)
                          {c.isActive ? ' — Aktif' : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </fieldset>
            )}
          </div>
          <DialogFooter className="shrink-0 px-6 py-4 border-t gap-2">
            <Button variant="outline" onClick={() => setShowCreateDialog(false)}>
              İptal
            </Button>
            <Button
              onClick={handleCreate}
              disabled={
                isCreatePending
                || updateMut.isPending
                || !newVersion.trim()
                || (editingCurriculumId === null && !dialogProgramId)
                || (editingCurriculumId === null && createMode === 'copy' && !copySourceId)
              }
            >
              {(isCreatePending || updateMut.isPending) && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              {editingCurriculumId !== null
                ? 'Kaydet'
                : createMode === 'copy' ? 'Kopyala' : 'Oluştur'}
            </Button>
          </DialogFooter>
        </ResizableDialogContent>
      </Dialog>

      {/* ── Add Course Dialog ── */}
      <AddCourseDialog
        open={showAddCourse}
        onClose={() => setShowAddCourse(false)}
        onAdd={handleAddCourse}
        isPending={addCourseMut.isPending}
        existingCourseIds={existingCourseIds}
        catalogRoute={ROUTES.COURSE_CATALOG_ADMIN}
      />

      {/* ── Course Detail / Edit Sheet ── */}
      <CourseDetailSheet
        key={`${detailCourseId}-${selectedCurriculumId}`}
        courseId={detailCourseId}
        curriculumId={selectedCurriculumId}
        open={showDetailSheet}
        onOpenChange={setShowDetailSheet}
        curriculumCourse={detailCurriculumCourse}
      />

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
    </div>
  );
}
