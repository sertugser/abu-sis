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
import { BookOpen, CheckCircle, Loader2, Plus, Settings, Trash2 } from 'lucide-react';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
} from '../hooks/useAdminAcademicStructure';
import {
  useCurriculumList,
  useCurriculumDetail,
  useCreateCurriculum,
  useDeleteCurriculum,
  useActivateCurriculum,
  useAddCurriculumCourse,
  useUpdateCurriculumCourse,
  useRemoveCurriculumCourse,
  useCopyCurriculum,
} from '../hooks/useCurriculumManagement';
import SemesterGroup from '../components/curriculum-management/SemesterGroup';
import AddCourseDialog from '../components/curriculum-management/AddCourseDialog';
import CourseDetailSheet from '../components/curriculum-management/CourseDetailSheet';
import { CurriculumEctsRulesDialog } from '../components/curriculum-management/CurriculumEctsRulesDialog';
import type { CurriculumCourseItemDto } from '../types/curriculumManagement';
import { ROUTES } from '@/lib/constants';

type SortBy = 'default' | 'code' | 'name' | 'ects';
type CreateMode = 'empty' | 'copy';

export default function CurriculumManagementPage() {
  // ── Filter state ──
  const [facultyId, setFacultyId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [programId, setProgramId] = useState<number | undefined>();
  const [selectedCurriculumId, setSelectedCurriculumId] = useState<number | null>(null);

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
  const [newGradingSystem, setNewGradingSystem] = useState('');
  const [newInternshipDuration, setNewInternshipDuration] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [createMode, setCreateMode] = useState<CreateMode>('empty');
  const [copySourceId, setCopySourceId] = useState<string>('');
  const [showAddCourse, setShowAddCourse] = useState(false);
  const [showEctsRulesDialog, setShowEctsRulesDialog] = useState(false);

  // ── Tabs ──

  // ── Course detail sheet ──
  const [detailCourseId, setDetailCourseId] = useState<number | null>(null);
  const [showDetailSheet, setShowDetailSheet] = useState(false);

  // ── Search & table filters ──
  const [searchTerm, setSearchTerm] = useState('');
  const [classFilter, setClassFilter] = useState<number | null>(null); // 1,2,3,4
  const [termTypeFilter, setTermTypeFilter] = useState<'all' | 'fall' | 'spring'>('all');
  const [sortBy, setSortBy] = useState<SortBy>('default');

  // ── Queries ──
  const { data: faculties, isLoading: loadingFaculties } = useAdminFaculties();
  const { data: departments, isLoading: loadingDepts } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs, isLoading: loadingPrograms } = useAdminProgramsByStructure(departmentId);
  const { data: curricula, isLoading: loadingCurricula } = useCurriculumList(programId);
  const { data: detail, isLoading: loadingDetail } = useCurriculumDetail(selectedCurriculumId);

  // ── Mutations ──
  const createMut = useCreateCurriculum();
  const deleteMut = useDeleteCurriculum();
  const activateMut = useActivateCurriculum();
  const addCourseMut = useAddCurriculumCourse();
  const updateCourseMut = useUpdateCurriculumCourse();
  const removeCourseMut = useRemoveCurriculumCourse();
  const copyMut = useCopyCurriculum();

  // ── Derived ──
  const semesterGroups = useMemo(() => {
    if (!detail) return [];
    const lower = searchTerm.toLowerCase();

    let filtered: CurriculumCourseItemDto[] = detail.courses;

    // text search
    if (lower) {
      filtered = filtered.filter(
        c =>
          c.courseCode.toLowerCase().includes(lower) ||
          c.courseName.toLowerCase().includes(lower),
      );
    }

    // class filter: 1 → semesters 1,2; 2 → semesters 3,4; 3 → semesters 5,6; 4 → semesters 7,8
    if (classFilter !== null) {
      const minSem = (classFilter - 1) * 2 + 1;
      const maxSem = classFilter * 2;
      filtered = filtered.filter(c => c.suggestedSemester >= minSem && c.suggestedSemester <= maxSem);
    }

    // term type filter: fall → odd semesters, spring → even semesters
    if (termTypeFilter === 'fall') {
      filtered = filtered.filter(c => c.suggestedSemester % 2 !== 0);
    } else if (termTypeFilter === 'spring') {
      filtered = filtered.filter(c => c.suggestedSemester % 2 === 0);
    }

    // sort
    if (sortBy === 'code') {
      filtered = [...filtered].sort((a, b) => a.courseCode.localeCompare(b.courseCode));
    } else if (sortBy === 'name') {
      filtered = [...filtered].sort((a, b) => a.courseName.localeCompare(b.courseName));
    } else if (sortBy === 'ects') {
      filtered = [...filtered].sort((a, b) => b.ects - a.ects);
    }

    const map = new Map<number, CurriculumCourseItemDto[]>();
    for (const c of filtered) {
      const arr = map.get(c.suggestedSemester) ?? [];
      arr.push(c);
      map.set(c.suggestedSemester, arr);
    }
    return Array.from(map.entries()).sort(([a], [b]) => a - b);
  }, [detail, searchTerm, classFilter, termTypeFilter, sortBy]);

  const existingCourseIds = useMemo(
    () => new Set(detail?.courses?.map(c => c.courseId) ?? []),
    [detail],
  );

  const totalEcts = useMemo(
    () => detail?.courses?.reduce((s, c) => s + c.ects, 0) ?? 0,
    [detail],
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
    setNewGradingSystem('');
    setNewInternshipDuration('');
    setNewDescription('');
    setCreateMode('empty');
    setCopySourceId('');
  }

  function handleCreate() {
    if (!programId || !newVersion.trim()) return;

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
      createMut.mutate({ programId, version: newVersion.trim(), ...extraFields }, {
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

  // ── Curriculum version dropdown label ──
  function versionLabel(c: { version: string; isActive: boolean }) {
    return c.isActive ? `${c.version} (Aktif)` : c.version;
  }

  const isCreatePending = createMut.isPending || copyMut.isPending;

  return (
    <div className="space-y-4">
      {/* ── Row 1: Filter bar (Fakulte / Bölüm / Program / Müfredat) ── */}
      <Card>
        <CardContent className="pt-4 pb-4">
          <div className={`grid items-end gap-3 ${programId ? 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-[1fr_1fr_1fr_auto_auto]' : 'grid-cols-1 sm:grid-cols-3'}`}>
            {/* Fakulte */}
            <div className="space-y-1">
              <Label className="text-xs">Fakülte</Label>
              <Select value={facultyId?.toString() ?? ''} onValueChange={handleFacultyChange}>
                <SelectTrigger className="h-8 text-sm">
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

            {/* Bölüm */}
            <div className="space-y-1">
              <Label className="text-xs">Bölüm</Label>
              <Select
                value={departmentId?.toString() ?? ''}
                onValueChange={handleDepartmentChange}
                disabled={!facultyId}
              >
                <SelectTrigger className="h-8 text-sm">
                  {loadingDepts
                    ? <Loader2 className="h-4 w-4 animate-spin" />
                    : <SelectValue placeholder="Bölüm seçin" />}
                </SelectTrigger>
                <SelectContent>
                  {(departments ?? []).map(d => (
                    <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Program */}
            <div className="space-y-1">
              <Label className="text-xs">Program</Label>
              <Select
                value={programId?.toString() ?? ''}
                onValueChange={handleProgramChange}
                disabled={!departmentId}
              >
                <SelectTrigger className="h-8 text-sm">
                  {loadingPrograms
                    ? <Loader2 className="h-4 w-4 animate-spin" />
                    : <SelectValue placeholder="Program seçin" />}
                </SelectTrigger>
                <SelectContent>
                  {(programs ?? []).map(p => (
                    <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Separator */}
            {/* separator removed — grid handles spacing */}

            {/* Müfredat version dropdown */}
            {programId && (
              <div className="space-y-1 min-w-[180px]">
                <Label className="text-xs">Müfredat</Label>
                {loadingCurricula ? (
                  <Skeleton className="h-9 w-44" />
                ) : (
                  <Select
                    value={selectedCurriculumId?.toString() ?? ''}
                    onValueChange={val => setSelectedCurriculumId(Number(val))}
                  >
                    <SelectTrigger className="h-8 text-sm">
                      <SelectValue placeholder="Versiyon seçin" />
                    </SelectTrigger>
                    <SelectContent>
                      {(curricula ?? []).map(c => (
                        <SelectItem key={c.id} value={c.id.toString()}>
                          {versionLabel(c)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            )}

            {/* + Yeni button */}
            {programId && (
              <div className="self-end">
                <Button
                  size="sm"
                  variant="outline"
                  className="h-9"
                  onClick={() => setShowCreateDialog(true)}
                >
                  <Plus className="h-3.5 w-3.5 mr-1" /> Yeni
                </Button>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* ── Empty state: no program selected ── */}
      {!programId && (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            <BookOpen className="h-12 w-12 mx-auto mb-3 opacity-30" />
            <p>Müfredatları görüntülemek için bir program seçin</p>
          </CardContent>
        </Card>
      )}

      {/* ── No curriculum selected ── */}
      {programId && !selectedCurriculumId && !loadingCurricula && (curricula ?? []).length === 0 && (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            <p className="mb-2">Bu program için henüz müfredat oluşturulmamış.</p>
            <Button variant="outline" onClick={() => setShowCreateDialog(true)}>
              <Plus className="h-4 w-4 mr-1" /> Yeni Müfredat Oluştur
            </Button>
          </CardContent>
        </Card>
      )}

      {programId && !selectedCurriculumId && (curricula ?? []).length > 0 && (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            Yukarıdan bir müfredat versiyonu seçin
          </CardContent>
        </Card>
      )}

      {/* ── Main content: curriculum detail ── */}
      {programId && selectedCurriculumId && (
        <>
          {/* ── Row 2: Search + table filters + header info ── */}
          {loadingDetail ? (
            <Skeleton className="h-24 w-full" />
          ) : detail && (
            <Card>
              <CardContent className="pt-3 pb-3">
                {/* Filter row */}
                <div className="flex flex-wrap items-center gap-2">
                  <Input
                    placeholder="Ders kodu veya adı ile ara..."
                    className="h-8 w-full sm:w-56 text-xs"
                    value={searchTerm}
                    onChange={e => setSearchTerm(e.target.value)}
                  />

                  {/* Seçmeli dersler pool üzerinden yönetilir — type filter kaldırıldı */}

                  {/* Sınıf filter */}
                  <Select
                    value={classFilter?.toString() ?? 'all'}
                    onValueChange={val => setClassFilter(val === 'all' ? null : Number(val))}
                  >
                    <SelectTrigger className="h-8 w-32 text-xs">
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

                  {/* Dönem filter */}
                  <Select
                    value={termTypeFilter}
                    onValueChange={val => setTermTypeFilter(val as 'all' | 'fall' | 'spring')}
                  >
                    <SelectTrigger className="h-8 w-32 text-xs">
                      <SelectValue placeholder="Dönem: Tümü" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">Dönem: Tümü</SelectItem>
                      <SelectItem value="fall">Güz</SelectItem>
                      <SelectItem value="spring">Bahar</SelectItem>
                    </SelectContent>
                  </Select>

                  {/* Sort */}
                  <Select
                    value={sortBy}
                    onValueChange={val => setSortBy(val as SortBy)}
                  >
                    <SelectTrigger className="h-8 w-40 text-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="default">Sırala: Varsayılan</SelectItem>
                      <SelectItem value="code">Sırala: Ders Kodu</SelectItem>
                      <SelectItem value="name">Sırala: Ders Adı</SelectItem>
                      <SelectItem value="ects">Sırala: AKTS</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                {/* Header info row */}
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 mt-3 pt-3 border-t">
                  <div className="flex flex-wrap items-center gap-2 text-sm">
                    <span className="font-semibold">{detail.programName} — {detail.version}</span>
                    {detail.isActive && <Badge variant="default" className="text-[10px]">Aktif</Badge>}
                    <span className="text-muted-foreground hidden sm:inline">|</span>
                    <span className="text-muted-foreground">{detail.courses.length} ders</span>
                    <span className="text-muted-foreground hidden sm:inline">|</span>
                    <span className="text-muted-foreground">{totalEcts} AKTS</span>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {!detail.isActive && (
                      <>
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-8 text-xs"
                          onClick={() => activateMut.mutate(selectedCurriculumId)}
                          disabled={activateMut.isPending}
                        >
                          {activateMut.isPending
                            ? <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
                            : <CheckCircle className="h-3.5 w-3.5 mr-1" />}
                          Aktif Et
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-8 text-xs text-destructive hover:text-destructive"
                          onClick={() => {
                            deleteMut.mutate(selectedCurriculumId);
                            setSelectedCurriculumId(null);
                          }}
                          disabled={deleteMut.isPending}
                        >
                          {deleteMut.isPending
                            ? <Loader2 className="h-3.5 w-3.5 animate-spin mr-1" />
                            : <Trash2 className="h-3.5 w-3.5 mr-1" />}
                          Sil
                        </Button>
                      </>
                    )}
                    <Button
                      size="sm"
                      variant="outline"
                      className="h-8 text-xs"
                      onClick={() => setShowEctsRulesDialog(true)}
                      title="Bu müfredata özel AKTS tavanı kuralları"
                    >
                      <Settings className="h-3.5 w-3.5 mr-1" />
                      AKTS Kuralları
                      {(detail.ectsLimitBelow200 != null
                        || detail.ectsLimit200To249 != null
                        || detail.ectsLimitAbove250 != null) && (
                        <Badge variant="secondary" className="ml-1 text-[10px] px-1 py-0">Özel</Badge>
                      )}
                    </Button>
                    <Button
                      size="sm"
                      className="h-8 text-xs"
                      onClick={() => setShowAddCourse(true)}
                    >
                      <Plus className="h-3.5 w-3.5 mr-1" /> Ders Ekle
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {loadingDetail ? (
            <div className="space-y-4">
              {[1, 2, 3].map(i => <Skeleton key={i} className="h-40" />)}
            </div>
          ) : !detail ? (
            <Card>
              <CardContent className="py-8 text-center text-muted-foreground">
                Veri yüklenemedi
              </CardContent>
            </Card>
          ) : semesterGroups.length === 0 ? (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                {searchTerm || classFilter !== null || termTypeFilter !== 'all'
                  ? 'Filtrenizle eşleşen ders bulunamadı'
                  : 'Henüz ders eklenmemiş'}
              </CardContent>
            </Card>
          ) : (
            semesterGroups.map(([sem, courses]) => (
              <SemesterGroup
                key={sem}
                semester={sem}
                courses={courses}
                onUpdateCourse={(courseId, req) =>
                  updateCourseMut.mutate({ curriculumId: selectedCurriculumId!, courseId, ...req })
                }
                onRemoveCourse={courseId =>
                  removeCourseMut.mutate({ curriculumId: selectedCurriculumId!, courseId })
                }
                onAddCourse={() => setShowAddCourse(true)}
                onShowCourseDetail={handleShowCourseDetail}
                isUpdating={updateCourseMut.isPending}
                isRemoving={removeCourseMut.isPending}
              />
            ))
          )}
        </>
      )}

      {/* ── Create Curriculum Dialog ── */}
      <Dialog open={showCreateDialog} onOpenChange={(open) => {
        setShowCreateDialog(open);
        if (!open) resetCreateForm();
      }}>
        <ResizableDialogContent defaultWidth={750} minWidth={500} minHeight={400} className="flex flex-col p-0 gap-0">
          <DialogHeader className="px-6 pt-6 pb-3 shrink-0">
            <DialogTitle>Yeni Müfredat Versiyonu</DialogTitle>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto px-6 space-y-5">
            {/* ── Temel Bilgiler ── */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Temel Bilgiler</legend>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label className="text-xs">Versiyon *</Label>
                  <Input value={newVersion} onChange={e => setNewVersion(e.target.value)} placeholder="2024" />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Müfredat Adı</Label>
                  <Input value={newName} onChange={e => setNewName(e.target.value)} placeholder="Bilgisayar Müh. 2024 Müfredatı" />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Kısa Adı</Label>
                  <Input value={newShortName} onChange={e => setNewShortName(e.target.value)} placeholder="BM-2024" />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">İngilizce Adı</Label>
                  <Input value={newEnglishName} onChange={e => setNewEnglishName(e.target.value)} placeholder="Computer Eng. 2024 Curriculum" />
                </div>
              </div>
            </fieldset>

            {/* ── Tarihler ── */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Tarihler</legend>
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
            </fieldset>

            {/* ── Resmi Bilgiler ── */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Resmi Bilgiler</legend>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label className="text-xs">Karar Tarihi</Label>
                  <Input value={newDecisionDate} onChange={e => setNewDecisionDate(e.target.value)} placeholder="01.01.2024" />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Karar No</Label>
                  <Input value={newDecisionNo} onChange={e => setNewDecisionNo(e.target.value)} placeholder="2024/01" />
                </div>
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Karar Açıklama</Label>
                <Input value={newDecisionNote} onChange={e => setNewDecisionNote(e.target.value)} placeholder="Senato kararı ile kabul edilmiştir" />
              </div>
            </fieldset>

            {/* ── Diğer ── */}
            <fieldset className="space-y-3">
              <legend className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Diğer</legend>
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label className="text-xs">Ders Geçme Sistemi</Label>
                  <Input value={newGradingSystem} onChange={e => setNewGradingSystem(e.target.value)} placeholder="Ders Geçme" />
                </div>
                <div className="space-y-1">
                  <Label className="text-xs">Staj Süresi (gün)</Label>
                  <Input type="number" min={0} value={newInternshipDuration} onChange={e => setNewInternshipDuration(e.target.value)} placeholder="40" />
                </div>
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Açıklama</Label>
                <Textarea value={newDescription} onChange={e => setNewDescription(e.target.value)} placeholder="Genel açıklama..." rows={2} />
              </div>
            </fieldset>

            {/* ── Kopyalama ── */}
            {(curricula ?? []).length > 0 && (
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
                      {(curricula ?? []).map(c => (
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
              disabled={isCreatePending || !newVersion.trim() || (createMode === 'copy' && !copySourceId)}
            >
              {isCreatePending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              {createMode === 'copy' ? 'Kopyala' : 'Oluştur'}
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

      {/* ── AKTS Kuralları Dialog ── */}
      {showEctsRulesDialog && detail && (
        <CurriculumEctsRulesDialog
          curriculum={detail}
          onClose={() => setShowEctsRulesDialog(false)}
        />
      )}
    </div>
  );
}
