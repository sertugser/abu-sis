import { useEffect, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  AlertCircle, CheckCircle2, Edit2, Package, Plus, Trash2, X,
} from 'lucide-react';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import {
  useOpenedCourses,
  useCurriculumCoursesForOpening,
  useDepartmentInstructors,
  useOpenCourse,
  useBulkOpenCourses,
  useUpdateOpenedSection,
  useCloseSection,
} from '../hooks/useCourseOpening';
import type { CurriculumCourseForOpeningDto } from '../types/courseOpening';

export default function CourseOpeningPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [showBulkDialog, setShowBulkDialog] = useState(false);

  // Set default dept when context loads
  useEffect(() => {
    if (selectedDeptId === null && departments.length > 0) {
      setSelectedDeptId(departments[0].departmentId);
    }
  }, [departments, selectedDeptId]);

  const deptId = selectedDeptId ?? departments[0]?.departmentId ?? null;
  const termId = activeTerm?.id ?? null;

  const openedQuery = useOpenedCourses(deptId, termId);

  if (contextQuery.isLoading) return <Skeleton className="h-96 rounded-xl" />;
  if (contextQuery.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sayfa yüklenemedi</AlertTitle>
        <AlertDescription>Yetki bilgileri alınamadı.</AlertDescription>
      </Alert>
    );
  }

  if (!termId) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Aktif dönem bulunamadı</AlertTitle>
        <AlertDescription>Aktif bir akademik dönem tanımlanmamış.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      {/* Üst bar - Filtreler */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium">Dönem:</label>
          <Badge variant="outline">{activeTerm?.name ?? 'Yükleniyor...'}</Badge>
        </div>

        <div className="flex items-center gap-2">
          <label className="text-sm font-medium">Bölüm:</label>
          <select
            className="h-9 rounded-md border border-input bg-background px-3 text-sm"
            value={deptId ?? ''}
            onChange={e => setSelectedDeptId(Number(e.target.value))}
          >
            {departments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
              </option>
            ))}
          </select>
        </div>

        <div className="ml-auto flex gap-2">
          <Button size="sm" onClick={() => setShowAddDialog(true)} disabled={!deptId || !termId}>
            <Plus className="h-4 w-4 mr-1" /> Ekle
          </Button>
          <Button size="sm" variant="outline" onClick={() => setShowBulkDialog(true)} disabled={!deptId || !termId}>
            <Package className="h-4 w-4 mr-1" /> Toplu Ders Aç
          </Button>
        </div>
      </div>

      {/* Ana tablo */}
      {openedQuery.isLoading || (!openedQuery.data && !openedQuery.isError) ? (
        <Skeleton className="h-64 rounded-xl" />
      ) : openedQuery.isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Veriler yüklenemedi</AlertTitle>
          <AlertDescription>
            {(openedQuery.error as { response?: { status?: number } })?.response?.status === 403
              ? 'Bu bölüm için yetkiniz bulunmuyor.'
              : 'Açılan ders verileri alınamadı. Lütfen tekrar deneyin.'}
          </AlertDescription>
        </Alert>
      ) : (
        <OpenedSectionsTable
          data={openedQuery.data!}
          deptId={deptId!}
          termId={termId!}
        />
      )}

      {/* Tek Ders Ekleme Dialog */}
      {showAddDialog && deptId && termId && (
        <AddCourseDialog
          departmentId={deptId}
          termId={termId}
          onClose={() => setShowAddDialog(false)}
        />
      )}

      {/* Toplu Ders Açma Dialog */}
      {showBulkDialog && deptId && termId && (
        <BulkOpenDialog
          departmentId={deptId}
          termId={termId}
          onClose={() => setShowBulkDialog(false)}
        />
      )}
    </div>
  );
}

// ── Açılan Dersler Tablosu ──

function OpenedSectionsTable({
  data,
  deptId,
  termId,
}: {
  data: { departmentName: string; termName: string; totalSections: number; sections: import('../types/courseOpening').OpenedCourseSectionDto[] };
  deptId: number;
  termId: number;
}) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editCapacity, setEditCapacity] = useState('');
  const [editInstructorId, setEditInstructorId] = useState('');
  const updateMutation = useUpdateOpenedSection(deptId, termId);
  const closeMutation = useCloseSection(deptId, termId);
  const instructorsQuery = useDepartmentInstructors(deptId);

  const handleEdit = (s: import('../types/courseOpening').OpenedCourseSectionDto) => {
    setEditingId(s.sectionId);
    setEditCapacity(s.capacity.toString());
    setEditInstructorId(s.instructorId?.toString() ?? '');
  };

  const handleSave = () => {
    if (editingId === null) return;
    updateMutation.mutate(
      {
        sectionId: editingId,
        capacity: editCapacity ? Number(editCapacity) : undefined,
        instructorId: editInstructorId ? Number(editInstructorId) : undefined,
      },
      { onSuccess: () => setEditingId(null) },
    );
  };

  const handleClose = (sectionId: number) => {
    if (confirm('Bu şubeyi kapatmak istediğinize emin misiniz?')) {
      closeMutation.mutate(sectionId);
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-sm">
          {data.departmentName} — {data.termName} ({data.totalSections} şube)
        </CardTitle>
      </CardHeader>
      <CardContent>
        {data.sections.length === 0 ? (
          <p className="text-center text-muted-foreground py-8">Henüz ders açılmamış.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">Şb.</TableHead>
                <TableHead>Ders Kodu</TableHead>
                <TableHead>Ders Adı</TableHead>
                <TableHead className="w-16">T+U</TableHead>
                <TableHead className="w-12">K</TableHead>
                <TableHead className="w-16">AKTS</TableHead>
                <TableHead className="w-12">Snf</TableHead>
                <TableHead className="w-28">KO/DO/K</TableHead>
                <TableHead>Öğretim Elemanı</TableHead>
                <TableHead className="w-24">İşlemler</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.sections.map(s => (
                <TableRow key={s.sectionId} className={!s.isActive ? 'opacity-50' : ''}>
                  <TableCell className="text-center">{s.sectionNumber}</TableCell>
                  <TableCell>
                    <span className="font-mono text-xs font-semibold text-primary">{s.courseCode}</span>
                  </TableCell>
                  <TableCell className="text-sm">{s.courseName}</TableCell>
                  <TableCell className="text-center">{s.theoryHours}+{s.labHours}</TableCell>
                  <TableCell className="text-center">{s.credits}</TableCell>
                  <TableCell className="text-center">{s.ects}</TableCell>
                  <TableCell className="text-center">{s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : '—'}</TableCell>
                  <TableCell className="text-center">
                    {editingId === s.sectionId ? (
                      <Input
                        className="h-7 w-20 text-xs"
                        value={editCapacity}
                        onChange={e => setEditCapacity(e.target.value)}
                      />
                    ) : (
                      <span>{s.capacity} / {s.enrolledCount} / {s.registeredCount}</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {editingId === s.sectionId ? (
                      <select
                        className="h-7 w-full rounded border border-input bg-background px-1 text-xs"
                        value={editInstructorId}
                        onChange={e => setEditInstructorId(e.target.value)}
                      >
                        <option value="">Seçiniz</option>
                        {(instructorsQuery.data ?? []).map(inst => (
                          <option key={inst.instructorId} value={inst.instructorId}>{inst.fullName}</option>
                        ))}
                      </select>
                    ) : (
                      <span className="text-sm">{s.instructorName ?? '—'}</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex gap-1">
                      {editingId === s.sectionId ? (
                        <>
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={handleSave} disabled={updateMutation.isPending}>
                            <CheckCircle2 className="h-4 w-4 text-green-600" />
                          </Button>
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setEditingId(null)}>
                            <X className="h-4 w-4" />
                          </Button>
                        </>
                      ) : (
                        <>
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => handleEdit(s)} disabled={!s.isActive}>
                            <Edit2 className="h-4 w-4" />
                          </Button>
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-7 w-7 text-destructive"
                            onClick={() => handleClose(s.sectionId)}
                            disabled={closeMutation.isPending || !s.isActive}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {closeMutation.isError && (
          <Alert variant="destructive" className="mt-3">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {(closeMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Şube kapatılamadı.'}
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}

// ── Tek Ders Ekleme Dialog ──

function AddCourseDialog({
  departmentId,
  termId,
  onClose,
}: {
  departmentId: number;
  termId: number;
  onClose: () => void;
}) {
  const curriculumQuery = useCurriculumCoursesForOpening(departmentId, termId);
  const instructorsQuery = useDepartmentInstructors(departmentId);
  const openMutation = useOpenCourse(departmentId, termId);

  const [selectedCourse, setSelectedCourse] = useState<CurriculumCourseForOpeningDto | null>(null);
  const [sectionCount, setSectionCount] = useState(1);
  const [capacity, setCapacity] = useState(40);
  const [instructorId, setInstructorId] = useState<string>('');
  const [search, setSearch] = useState('');

  const filteredCourses = (curriculumQuery.data ?? []).filter(
    c => c.courseCode.toLowerCase().includes(search.toLowerCase()) || c.courseName.toLowerCase().includes(search.toLowerCase()),
  );

  const handleSubmit = () => {
    if (!selectedCourse) return;
    openMutation.mutate(
      {
        courseId: selectedCourse.courseId,
        sectionCount,
        capacityPerSection: capacity,
        instructorId: instructorId ? Number(instructorId) : undefined,
      },
      {
        onSuccess: () => {
          setSelectedCourse(null);
          onClose();
        },
      },
    );
  };

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Ders Aç</DialogTitle>
          <DialogDescription>Müfredat derslerinden seçim yaparak yeni şube açın.</DialogDescription>
        </DialogHeader>

        {curriculumQuery.isLoading ? (
          <Skeleton className="h-48" />
        ) : (
          <>
            <Input
              placeholder="Ders kodu veya adı ile ara..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="mb-3"
            />

            <div className="max-h-60 overflow-y-auto border rounded-md">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10" />
                    <TableHead>Ders Kodu</TableHead>
                    <TableHead>Ders Adı</TableHead>
                    <TableHead>AKTS</TableHead>
                    <TableHead>Snf</TableHead>
                    <TableHead>Durum</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredCourses.map(c => (
                    <TableRow
                      key={c.courseId}
                      className={`cursor-pointer ${selectedCourse?.courseId === c.courseId ? 'bg-primary/10' : ''}`}
                      onClick={() => setSelectedCourse(c)}
                    >
                      <TableCell>
                        <input
                          type="radio"
                          checked={selectedCourse?.courseId === c.courseId}
                          onChange={() => setSelectedCourse(c)}
                        />
                      </TableCell>
                      <TableCell>
                        <span className="font-mono text-xs">{c.courseCode}</span>
                      </TableCell>
                      <TableCell className="text-sm">{c.courseName}</TableCell>
                      <TableCell>{c.ects}</TableCell>
                      <TableCell>{Math.ceil(c.suggestedSemester / 2)}</TableCell>
                      <TableCell>
                        {c.alreadyOpened ? (
                          <Badge variant="secondary">{c.existingSectionCount} şube açık</Badge>
                        ) : (
                          <Badge variant="outline">{c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}</Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {selectedCourse && (
              <div className="grid grid-cols-3 gap-3 mt-4">
                <div className="space-y-1">
                  <label className="text-xs font-medium">Şube Sayısı</label>
                  <Input type="number" min={1} max={10} value={sectionCount} onChange={e => setSectionCount(Number(e.target.value))} />
                </div>
                <div className="space-y-1">
                  <label className="text-xs font-medium">Kontenjan</label>
                  <Input type="number" min={1} max={300} value={capacity} onChange={e => setCapacity(Number(e.target.value))} />
                </div>
                <div className="space-y-1">
                  <label className="text-xs font-medium">Öğretim Elemanı (Opsiyonel)</label>
                  <select
                    className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={instructorId}
                    onChange={e => setInstructorId(e.target.value)}
                  >
                    <option value="">Seçiniz</option>
                    {(instructorsQuery.data ?? []).map(inst => (
                      <option key={inst.instructorId} value={inst.instructorId}>{inst.fullName}</option>
                    ))}
                  </select>
                </div>
              </div>
            )}
          </>
        )}

        {openMutation.isError && (
          <Alert variant="destructive" className="mt-2">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {(openMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Ders açılamadı.'}
            </AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>İptal</Button>
          <Button onClick={handleSubmit} disabled={!selectedCourse || openMutation.isPending}>
            {openMutation.isPending ? 'Açılıyor...' : 'Ders Aç'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Toplu Ders Açma Dialog ──

function BulkOpenDialog({
  departmentId,
  termId,
  onClose,
}: {
  departmentId: number;
  termId: number;
  onClose: () => void;
}) {
  const curriculumQuery = useCurriculumCoursesForOpening(departmentId, termId);
  const bulkMutation = useBulkOpenCourses(departmentId, termId);

  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [defaultCapacity, setDefaultCapacity] = useState(40);

  const courses = curriculumQuery.data ?? [];
  const unopenedCourses = courses.filter(c => !c.alreadyOpened);

  // Select all compulsory courses by default on first load
  const [initialized, setInitialized] = useState(false);
  if (!initialized && unopenedCourses.length > 0) {
    const compulsoryIds = new Set(unopenedCourses.filter(c => c.isCompulsory).map(c => c.courseId));
    setSelectedIds(compulsoryIds);
    setInitialized(true);
  }

  const toggleCourse = (courseId: number) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(courseId)) next.delete(courseId);
      else next.add(courseId);
      return next;
    });
  };

  const toggleAll = () => {
    if (selectedIds.size === unopenedCourses.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(unopenedCourses.map(c => c.courseId)));
    }
  };

  const handleSubmit = () => {
    if (selectedIds.size === 0) return;
    bulkMutation.mutate(
      {
        courses: Array.from(selectedIds).map(courseId => ({
          courseId,
          sectionCount: 1,
          capacityPerSection: defaultCapacity,
        })),
      },
      {
        onSuccess: () => {
          // Don't auto-close — show result
        },
      },
    );
  };

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-3xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Toplu Ders Aç</DialogTitle>
          <DialogDescription>Seçilen müfredat derslerini tek seferde açın.</DialogDescription>
        </DialogHeader>

        {curriculumQuery.isLoading ? (
          <Skeleton className="h-48" />
        ) : (
          <>
            <div className="flex items-center gap-3 mb-3">
              <div className="space-y-1">
                <label className="text-xs font-medium">Varsayılan Kontenjan</label>
                <Input type="number" min={1} max={300} value={defaultCapacity} onChange={e => setDefaultCapacity(Number(e.target.value))} className="w-24" />
              </div>
              <Button size="sm" variant="outline" onClick={toggleAll} className="mt-5">
                {selectedIds.size === unopenedCourses.length ? 'Hiçbirini Seçme' : 'Tümünü Seç'}
              </Button>
              <span className="text-sm text-muted-foreground mt-5">
                {selectedIds.size} ders seçili
              </span>
            </div>

            <div className="max-h-64 overflow-y-auto border rounded-md">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10" />
                    <TableHead>Ders Kodu</TableHead>
                    <TableHead>Ders Adı</TableHead>
                    <TableHead>AKTS</TableHead>
                    <TableHead>Snf</TableHead>
                    <TableHead>Tür</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {unopenedCourses.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center text-muted-foreground py-6">
                        Tüm müfredat dersleri zaten açılmış.
                      </TableCell>
                    </TableRow>
                  ) : (
                    unopenedCourses.map(c => (
                      <TableRow key={c.courseId} className="cursor-pointer" onClick={() => toggleCourse(c.courseId)}>
                        <TableCell>
                          <input type="checkbox" checked={selectedIds.has(c.courseId)} onChange={() => toggleCourse(c.courseId)} />
                        </TableCell>
                        <TableCell>
                          <span className="font-mono text-xs">{c.courseCode}</span>
                        </TableCell>
                        <TableCell className="text-sm">{c.courseName}</TableCell>
                        <TableCell>{c.ects}</TableCell>
                        <TableCell>{Math.ceil(c.suggestedSemester / 2)}</TableCell>
                        <TableCell>
                          <Badge variant={c.isCompulsory ? 'default' : 'outline'}>
                            {c.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </>
        )}

        {bulkMutation.data && (
          <Alert variant={bulkMutation.data.errors.length > 0 ? 'destructive' : 'default'} className="mt-3">
            <CheckCircle2 className="h-4 w-4" />
            <AlertTitle>Sonuç</AlertTitle>
            <AlertDescription>
              <p>{bulkMutation.data.totalCoursesOpened} ders açıldı, {bulkMutation.data.totalSectionsCreated} şube oluşturuldu.</p>
              {bulkMutation.data.errors.map((err, i) => (
                <p key={i} className="text-xs text-destructive">{err}</p>
              ))}
            </AlertDescription>
          </Alert>
        )}

        {bulkMutation.isError && (
          <Alert variant="destructive" className="mt-2">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Toplu ders açma işlemi başarısız oldu.</AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Kapat</Button>
          <Button onClick={handleSubmit} disabled={selectedIds.size === 0 || bulkMutation.isPending}>
            {bulkMutation.isPending ? 'Açılıyor...' : `Seçilen Dersleri Aç (${selectedIds.size})`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
