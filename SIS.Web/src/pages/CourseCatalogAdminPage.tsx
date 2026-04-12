import { useState, useMemo } from 'react';
import {
  Search, Plus, Edit2, Trash2, X, BookOpen, BarChart3, GitBranch,
  AlertTriangle,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
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
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
} from '../hooks/useAdminAcademicStructure';
import {
  COURSE_LANGUAGE_LABELS,
  TEACHING_METHOD_LABELS,
  COURSE_STATUS_LABELS,
  COURSE_LEVEL_LABELS,
} from '@/lib/constants';

// ─── Helpers ────────────────────────────────────────────────────────────────

type CourseTypeFilter = 'all' | 'theory' | 'lab' | 'mixed';

function emptyCourseForm(): CreateCourseDto {
  return {
    code: '',
    name: '',
    englishName: null,
    ects: 6,
    theoryHours: 3,
    labHours: 0,
    isNonGpa: false,
    examType: null,
    departmentId: null,
    language: 'Turkish',
    teachingMethod: 'FaceToFace',
    status: 'Active',
    level: 'Undergraduate',
    description: null,
    nationalCredit: null,
  };
}

function applyTypeFilter(courses: CourseListItemDto[], typeFilter: CourseTypeFilter) {
  return courses.filter(c => {
    if (typeFilter === 'theory') return c.labHours === 0;
    if (typeFilter === 'lab') return c.theoryHours === 0 && c.labHours > 0;
    if (typeFilter === 'mixed') return c.theoryHours > 0 && c.labHours > 0;
    return true;
  });
}

// ─── Prerequisite Chain Tree ────────────────────────────────────────────────

function PrerequisiteChainTree({ nodes, depth = 0 }: { nodes: PrerequisiteChainNodeDto[]; depth?: number }) {
  return (
    <div className={depth > 0 ? 'ml-4 border-l-2 border-border pl-3' : ''}>
      {nodes.map(node => (
        <div key={node.courseId} className="py-1">
          <div className="flex items-center gap-1.5">
            {depth > 0 && <span className="text-muted-foreground/50 text-xs">→</span>}
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

// ─── Field Section Wrapper ───────────────────────────────────────────────────

function FieldSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <fieldset className="border border-border rounded-lg px-4 pt-2 pb-4">
      <legend className="text-xs font-semibold text-muted-foreground px-1 uppercase tracking-wide">{title}</legend>
      {children}
    </fieldset>
  );
}

// ─── Styled native select (shared pattern) ──────────────────────────────────

function NativeSelect({
  value,
  onChange,
  disabled,
  children,
  className = '',
}: {
  value: string | number;
  onChange: (v: string) => void;
  disabled?: boolean;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <select
      className={`w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50 disabled:cursor-not-allowed ${className}`}
      value={value}
      onChange={e => onChange(e.target.value)}
      disabled={disabled}
    >
      {children}
    </select>
  );
}

// ─── Course Form Fields (shared between Create & Edit) ──────────────────────

interface CourseFormFieldsProps {
  form: CreateCourseDto;
  onChange: (f: CreateCourseDto) => void;
  departments: { id: number; name: string }[];
  disabled?: boolean;
}

function CourseFormFields({ form, onChange, departments, disabled = false }: CourseFormFieldsProps) {
  const descLen = form.description?.length ?? 0;

  return (
    <div className="space-y-4 overflow-y-auto">
      {/* Kimlik */}
      <FieldSection title="Kimlik">
        <div className="grid grid-cols-2 gap-3 mt-2">
          <div className="space-y-1">
            <Label className="text-xs">Ders Kodu</Label>
            <Input
              value={form.code}
              onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })}
              placeholder="örn. BM101"
              maxLength={20}
              disabled={disabled}
              className="font-mono"
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Bölüm</Label>
            <NativeSelect
              value={form.departmentId ?? 'none'}
              onChange={v => onChange({ ...form, departmentId: v === 'none' ? null : Number(v) })}
              disabled={disabled}
            >
              <option value="none">Bölüm yok (Ortak ders)</option>
              {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </NativeSelect>
          </div>
        </div>
        <div className="space-y-1 mt-3">
          <Label className="text-xs">Ders Adı</Label>
          <Input
            value={form.name}
            onChange={e => onChange({ ...form, name: e.target.value })}
            placeholder="örn. Programlamaya Giriş"
            disabled={disabled}
          />
        </div>
        <div className="space-y-1 mt-3">
          <Label className="text-xs">İngilizce Adı</Label>
          <Input
            value={form.englishName ?? ''}
            onChange={e => onChange({ ...form, englishName: e.target.value || null })}
            placeholder="örn. Introduction to Programming"
            disabled={disabled}
          />
        </div>
      </FieldSection>

      {/* Kredi & Saat */}
      <FieldSection title="Kredi &amp; Saat">
        <div className="grid grid-cols-4 gap-3 mt-2">
          <div className="space-y-1">
            <Label className="text-xs">AKTS</Label>
            <Input
              type="number" min={1} max={30}
              value={form.ects}
              onChange={e => onChange({ ...form, ects: Number(e.target.value) })}
              disabled={disabled}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Ulusal Kredi</Label>
            <Input
              type="number" min={0} max={20}
              value={form.nationalCredit ?? ''}
              onChange={e => onChange({ ...form, nationalCredit: e.target.value ? Number(e.target.value) : null })}
              disabled={disabled}
              placeholder="—"
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Teori Saati</Label>
            <Input
              type="number" min={0} max={10}
              value={form.theoryHours}
              onChange={e => onChange({ ...form, theoryHours: Number(e.target.value) })}
              disabled={disabled}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Lab Saati</Label>
            <Input
              type="number" min={0} max={10}
              value={form.labHours}
              onChange={e => onChange({ ...form, labHours: Number(e.target.value) })}
              disabled={disabled}
            />
          </div>
        </div>
      </FieldSection>

      {/* Akademik Bilgiler */}
      <FieldSection title="Akademik Bilgiler">
        <div className="grid grid-cols-2 gap-3 mt-2">
          <div className="space-y-1">
            <Label className="text-xs">Dil</Label>
            <NativeSelect
              value={form.language}
              onChange={v => onChange({ ...form, language: v })}
              disabled={disabled}
            >
              {Object.entries(COURSE_LANGUAGE_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </NativeSelect>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Öğretim Şekli</Label>
            <NativeSelect
              value={form.teachingMethod}
              onChange={v => onChange({ ...form, teachingMethod: v })}
              disabled={disabled}
            >
              {Object.entries(TEACHING_METHOD_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </NativeSelect>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Seviye</Label>
            <NativeSelect
              value={form.level}
              onChange={v => onChange({ ...form, level: v })}
              disabled={disabled}
            >
              {Object.entries(COURSE_LEVEL_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </NativeSelect>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Durum</Label>
            <NativeSelect
              value={form.status}
              onChange={v => onChange({ ...form, status: v })}
              disabled={disabled}
            >
              {Object.entries(COURSE_STATUS_LABELS).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </NativeSelect>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Sınav Değer Tipi</Label>
            <Input
              value={form.examType ?? ''}
              onChange={e => onChange({ ...form, examType: e.target.value || null })}
              placeholder="örn. Yazılı"
              disabled={disabled}
            />
          </div>
          <div className="flex items-center gap-2 pt-5">
            <input
              type="checkbox"
              id="isNonGpa"
              checked={form.isNonGpa}
              onChange={e => onChange({ ...form, isNonGpa: e.target.checked })}
              disabled={disabled}
              className="h-4 w-4 rounded border-border accent-primary"
            />
            <label htmlFor="isNonGpa" className="text-sm text-muted-foreground cursor-pointer select-none">
              Non-GPA (Ortalamasız)
            </label>
          </div>
        </div>
      </FieldSection>

      {/* Açıklama */}
      <FieldSection title="Açıklama">
        <div className="mt-2 space-y-1">
          <textarea
            className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50 disabled:cursor-not-allowed resize-none"
            rows={4}
            maxLength={1000}
            value={form.description ?? ''}
            onChange={e => onChange({ ...form, description: e.target.value || null })}
            disabled={disabled}
            placeholder="Ders açıklaması..."
          />
          <p className="text-[11px] text-muted-foreground text-right">{descLen} / 1000</p>
        </div>
      </FieldSection>
    </div>
  );
}

// ─── Course Detail Dialog ────────────────────────────────────────────────────

interface DetailDialogProps {
  courseId: number;
  allCourses: CourseListItemDto[];
  onClose: () => void;
  onDeleted: () => void;
}

function CourseDetailDialog({ courseId, allCourses, onClose, onDeleted }: DetailDialogProps) {
  const { data: detail, isLoading } = useAdminCourseDetail(courseId);
  const updateCourse = useUpdateCourse();
  const deleteCourse = useDeleteCourse();
  const addPrereq = useAddPrerequisite();
  const removePrereq = useRemovePrerequisite();
  const addEquiv = useAddEquivalency();
  const removeEquiv = useRemoveEquivalency();

  const [editMode, setEditMode] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [formError, setFormError] = useState('');
  const [prereqSearch, setPrereqSearch] = useState('');
  const [equivSearch, setEquivSearch] = useState('');

  // Get all departments (unfiltered) for the edit form
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);

  // Sync form from detail
  const initialForm = useMemo<CreateCourseDto>(() => {
    if (!detail) return emptyCourseForm();
    return {
      code: detail.code,
      name: detail.name,
      englishName: detail.englishName,
      ects: detail.ects,
      theoryHours: detail.theoryHours,
      labHours: detail.labHours,
      isNonGpa: detail.isNonGpa,
      examType: detail.examType,
      departmentId: detail.departmentId,
      language: detail.language,
      teachingMethod: detail.teachingMethod,
      status: detail.status,
      level: detail.level,
      description: detail.description,
      nationalCredit: detail.nationalCredit,
    };
  }, [detail]);

  const [form, setForm] = useState<CreateCourseDto>(emptyCourseForm());
  // Sync when detail loads or editMode toggled on
  useMemo(() => {
    if (detail) setForm(initialForm);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail?.id]);

  function handleCancelEdit() {
    setEditMode(false);
    setForm(initialForm);
    setFormError('');
  }

  async function handleSave() {
    if (!form.code.trim() || !form.name.trim()) {
      setFormError('Kod ve ad zorunludur.');
      return;
    }
    try {
      await updateCourse.mutateAsync({ id: courseId, ...form });
      setEditMode(false);
      setFormError('');
    } catch (e: unknown) {
      setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Güncelleme başarısız.');
    }
  }

  const [deleteError, setDeleteError] = useState<string | null>(null);

  async function handleDelete() {
    setDeleteError(null);
    try {
      await deleteCourse.mutateAsync(courseId);
      onDeleted();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Silinemedi.';
      setDeleteError(msg);
    }
  }

  const existingPrereqIds = useMemo(() => new Set((detail?.prerequisites ?? []).map(p => p.courseId)), [detail]);
  const existingEquivIds = useMemo(() => new Set((detail?.equivalencies ?? []).map(e => e.courseId)), [detail]);

  const prereqCandidates = useMemo(() =>
    allCourses.filter(c =>
      c.id !== courseId && !existingPrereqIds.has(c.id) &&
      (prereqSearch
        ? c.code.toLowerCase().includes(prereqSearch.toLowerCase()) ||
          c.name.toLowerCase().includes(prereqSearch.toLowerCase())
        : false)
    ).slice(0, 8),
    [allCourses, courseId, existingPrereqIds, prereqSearch]
  );

  const equivCandidates = useMemo(() =>
    allCourses.filter(c =>
      c.id !== courseId && !existingEquivIds.has(c.id) &&
      (equivSearch
        ? c.code.toLowerCase().includes(equivSearch.toLowerCase()) ||
          c.name.toLowerCase().includes(equivSearch.toLowerCase())
        : false)
    ).slice(0, 8),
    [allCourses, courseId, existingEquivIds, equivSearch]
  );

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <ResizableDialogContent defaultWidth={720} minWidth={500} minHeight={400}>
        {isLoading || !detail ? (
          <div className="flex items-center justify-center h-48 text-muted-foreground text-sm">Yükleniyor...</div>
        ) : (
          <div className="flex flex-col h-full min-h-0">
            {/* Header */}
            <div className="pb-3 flex-shrink-0">
              <div className="flex items-center gap-3 mb-1 pr-8">
                <span className="font-mono font-bold text-lg text-foreground">{detail.code}</span>
                <span className="text-sm text-foreground font-medium">{detail.name}</span>
                {detail.isNonGpa && <Badge variant="secondary" className="text-[10px]">Non-GPA</Badge>}
              </div>
              <p className="text-xs text-muted-foreground mb-2">
                {detail.ects} AKTS · {detail.theoryHours}T+{detail.labHours}L
                {detail.departmentName && ` · ${detail.departmentName}`}
              </p>
              <div className="flex items-center gap-2 relative z-10">
                {!editMode && (
                  <Button size="sm" variant="outline" className="h-7 text-xs" onClick={() => setEditMode(true)}>
                    <Edit2 className="h-3 w-3 mr-1" /> Düzenle
                  </Button>
                )}
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7 text-xs text-destructive hover:text-destructive hover:bg-destructive/5"
                  onClick={() => setShowDeleteConfirm(true)}
                >
                  <Trash2 className="h-3 w-3 mr-1" /> Sil
                </Button>
              </div>
            </div>

            <Separator className="flex-shrink-0" />

            {/* Tabs */}
            <Tabs defaultValue="temel" className="flex-1 flex flex-col min-h-0">
              <TabsList className="w-full grid grid-cols-4 mb-3 flex-shrink-0">
                <TabsTrigger value="temel">Temel Bilgiler</TabsTrigger>
                <TabsTrigger value="onkosullar">İlişkiler</TabsTrigger>
                <TabsTrigger value="mufredat">Müfredat</TabsTrigger>
                <TabsTrigger value="istatistik">İstatistikler</TabsTrigger>
              </TabsList>

              {/* Tab 1: Temel Bilgiler */}
              <TabsContent value="temel" className="flex-1 overflow-y-auto">
                <CourseFormFields
                  form={form}
                  onChange={f => { setForm(f); setFormError(''); }}
                  departments={allDepartments}
                  disabled={!editMode}
                />
                {formError && <p className="text-sm text-destructive mt-2">{formError}</p>}
              </TabsContent>

              {/* Tab 2: Önkoşullar & Eşdeğerlikler */}
              <TabsContent value="onkosullar" className="flex-1 overflow-y-auto space-y-6">
                {/* Prerequisites */}
                <div>
                  <h3 className="text-sm font-semibold text-foreground mb-2">
                    Önkoşullar ({detail.prerequisites.length})
                  </h3>
                  {detail.prerequisites.length > 0 && (
                    <div className="flex flex-wrap gap-1.5 mb-3">
                      {detail.prerequisites.map(p => (
                        <div key={p.courseId} className="flex items-center gap-1 bg-muted rounded-full pl-2.5 pr-1 py-0.5 text-xs">
                          <span className="font-mono font-medium text-foreground">{p.code}</span>
                          <span className="text-muted-foreground">{p.name}</span>
                          <button
                            className="ml-0.5 text-muted-foreground hover:text-destructive transition-colors"
                            onClick={() => removePrereq.mutate({ courseId, requiredCourseId: p.courseId })}
                          >
                            <X className="h-3 w-3" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                    <Input
                      className="pl-8 h-8 text-xs"
                      placeholder="Önkoşul ekle..."
                      value={prereqSearch}
                      onChange={e => setPrereqSearch(e.target.value)}
                    />
                  </div>
                  {prereqSearch && prereqCandidates.length > 0 && (
                    <div className="border border-border rounded-md mt-1 divide-y divide-border max-h-40 overflow-y-auto bg-background">
                      {prereqCandidates.map(c => (
                        <button
                          key={c.id}
                          className="w-full text-left px-3 py-1.5 text-xs hover:bg-muted/50 flex items-center justify-between"
                          onClick={() => { addPrereq.mutate({ courseId, requiredCourseId: c.id }); setPrereqSearch(''); }}
                        >
                          <span><span className="font-mono font-medium">{c.code}</span> — {c.name}</span>
                          <Plus className="h-3 w-3 text-muted-foreground" />
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {/* Equivalencies */}
                <div>
                  <h3 className="text-sm font-semibold text-foreground mb-2">
                    Eşdeğer Dersler ({detail.equivalencies.length})
                  </h3>
                  {detail.equivalencies.length > 0 && (
                    <div className="flex flex-wrap gap-1.5 mb-3">
                      {detail.equivalencies.map(e => (
                        <div key={e.courseId} className="flex items-center gap-1 bg-primary/10 rounded-full pl-2.5 pr-1 py-0.5 text-xs">
                          <span className="font-mono font-medium text-primary">{e.code}</span>
                          <span className="text-primary/70">{e.name}</span>
                          <button
                            className="ml-0.5 text-primary/40 hover:text-destructive transition-colors"
                            onClick={() => removeEquiv.mutate({ courseId, equivalentCourseId: e.courseId })}
                          >
                            <X className="h-3 w-3" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                    <Input
                      className="pl-8 h-8 text-xs"
                      placeholder="Eşdeğer ders ekle..."
                      value={equivSearch}
                      onChange={e => setEquivSearch(e.target.value)}
                    />
                  </div>
                  {equivSearch && equivCandidates.length > 0 && (
                    <div className="border border-border rounded-md mt-1 divide-y divide-border max-h-40 overflow-y-auto bg-background">
                      {equivCandidates.map(c => (
                        <button
                          key={c.id}
                          className="w-full text-left px-3 py-1.5 text-xs hover:bg-muted/50 flex items-center justify-between"
                          onClick={() => { addEquiv.mutate({ courseId, equivalentCourseId: c.id }); setEquivSearch(''); }}
                        >
                          <span><span className="font-mono font-medium">{c.code}</span> — {c.name}</span>
                          <Plus className="h-3 w-3 text-muted-foreground" />
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {/* Prerequisite chain */}
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
              </TabsContent>

              {/* Tab 3: Müfredat Atamaları */}
              <TabsContent value="mufredat" className="flex-1 overflow-y-auto">
                {detail.curriculumAssignments.length === 0 ? (
                  <div className="py-12 text-center">
                    <BookOpen className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                    <p className="text-sm text-muted-foreground">Bu ders henüz bir müfredata atanmamış.</p>
                  </div>
                ) : (
                  <div className="space-y-2">
                    {detail.curriculumAssignments.map(a => (
                      <div
                        key={`${a.curriculumId}-${a.suggestedSemester}`}
                        className="flex items-center justify-between bg-muted/40 rounded-md px-3 py-2.5 text-xs"
                      >
                        <div>
                          <span className="font-medium text-foreground">{a.programName}</span>
                          <span className="text-muted-foreground ml-1.5">v{a.curriculumVersion}</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="text-muted-foreground">{a.suggestedSemester}. dönem</span>
                          <Badge variant={a.isCompulsory ? 'default' : 'secondary'} className="text-[10px] py-0">
                            {a.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                          </Badge>
                          {a.isActive && (
                            <Badge variant="success" className="text-[10px] py-0">Aktif</Badge>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </TabsContent>

              {/* Tab 4: İstatistikler */}
              <TabsContent value="istatistik" className="flex-1 overflow-y-auto">
                {!detail.stats ? (
                  <div className="py-12 text-center">
                    <BarChart3 className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                    <p className="text-sm text-muted-foreground">Bu ders henüz hiç açılmamış.</p>
                  </div>
                ) : (
                  <div className="space-y-5">
                    {/* Özet Bilgiler */}
                    <div>
                      <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">Genel Bakış</h4>
                      <div className="grid grid-cols-4 gap-2">
                        <div className="border rounded-lg p-3 text-center">
                          <p className="text-lg font-bold text-foreground">{detail.stats.totalTimesOpened}</p>
                          <p className="text-[11px] text-muted-foreground">Açıldığı Dönem</p>
                        </div>
                        <div className="border rounded-lg p-3 text-center">
                          <p className="text-lg font-bold text-foreground">{detail.stats.totalStudentsAllTime}</p>
                          <p className="text-[11px] text-muted-foreground">Toplam Kayıt</p>
                        </div>
                        <div className="border rounded-lg p-3 text-center">
                          <p className="text-lg font-bold text-foreground">{detail.stats.prerequisiteForCount}</p>
                          <p className="text-[11px] text-muted-foreground">Bağımlı Ders</p>
                        </div>
                        <div className="border rounded-lg p-3 text-center">
                          <p className="text-lg font-bold text-foreground">%{detail.stats.passRate}</p>
                          <p className="text-[11px] text-muted-foreground">Başarı Oranı</p>
                        </div>
                      </div>
                    </div>

                    {/* Son Dönem */}
                    {detail.stats.lastTermName && (
                      <div>
                        <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">
                          Son Dönem — {detail.stats.lastTermName}
                        </h4>
                        <div className="grid grid-cols-3 gap-2">
                          <div className="border rounded-lg p-3">
                            <p className="text-xs text-muted-foreground mb-1">Şube / Kayıtlı / Kontenjan</p>
                            <p className="text-sm font-semibold">
                              {detail.stats.lastTermSectionCount} şube · {detail.stats.lastTermTotalEnrolled} / {detail.stats.lastTermTotalCapacity} öğrenci
                            </p>
                          </div>
                          <div className="border rounded-lg p-3">
                            <p className="text-xs text-muted-foreground mb-1">Doluluk Oranı</p>
                            <div className="flex items-center gap-2">
                              <div className="flex-1 h-2 bg-muted rounded-full overflow-hidden">
                                <div
                                  className={`h-full rounded-full ${detail.stats.lastTermFillRate >= 90 ? 'bg-red-500' : detail.stats.lastTermFillRate >= 70 ? 'bg-amber-500' : 'bg-emerald-500'}`}
                                  style={{ width: `${Math.min(detail.stats.lastTermFillRate, 100)}%` }}
                                />
                              </div>
                              <span className="text-sm font-semibold">%{detail.stats.lastTermFillRate}</span>
                            </div>
                          </div>
                          <div className="border rounded-lg p-3">
                            <p className="text-xs text-muted-foreground mb-1">Bırakma / Çekilme</p>
                            <p className="text-sm font-semibold">
                              {detail.stats.totalDropped} bırakma · {detail.stats.totalWithdrawn} çekilme
                            </p>
                          </div>
                        </div>
                      </div>
                    )}

                    {/* Not Dağılımı */}
                    {Object.keys(detail.stats.gradeDistribution).length > 0 && (
                      <div>
                        <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">Not Dağılımı</h4>
                        <div className="space-y-1.5">
                          {['AA', 'BA', 'BB', 'CB', 'CC', 'DC', 'DD', 'FF', 'NA', 'W', 'S', 'U'].map(grade => {
                            const count = detail.stats!.gradeDistribution[grade] ?? 0;
                            if (count === 0) return null;
                            const total = Object.values(detail.stats!.gradeDistribution).reduce((a, b) => a + b, 0);
                            const pct = total > 0 ? (count / total) * 100 : 0;
                            const isPass = ['AA', 'BA', 'BB', 'CB', 'CC', 'DC', 'DD', 'S'].includes(grade);
                            return (
                              <div key={grade} className="flex items-center gap-2">
                                <span className={`text-xs font-mono w-6 text-right ${isPass ? 'text-emerald-600' : 'text-red-500'}`}>{grade}</span>
                                <div className="flex-1 h-4 bg-muted rounded overflow-hidden">
                                  <div
                                    className={`h-full rounded ${isPass ? 'bg-emerald-500/70' : 'bg-red-400/70'}`}
                                    style={{ width: `${pct}%` }}
                                  />
                                </div>
                                <span className="text-xs text-muted-foreground w-16 text-right">{count} (%{pct.toFixed(0)})</span>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    )}

                    {/* Dönem Trendi */}
                    {detail.stats.termTrends.length > 1 && (
                      <div>
                        <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">Dönem Trendi</h4>
                        <div className="border rounded-lg overflow-hidden">
                          <table className="w-full text-xs">
                            <thead>
                              <tr className="bg-muted/50">
                                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Dönem</th>
                                <th className="text-center px-3 py-2 font-medium text-muted-foreground">Şube</th>
                                <th className="text-center px-3 py-2 font-medium text-muted-foreground">Kayıtlı</th>
                                <th className="text-center px-3 py-2 font-medium text-muted-foreground">Kontenjan</th>
                                <th className="text-center px-3 py-2 font-medium text-muted-foreground">Doluluk</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y">
                              {detail.stats.termTrends.map(t => {
                                const fill = t.capacity > 0 ? Math.round((t.enrolled / t.capacity) * 100) : 0;
                                return (
                                  <tr key={t.termName}>
                                    <td className="px-3 py-2 font-medium">{t.termName}</td>
                                    <td className="px-3 py-2 text-center">{t.sectionCount}</td>
                                    <td className="px-3 py-2 text-center">{t.enrolled}</td>
                                    <td className="px-3 py-2 text-center">{t.capacity}</td>
                                    <td className="px-3 py-2 text-center">
                                      <span className={`${fill >= 90 ? 'text-red-500' : fill >= 70 ? 'text-amber-500' : 'text-emerald-500'} font-medium`}>
                                        %{fill}
                                      </span>
                                    </td>
                                  </tr>
                                );
                              })}
                            </tbody>
                          </table>
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </TabsContent>
            </Tabs>

            {/* Footer */}
            <Separator className="mt-3 mb-3" />
            <DialogFooter>
              <div className="flex w-full items-center justify-between">
                <Button variant="outline" size="sm" onClick={onClose}>Kapat</Button>
                {editMode && (
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={handleCancelEdit}>İptal</Button>
                    <Button size="sm" onClick={handleSave} disabled={updateCourse.isPending}>
                      {updateCourse.isPending ? 'Kaydediliyor...' : 'Kaydet'}
                    </Button>
                  </div>
                )}
              </div>
            </DialogFooter>
          </div>
        )}

        {/* Delete confirmation overlay */}
        {showDeleteConfirm && (
          <div className="absolute inset-0 z-50 flex items-center justify-center bg-background/90 backdrop-blur-sm rounded-xl">
            <div className="bg-card border border-border rounded-lg p-6 shadow-lg max-w-md w-full mx-4">
              <div className="flex items-center gap-3 mb-3">
                <AlertTriangle className="h-5 w-5 text-destructive flex-shrink-0" />
                <h3 className="font-semibold text-foreground">Dersi Sil</h3>
              </div>
              {deleteError ? (
                <div className="mb-4">
                  <p className="text-sm font-medium text-destructive mb-2">Bu ders silinemez:</p>
                  <div className="bg-destructive/5 border border-destructive/20 rounded-md p-3 text-sm text-foreground whitespace-pre-line">
                    {deleteError}
                  </div>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground mb-4">
                  <strong className="text-foreground">{detail?.code} — {detail?.name}</strong> silinecek.
                  Bu işlem geri alınamaz.
                </p>
              )}
              <div className="flex justify-end gap-2">
                <Button variant="outline" size="sm" onClick={() => { setShowDeleteConfirm(false); setDeleteError(null); }}>
                  {deleteError ? 'Kapat' : 'İptal'}
                </Button>
                {!deleteError && (
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={handleDelete}
                    disabled={deleteCourse.isPending}
                  >
                    {deleteCourse.isPending ? 'Siliniyor...' : 'Sil'}
                  </Button>
                )}
              </div>
            </div>
          </div>
        )}
      </ResizableDialogContent>
    </Dialog>
  );
}

// ─── Create Course Dialog ────────────────────────────────────────────────────

interface CreateDialogProps {
  onClose: () => void;
  onCreated: (id: number) => void;
}

function CreateCourseDialog({ onClose, onCreated }: CreateDialogProps) {
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);
  const createCourse = useCreateCourse();
  const [form, setForm] = useState<CreateCourseDto>(emptyCourseForm());
  const [formError, setFormError] = useState('');

  async function handleCreate() {
    if (!form.code.trim() || !form.name.trim()) { setFormError('Kod ve ad zorunludur.'); return; }
    try {
      const result = await createCourse.mutateAsync(form);
      onCreated(result.id);
    } catch (e: unknown) {
      setFormError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Hata oluştu.');
    }
  }

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <ResizableDialogContent defaultWidth={640} minWidth={480} minHeight={400}>
        <div className="flex flex-col h-full min-h-0">
          <h2 className="text-base font-semibold text-foreground mb-4 pr-8">Ders Ekle</h2>
          <Separator className="mb-4" />
          <div className="flex-1 overflow-y-auto">
            <CourseFormFields
              form={form}
              onChange={f => { setForm(f); setFormError(''); }}
              departments={allDepartments}
            />
            {formError && <p className="text-sm text-destructive mt-2">{formError}</p>}
          </div>
          <Separator className="mt-4 mb-3" />
          <DialogFooter className="flex-shrink-0">
            <Button variant="outline" onClick={onClose}>İptal</Button>
            <Button onClick={handleCreate} disabled={createCourse.isPending}>
              {createCourse.isPending ? 'Kaydediliyor...' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </div>
      </ResizableDialogContent>
    </Dialog>
  );
}

// ─── Main Page ───────────────────────────────────────────────────────────────

export default function CourseCatalogAdminPage() {
  // Filter state
  const [search, setSearch] = useState('');
  const [facultyId, setFacultyId] = useState<number | undefined>(undefined);
  const [departmentId, setDepartmentId] = useState<number | undefined>(undefined);
  const [statusFilter, setStatusFilter] = useState<string | undefined>('Active'); // default: Aktif
  const [languageFilter, setLanguageFilter] = useState<string | undefined>(undefined);
  const [levelFilter, setLevelFilter] = useState<string | undefined>(undefined);
  const [typeFilter, setTypeFilter] = useState<CourseTypeFilter>('all');

  // Dialog state
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  // Data
  const { data: faculties = [] } = useAdminFaculties();
  const { data: deptsByFaculty = [] } = useAdminDepartmentsByStructure(facultyId);

  const { data: allCourses = [], isLoading } = useAdminCourses({
    search: search || undefined,
    departmentId,
    facultyId,
    language: languageFilter,
    level: levelFilter,
    status: statusFilter,
  });

  const courses = useMemo(() => applyTypeFilter(allCourses, typeFilter), [allCourses, typeFilter]);

  function handleFacultyChange(val: string) {
    if (val === 'all') {
      setFacultyId(undefined);
      setDepartmentId(undefined);
    } else {
      setFacultyId(Number(val));
      setDepartmentId(undefined);
    }
  }

  const courseTypeOptions: [CourseTypeFilter, string][] = [
    ['all', 'Tümü'],
    ['theory', 'Sadece Teori'],
    ['lab', 'Sadece Lab'],
    ['mixed', 'Teori+Lab'],
  ];

  return (
    <div className="space-y-4">
      {/* Filter Bar */}
      <Card>
        <CardContent className="pt-3 pb-3">
          <div className="flex items-center gap-2.5 flex-wrap">
            {/* Search */}
            <div className="relative w-[220px]">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <Input
                className="pl-8 h-8 text-sm"
                placeholder="Kod veya isimle ara..."
                value={search}
                onChange={e => setSearch(e.target.value)}
              />
            </div>

            <div className="h-5 w-px bg-border" />

            {/* Durum */}
            <Select
              value={statusFilter !== undefined ? String(statusFilter) : 'all'}
              onValueChange={v => setStatusFilter(v === 'all' ? undefined : v)}
            >
              <SelectTrigger className="h-8 w-[95px] text-xs">
                <SelectValue placeholder="Durum" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm Durumlar</SelectItem>
                {Object.entries(COURSE_STATUS_LABELS).map(([k, v]) => (
                  <SelectItem key={k} value={k}>{v}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            {/* Dil */}
            <Select
              value={languageFilter !== undefined ? String(languageFilter) : 'all'}
              onValueChange={v => setLanguageFilter(v === 'all' ? undefined : v)}
            >
              <SelectTrigger className="h-8 w-[105px] text-xs">
                <SelectValue placeholder="Tüm Diller" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm Diller</SelectItem>
                {Object.entries(COURSE_LANGUAGE_LABELS).map(([k, v]) => (
                  <SelectItem key={k} value={k}>{v}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            {/* Seviye */}
            <Select
              value={levelFilter !== undefined ? String(levelFilter) : 'all'}
              onValueChange={v => setLevelFilter(v === 'all' ? undefined : v)}
            >
              <SelectTrigger className="h-8 w-[115px] text-xs">
                <SelectValue placeholder="Tüm Seviyeler" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm Seviyeler</SelectItem>
                {Object.entries(COURSE_LEVEL_LABELS).map(([k, v]) => (
                  <SelectItem key={k} value={k}>{v}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            {/* Fakülte — seçilmemişse dropdown, seçilmişse chip */}
            {facultyId === undefined ? (
              <Select value="all" onValueChange={handleFacultyChange}>
                <SelectTrigger className="h-8 w-[120px] text-xs">
                  <SelectValue placeholder="Fakülte" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all" disabled>Fakülte seçin</SelectItem>
                  {faculties.map(f => (
                    <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            ) : (
              <div className="flex items-center gap-1 bg-primary/10 text-primary rounded-md px-2.5 h-8 text-xs font-medium">
                <span className="truncate max-w-[180px]">{faculties.find(f => f.id === facultyId)?.name ?? 'Fakülte'}</span>
                <button onClick={() => { setFacultyId(undefined); setDepartmentId(undefined); }} className="ml-0.5 hover:text-destructive">
                  <X className="h-3 w-3" />
                </button>
              </div>
            )}

            {/* Bölüm — seçilmemişse dropdown, seçilmişse chip */}
            {facultyId !== undefined && (
              departmentId === undefined ? (
                <Select value="all" onValueChange={v => setDepartmentId(v === 'all' ? undefined : Number(v))}>
                  <SelectTrigger className="h-8 w-[120px] text-xs">
                    <SelectValue placeholder="Bölüm" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all" disabled>Bölüm seçin</SelectItem>
                    {deptsByFaculty.map(d => (
                      <SelectItem key={d.id} value={String(d.id)}>{d.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <div className="flex items-center gap-1 bg-primary/10 text-primary rounded-md px-2.5 h-8 text-xs font-medium">
                  <span className="truncate max-w-[220px]">{deptsByFaculty.find(d => d.id === departmentId)?.name ?? 'Bölüm'}</span>
                  <button onClick={() => setDepartmentId(undefined)} className="ml-0.5 hover:text-destructive">
                    <X className="h-3 w-3" />
                  </button>
                </div>
              )
            )}

            <div className="h-5 w-px bg-border" />

            {/* Course type tabs */}
            <div className="flex items-center gap-0.5 bg-muted rounded-md p-0.5">
              {courseTypeOptions.map(([key, label]) => (
                <button
                  key={key}
                  onClick={() => setTypeFilter(key)}
                  className={`px-2.5 py-1 text-xs rounded transition-all ${
                    typeFilter === key
                      ? 'bg-background text-foreground shadow-sm font-medium'
                      : 'text-muted-foreground hover:text-foreground'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>

            {/* Count + Add */}
            <div className="ml-auto flex items-center gap-3">
              <span className="text-xs text-muted-foreground whitespace-nowrap">
                {!isLoading && `${courses.length} ders`}
              </span>
              <Button size="sm" className="h-8 text-xs" onClick={() => setCreateOpen(true)}>
                <Plus className="h-3.5 w-3.5 mr-1" /> Ders Ekle
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Course Table */}
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="py-16 text-center text-muted-foreground text-sm">Yükleniyor...</div>
          ) : courses.length === 0 ? (
            <div className="py-16 text-center">
              <BookOpen className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Ders bulunamadı.</p>
              <p className="text-xs text-muted-foreground/60 mt-1">Filtrelerinizi değiştirmeyi deneyin.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border bg-muted/30">
                    <th className="text-left text-xs font-medium text-muted-foreground px-4 py-3 w-[110px]">Ders Kodu</th>
                    <th className="text-left text-xs font-medium text-muted-foreground px-4 py-3 w-[30%]">Ders Adı</th>
                    <th className="text-center text-xs font-medium text-muted-foreground px-3 py-3 w-[55px]">AKTS</th>
                    <th className="text-center text-xs font-medium text-muted-foreground px-3 py-3 w-[55px]">T+U</th>
                    <th className="text-center text-xs font-medium text-muted-foreground px-3 py-3 w-[80px]">Dil</th>
                    <th className="text-center text-xs font-medium text-muted-foreground px-3 py-3 w-[80px]">Seviye</th>
                    <th className="text-left text-xs font-medium text-muted-foreground px-4 py-3">Bölüm</th>
                    <th className="text-center text-xs font-medium text-muted-foreground px-3 py-3 w-[75px]">Durum</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {courses.map(c => (
                    <tr
                      key={c.id}
                      className="hover:bg-muted/20 cursor-pointer transition-colors"
                      onClick={() => setSelectedCourseId(c.id)}
                    >
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-1.5">
                          <span className="font-mono font-semibold text-foreground text-sm">{c.code}</span>
                          {c.isNonGpa && (
                            <Badge variant="secondary" className="text-[10px] py-0 px-1">Non-GPA</Badge>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-foreground">{c.name}</td>
                      <td className="px-3 py-3 text-center text-muted-foreground">{c.ects}</td>
                      <td className="px-3 py-3 text-center font-mono text-muted-foreground">{c.theoryHours}+{c.labHours}</td>
                      <td className="px-3 py-3 text-center">
                        <Badge variant="outline" className="text-[11px]">
                          {COURSE_LANGUAGE_LABELS[c.language] ?? c.language}
                        </Badge>
                      </td>
                      <td className="px-3 py-3 text-center">
                        <Badge variant="outline" className="text-[11px]">
                          {COURSE_LEVEL_LABELS[c.level] ?? c.level}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-sm">
                        {c.departmentName
                          ? <span className="text-muted-foreground">{c.departmentName}</span>
                          : <span className="italic text-muted-foreground/40">Ortak</span>
                        }
                      </td>
                      <td className="px-3 py-3 text-center">
                        <Badge variant={c.status === 1 ? 'success' : 'secondary'} className="text-[11px]">
                          {COURSE_STATUS_LABELS[c.status] ?? c.status}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Detail Dialog */}
      {selectedCourseId !== null && (
        <CourseDetailDialog
          courseId={selectedCourseId}
          allCourses={allCourses}
          onClose={() => setSelectedCourseId(null)}
          onDeleted={() => setSelectedCourseId(null)}
        />
      )}

      {/* Create Dialog */}
      {createOpen && (
        <CreateCourseDialog
          onClose={() => setCreateOpen(false)}
          onCreated={id => { setCreateOpen(false); setSelectedCourseId(id); }}
        />
      )}
    </div>
  );
}
