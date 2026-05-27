import { useState, useMemo, useEffect, useRef } from 'react';
import {
  Search, Plus, Edit2, Trash2, X, BookOpen, BarChart3, GitBranch,
  AlertTriangle, AlertCircle, SlidersHorizontal, Copy, Users, CalendarClock, Layers,
  IdCard, Clock, GraduationCap, ShieldCheck, FileText,
  ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight,
  ArrowUpDown, ArrowUp, ArrowDown, Columns3, Rows3,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Card, CardContent } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useToast } from '@/components/ui/global-toast';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { cn } from '@/lib/utils';
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
  useCrossLists,
  useAddCrossList,
  useRemoveCrossList,
  useCourseSections,
  useBulkDeleteCourses,
  type CourseListItemDto,
  type CreateCourseDto,
  type PrerequisiteChainNodeDto,
  type BulkDeleteResultItem,
  type ClassroomTypeValue,
  CLASSROOM_TYPE_LABELS,
  CLASSROOM_TYPE_OPTIONS,
} from '../hooks/useAdminCourseCatalog';
import {
  useAdminDepartmentsByStructure,
} from '../hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '../hooks/useAdminLookup';
import { usePermissions } from '../hooks/usePermissions';
import {
  COURSE_LANGUAGE_LABELS,
  TEACHING_METHOD_LABELS,
  COURSE_STATUS_LABELS,
  COURSE_LEVEL_LABELS,
} from '@/lib/constants';

// ─── Helpers ────────────────────────────────────────────────────────────────

type CourseTypeFilter = 'all' | 'theory' | 'lab' | 'mixed';

const DAY_LABELS: Record<string, string> = {
  Monday: 'Pzt',
  Tuesday: 'Sa',
  Wednesday: 'Çar',
  Thursday: 'Per',
  Friday: 'Cu',
  Saturday: 'Cmt',
  Sunday: 'Paz',
};

function formatScheduleSummary(schedules: { dayOfWeek: string; startTime: string; endTime: string }[]): string {
  if (schedules.length === 0) return '—';
  return schedules
    .map(s => `${DAY_LABELS[s.dayOfWeek] ?? s.dayOfWeek} ${s.startTime}-${s.endTime}`)
    .join(', ');
}

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
    // Ulusal Kredi: Türk eğitim sisteminde theory + lab/2 konvensiyonu, default 3 (3T+0L tipik ders).
    nationalCredit: 3,
    // Proliz "Diğer Ön Koşul Tanımları"
    minCreditsRequired: null,
    minEctsRequired: null,
    requiresNoFailedLowerCourses: false,
    canBeTakenStandalone: false,
    // Phase 2 — Proliz B "Genel Bilgiler" (defaults; not yet surfaced in catalog admin form,
    // but pass-through prevents data loss on save when set via curriculum-side dialog)
    alternativeName: null,
    showInInfoPackage: true,
    applicationHours: null,
    openType: null,
    // Plan 10 — Derslik tipi tercihleri (opsiyonel; null = scheduler otomatik seçsin)
    theoryClassroomType: null,
    labClassroomType: null,
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

interface FieldSectionProps {
  icon?: React.ComponentType<{ className?: string }>;
  iconColor?: string;
  title: string;
  children: React.ReactNode;
}

function FieldSection({ icon: Icon, iconColor = 'text-primary', title, children }: FieldSectionProps) {
  return (
    <fieldset className="border border-border rounded-lg px-6 pt-4 pb-6">
      <legend className="text-[11px] font-semibold text-foreground/80 px-2 uppercase tracking-wide flex items-center gap-1.5">
        {Icon && <Icon className={cn('h-3.5 w-3.5', iconColor)} />}
        {title}
      </legend>
      {children}
    </fieldset>
  );
}

// Okuma modunda input/select/textarea: opaklık tam, border/bg soluk, cursor default
const READ_MODE_CLASS = 'disabled:opacity-100 disabled:cursor-default disabled:bg-muted/30 disabled:border-muted/50 disabled:text-foreground';

// ── Zorunlu alan etiketi (kırmızı yıldız) ──
// Sebep-sonuç: Backend (AdminCourseCatalogService.CreateCourseAsync) DB schema'dan
// türeyen şu kuralları zorlar:
//  - Code: non-nullable + unique index (DB)
//  - Name: non-nullable
//  - DepartmentId: explicit "Ders sahipliği zorunludur" check
//  - Ects/TheoryHours/LabHours: non-nullable int
function RequiredLabel({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <Label className={cn('text-xs', className)}>
      {children}
      <span className="text-destructive ml-0.5 font-semibold" aria-hidden="true">*</span>
      <span className="sr-only"> (zorunlu)</span>
    </Label>
  );
}

/** Pre-flight: server'ın gerçek doğrulama kurallarını ön-görür. Eksik alan listesi döner. */
function collectMissingRequiredFields(form: CreateCourseDto): string[] {
  const missing: string[] = [];
  if (!form.code.trim()) missing.push('Ders Kodu');
  if (!form.name.trim()) missing.push('Ders Adı');
  if (form.departmentId == null) missing.push('Bölüm (ders sahipliği zorunlu)');
  if (!form.ects || form.ects < 1 || form.ects > 30) missing.push('AKTS (1-30 aralığında)');
  // Ulusal Kredi (YÖKSİS zorunlu): null/undefined kabul edilmez, 0-20 aralığı.
  if (form.nationalCredit == null || form.nationalCredit < 0 || form.nationalCredit > 20) {
    missing.push('Ulusal Kredi (0-20 aralığında, YÖKSİS raporlama zorunlu)');
  }
  if (form.theoryHours < 0 || form.theoryHours > 10) missing.push('Teori Saati (0-10 arası)');
  if (form.labHours < 0 || form.labHours > 10) missing.push('Lab Saati (0-10 arası)');
  return missing;
}

function getServerErrorMessage(error: unknown, fallback: string): string {
  return (
    (error as { response?: { data?: { message?: string } } })?.response?.data?.message ??
    fallback
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
      className={cn(
        'w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring',
        READ_MODE_CLASS,
        className,
      )}
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

// ── Kimlik bölümü — 2 sütunlu, sol uzun isimler, sağ kod/bölüm/checkbox ──
function KimlikSection({ form, onChange, departments, disabled = false }: CourseFormFieldsProps) {
  return (
    <FieldSection icon={IdCard} iconColor="text-blue-600" title="Kimlik">
      <div className="grid grid-cols-2 gap-6 mt-3">
        {/* Sol kolon — uzun ad alanları */}
        <div className="space-y-4">
          <div className="space-y-1">
            <RequiredLabel>Ders Adı</RequiredLabel>
            <Input
              value={form.name}
              onChange={e => onChange({ ...form, name: e.target.value })}
              placeholder="örn. Programlamaya Giriş"
              disabled={disabled}
              aria-required="true"
              aria-invalid={!disabled && !form.name.trim()}
              className={READ_MODE_CLASS}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">İngilizce Adı</Label>
            <Input
              value={form.englishName ?? ''}
              onChange={e => onChange({ ...form, englishName: e.target.value || null })}
              placeholder="örn. Introduction to Programming"
              disabled={disabled}
              className={READ_MODE_CLASS}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Diğer Adı</Label>
            <Input
              value={form.alternativeName ?? ''}
              onChange={e => onChange({ ...form, alternativeName: e.target.value || null })}
              placeholder="Alternatif ders adı"
              disabled={disabled}
              className={READ_MODE_CLASS}
            />
          </div>
        </div>
        {/* Sağ kolon — kısa kod/bölüm + bayraklar */}
        <div className="space-y-4">
          <div className="space-y-1">
            <RequiredLabel>Ders Kodu</RequiredLabel>
            <Input
              value={form.code}
              onChange={e => onChange({ ...form, code: e.target.value.toUpperCase() })}
              placeholder="örn. BM101"
              maxLength={20}
              disabled={disabled}
              aria-required="true"
              aria-invalid={!disabled && !form.code.trim()}
              className={cn('font-mono', READ_MODE_CLASS)}
            />
          </div>
          <div className="space-y-1">
            <RequiredLabel>Bölüm</RequiredLabel>
            <NativeSelect
              value={form.departmentId ?? 'none'}
              onChange={v => onChange({ ...form, departmentId: v === 'none' ? null : Number(v) })}
              disabled={disabled}
            >
              <option value="none">— Bölüm seçin —</option>
              {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </NativeSelect>
          </div>
          <div className="flex items-center gap-2 pt-5">
            <input
              type="checkbox"
              id="showInInfoPackage"
              checked={form.showInInfoPackage ?? true}
              onChange={e => onChange({ ...form, showInInfoPackage: e.target.checked })}
              disabled={disabled}
              className="h-4 w-4 rounded border-border accent-primary"
            />
            <label htmlFor="showInInfoPackage" className="text-sm text-muted-foreground cursor-pointer select-none">
              Bilgi Paketinde Göster
            </label>
          </div>
        </div>
      </div>
    </FieldSection>
  );
}

// ── Açıklama bölümü ──
function AciklamaSection({ form, onChange, disabled = false }: CourseFormFieldsProps) {
  const descLen = form.description?.length ?? 0;
  return (
    <FieldSection icon={FileText} iconColor="text-amber-600" title="Açıklama">
      <div className="mt-3 space-y-1.5">
        <textarea
          className={cn(
            'w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring resize-none',
            READ_MODE_CLASS,
          )}
          rows={5}
          maxLength={1000}
          value={form.description ?? ''}
          onChange={e => onChange({ ...form, description: e.target.value || null })}
          disabled={disabled}
          placeholder="Ders açıklaması..."
        />
        <p className="text-[11px] text-muted-foreground text-right">{descLen} / 1000</p>
      </div>
    </FieldSection>
  );
}

// ── Kredi & Saat ──
function KrediSaatSection({ form, onChange, disabled = false }: CourseFormFieldsProps) {
  return (
    <FieldSection icon={Clock} iconColor="text-purple-600" title="Kredi & Saat">
      <div className="grid grid-cols-5 gap-4 mt-3">
        <div className="space-y-1">
          <RequiredLabel>AKTS</RequiredLabel>
          <Input type="number" min={1} max={30} value={form.ects}
            onChange={e => onChange({ ...form, ects: Number(e.target.value) })}
            disabled={disabled}
            aria-required="true"
            aria-invalid={!disabled && (!form.ects || form.ects < 1)}
            className={READ_MODE_CLASS} />
        </div>
        <div className="space-y-1">
          <RequiredLabel>Ulusal Kredi</RequiredLabel>
          <Input type="number" min={0} max={20} value={form.nationalCredit}
            onChange={e => onChange({ ...form, nationalCredit: e.target.value === '' ? 0 : Number(e.target.value) })}
            disabled={disabled}
            aria-required="true"
            aria-invalid={!disabled && (form.nationalCredit == null || form.nationalCredit < 0 || form.nationalCredit > 20)}
            className={READ_MODE_CLASS} />
        </div>
        <div className="space-y-1">
          <RequiredLabel>Teori Saati</RequiredLabel>
          <Input type="number" min={0} max={10} value={form.theoryHours}
            onChange={e => onChange({ ...form, theoryHours: Number(e.target.value) })}
            disabled={disabled}
            aria-required="true"
            aria-invalid={!disabled && (form.theoryHours < 0 || form.theoryHours > 10)}
            className={READ_MODE_CLASS} />
        </div>
        <div className="space-y-1">
          <RequiredLabel>Lab Saati</RequiredLabel>
          <Input type="number" min={0} max={10} value={form.labHours}
            onChange={e => onChange({ ...form, labHours: Number(e.target.value) })}
            disabled={disabled}
            aria-required="true"
            aria-invalid={!disabled && (form.labHours < 0 || form.labHours > 10)}
            className={READ_MODE_CLASS} />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Uygulama Saati</Label>
          <Input type="number" min={0} max={10} value={form.applicationHours ?? ''}
            onChange={e => onChange({ ...form, applicationHours: e.target.value ? Number(e.target.value) : null })}
            disabled={disabled} placeholder="—" className={READ_MODE_CLASS} />
        </div>
      </div>
      {/* Plan 10 — Derslik tipi tercihleri (opsiyonel; H4 ClassroomType encoder okuyor) */}
      <div className="grid grid-cols-2 gap-x-6 gap-y-4 mt-4 pt-4 border-t border-border/60">
        <div className="space-y-1">
          <Label className="text-xs">
            Teori Sınıf Tipi
            <span className="text-muted-foreground ml-1 text-[10px] font-normal">(opsiyonel)</span>
          </Label>
          <NativeSelect
            value={form.theoryClassroomType ?? '__auto__'}
            onChange={v => onChange({
              ...form,
              theoryClassroomType: v === '__auto__' ? null : (v as ClassroomTypeValue),
            })}
            disabled={disabled || form.theoryHours <= 0}
          >
            <option value="__auto__">— Otomatik (kısıt yok) —</option>
            {CLASSROOM_TYPE_OPTIONS.map(opt => (
              <option key={opt} value={opt}>{CLASSROOM_TYPE_LABELS[opt]}</option>
            ))}
          </NativeSelect>
          <p className="text-[10px] text-muted-foreground">
            Seçilirse teori bu tipte derslikte yerleşir; boş ise otomatik seçim.
          </p>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">
            Lab Sınıf Tipi
            <span className="text-muted-foreground ml-1 text-[10px] font-normal">(opsiyonel)</span>
          </Label>
          <NativeSelect
            value={form.labClassroomType ?? '__auto__'}
            onChange={v => onChange({
              ...form,
              labClassroomType: v === '__auto__' ? null : (v as ClassroomTypeValue),
            })}
            disabled={disabled || form.labHours <= 0}
          >
            <option value="__auto__">— Otomatik (kısıt yok) —</option>
            {CLASSROOM_TYPE_OPTIONS.map(opt => (
              <option key={opt} value={opt}>{CLASSROOM_TYPE_LABELS[opt]}</option>
            ))}
          </NativeSelect>
          <p className="text-[10px] text-muted-foreground">
            Seçilirse lab bu tipte derslikte yerleşir; boş ise otomatik seçim.
          </p>
        </div>
      </div>
    </FieldSection>
  );
}

// ── Akademik Bilgiler ──
function AkademikBilgilerSection({ form, onChange, disabled = false }: CourseFormFieldsProps) {
  return (
    <FieldSection icon={GraduationCap} iconColor="text-emerald-600" title="Akademik Bilgiler">
      <div className="grid grid-cols-2 gap-x-6 gap-y-4 mt-3">
        <div className="space-y-1">
          <Label className="text-xs">Dil</Label>
          <NativeSelect value={form.language} onChange={v => onChange({ ...form, language: v })} disabled={disabled}>
            {Object.entries(COURSE_LANGUAGE_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </NativeSelect>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Öğretim Şekli</Label>
          <NativeSelect value={form.teachingMethod} onChange={v => onChange({ ...form, teachingMethod: v })} disabled={disabled}>
            {Object.entries(TEACHING_METHOD_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </NativeSelect>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Seviye</Label>
          <NativeSelect value={form.level} onChange={v => onChange({ ...form, level: v })} disabled={disabled}>
            {Object.entries(COURSE_LEVEL_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </NativeSelect>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Durum</Label>
          <NativeSelect value={form.status} onChange={v => onChange({ ...form, status: v })} disabled={disabled}>
            {Object.entries(COURSE_STATUS_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </NativeSelect>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Sınav Değer Tipi</Label>
          <Input value={form.examType ?? ''}
            onChange={e => onChange({ ...form, examType: e.target.value || null })}
            placeholder="örn. Yazılı" disabled={disabled} className={READ_MODE_CLASS} />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Açma Tipi</Label>
          <NativeSelect
            value={form.openType ?? '__none__'}
            onChange={v => onChange({
              ...form,
              openType: v === '__none__' ? null : (v as 'Standart' | 'Closed' | 'PendingApproval'),
            })}
            disabled={disabled}
          >
            <option value="__none__">— Standart (varsayılan) —</option>
            <option value="Standart">Standart</option>
            <option value="Closed">Kapatıldı</option>
            <option value="PendingApproval">Açık (Onay Bekleniyor)</option>
          </NativeSelect>
        </div>
      </div>
      <div className="flex items-center gap-2 mt-3">
        <input
          type="checkbox" id="isNonGpa" checked={form.isNonGpa}
          onChange={e => onChange({ ...form, isNonGpa: e.target.checked })}
          disabled={disabled}
          className="h-4 w-4 rounded border-border accent-primary"
        />
        <label htmlFor="isNonGpa" className="text-sm text-muted-foreground cursor-pointer select-none">
          Non-GPA (Ortalamasız)
        </label>
      </div>
    </FieldSection>
  );
}

// ── Ön Koşul Kuralları ──
function OnKosulSection({ form, onChange, disabled = false }: CourseFormFieldsProps) {
  return (
    <FieldSection icon={ShieldCheck} iconColor="text-rose-600" title="Ön Koşul Kuralları">
      <div className="grid grid-cols-2 gap-x-6 gap-y-4 mt-3">
        <div className="space-y-1">
          <Label className="text-xs">Min Kredi Ön Koşul</Label>
          <Input type="number" min={0} value={form.minCreditsRequired ?? ''}
            onChange={e => onChange({ ...form, minCreditsRequired: e.target.value ? Number(e.target.value) : null })}
            disabled={disabled} placeholder="—" className={READ_MODE_CLASS} />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Min AKTS Ön Koşul</Label>
          <Input type="number" min={0} value={form.minEctsRequired ?? ''}
            onChange={e => onChange({ ...form, minEctsRequired: e.target.value ? Number(e.target.value) : null })}
            disabled={disabled} placeholder="—" className={READ_MODE_CLASS} />
        </div>
      </div>
      <div className="mt-3 space-y-2">
        <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
          <input type="checkbox" checked={form.requiresNoFailedLowerCourses ?? false}
            onChange={e => onChange({ ...form, requiresNoFailedLowerCourses: e.target.checked })}
            disabled={disabled}
            className="h-4 w-4 rounded border-border accent-primary" />
          <span>Alttan Dersi Olmayanlar Alabilir</span>
        </label>
        <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
          <input type="checkbox" checked={form.canBeTakenStandalone ?? false}
            onChange={e => onChange({ ...form, canBeTakenStandalone: e.target.checked })}
            disabled={disabled}
            className="h-4 w-4 rounded border-border accent-primary" />
          <span>Bu Ders Tek Başına Alınabilir</span>
        </label>
      </div>
    </FieldSection>
  );
}

// ── Wrapper'lar ──
function GenelFields(props: CourseFormFieldsProps) {
  return (
    <div className="space-y-5 overflow-y-auto">
      <KimlikSection {...props} />
      <AciklamaSection {...props} />
    </div>
  );
}

function AkademikFields(props: CourseFormFieldsProps) {
  return (
    <div className="space-y-5 overflow-y-auto">
      <KrediSaatSection {...props} />
      <AkademikBilgilerSection {...props} />
      <OnKosulSection {...props} />
    </div>
  );
}

// Tüm bölümler tek seferde — Create dialog'unda kullanılır
function CourseFormFields(props: CourseFormFieldsProps) {
  return (
    <div className="space-y-5 overflow-y-auto">
      <KimlikSection {...props} />
      <KrediSaatSection {...props} />
      <AkademikBilgilerSection {...props} />
      <OnKosulSection {...props} />
      <AciklamaSection {...props} />
    </div>
  );
}

// ─── Course Detail Dialog ────────────────────────────────────────────────────

interface DetailDialogProps {
  courseId: number;
  allCourses: CourseListItemDto[];
  onClose: () => void;
  onDeleted: () => void;
  onDuplicate: (template: CreateCourseDto) => void;
}

function CourseDetailDialog({ courseId, allCourses, onClose, onDeleted, onDuplicate }: DetailDialogProps) {
  const { data: detail, isLoading } = useAdminCourseDetail(courseId);
  const updateCourse = useUpdateCourse();
  const deleteCourse = useDeleteCourse();
  const addPrereq = useAddPrerequisite();
  const removePrereq = useRemovePrerequisite();
  const addEquiv = useAddEquivalency();
  const removeEquiv = useRemoveEquivalency();
  const { data: crossLists = [] } = useCrossLists(courseId);
  const addCrossList = useAddCrossList();
  const removeCrossList = useRemoveCrossList();
  const { data: sections = [], isLoading: sectionsLoading } = useCourseSections(courseId);

  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('menu.ders-katalogu.create');
  const canEdit = hasPermission('menu.ders-katalogu.edit');
  const canDelete = hasPermission('menu.ders-katalogu.delete');
  const canPrerequisite = hasPermission('menu.ders-katalogu.prerequisite');
  const canEquivalency = hasPermission('menu.ders-katalogu.equivalency');
  const canCrossList = hasPermission('menu.ders-katalogu.cross-list');

  const [editMode, setEditMode] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [formError, setFormError] = useState('');
  const [prereqSearch, setPrereqSearch] = useState('');
  const [equivSearch, setEquivSearch] = useState('');
  const [crossListSearch, setCrossListSearch] = useState('');
  const { showSuccess, showError, showWarning } = useToast();

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
      minCreditsRequired: detail.minCreditsRequired,
      minEctsRequired: detail.minEctsRequired,
      requiresNoFailedLowerCourses: detail.requiresNoFailedLowerCourses,
      canBeTakenStandalone: detail.canBeTakenStandalone,
      // Phase 2 pass-through: preserve values set from curriculum-side dialog
      alternativeName: detail.alternativeName,
      showInInfoPackage: detail.showInInfoPackage,
      applicationHours: detail.applicationHours,
      openType: detail.openType,
      // Plan 10 — Derslik tipi tercihleri
      theoryClassroomType: detail.theoryClassroomType,
      labClassroomType: detail.labClassroomType,
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
    // Pre-flight: server kurallarını ön-gör (UpdateCourseAsync da aynı kuralları zorlar).
    const missing = collectMissingRequiredFields(form);
    if (missing.length > 0) {
      setFormError('');
      showWarning(
        'Değişiklikler kaydedilemedi',
        `${missing.length} zorunlu alan eksik: ${missing.join(', ')}. Lütfen tamamlayıp tekrar deneyin.`,
        { center: true, durationMs: 10000 },
      );
      return;
    }
    try {
      const result = await updateCourse.mutateAsync({ id: courseId, ...form });
      setEditMode(false);
      setFormError('');
      showSuccess(
        'Ders güncellendi',
        `${result.code} — ${result.name}${result.departmentName ? ` · ${result.departmentName}` : ''}`,
      );
    } catch (e: unknown) {
      const msg = getServerErrorMessage(e, 'Güncelleme başarısız.');
      setFormError(msg);
      showError('Ders güncellenemedi', msg);
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

  const existingCrossListIds = useMemo(() => new Set(crossLists.map(x => x.courseId)), [crossLists]);
  const crossListCandidates = useMemo(() =>
    allCourses.filter(c =>
      c.id !== courseId && !existingCrossListIds.has(c.id) &&
      (crossListSearch
        ? c.code.toLowerCase().includes(crossListSearch.toLowerCase()) ||
          c.name.toLowerCase().includes(crossListSearch.toLowerCase())
        : false)
    ).slice(0, 8),
    [allCourses, courseId, existingCrossListIds, crossListSearch]
  );

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <ResizableDialogContent defaultWidth={1040} defaultHeight={800} minWidth={500} minHeight={400}>
        {isLoading || !detail ? (
          <div className="flex items-center justify-center h-48 text-muted-foreground text-sm">Yükleniyor...</div>
        ) : (
          <div className="flex flex-col h-full min-h-0">
            {/* Header — başlık satırı + alt bilgi (action butonları footer'a taşındı) */}
            <div className="pb-4 flex-shrink-0 pr-24">
              <div className="flex items-baseline gap-3 mb-1.5 flex-wrap">
                <span className="font-mono font-bold text-lg text-foreground tracking-wide">{detail.code}</span>
                <span className="text-base text-foreground font-medium truncate">{detail.name}</span>
                {detail.isNonGpa && <Badge variant="secondary" className="text-[10px] self-center">Non-GPA</Badge>}
              </div>
              <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <span className="font-medium">{detail.ects} AKTS</span>
                <span className="text-muted-foreground/40">·</span>
                <span className="font-mono">{detail.theoryHours}T+{detail.labHours}L</span>
                {detail.departmentName && (
                  <>
                    <span className="text-muted-foreground/40">·</span>
                    <span>{detail.departmentName}</span>
                  </>
                )}
              </div>
            </div>

            <Separator className="flex-shrink-0" />

            {/* Validation alerts — edit modunda server hatası gösterilir */}
            {editMode && formError && (
              <Alert variant="destructive" className="mt-3 mb-1">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  <p className="font-medium mb-0.5">Sunucu doğrulamayı reddetti</p>
                  <p className="text-sm">{formError}</p>
                </AlertDescription>
              </Alert>
            )}
            {editMode && (
              <p className="text-[11px] text-muted-foreground mt-2 px-0.5">
                <span className="text-destructive font-semibold">*</span> işaretli alanlar zorunludur.
              </p>
            )}

            {/* Tabs */}
            <Tabs defaultValue="genel" className="flex-1 flex flex-col min-h-0 mt-1">
              <TabsList className="h-auto p-0 mb-4 flex-shrink-0 w-full bg-transparent border-b rounded-none">
                <TabsTrigger
                  value="genel"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <BookOpen className="h-3.5 w-3.5" /> Genel
                </TabsTrigger>
                <TabsTrigger
                  value="akademik"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <SlidersHorizontal className="h-3.5 w-3.5" /> Akademik
                </TabsTrigger>
                <TabsTrigger
                  value="onkosullar"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <GitBranch className="h-3.5 w-3.5" /> İlişkiler
                </TabsTrigger>
                <TabsTrigger
                  value="mufredat"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <Layers className="h-3.5 w-3.5" /> Müfredat
                </TabsTrigger>
                <TabsTrigger
                  value="program"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <CalendarClock className="h-3.5 w-3.5" /> Ders Programı
                </TabsTrigger>
                <TabsTrigger
                  value="hocalar"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <Users className="h-3.5 w-3.5" /> Öğretim Üyeleri
                </TabsTrigger>
                <TabsTrigger
                  value="istatistik"
                  className="flex-1 rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:shadow-none data-[state=active]:text-primary px-3.5 py-2.5 text-xs font-medium gap-1.5 transition-colors hover:text-foreground"
                >
                  <BarChart3 className="h-3.5 w-3.5" /> İstatistik
                </TabsTrigger>
              </TabsList>

              {/* Tab 1: Genel — Kimlik + Açıklama */}
              <TabsContent value="genel" className="flex-1 overflow-y-auto">
                <GenelFields
                  form={form}
                  onChange={f => {
                    setForm(f);
                    setFormError('');
                  }}
                  departments={allDepartments}
                  disabled={!editMode}
                />
              </TabsContent>

              {/* Tab 2: Akademik — Kredi & Saat + Akademik Bilgiler + Ön Koşul Kuralları */}
              <TabsContent value="akademik" className="flex-1 overflow-y-auto">
                <AkademikFields
                  form={form}
                  onChange={f => {
                    setForm(f);
                    setFormError('');
                  }}
                  departments={allDepartments}
                  disabled={!editMode}
                />
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
                          {canPrerequisite && (
                            <button
                              className="ml-0.5 text-muted-foreground hover:text-destructive transition-colors"
                              onClick={() => removePrereq.mutate({ courseId, requiredCourseId: p.courseId })}
                            >
                              <X className="h-3 w-3" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                  {canPrerequisite && (
                    <div className="relative">
                      <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                      <Input
                        className="pl-8 h-8 text-xs"
                        placeholder="Önkoşul ekle..."
                        value={prereqSearch}
                        onChange={e => setPrereqSearch(e.target.value)}
                      />
                    </div>
                  )}
                  {canPrerequisite && prereqSearch && prereqCandidates.length > 0 && (
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

                {/* Dependent Courses (read-only — ters yön, ilgili dersin Önkoşullar bölümünden yönetilir) */}
                {detail.dependentCourses && detail.dependentCourses.length > 0 && (
                  <div>
                    <h3 className="text-sm font-semibold text-foreground mb-2">
                      Bu Dersi Önkoşul Alanlar ({detail.dependentCourses.length})
                    </h3>
                    <p className="text-[11px] text-muted-foreground mb-2">
                      Aşağıdaki dersler bu dersi önkoşul olarak istiyor. Düzenleme ilgili dersin "Önkoşullar" bölümünden yapılır.
                    </p>
                    <div className="flex flex-wrap gap-1.5">
                      {detail.dependentCourses.map(d => (
                        <div key={d.courseId} className="flex items-center gap-1 bg-muted/60 rounded-full px-2.5 py-0.5 text-xs">
                          <span className="font-mono font-medium text-foreground">{d.code}</span>
                          <span className="text-muted-foreground">{d.name}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

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
                          {canEquivalency && (
                            <button
                              className="ml-0.5 text-primary/40 hover:text-destructive transition-colors"
                              onClick={() => removeEquiv.mutate({ courseId, equivalentCourseId: e.courseId })}
                            >
                              <X className="h-3 w-3" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                  {canEquivalency && (
                    <div className="relative">
                      <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                      <Input
                        className="pl-8 h-8 text-xs"
                        placeholder="Eşdeğer ders ekle..."
                        value={equivSearch}
                        onChange={e => setEquivSearch(e.target.value)}
                      />
                    </div>
                  )}
                  {canEquivalency && equivSearch && equivCandidates.length > 0 && (
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

                {/* Cross-List (Madde 14) */}
                <div>
                  <h3 className="text-sm font-semibold text-foreground mb-2">
                    Cross-List Dersler ({crossLists.length})
                  </h3>
                  <p className="text-[11px] text-muted-foreground mb-2">
                    İki ders kodunun aynı dersi temsil ettiğini belirtir. Örn: YZM301 ≡ BIL301.
                  </p>
                  {crossLists.length > 0 && (
                    <div className="flex flex-wrap gap-1.5 mb-3">
                      {crossLists.map(x => (
                        <div key={x.courseId} className="flex items-center gap-1 bg-violet-500/10 rounded-full pl-2.5 pr-1 py-0.5 text-xs">
                          <span className="font-mono font-medium text-violet-700">{x.code}</span>
                          <span className="text-violet-700/70">{x.name}</span>
                          {canCrossList && (
                            <button
                              className="ml-0.5 text-violet-700/40 hover:text-destructive transition-colors"
                              onClick={() => removeCrossList.mutate({ courseId, linkedCourseId: x.courseId })}
                            >
                              <X className="h-3 w-3" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                  {canCrossList && (
                    <div className="relative">
                      <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                      <Input
                        className="pl-8 h-8 text-xs"
                        placeholder="Cross-list ders ekle..."
                        value={crossListSearch}
                        onChange={e => setCrossListSearch(e.target.value)}
                      />
                    </div>
                  )}
                  {canCrossList && crossListSearch && crossListCandidates.length > 0 && (
                    <div className="border border-border rounded-md mt-1 divide-y divide-border max-h-40 overflow-y-auto bg-background">
                      {crossListCandidates.map(c => (
                        <button
                          key={c.id}
                          className="w-full text-left px-3 py-1.5 text-xs hover:bg-muted/50 flex items-center justify-between"
                          onClick={() => { addCrossList.mutate({ courseId, linkedCourseId: c.id }); setCrossListSearch(''); }}
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

              {/* Tab 4: Ders Programı — açıldığı şubeler tablosu */}
              <TabsContent value="program" className="flex-1 overflow-y-auto">
                {sectionsLoading ? (
                  <div className="py-12 text-center text-muted-foreground text-sm">Yükleniyor...</div>
                ) : sections.length === 0 ? (
                  <div className="py-12 text-center">
                    <CalendarClock className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                    <p className="text-sm text-muted-foreground">Bu ders henüz hiç açılmamış.</p>
                  </div>
                ) : (
                  <div>
                    <p className="text-xs text-muted-foreground mb-2">
                      {sections.length} şube · {new Set(sections.map(s => s.termId)).size} dönem
                    </p>
                    <div className="border rounded-lg overflow-hidden">
                      <table className="w-full text-xs">
                        <thead>
                          <tr className="bg-muted/50">
                            <th className="text-left px-3 py-2 font-medium text-muted-foreground">Dönem</th>
                            <th className="text-center px-2 py-2 font-medium text-muted-foreground w-[55px]">Şube</th>
                            <th className="text-left px-3 py-2 font-medium text-muted-foreground">Hoca</th>
                            <th className="text-left px-3 py-2 font-medium text-muted-foreground">Saatler</th>
                            <th className="text-left px-3 py-2 font-medium text-muted-foreground">Derslik</th>
                            <th className="text-center px-2 py-2 font-medium text-muted-foreground w-[80px]">Kapasite</th>
                            <th className="text-center px-2 py-2 font-medium text-muted-foreground w-[70px]">Durum</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y">
                          {sections.map(s => {
                            const classrooms = Array.from(
                              new Set(s.schedules.map(sc => sc.classroom).filter((c): c is string => !!c))
                            );
                            return (
                              <tr key={s.sectionId}>
                                <td className="px-3 py-2 font-medium">{s.termName}</td>
                                <td className="px-2 py-2 text-center font-mono">{s.sectionNumber}</td>
                                <td className="px-3 py-2">
                                  {s.instructorFullName ?? <span className="italic text-muted-foreground/60">— Atanmamış —</span>}
                                </td>
                                <td className="px-3 py-2 font-mono text-[11px]">{formatScheduleSummary(s.schedules)}</td>
                                <td className="px-3 py-2">{classrooms.length === 0 ? '—' : classrooms.join(', ')}</td>
                                <td className="px-2 py-2 text-center font-mono">{s.enrolledCount}/{s.capacity}</td>
                                <td className="px-2 py-2 text-center">
                                  <Badge
                                    variant={s.status === 'Active' ? 'success' : s.status === 'Cancelled' ? 'destructive' : 'secondary'}
                                    className="text-[10px] py-0"
                                  >
                                    {s.status === 'Active' ? 'Aktif' : s.status === 'Draft' ? 'Taslak' : s.status === 'Cancelled' ? 'İptal' : s.status}
                                  </Badge>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </TabsContent>

              {/* Tab 5: Öğretim Üyeleri — bu dersi verenler özet listesi */}
              <TabsContent value="hocalar" className="flex-1 overflow-y-auto">
                {sectionsLoading ? (
                  <div className="py-12 text-center text-muted-foreground text-sm">Yükleniyor...</div>
                ) : (() => {
                  const map = new Map<number, { name: string; terms: Set<number>; sections: number; enrolled: number }>();
                  for (const s of sections) {
                    if (!s.instructorId) continue;
                    const entry = map.get(s.instructorId) ?? { name: s.instructorFullName ?? '—', terms: new Set<number>(), sections: 0, enrolled: 0 };
                    entry.terms.add(s.termId);
                    entry.sections += 1;
                    entry.enrolled += s.enrolledCount;
                    map.set(s.instructorId, entry);
                  }
                  const summary = Array.from(map, ([id, v]) => ({
                    id,
                    name: v.name,
                    termCount: v.terms.size,
                    sectionCount: v.sections,
                    enrolled: v.enrolled,
                  })).sort((a, b) => b.sectionCount - a.sectionCount);
                  const unassignedSections = sections.filter(s => !s.instructorId).length;

                  if (summary.length === 0 && unassignedSections === 0) {
                    return (
                      <div className="py-12 text-center">
                        <Users className="h-8 w-8 text-muted-foreground/30 mx-auto mb-2" />
                        <p className="text-sm text-muted-foreground">Bu dersi henüz hiçbir hoca vermemiş.</p>
                      </div>
                    );
                  }
                  return (
                    <div>
                      <p className="text-xs text-muted-foreground mb-2">
                        {summary.length} farklı öğretim üyesi
                        {unassignedSections > 0 && ` · ${unassignedSections} şube hocasız`}
                      </p>
                      {summary.length > 0 && (
                        <div className="border rounded-lg overflow-hidden">
                          <table className="w-full text-xs">
                            <thead>
                              <tr className="bg-muted/50">
                                <th className="text-left px-3 py-2 font-medium text-muted-foreground">Öğretim Üyesi</th>
                                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-[80px]">Dönem</th>
                                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-[80px]">Şube</th>
                                <th className="text-center px-2 py-2 font-medium text-muted-foreground w-[100px]">Toplam Kayıt</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y">
                              {summary.map(i => (
                                <tr key={i.id}>
                                  <td className="px-3 py-2 font-medium">{i.name}</td>
                                  <td className="px-2 py-2 text-center font-mono">{i.termCount}</td>
                                  <td className="px-2 py-2 text-center font-mono">{i.sectionCount}</td>
                                  <td className="px-2 py-2 text-center font-mono">{i.enrolled}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </div>
                  );
                })()}
              </TabsContent>

              {/* Tab 6: İstatistikler */}
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

            {/* Footer — sol: Düzenle/Çoğalt/Sil, sağ: Kapat ya da İptal/Kaydet
                Tüm butonlar min-w-[100px] ile eşit görsel boyutta */}
            <Separator className="mt-3 mb-3" />
            <DialogFooter className="flex-row justify-between sm:justify-between gap-2 w-full">
              <div className="flex items-center gap-2">
                {!editMode && canEdit && (
                  <Button size="sm" variant="outline" className="min-w-[100px]" onClick={() => setEditMode(true)}>
                    <Edit2 className="h-3.5 w-3.5 mr-1.5" /> Düzenle
                  </Button>
                )}
                {!editMode && canCreate && (
                  <Button size="sm" variant="outline" className="min-w-[100px]" onClick={() => onDuplicate(initialForm)}>
                    <Copy className="h-3.5 w-3.5 mr-1.5" /> Çoğalt
                  </Button>
                )}
                {canDelete && (
                  <Button
                    size="sm"
                    variant="outline"
                    className="min-w-[100px] text-destructive hover:text-destructive hover:bg-destructive/5"
                    onClick={() => setShowDeleteConfirm(true)}
                  >
                    <Trash2 className="h-3.5 w-3.5 mr-1.5" /> Sil
                  </Button>
                )}
              </div>
              <div className="flex items-center gap-2">
                {editMode ? (
                  <>
                    <Button variant="outline" size="sm" className="min-w-[100px]" onClick={handleCancelEdit}>
                      <X className="h-3.5 w-3.5 mr-1.5" /> İptal
                    </Button>
                    <Button size="sm" className="min-w-[100px]" onClick={handleSave} disabled={updateCourse.isPending}>
                      {updateCourse.isPending ? 'Kaydediliyor...' : 'Kaydet'}
                    </Button>
                  </>
                ) : (
                  <Button variant="outline" size="sm" className="min-w-[100px]" onClick={onClose}>
                    <X className="h-3.5 w-3.5 mr-1.5" /> Kapat
                  </Button>
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
  /** Var ise dialog bu form ile önyüklü açılır (Çoğalt akışı). Kod boş bırakılır, ad sonuna "(Kopya)" eklenir. */
  template?: CreateCourseDto;
}

function CreateCourseDialog({ onClose, onCreated, template }: CreateDialogProps) {
  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(undefined);
  const createCourse = useCreateCourse();
  const { showSuccess, showError, showWarning } = useToast();
  const [form, setForm] = useState<CreateCourseDto>(() => {
    if (!template) return emptyCourseForm();
    return { ...template, code: '', name: `${template.name} (Kopya)` };
  });
  const [serverError, setServerError] = useState('');

  async function handleCreate() {
    // 1) Pre-flight: server kurallarını ön-gör (Service.CreateCourseAsync mirror).
    const missing = collectMissingRequiredFields(form);
    if (missing.length > 0) {
      setServerError('');
      showWarning(
        'Ders oluşturulamadı',
        `${missing.length} zorunlu alan eksik: ${missing.join(', ')}. Lütfen tamamlayıp tekrar deneyin.`,
        { center: true, durationMs: 10000 },
      );
      return;
    }

    // 2) Submit — server'ın InvalidOperationException mesajları doğrudan gösterilir.
    try {
      const result = await createCourse.mutateAsync(form);
      showSuccess(
        'Ders başarıyla oluşturuldu',
        `${result.code} — ${result.name}${result.departmentName ? ` · ${result.departmentName}` : ''} (ID: ${result.id})`,
      );
      onCreated(result.id);
    } catch (e: unknown) {
      const msg = getServerErrorMessage(e, 'Ders oluşturulurken bir hata oluştu.');
      setServerError(msg);
      showError('Ders oluşturulamadı', msg);
    }
  }

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <ResizableDialogContent defaultWidth={1040} defaultHeight={800} minWidth={500} minHeight={400}>
        <div className="flex flex-col h-full min-h-0">
          <div className="mb-4 pr-8">
            <h2 className="text-base font-semibold text-foreground">
              {template ? 'Dersi Çoğalt' : 'Yeni Ders Aç'}
            </h2>
            <p className="text-[11px] text-muted-foreground mt-1">
              <span className="text-destructive font-semibold">*</span> işaretli alanlar zorunludur.
            </p>
          </div>
          <Separator className="mb-4" />
          <div className="flex-1 overflow-y-auto">
            {serverError && (
              <Alert variant="destructive" className="mb-4">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  <p className="font-medium mb-0.5">Sunucu doğrulamayı reddetti</p>
                  <p className="text-sm">{serverError}</p>
                </AlertDescription>
              </Alert>
            )}
            <CourseFormFields
              form={form}
              onChange={f => {
                setForm(f);
                setServerError('');
              }}
              departments={allDepartments}
            />
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

const PAGE_SIZE = 50;

function SortIcon({ active, dir }: { active: boolean; dir: 'asc' | 'desc' }) {
  if (!active) return <ArrowUpDown className="h-3 w-3 inline-block ml-1 text-muted-foreground/40" />;
  return dir === 'asc'
    ? <ArrowUp className="h-3 w-3 inline-block ml-1 text-primary" />
    : <ArrowDown className="h-3 w-3 inline-block ml-1 text-primary" />;
}

export default function CourseCatalogAdminPage() {
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('menu.ders-katalogu.create');
  const canDelete = hasPermission('menu.ders-katalogu.delete');

  // Filter state
  const [search, setSearch] = useState('');
  const [facultyId, setFacultyId] = useState<number | undefined>(undefined);
  const [departmentId, setDepartmentId] = useState<number | undefined>(undefined);
  const [statusFilter, setStatusFilter] = useState<string | undefined>('Active'); // default: Aktif
  const [languageFilter, setLanguageFilter] = useState<string | undefined>(undefined);
  const [levelFilter, setLevelFilter] = useState<string | undefined>(undefined);
  const [typeFilter, setTypeFilter] = useState<CourseTypeFilter>('all');
  const [teachingMethodFilter, setTeachingMethodFilter] = useState<string | undefined>(undefined);
  const [nonGpaFilter, setNonGpaFilter] = useState<'all' | 'gpa' | 'nonGpa'>('all');

  // Dialog state
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [duplicateTemplate, setDuplicateTemplate] = useState<CreateCourseDto | null>(null);

  // Toplu silme state
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [bulkDeleteConfirmOpen, setBulkDeleteConfirmOpen] = useState(false);
  const [bulkDeleteResult, setBulkDeleteResult] = useState<BulkDeleteResultItem[] | null>(null);
  const bulkDeleteMutation = useBulkDeleteCourses();

  // Sıralama
  type SortKey = 'code' | 'name' | 'ects' | 'departmentName';
  const [sortKey, setSortKey] = useState<SortKey | null>(null);
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');

  // Yoğun mod (kompakt satırlar)
  const [dense, setDense] = useState(false);

  // Sütun görünürlüğü — opsiyonel sütunlar gizlenebilir (Proliz hizalı)
  type OptionalCol = 'nationalCredit' | 'semester' | 'kontenjan' | 'requirement' | 'program' | 'instructor';
  const [hiddenCols, setHiddenCols] = useState<Set<OptionalCol>>(new Set());

  // Arama input ref — Ctrl+K kısayolu için
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Ctrl/Cmd+K → arama input'a focus
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        searchInputRef.current?.focus();
        searchInputRef.current?.select();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []);

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);

  // Data
  const { data: faculties = [] } = useFacultyLookup();
  const { data: deptsByFaculty = [] } = useAdminDepartmentsByStructure(facultyId);

  const { data: allCourses = [], isLoading } = useAdminCourses({
    search: search || undefined,
    departmentId,
    facultyId,
    language: languageFilter,
    level: levelFilter,
    status: statusFilter,
  });

  const courses = useMemo(() => {
    let filtered = applyTypeFilter(allCourses, typeFilter);
    if (teachingMethodFilter) {
      // API enum'u string olarak döndürüyor; tip `number` tanımlı ama gerçekte "FaceToFace"/"Online"/"Hybrid"
      filtered = filtered.filter(c => String(c.teachingMethod) === teachingMethodFilter);
    }
    if (nonGpaFilter === 'gpa') {
      filtered = filtered.filter(c => !c.isNonGpa);
    } else if (nonGpaFilter === 'nonGpa') {
      filtered = filtered.filter(c => c.isNonGpa);
    }
    return filtered;
  }, [allCourses, typeFilter, teachingMethodFilter, nonGpaFilter]);

  // Sıralama uygulanmış liste (sortKey null ise varsayılan sırada)
  const sortedCourses = useMemo(() => {
    if (!sortKey) return courses;
    const arr = [...courses];
    arr.sort((a, b) => {
      const av = a[sortKey];
      const bv = b[sortKey];
      // Null değerler sona düşsün
      if (av == null && bv == null) return 0;
      if (av == null) return 1;
      if (bv == null) return -1;
      const cmp = typeof av === 'number' && typeof bv === 'number'
        ? av - bv
        : String(av).localeCompare(String(bv), 'tr');
      return sortDir === 'asc' ? cmp : -cmp;
    });
    return arr;
  }, [courses, sortKey, sortDir]);

  const totalPages = Math.max(1, Math.ceil(sortedCourses.length / PAGE_SIZE));
  const pagedCourses = useMemo(
    () => sortedCourses.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [sortedCourses, currentPage]
  );

  function toggleSort(key: SortKey) {
    if (sortKey === key) {
      setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  }

  // Filtre veya arama değişince ilk sayfaya dön
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setCurrentPage(1);
  }, [search, facultyId, departmentId, statusFilter, languageFilter, levelFilter, typeFilter, teachingMethodFilter, nonGpaFilter]);

  // Liste kısalırsa açık sayfa son sayfanın ötesine düşmesin
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);

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

  // Aktif (default'tan farklı) filtrelerin sayısı — butondaki rozet için
  const activeFilterCount = useMemo(() => {
    let count = 0;
    if (statusFilter !== 'Active') count++;
    if (languageFilter !== undefined) count++;
    if (levelFilter !== undefined) count++;
    if (facultyId !== undefined) count++;
    if (departmentId !== undefined) count++;
    if (typeFilter !== 'all') count++;
    if (teachingMethodFilter !== undefined) count++;
    if (nonGpaFilter !== 'all') count++;
    return count;
  }, [statusFilter, languageFilter, levelFilter, facultyId, departmentId, typeFilter, teachingMethodFilter, nonGpaFilter]);

  return (
    <div className="space-y-4">
      {/* Filter Bar — single search + Filters popover */}
      <Card>
        <CardContent className="pt-3 pb-3">
          <div className="flex items-center gap-2.5 flex-wrap">
            {/* Search */}
            <div className="relative flex-1 min-w-[260px] max-w-[420px]">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <Input
                ref={searchInputRef}
                className="pl-8 pr-8 h-8 text-sm"
                placeholder="Kod veya isimle ara..."
                value={search}
                onChange={e => setSearch(e.target.value)}
              />
              {search && (
                <button
                  type="button"
                  onClick={() => {
                    setSearch('');
                    searchInputRef.current?.focus();
                  }}
                  className="absolute right-2 top-1/2 -translate-y-1/2 inline-flex items-center justify-center h-5 w-5 rounded text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                  aria-label="Aramayı temizle"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              )}
            </div>

            {/* Filtreler — tek buton, popover içinde tüm filtreler */}
            <Popover>
              <PopoverTrigger asChild>
                <Button variant="outline" size="sm" className="h-8 gap-2 text-xs">
                  <SlidersHorizontal className="h-3.5 w-3.5" />
                  Filtreler
                  {activeFilterCount > 0 && (
                    <Badge variant="secondary" className="h-5 px-1.5 text-[10px] rounded-full">
                      {activeFilterCount}
                    </Badge>
                  )}
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-[360px] p-4 space-y-3" align="start">
                <div className="flex items-center justify-between">
                  <h4 className="text-sm font-semibold">Filtreler</h4>
                  {activeFilterCount > 0 && (
                    <button
                      onClick={() => {
                        setStatusFilter('Active');
                        setLanguageFilter(undefined);
                        setLevelFilter(undefined);
                        setFacultyId(undefined);
                        setDepartmentId(undefined);
                        setTypeFilter('all');
                        setTeachingMethodFilter(undefined);
                        setNonGpaFilter('all');
                      }}
                      className="text-[11px] text-muted-foreground hover:text-destructive"
                    >
                      Tümünü temizle
                    </button>
                  )}
                </div>

                <div className="grid grid-cols-2 gap-3">
                  {/* Durum */}
                  <div className="space-y-1">
                    <Label className="text-[11px] text-muted-foreground">Durum</Label>
                    <Select
                      value={statusFilter !== undefined ? String(statusFilter) : 'all'}
                      onValueChange={v => setStatusFilter(v === 'all' ? undefined : v)}
                    >
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue placeholder="Tümü" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tümü</SelectItem>
                        {Object.entries(COURSE_STATUS_LABELS).map(([k, v]) => (
                          <SelectItem key={k} value={k}>{v}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Dil */}
                  <div className="space-y-1">
                    <Label className="text-[11px] text-muted-foreground">Dil</Label>
                    <Select
                      value={languageFilter !== undefined ? String(languageFilter) : 'all'}
                      onValueChange={v => setLanguageFilter(v === 'all' ? undefined : v)}
                    >
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue placeholder="Tümü" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tümü</SelectItem>
                        {Object.entries(COURSE_LANGUAGE_LABELS).map(([k, v]) => (
                          <SelectItem key={k} value={k}>{v}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Seviye */}
                  <div className="space-y-1">
                    <Label className="text-[11px] text-muted-foreground">Seviye</Label>
                    <Select
                      value={levelFilter !== undefined ? String(levelFilter) : 'all'}
                      onValueChange={v => setLevelFilter(v === 'all' ? undefined : v)}
                    >
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue placeholder="Tümü" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tümü</SelectItem>
                        {Object.entries(COURSE_LEVEL_LABELS).map(([k, v]) => (
                          <SelectItem key={k} value={k}>{v}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Ders Tipi */}
                  <div className="space-y-1">
                    <Label className="text-[11px] text-muted-foreground">Ders Tipi</Label>
                    <Select value={typeFilter} onValueChange={v => setTypeFilter(v as CourseTypeFilter)}>
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {courseTypeOptions.map(([key, label]) => (
                          <SelectItem key={key} value={key}>{label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Öğretim Şekli */}
                  <div className="space-y-1">
                    <Label className="text-[11px] text-muted-foreground">Öğretim Şekli</Label>
                    <Select
                      value={teachingMethodFilter ?? 'all'}
                      onValueChange={v => setTeachingMethodFilter(v === 'all' ? undefined : v)}
                    >
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue placeholder="Tümü" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tümü</SelectItem>
                        {Object.entries(TEACHING_METHOD_LABELS).map(([k, v]) => (
                          <SelectItem key={k} value={k}>{v}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* GPA Türü */}
                  <div className="space-y-1">
                    <Label className="text-[11px] text-muted-foreground">GPA Türü</Label>
                    <Select value={nonGpaFilter} onValueChange={v => setNonGpaFilter(v as 'all' | 'gpa' | 'nonGpa')}>
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tümü</SelectItem>
                        <SelectItem value="gpa">GPA'ya Giren</SelectItem>
                        <SelectItem value="nonGpa">GPA Dışı (S/U)</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Fakülte (full width) */}
                  <div className="space-y-1 col-span-2">
                    <Label className="text-[11px] text-muted-foreground">Fakülte</Label>
                    <Select
                      value={facultyId !== undefined ? String(facultyId) : 'all'}
                      onValueChange={handleFacultyChange}
                    >
                      <SelectTrigger className="h-8 text-xs">
                        <SelectValue placeholder="Tümü" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">Tümü</SelectItem>
                        {faculties.map(f => (
                          <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Bölüm (full width, aktif fakülte varsa) */}
                  {facultyId !== undefined && (
                    <div className="space-y-1 col-span-2">
                      <Label className="text-[11px] text-muted-foreground">Bölüm</Label>
                      <Select
                        value={departmentId !== undefined ? String(departmentId) : 'all'}
                        onValueChange={v => setDepartmentId(v === 'all' ? undefined : Number(v))}
                      >
                        <SelectTrigger className="h-8 text-xs">
                          <SelectValue placeholder="Tümü" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="all">Tümü</SelectItem>
                          {deptsByFaculty.map(d => (
                            <SelectItem key={d.id} value={String(d.id)}>{d.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  )}
                </div>
              </PopoverContent>
            </Popover>

            {/* Aktif filtre chip'leri — bir bakışta görünür */}
            {activeFilterCount > 0 && (
              <div className="flex items-center gap-1.5 flex-wrap">
                {facultyId !== undefined && (
                  <div className="flex items-center gap-1 bg-primary/10 text-primary rounded-md px-2.5 h-8 text-xs font-medium">
                    <span className="truncate max-w-[180px]">{faculties.find(f => f.id === facultyId)?.name ?? 'Fakülte'}</span>
                    <button onClick={() => { setFacultyId(undefined); setDepartmentId(undefined); }} className="ml-0.5 hover:text-destructive">
                      <X className="h-3 w-3" />
                    </button>
                  </div>
                )}
                {departmentId !== undefined && (
                  <div className="flex items-center gap-1 bg-primary/10 text-primary rounded-md px-2.5 h-8 text-xs font-medium">
                    <span className="truncate max-w-[160px]">{deptsByFaculty.find(d => d.id === departmentId)?.name ?? 'Bölüm'}</span>
                    <button onClick={() => setDepartmentId(undefined)} className="ml-0.5 hover:text-destructive">
                      <X className="h-3 w-3" />
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* Sağda: Sütun seçici + Yoğun mod + Yeni Ders */}
            <div className="ml-auto flex items-center gap-2">
              {/* Sütun görünürlüğü */}
              <Popover>
                <PopoverTrigger asChild>
                  <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs" title="Sütunları seç">
                    <Columns3 className="h-3.5 w-3.5" />
                    Sütunlar
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-[220px] p-3 space-y-2" align="end">
                  <p className="text-xs font-semibold text-foreground/80 mb-2">Görünür Sütunlar</p>
                  {([
                    ['nationalCredit', 'K (Ulusal Kredi)'],
                    ['semester', 'Snf (Yarıyıl)'],
                    ['kontenjan', 'KO/DO/Kon'],
                    ['requirement', 'Zor. (Zorunlu/Seçmeli)'],
                    ['program', 'Program'],
                    ['instructor', 'Öğretim Üyesi'],
                  ] as const).map(([key, label]) => {
                    const visible = !hiddenCols.has(key);
                    return (
                      <label key={key} className="flex items-center gap-2 text-sm cursor-pointer select-none">
                        <input
                          type="checkbox"
                          checked={visible}
                          onChange={e => {
                            const next = new Set(hiddenCols);
                            if (e.target.checked) next.delete(key);
                            else next.add(key);
                            setHiddenCols(next);
                          }}
                          className="h-4 w-4 rounded border-border accent-primary"
                        />
                        <span>{label}</span>
                      </label>
                    );
                  })}
                </PopoverContent>
              </Popover>

              {/* Yoğun mod */}
              <Button
                variant="outline"
                size="sm"
                className="h-8 gap-1.5 text-xs"
                onClick={() => setDense(d => !d)}
                title={dense ? 'Konfor moduna geç' : 'Yoğun moda geç'}
              >
                <Rows3 className="h-3.5 w-3.5" />
                {dense ? 'Konfor' : 'Yoğun'}
              </Button>

              {canCreate && (
                <Button size="sm" className="h-8 text-xs" onClick={() => setCreateOpen(true)}>
                  <Plus className="h-3.5 w-3.5 mr-1" /> Yeni Ders Aç
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Toplu seçim banner — yalnızca seçim varken görünür */}
      {selectedIds.size > 0 && (
        <Card className="border-primary/40 bg-primary/5">
          <CardContent className="py-2.5 flex items-center gap-3 flex-wrap">
            <span className="text-sm font-medium text-primary">
              {selectedIds.size} ders seçildi
            </span>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={() => setSelectedIds(new Set())}
            >
              Seçimi Temizle
            </Button>
            {canDelete && (
              <Button
                variant="destructive"
                size="sm"
                className="h-7 text-xs ml-auto"
                onClick={() => setBulkDeleteConfirmOpen(true)}
              >
                <Trash2 className="h-3.5 w-3.5 mr-1" />
                Seçilenleri Sil ({selectedIds.size})
              </Button>
            )}
          </CardContent>
        </Card>
      )}

      {/* Course Table */}
      <Card className="overflow-hidden">
        <CardContent className="p-0">
          {isLoading ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border bg-muted/30">
                    <th className="px-4 py-3 w-[42px]"></th>
                    <th className="px-4 py-3 w-[110px] text-left text-[13px] font-semibold text-foreground/70">Ders Kodu</th>
                    <th className="px-4 py-3 text-left text-[13px] font-semibold text-foreground/70">Ders Adı</th>
                    <th className="px-4 py-3 w-[60px] text-center text-[13px] font-semibold text-foreground/70">T+U</th>
                    {!hiddenCols.has('nationalCredit') && <th className="px-4 py-3 w-[50px] text-center text-[13px] font-semibold text-foreground/70">K</th>}
                    <th className="px-4 py-3 w-[72px] text-center text-[13px] font-semibold text-foreground/70">AKTS</th>
                    {!hiddenCols.has('semester') && <th className="px-4 py-3 w-[60px] text-center text-[13px] font-semibold text-foreground/70">Snf</th>}
                    {!hiddenCols.has('kontenjan') && <th className="px-4 py-3 w-[90px] text-center text-[13px] font-semibold text-foreground/70">KO/DO/Kon</th>}
                    {!hiddenCols.has('requirement') && <th className="px-4 py-3 w-[70px] text-center text-[13px] font-semibold text-foreground/70">Zor.</th>}
                    {!hiddenCols.has('program') && <th className="px-4 py-3 text-left text-[13px] font-semibold text-foreground/70">Program</th>}
                    {!hiddenCols.has('instructor') && <th className="px-4 py-3 text-left text-[13px] font-semibold text-foreground/70">Öğretim Üyesi</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {Array.from({ length: 8 }).map((_, i) => (
                    <tr key={i}>
                      <td className="px-4 py-3"><Skeleton className="h-4 w-4 mx-auto" /></td>
                      <td className="px-4 py-3"><Skeleton className="h-4 w-16" /></td>
                      <td className="px-4 py-3"><Skeleton className="h-4 w-3/4" /></td>
                      <td className="px-4 py-3"><Skeleton className="h-4 w-8 mx-auto" /></td>
                      {!hiddenCols.has('nationalCredit') && <td className="px-4 py-3"><Skeleton className="h-4 w-5 mx-auto" /></td>}
                      <td className="px-4 py-3"><Skeleton className="h-4 w-6 mx-auto" /></td>
                      {!hiddenCols.has('semester') && <td className="px-4 py-3"><Skeleton className="h-4 w-6 mx-auto" /></td>}
                      {!hiddenCols.has('kontenjan') && <td className="px-4 py-3"><Skeleton className="h-4 w-4 mx-auto rounded-full" /></td>}
                      {!hiddenCols.has('requirement') && <td className="px-4 py-3"><Skeleton className="h-4 w-8 mx-auto" /></td>}
                      {!hiddenCols.has('program') && <td className="px-4 py-3"><Skeleton className="h-4 w-2/3" /></td>}
                      {!hiddenCols.has('instructor') && <td className="px-4 py-3"><Skeleton className="h-4 w-1/2" /></td>}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : courses.length === 0 ? (
            <div className="py-16 text-center">
              <BookOpen className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Ders bulunamadı.</p>
              <p className="text-xs text-muted-foreground/60 mt-1 mb-4">Filtrelerinizi değiştirin veya yeni bir ders ekleyin.</p>
              <div className="flex items-center justify-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setSearch('');
                    setStatusFilter('Active');
                    setLanguageFilter(undefined);
                    setLevelFilter(undefined);
                    setFacultyId(undefined);
                    setDepartmentId(undefined);
                    setTypeFilter('all');
                    setTeachingMethodFilter(undefined);
                    setNonGpaFilter('all');
                  }}
                >
                  <X className="h-3.5 w-3.5 mr-1.5" /> Filtreleri Temizle
                </Button>
                {canCreate && (
                  <Button size="sm" onClick={() => setCreateOpen(true)}>
                    <Plus className="h-3.5 w-3.5 mr-1.5" /> Yeni Ders Aç
                  </Button>
                )}
              </div>
            </div>
          ) : (
            <>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="sticky top-0 z-10 bg-card/95 backdrop-blur-sm border-b border-border">
                  <tr>
                    <th className="text-center px-4 py-3 w-[42px]">
                      <input
                        type="checkbox"
                        className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                        title="Bu sayfadakileri seç / kaldır"
                        checked={pagedCourses.length > 0 && pagedCourses.every(c => selectedIds.has(c.id))}
                        onChange={e => {
                          const next = new Set(selectedIds);
                          if (e.target.checked) {
                            for (const c of pagedCourses) next.add(c.id);
                          } else {
                            for (const c of pagedCourses) next.delete(c.id);
                          }
                          setSelectedIds(next);
                        }}
                      />
                    </th>
                    <th className="text-left text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[110px] cursor-pointer select-none hover:text-foreground whitespace-nowrap" onClick={() => toggleSort('code')}>
                      Ders Kodu
                      <SortIcon active={sortKey === 'code'} dir={sortDir} />
                    </th>
                    <th className="text-left text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[28%] cursor-pointer select-none hover:text-foreground whitespace-nowrap" onClick={() => toggleSort('name')}>
                      Ders Adı
                      <SortIcon active={sortKey === 'name'} dir={sortDir} />
                    </th>
                    <th className="text-center text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[60px] whitespace-nowrap">T+U</th>
                    {!hiddenCols.has('nationalCredit') && (
                      <th className="text-center text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[50px] whitespace-nowrap" title="Ulusal Kredi">K</th>
                    )}
                    <th className="text-center text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[72px] cursor-pointer select-none hover:text-foreground whitespace-nowrap" onClick={() => toggleSort('ects')}>
                      AKTS
                      <SortIcon active={sortKey === 'ects'} dir={sortDir} />
                    </th>
                    {!hiddenCols.has('semester') && (
                      <th className="text-center text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[60px] whitespace-nowrap" title="Önerilen sınıf — müfredat yarıyılından hesaplanır (1-2 yarıyıl=1. sınıf, 3-4=2., 5-6=3., 7-8=4.)">Snf</th>
                    )}
                    {!hiddenCols.has('kontenjan') && (
                      <th className="text-center text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[90px] whitespace-nowrap" title="Aktif dönemde açılış durumu">KO/DO/Kon</th>
                    )}
                    {!hiddenCols.has('requirement') && (
                      <th className="text-center text-[13px] font-semibold text-foreground/70 px-4 py-3 w-[70px] whitespace-nowrap" title="Müfredattaki tip — Zorunlu / Seçmeli / Karma">Zor.</th>
                    )}
                    {!hiddenCols.has('program') && (
                      <th className="text-left text-[13px] font-semibold text-foreground/70 px-4 py-3 cursor-pointer select-none hover:text-foreground whitespace-nowrap" onClick={() => toggleSort('departmentName')}>
                        Program
                        <SortIcon active={sortKey === 'departmentName'} dir={sortDir} />
                      </th>
                    )}
                    {!hiddenCols.has('instructor') && (
                      <th className="text-left text-[13px] font-semibold text-foreground/70 px-4 py-3 whitespace-nowrap" title="Aktif dönemde dersi veren öğretim üyesi">Öğretim Üyesi</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {pagedCourses.map((c, idx) => {
                    const isEven = idx % 2 === 0;
                    const rowPad = dense ? 'py-1.5' : 'py-3';
                    return (
                      <tr
                        key={c.id}
                        className={cn(
                          'cursor-pointer transition-colors border-l-2 border-transparent',
                          isEven ? 'bg-card' : 'bg-muted/20',
                          'hover:bg-primary/5 hover:border-l-primary',
                        )}
                        onClick={() => setSelectedCourseId(c.id)}
                      >
                        <td className={cn('px-4 text-center', rowPad)} onClick={e => e.stopPropagation()}>
                          <input
                            type="checkbox"
                            className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                            checked={selectedIds.has(c.id)}
                            onChange={e => {
                              const next = new Set(selectedIds);
                              if (e.target.checked) next.add(c.id); else next.delete(c.id);
                              setSelectedIds(next);
                            }}
                          />
                        </td>
                        <td className={cn('px-4', rowPad)}>
                          <div className="flex items-center gap-1.5">
                            <span className="font-mono font-semibold text-foreground text-sm">{c.code}</span>
                            {c.isNonGpa && (
                              <Badge variant="secondary" className="text-[10px] py-0 px-1">Non-GPA</Badge>
                            )}
                          </div>
                        </td>
                        <td className={cn('px-4 text-foreground', rowPad)}>{c.name}</td>
                        <td className={cn('px-4 text-center font-mono tabular-nums text-muted-foreground', rowPad)}>{c.theoryHours}+{c.labHours}</td>
                        {!hiddenCols.has('nationalCredit') && (
                          <td className={cn('px-4 text-center tabular-nums text-muted-foreground', rowPad)}>
                            {c.nationalCredit}
                          </td>
                        )}
                        <td className={cn('px-4 text-center tabular-nums text-muted-foreground', rowPad)}>{c.ects}</td>
                        {!hiddenCols.has('semester') && (
                          <td className={cn('px-4 text-center tabular-nums text-muted-foreground', rowPad)}>
                            {c.semesterMin == null || c.semesterMax == null
                              ? <span className="text-muted-foreground/40">—</span>
                              : (() => {
                                  const minClass = Math.ceil(c.semesterMin / 2);
                                  const maxClass = Math.ceil(c.semesterMax / 2);
                                  return minClass === maxClass ? minClass : `${minClass}-${maxClass}`;
                                })()}
                          </td>
                        )}
                        {!hiddenCols.has('kontenjan') && (
                          <td className={cn('px-4 text-center', rowPad)}>
                            {c.isOpenInActiveTerm
                              ? <span className="inline-flex h-4 w-4 items-center justify-center rounded-full bg-emerald-500/15" title="Aktif dönemde açık">
                                  <span className="h-2 w-2 rounded-full bg-emerald-500" />
                                </span>
                              : <span className="text-muted-foreground/40 text-xs">—</span>
                            }
                          </td>
                        )}
                        {!hiddenCols.has('requirement') && (
                          <td className={cn('px-4 text-center', rowPad)}>
                            {c.requirementType === 'Compulsory' && (
                              <Badge variant="outline" className="text-[10px] py-0 px-1.5 border-emerald-500/40 text-emerald-700 bg-emerald-50">Z</Badge>
                            )}
                            {c.requirementType === 'Elective' && (
                              <Badge variant="outline" className="text-[10px] py-0 px-1.5 border-sky-500/40 text-sky-700 bg-sky-50">S</Badge>
                            )}
                            {c.requirementType === 'Mixed' && (
                              <Badge variant="outline" className="text-[10px] py-0 px-1.5 border-amber-500/40 text-amber-700 bg-amber-50">Karma</Badge>
                            )}
                            {c.requirementType === 'None' && (
                              <span className="text-muted-foreground/40 text-xs">—</span>
                            )}
                          </td>
                        )}
                        {!hiddenCols.has('program') && (
                          <td className={cn('px-4 text-sm', rowPad)}>
                            {c.departmentName
                              ? <span className="text-muted-foreground">{c.departmentName}</span>
                              : <span className="italic text-muted-foreground/40">Ortak</span>
                            }
                          </td>
                        )}
                        {!hiddenCols.has('instructor') && (
                          <td className={cn('px-4 text-sm', rowPad)}>
                            {c.activeTermInstructorName
                              ? <span className="text-muted-foreground">{c.activeTermInstructorName}</span>
                              : <span className="text-muted-foreground/40">—</span>
                            }
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Pagination — Müfredat sayfasıyla aynı stil */}
            <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2.5 border-t bg-muted/20 text-sm">
              <span className="text-muted-foreground">
                {courses.length} ders — Sayfa {currentPage}/{totalPages}
              </span>
              {totalPages > 1 && (
                <div className="flex items-center gap-1">
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === 1} onClick={() => setCurrentPage(1)}>
                    <ChevronsLeft className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === 1} onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}>
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <span className="px-2 min-w-[2rem] text-center">{currentPage}</span>
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === totalPages} onClick={() => setCurrentPage(Math.min(totalPages, currentPage + 1))}>
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === totalPages} onClick={() => setCurrentPage(totalPages)}>
                    <ChevronsRight className="h-4 w-4" />
                  </Button>
                </div>
              )}
            </div>
            </>
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
          onDuplicate={tpl => {
            setSelectedCourseId(null);
            setDuplicateTemplate(tpl);
          }}
        />
      )}

      {/* Create Dialog */}
      {createOpen && (
        <CreateCourseDialog
          onClose={() => setCreateOpen(false)}
          onCreated={id => { setCreateOpen(false); setSelectedCourseId(id); }}
        />
      )}

      {/* Duplicate Dialog (Çoğalt) */}
      {duplicateTemplate && (
        <CreateCourseDialog
          template={duplicateTemplate}
          onClose={() => setDuplicateTemplate(null)}
          onCreated={id => { setDuplicateTemplate(null); setSelectedCourseId(id); }}
        />
      )}

      {/* Toplu silme onay dialog'u */}
      {bulkDeleteConfirmOpen && (
        <Dialog open onOpenChange={open => { if (!open) setBulkDeleteConfirmOpen(false); }}>
          <DialogContent className="max-w-md">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-destructive" />
                Seçili dersleri sil
              </DialogTitle>
              <DialogDescription>
                <strong className="text-foreground">{selectedIds.size} ders</strong> silinecek.
                Bazı dersler müfredatta veya açılmış şubelerde kullanılıyor olabilir; bu dersler silinemez,
                tek tek raporlanır. Silinemeyen dersler için detayda neden görüntülenir.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline" onClick={() => setBulkDeleteConfirmOpen(false)}>İptal</Button>
              <Button
                variant="destructive"
                disabled={bulkDeleteMutation.isPending}
                onClick={async () => {
                  const ids = Array.from(selectedIds);
                  const result = await bulkDeleteMutation.mutateAsync(ids);
                  setBulkDeleteConfirmOpen(false);
                  setBulkDeleteResult(result);
                  // Başarıyla silinenler seçimden düşülür
                  const failedIds = new Set(result.filter(r => !r.success).map(r => r.id));
                  setSelectedIds(failedIds);
                }}
              >
                {bulkDeleteMutation.isPending ? 'Siliniyor...' : 'Sil'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}

      {/* Toplu silme sonuç dialog'u */}
      {bulkDeleteResult && (
        <Dialog open onOpenChange={open => { if (!open) setBulkDeleteResult(null); }}>
          <DialogContent className="max-w-lg max-h-[70vh] flex flex-col">
            <DialogHeader>
              <DialogTitle>Toplu Silme Sonucu</DialogTitle>
              {(() => {
                const ok = bulkDeleteResult.filter(r => r.success).length;
                const failed = bulkDeleteResult.filter(r => !r.success).length;
                return (
                  <DialogDescription>
                    <span className="text-emerald-600 font-medium">{ok} başarılı</span>
                    {failed > 0 && (
                      <> · <span className="text-destructive font-medium">{failed} silinemedi</span></>
                    )}
                  </DialogDescription>
                );
              })()}
            </DialogHeader>
            {(() => {
              const failed = bulkDeleteResult.filter(r => !r.success);
              return (
                <>
                  {failed.length > 0 && (
                    <div className="flex-1 overflow-y-auto border rounded-md divide-y">
                      {failed.map(r => {
                        const c = allCourses.find(x => x.id === r.id);
                        return (
                          <div key={r.id} className="p-2.5 text-xs">
                            <div className="flex items-center gap-2 mb-1">
                              <span className="font-mono font-semibold text-foreground">{c?.code ?? `#${r.id}`}</span>
                              <span className="text-muted-foreground">{c?.name ?? ''}</span>
                            </div>
                            <p className="text-destructive">{r.message}</p>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </>
              );
            })()}
            <DialogFooter className="mt-3">
              <Button onClick={() => setBulkDeleteResult(null)}>Kapat</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}
