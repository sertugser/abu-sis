import { useState } from 'react';
import { Search, Plus, Edit2, Trash2, X, ChevronRight, BookOpen, BarChart3, GitBranch } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  useAdminCourses,
  useAdminCourseDetail,
  useCreateCourse,
  useUpdateCourse,
  useDeleteCourse,
  useAddPrerequisite,
  useRemovePrerequisite,
  useAddEquivalency,
  useRemoveEquivalency,
  type CourseListItemDto,
  type CreateCourseDto,
  type PrerequisiteChainNodeDto,
} from '../hooks/useAdminCourseCatalog';
import { useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';

// ─── Course Form ─────────────────────────────────────────────────

function emptyCourseForm(): CreateCourseDto {
  return { code: '', name: '', ects: 6, theoryHours: 3, labHours: 0, isNonGpa: false, departmentId: null };
}

interface CourseFormProps {
  form: CreateCourseDto;
  onChange: (f: CreateCourseDto) => void;
  departments: { id: number; name: string }[];
}

function CourseFormFields({ form, onChange, departments }: CourseFormProps) {
  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Ders Kodu</label>
          <Input value={form.code} onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })} placeholder="örn. BM101" maxLength={20} />
        </div>
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">AKTS</label>
          <Input type="number" min={1} max={30} value={form.ects} onChange={e => onChange({ ...form, ects: Number(e.target.value) })} />
        </div>
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Ders Adı</label>
        <Input value={form.name} onChange={e => onChange({ ...form, name: e.target.value })} placeholder="örn. Programlamaya Giriş" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Teorik Saat</label>
          <Input type="number" min={0} max={10} value={form.theoryHours} onChange={e => onChange({ ...form, theoryHours: Number(e.target.value) })} />
        </div>
        <div>
          <label className="text-sm font-medium text-foreground block mb-1">Lab Saati</label>
          <Input type="number" min={0} max={10} value={form.labHours} onChange={e => onChange({ ...form, labHours: Number(e.target.value) })} />
        </div>
      </div>
      <div>
        <label className="text-sm font-medium text-foreground block mb-1">Sahip Bölüm</label>
        <select
          className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          value={form.departmentId ?? ''}
          onChange={e => onChange({ ...form, departmentId: e.target.value ? Number(e.target.value) : null })}
        >
          <option value="">Bölüm yok (Ortak ders)</option>
          {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
      </div>
      <div className="flex items-center gap-2">
        <input
          type="checkbox"
          id="isNonGpa"
          checked={form.isNonGpa}
          onChange={e => onChange({ ...form, isNonGpa: e.target.checked })}
          className="h-4 w-4 rounded border-border"
        />
        <label htmlFor="isNonGpa" className="text-sm text-muted-foreground">Not ortalamasına dahil değil (Non-GPA)</label>
      </div>
    </div>
  );
}

// ─── Course Detail Panel ─────────────────────────────────────────

interface DetailPanelProps {
  courseId: number;
  allCourses: CourseListItemDto[];
  onClose: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

function CourseDetailPanel({ courseId, allCourses, onClose, onEdit, onDelete }: DetailPanelProps) {
  const { data: detail, isLoading } = useAdminCourseDetail(courseId);
  const addPrereq = useAddPrerequisite();
  const removePrereq = useRemovePrerequisite();
  const addEquiv = useAddEquivalency();
  const removeEquiv = useRemoveEquivalency();

  const [prereqSearch, setPrereqSearch] = useState('');
  const [equivSearch, setEquivSearch] = useState('');

  if (isLoading || !detail) {
    return <div className="flex items-center justify-center h-full text-muted-foreground">Yükleniyor...</div>;
  }

  const existingPrereqIds = new Set(detail.prerequisites.map(p => p.courseId));
  const existingEquivIds = new Set(detail.equivalencies.map(e => e.courseId));

  const prereqCandidates = allCourses.filter(c =>
    c.id !== courseId && !existingPrereqIds.has(c.id) &&
    (c.code.toLowerCase().includes(prereqSearch.toLowerCase()) || c.name.toLowerCase().includes(prereqSearch.toLowerCase()))
  ).slice(0, 8);

  const equivCandidates = allCourses.filter(c =>
    c.id !== courseId && !existingEquivIds.has(c.id) &&
    (c.code.toLowerCase().includes(equivSearch.toLowerCase()) || c.name.toLowerCase().includes(equivSearch.toLowerCase()))
  ).slice(0, 8);

  return (
    <div className="flex flex-col h-full">
      {/* Detail Header */}
      <div className="flex items-start justify-between p-5 border-b border-border">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Badge variant="outline" className="font-mono">{detail.code}</Badge>
            {detail.isNonGpa && <Badge variant="secondary">Non-GPA</Badge>}
          </div>
          <h2 className="text-base font-semibold text-foreground">{detail.name}</h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            {detail.ects} AKTS · {detail.theoryHours}T+{detail.labHours}L
            {detail.departmentName && ` · ${detail.departmentName}`}
          </p>
        </div>
        <div className="flex items-center gap-1">
          <Button size="sm" variant="ghost" onClick={onEdit}><Edit2 className="h-3.5 w-3.5" /></Button>
          <Button size="sm" variant="ghost" onClick={onDelete}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button>
          <Button size="sm" variant="ghost" onClick={onClose}><X className="h-4 w-4" /></Button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-5 space-y-6">
        {/* Prerequisites */}
        <div>
          <h3 className="text-sm font-semibold text-foreground mb-2">Önkoşullar ({detail.prerequisites.length})</h3>
          {detail.prerequisites.length > 0 && (
            <div className="flex flex-wrap gap-1.5 mb-3">
              {detail.prerequisites.map(p => (
                <div key={p.courseId} className="flex items-center gap-1 bg-muted rounded-full pl-2.5 pr-1 py-0.5 text-xs">
                  <span className="font-mono font-medium text-foreground">{p.code}</span>
                  <span className="text-muted-foreground">{p.name}</span>
                  <button className="ml-0.5 text-muted-foreground hover:text-destructive" onClick={() => removePrereq.mutate({ courseId, requiredCourseId: p.courseId })}>
                    <X className="h-3 w-3" />
                  </button>
                </div>
              ))}
            </div>
          )}
          <div className="relative">
            <Search className="absolute left-2.5 top-2 h-3.5 w-3.5 text-muted-foreground" />
            <Input className="pl-7 h-8 text-xs" placeholder="Önkoşul ekle..." value={prereqSearch} onChange={e => setPrereqSearch(e.target.value)} />
          </div>
          {prereqSearch && prereqCandidates.length > 0 && (
            <div className="border border-border rounded-md mt-1 divide-y divide-border max-h-40 overflow-y-auto bg-background">
              {prereqCandidates.map(c => (
                <button key={c.id} className="w-full text-left px-3 py-1.5 text-xs hover:bg-muted/50 flex items-center justify-between"
                  onClick={() => { addPrereq.mutate({ courseId, requiredCourseId: c.id }); setPrereqSearch(''); }}>
                  <span><span className="font-mono font-medium">{c.code}</span> — {c.name}</span>
                  <Plus className="h-3 w-3 text-muted-foreground" />
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Equivalencies */}
        <div>
          <h3 className="text-sm font-semibold text-foreground mb-2">Eşdeğer Dersler ({detail.equivalencies.length})</h3>
          {detail.equivalencies.length > 0 && (
            <div className="flex flex-wrap gap-1.5 mb-3">
              {detail.equivalencies.map(e => (
                <div key={e.courseId} className="flex items-center gap-1 bg-primary/8 rounded-full pl-2.5 pr-1 py-0.5 text-xs">
                  <span className="font-mono font-medium text-primary">{e.code}</span>
                  <span className="text-primary/70">{e.name}</span>
                  <button className="ml-0.5 text-primary/40 hover:text-destructive" onClick={() => removeEquiv.mutate({ courseId, equivalentCourseId: e.courseId })}>
                    <X className="h-3 w-3" />
                  </button>
                </div>
              ))}
            </div>
          )}
          <div className="relative">
            <Search className="absolute left-2.5 top-2 h-3.5 w-3.5 text-muted-foreground" />
            <Input className="pl-7 h-8 text-xs" placeholder="Eşdeğer ders ekle..." value={equivSearch} onChange={e => setEquivSearch(e.target.value)} />
          </div>
          {equivSearch && equivCandidates.length > 0 && (
            <div className="border border-border rounded-md mt-1 divide-y divide-border max-h-40 overflow-y-auto bg-background">
              {equivCandidates.map(c => (
                <button key={c.id} className="w-full text-left px-3 py-1.5 text-xs hover:bg-muted/50 flex items-center justify-between"
                  onClick={() => { addEquiv.mutate({ courseId, equivalentCourseId: c.id }); setEquivSearch(''); }}>
                  <span><span className="font-mono font-medium">{c.code}</span> — {c.name}</span>
                  <Plus className="h-3 w-3 text-muted-foreground" />
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Curriculum Assignments */}
        <div>
          <h3 className="text-sm font-semibold text-foreground mb-2">Müfredat Atamaları ({detail.curriculumAssignments.length})</h3>
          {detail.curriculumAssignments.length === 0 ? (
            <p className="text-xs text-muted-foreground">Herhangi bir müfredatta yer almıyor.</p>
          ) : (
            <div className="space-y-1.5">
              {detail.curriculumAssignments.map(a => (
                <div key={`${a.curriculumId}-${a.suggestedSemester}`} className="flex items-center justify-between bg-muted/40 rounded-md px-3 py-2 text-xs">
                  <div>
                    <span className="font-medium text-foreground">{a.programName}</span>
                    <span className="text-muted-foreground ml-1">v{a.curriculumVersion}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-muted-foreground">{a.suggestedSemester}. dönem</span>
                    {a.isCompulsory
                      ? <Badge variant="default" className="text-[10px] py-0">Zorunlu</Badge>
                      : <Badge variant="secondary" className="text-[10px] py-0">Seçmeli</Badge>}
                    {a.isActive && <Badge variant="success" className="text-[10px] py-0">Aktif</Badge>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Previous Term Stats */}
        {detail.stats && (
          <div>
            <h3 className="text-sm font-semibold text-foreground mb-2 flex items-center gap-1.5">
              <BarChart3 className="h-3.5 w-3.5" />
              Dönem İstatistikleri
            </h3>
            <div className="grid grid-cols-2 gap-2">
              <div className="bg-blue-50 rounded-lg p-3 text-center">
                <p className="text-lg font-bold text-blue-700">{detail.stats.totalTimesOpened}</p>
                <p className="text-[11px] text-blue-600/70">Açıldığı Dönem</p>
              </div>
              <div className="bg-emerald-50 rounded-lg p-3 text-center">
                <p className="text-lg font-bold text-emerald-700">{detail.stats.lastTermSectionCount}</p>
                <p className="text-[11px] text-emerald-600/70">Son Dönem Şube</p>
              </div>
              <div className="bg-amber-50 rounded-lg p-3 text-center">
                <p className="text-lg font-bold text-amber-700">{detail.stats.lastTermTotalEnrolled}</p>
                <p className="text-[11px] text-amber-600/70">Son Dönem Kayıtlı</p>
              </div>
              <div className="bg-violet-50 rounded-lg p-3 text-center">
                <p className="text-lg font-bold text-violet-700">{detail.stats.lastTermAvgCapacity}</p>
                <p className="text-[11px] text-violet-600/70">Ort. Kontenjan</p>
              </div>
            </div>
            {detail.stats.lastTermName && (
              <p className="text-[11px] text-muted-foreground mt-1.5 text-center">
                Son dönem: {detail.stats.lastTermName}
              </p>
            )}
          </div>
        )}

        {/* Prerequisite Chain */}
        {detail.prerequisiteChain && detail.prerequisiteChain.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold text-foreground mb-2 flex items-center gap-1.5">
              <GitBranch className="h-3.5 w-3.5" />
              Önkoşul Zinciri
            </h3>
            <div className="bg-muted/30 rounded-lg p-3">
              <PrerequisiteChainTree nodes={detail.prerequisiteChain} />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function PrerequisiteChainTree({ nodes, depth = 0 }: { nodes: PrerequisiteChainNodeDto[]; depth?: number }) {
  return (
    <div className={depth > 0 ? 'ml-4 border-l-2 border-gray-200 pl-3' : ''}>
      {nodes.map(node => (
        <div key={node.courseId} className="py-1">
          <div className="flex items-center gap-1.5">
            {depth > 0 && <span className="text-gray-300 text-xs">→</span>}
            <span className="font-mono text-xs font-semibold text-foreground">{node.code}</span>
            <span className="text-xs text-muted-foreground truncate">{node.name}</span>
          </div>
          {node.prerequisites.length > 0 && (
            <PrerequisiteChainTree nodes={node.prerequisites} depth={depth + 1} />
          )}
        </div>
      ))}
    </div>
  );
}

// ─── Main Page ───────────────────────────────────────────────────

type CourseTypeFilter = 'all' | 'theory' | 'lab' | 'mixed';

export default function CourseCatalogAdminPage() {
  const [search, setSearch] = useState('');
  const [deptFilter, setDeptFilter] = useState<number | undefined>(undefined);
  const [typeFilter, setTypeFilter] = useState<CourseTypeFilter>('all');
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [editCourse, setEditCourse] = useState<CourseListItemDto | null>(null);
  const [deleteCourse, setDeleteCourse] = useState<CourseListItemDto | null>(null);
  const [form, setForm] = useState<CreateCourseDto>(emptyCourseForm());
  const [formError, setFormError] = useState('');

  const { data: allCourses = [], isLoading } = useAdminCourses({ search: search || undefined, departmentId: deptFilter });
  const { data: departments = [] } = useAdminDepartmentsByStructure(undefined);

  // Frontend-side filters
  const courses = allCourses.filter(c => {
    if (typeFilter === 'theory' && c.labHours > 0) return false;
    if (typeFilter === 'lab' && (c.labHours === 0 || c.theoryHours > 0)) return false;
    if (typeFilter === 'mixed' && (c.labHours === 0 || c.theoryHours === 0)) return false;
    return true;
  });
  const createCourse = useCreateCourse();
  const updateCourse = useUpdateCourse();
  const deleteCourseM = useDeleteCourse();

  function openCreate() { setForm(emptyCourseForm()); setFormError(''); setCreateOpen(true); }
  function openEdit(c: CourseListItemDto) {
    setForm({ code: c.code, name: c.name, ects: c.ects, theoryHours: c.theoryHours, labHours: c.labHours, isNonGpa: c.isNonGpa, departmentId: c.departmentId });
    setFormError(''); setEditCourse(c);
  }

  async function handleCreate() {
    if (!form.code.trim() || !form.name.trim()) { setFormError('Kod ve ad zorunludur.'); return; }
    try {
      const result = await createCourse.mutateAsync(form);
      setCreateOpen(false);
      setSelectedCourseId(result.id);
    } catch (e: unknown) {
      setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.');
    }
  }

  async function handleUpdate() {
    if (!editCourse) return;
    if (!form.code.trim() || !form.name.trim()) { setFormError('Kod ve ad zorunludur.'); return; }
    try { await updateCourse.mutateAsync({ id: editCourse.id, ...form }); setEditCourse(null); }
    catch (e: unknown) { setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.'); }
  }

  async function handleDelete() {
    if (!deleteCourse) return;
    try {
      await deleteCourseM.mutateAsync(deleteCourse.id);
      setDeleteCourse(null);
      if (selectedCourseId === deleteCourse.id) setSelectedCourseId(null);
    } catch (e: unknown) {
      alert((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.');
    }
  }

  const selectedCourse = courses.find(c => c.id === selectedCourseId) ?? null;

  return (
    <div className="flex h-[calc(100vh-4rem)] overflow-hidden -mx-6 -my-5 lg:-mx-8">
      {/* Left: Course List */}
      <div className={`flex flex-col border-r border-border transition-all bg-background ${selectedCourseId ? 'w-1/2' : 'w-full'}`}>
        {/* Filters + Action */}
        <div className="flex flex-col border-b border-border px-5 py-4 gap-3">
          <div className="flex flex-wrap gap-3 items-center">
            <div className="relative flex-1 min-w-[180px]">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input className="pl-9" placeholder="Kod veya isimle ara..." value={search} onChange={e => setSearch(e.target.value)} />
            </div>
            <select
              className="rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
              value={deptFilter ?? ''}
              onChange={e => setDeptFilter(e.target.value ? Number(e.target.value) : undefined)}
            >
              <option value="">Tüm Bölümler</option>
              {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
            <div className="flex items-center gap-2 ml-auto">
              <span className="text-sm text-muted-foreground">{isLoading ? '' : `${courses.length} ders`}</span>
              <Button size="sm" onClick={openCreate}><Plus className="h-4 w-4 mr-1" /> Ders Ekle</Button>
            </div>
          </div>
          <div className="flex items-center gap-1.5">
            {([
              ['all', 'Tümü'],
              ['theory', 'Sadece Teori'],
              ['lab', 'Sadece Lab'],
              ['mixed', 'Teori + Lab'],
            ] as [CourseTypeFilter, string][]).map(([key, label]) => (
              <button
                key={key}
                onClick={() => setTypeFilter(key)}
                className={`px-3 py-1 text-xs rounded-full border transition ${
                  typeFilter === key
                    ? 'bg-[#003366] text-white border-[#003366]'
                    : 'bg-white text-gray-600 hover:bg-gray-50 border-gray-200'
                }`}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* Course List */}
        <div className="flex-1 overflow-y-auto">
          {isLoading ? (
            <div className="py-16 text-center text-muted-foreground">Yükleniyor...</div>
          ) : courses.length === 0 ? (
            <div className="py-16 text-center">
              <BookOpen className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground">Ders bulunamadı.</p>
            </div>
          ) : (
            <div className="divide-y divide-border">
              {courses.map(c => (
                <button
                  key={c.id}
                  className={`w-full text-left px-5 py-3.5 hover:bg-muted/30 flex items-center justify-between transition-colors ${
                    selectedCourseId === c.id ? 'bg-primary/5 border-r-2 border-primary' : ''
                  }`}
                  onClick={() => setSelectedCourseId(prev => prev === c.id ? null : c.id)}
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-0.5">
                      <span className="font-mono text-sm font-semibold text-foreground">{c.code}</span>
                      {c.isNonGpa && <Badge variant="secondary" className="text-[10px] py-0">Non-GPA</Badge>}
                    </div>
                    <p className="text-sm text-muted-foreground truncate">{c.name}</p>
                    <div className="flex items-center gap-3 mt-0.5 text-xs text-muted-foreground/70">
                      <span>{c.ects} AKTS</span>
                      <span>{c.theoryHours}T+{c.labHours}L</span>
                      {c.departmentName && <span>{c.departmentName}</span>}
                      {c.prerequisiteCount > 0 && <span>{c.prerequisiteCount} önkoşul</span>}
                    </div>
                  </div>
                  <ChevronRight className={`h-4 w-4 text-muted-foreground flex-shrink-0 transition-transform ${selectedCourseId === c.id ? 'rotate-90' : ''}`} />
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Right: Detail Panel */}
      {selectedCourseId && (
        <div className="w-1/2 flex flex-col bg-background border-l border-border">
          <CourseDetailPanel
            courseId={selectedCourseId}
            allCourses={courses}
            onClose={() => setSelectedCourseId(null)}
            onEdit={() => selectedCourse && openEdit(selectedCourse)}
            onDelete={() => selectedCourse && setDeleteCourse(selectedCourse)}
          />
        </div>
      )}

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={o => !o && setCreateOpen(false)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Ders Ekle</DialogTitle></DialogHeader>
          <CourseFormFields form={form} onChange={f => { setForm(f); setFormError(''); }} departments={departments} />
          {formError && <p className="text-sm text-destructive">{formError}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={handleCreate} disabled={createCourse.isPending}>{createCourse.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Dialog */}
      <Dialog open={!!editCourse} onOpenChange={o => !o && setEditCourse(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Ders Düzenle</DialogTitle></DialogHeader>
          <CourseFormFields form={form} onChange={f => { setForm(f); setFormError(''); }} departments={departments} />
          {formError && <p className="text-sm text-destructive">{formError}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditCourse(null)}>İptal</Button>
            <Button onClick={handleUpdate} disabled={updateCourse.isPending}>{updateCourse.isPending ? 'Kaydediliyor...' : 'Kaydet'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Dialog */}
      <Dialog open={!!deleteCourse} onOpenChange={o => !o && setDeleteCourse(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>Ders Sil</DialogTitle></DialogHeader>
          <p className="text-sm text-muted-foreground">
            <strong className="text-foreground">{deleteCourse?.code} — {deleteCourse?.name}</strong> silinecek. Bu işlem geri alınamaz.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteCourse(null)}>İptal</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteCourseM.isPending}>{deleteCourseM.isPending ? 'Siliniyor...' : 'Sil'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
