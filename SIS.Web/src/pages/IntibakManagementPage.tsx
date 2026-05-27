import { useEffect, useMemo, useRef, useState } from 'react';
import {
  AlertCircle,
  ArrowRight,
  CircleDashed,
  Link2,
  Link2Off,
  Loader2,
  RotateCcw,
  Save,
  Search,
  Users,
  X,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/global-toast';
import { cn } from '@/lib/utils';
import { useAdminCourses } from '../hooks/useAdminCourseCatalog';
import { useDebounce } from '../hooks/useDebounce';
import {
  useCourseIntibakStatistics,
  useIntibakCourses,
  useIntibakCurricula,
  useSaveIntibak,
} from '../hooks/useIntibak';
import type { CurriculumStatusValue } from '../types/curriculumManagement';
import type {
  CourseIntibakStatisticsDto,
  IntibakBulkItem,
  IntibakCourseDto,
  IntibakCurriculumDto,
} from '../api/intibak.api';

/* ── Müfredat durumu görsel meta ── */
const STATUS_META: Record<CurriculumStatusValue, { label: string; className: string }> = {
  Draft: { label: 'Taslak', className: 'bg-amber-50 text-amber-700 border-amber-200' },
  Published: { label: 'Yayında', className: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  Archived: { label: 'Arşiv', className: 'bg-gray-100 text-gray-600 border-gray-200' },
};

/* ── Yarıyıl → Sınıf / Dönem türetimi ── */
function semesterToYear(sem: number): number | null {
  return sem > 0 ? Math.ceil(sem / 2) : null;
}
function semesterToSeason(sem: number): 'Güz' | 'Bahar' | null {
  if (sem <= 0) return null;
  return sem % 2 === 1 ? 'Güz' : 'Bahar';
}

export default function IntibakManagementPage() {
  const toast = useToast();
  const curriculaQuery = useIntibakCurricula();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [onlyUnmapped, setOnlyUnmapped] = useState(false);
  const [yearFilter, setYearFilter] = useState<'all' | '1' | '2' | '3' | '4'>('all');
  const [seasonFilter, setSeasonFilter] = useState<'all' | 'Güz' | 'Bahar'>('all');

  // draft: courseId -> previousCourseId|null (+ display code/name for picker)
  const [draft, setDraft] = useState<Map<number, IntibakCourseDto>>(new Map());

  const coursesQuery = useIntibakCourses(selectedId);
  const saveMut = useSaveIntibak(selectedId);
  const statsQuery = useCourseIntibakStatistics(selectedCourseId);

  const curricula = curriculaQuery.data ?? [];
  const courses = useMemo(() => coursesQuery.data ?? [], [coursesQuery.data]);

  // Müfredat değişince / data gelince draft'ı orijinalden başlat + ilk dersi seç
  useEffect(() => {
    const next = new Map<number, IntibakCourseDto>();
    for (const c of courses) next.set(c.courseId, c);
    setDraft(next);
    setSelectedCourseId(prev =>
      prev !== null && next.has(prev) ? prev : (courses[0]?.courseId ?? null),
    );
  }, [courses]);

  const resetDraft = () => {
    const next = new Map<number, IntibakCourseDto>();
    for (const c of courses) next.set(c.courseId, c);
    setDraft(next);
  };

  const setRow = (courseId: number, prev: { id: number; code: string; name: string } | null) => {
    setDraft(prevMap => {
      const next = new Map(prevMap);
      const base = next.get(courseId);
      if (!base) return prevMap;
      next.set(courseId, {
        ...base,
        previousCourseId: prev?.id ?? null,
        previousCourseCode: prev?.code ?? null,
        previousCourseName: prev?.name ?? null,
      });
      return next;
    });
  };

  // Orijinale göre değişen satırlar
  const changedItems = useMemo<IntibakBulkItem[]>(() => {
    const result: IntibakBulkItem[] = [];
    for (const original of courses) {
      const d = draft.get(original.courseId);
      if (!d) continue;
      if (d.previousCourseId !== original.previousCourseId) {
        result.push({ courseId: original.courseId, previousCourseId: d.previousCourseId });
      }
    }
    return result;
  }, [courses, draft]);

  const visibleCourses = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr-TR');
    return courses.filter(c => {
      const d = draft.get(c.courseId);
      if (onlyUnmapped && (d?.previousCourseId ?? null) !== null) return false;
      if (yearFilter !== 'all' && String(semesterToYear(c.suggestedSemester) ?? '') !== yearFilter) return false;
      if (seasonFilter !== 'all' && semesterToSeason(c.suggestedSemester) !== seasonFilter) return false;
      if (!q) return true;
      return c.courseCode.toLocaleLowerCase('tr-TR').includes(q)
        || c.courseName.toLocaleLowerCase('tr-TR').includes(q);
    });
  }, [courses, draft, search, onlyUnmapped, yearFilter, seasonFilter]);

  const mappedCount = useMemo(
    () => courses.filter(c => (draft.get(c.courseId)?.previousCourseId ?? null) !== null).length,
    [courses, draft],
  );

  const selectedCurriculum = curricula.find(c => c.id === selectedId) ?? null;
  const selectedCourse = selectedCourseId !== null
    ? (draft.get(selectedCourseId) ?? courses.find(c => c.courseId === selectedCourseId) ?? null)
    : null;
  const isLocked = selectedCurriculum?.status === 'Published' || selectedCurriculum?.status === 'Archived';
  const hasChanges = changedItems.length > 0;

  const handleSave = () => {
    if (!hasChanges || selectedId === null) return;
    saveMut.mutate(changedItems, {
      onSuccess: data => toast.showSuccess('İntibak kaydedildi', data.message),
      onError: (err: unknown) =>
        toast.showError('Kaydetme başarısız', err instanceof Error ? err.message : 'Beklenmeyen bir hata oluştu.'),
    });
  };

  return (
    <div className="space-y-4">
      {/* ── TEK KUTU: Müfredat seçici + seçili ders detayı + istatistikler ── */}
      <Card>
        <CardContent className="p-0">
          {/* Müfredat seçici */}
          <div className="flex flex-col gap-3 p-4 md:flex-row md:items-end">
            <div className="flex-1">
              <label className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Müfredat
              </label>
              {curriculaQuery.isLoading ? (
                <Skeleton className="h-10 w-full" />
              ) : (
                <div className="relative">
                  <select
                    value={selectedId ?? ''}
                    onChange={e => setSelectedId(e.target.value ? Number(e.target.value) : null)}
                    className="h-10 w-full appearance-none rounded-md border border-input bg-background pl-3 pr-9 text-sm outline-none transition-colors focus:border-primary/50 focus:ring-2 focus:ring-ring"
                  >
                    <option value="">— Müfredat seçin —</option>
                    {curricula.map(c => (
                      <option key={c.id} value={c.id}>
                        {c.programCode} {c.version} — {c.departmentName} ({c.mappedCount}/{c.courseCount} intibak){c.isActive ? ' • aktif' : ''}
                      </option>
                    ))}
                  </select>
                  <ArrowRight className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 rotate-90 text-muted-foreground" />
                </div>
              )}
            </div>

            {selectedCurriculum && (
              <div className="flex flex-wrap items-center gap-2 md:pb-2">
                <Badge variant="outline" className="font-medium">{selectedCurriculum.departmentName}</Badge>
                {selectedCurriculum.isActive && (
                  <Badge className="border-blue-200 bg-blue-50 text-blue-700">Aktif</Badge>
                )}
                <Badge variant="outline" className={cn('font-medium', STATUS_META[selectedCurriculum.status].className)}>
                  {STATUS_META[selectedCurriculum.status].label}
                </Badge>
              </div>
            )}
          </div>

          {selectedId === null ? (
            <div className="flex flex-col items-center justify-center border-t px-4 py-12 text-center text-muted-foreground">
              <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-2xl bg-muted">
                <Link2 className="h-6 w-6 opacity-40" />
              </div>
              <p className="text-sm font-medium text-foreground">İntibak tanımlamak için bir müfredat seçin</p>
              <p className="mt-1 text-xs">Müfredat seçince dersi, mevcut eşleştirmeleri ve öğrenci istatistiklerini burada görürsünüz.</p>
            </div>
          ) : (
            <>
              <Separator />
              <CourseDetailBody
                course={selectedCourse}
                curriculum={selectedCurriculum}
                stats={statsQuery.data ?? null}
                statsLoading={statsQuery.isLoading}
              />
            </>
          )}
        </CardContent>
      </Card>

      {selectedId !== null && (
        <>
          {/* ── Dersler Tablosu ── */}
          <Card className="overflow-hidden">
            {/* Toolbar */}
            <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/30 px-4 py-3">
              <h2 className="text-sm font-semibold">
                Dersler{' '}
                <span className="font-normal text-muted-foreground">
                  · {mappedCount}/{courses.length} intibak tanımlı
                </span>
              </h2>
              <div className="flex flex-wrap items-center gap-2">
                <FilterSelect
                  label="Sınıf"
                  value={yearFilter}
                  onChange={v => setYearFilter(v as typeof yearFilter)}
                  options={[
                    { value: 'all', label: 'Tümü' },
                    { value: '1', label: '1' },
                    { value: '2', label: '2' },
                    { value: '3', label: '3' },
                    { value: '4', label: '4' },
                  ]}
                />
                <FilterSelect
                  label="Dönem"
                  value={seasonFilter}
                  onChange={v => setSeasonFilter(v as typeof seasonFilter)}
                  options={[
                    { value: 'all', label: 'Tümü' },
                    { value: 'Güz', label: 'Güz' },
                    { value: 'Bahar', label: 'Bahar' },
                  ]}
                />
                <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground">
                  <Switch checked={onlyUnmapped} onCheckedChange={setOnlyUnmapped} />
                  Sadece tanımsız
                </label>
                <div className="relative">
                  <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    placeholder="Ders ara..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    className="h-9 w-48 pl-8"
                  />
                </div>
              </div>
            </div>

            {isLocked && (
              <Alert variant="warning" className="rounded-none border-x-0 border-t-0 py-2">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription className="text-xs">
                  Bu müfredat {selectedCurriculum?.status === 'Published' ? 'yayında' : 'arşivde'} — intibak salt okunur. Düzenlemek için yeni sürüm oluşturun.
                </AlertDescription>
              </Alert>
            )}

            <CardContent className="p-0">
              {coursesQuery.isLoading ? (
                <div className="space-y-2 p-4">
                  {Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
                </div>
              ) : visibleCourses.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-16 text-center text-sm text-muted-foreground">
                  <CircleDashed className="mb-3 h-8 w-8 opacity-30" />
                  {onlyUnmapped ? 'İntibakı tanımsız ders kalmadı.' : 'Eşleşen ders bulunamadı.'}
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow className="hover:bg-transparent">
                      <TableHead className="w-[260px]">Ders</TableHead>
                      <TableHead className="w-[56px] text-center">Z/S</TableHead>
                      <TableHead className="w-[56px] text-center">KRD</TableHead>
                      <TableHead className="w-[56px] text-center">AKTS</TableHead>
                      <TableHead className="w-[56px] text-center">Snf</TableHead>
                      <TableHead className="w-[72px]">Dönem</TableHead>
                      <TableHead>
                        <span className="inline-flex items-center gap-1.5">
                          <ArrowRight className="h-3.5 w-3.5" />
                          Önceki Ders (İntibak)
                        </span>
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {visibleCourses.map(c => {
                      const d = draft.get(c.courseId) ?? c;
                      const isSelected = c.courseId === selectedCourseId;
                      const year = semesterToYear(c.suggestedSemester);
                      const season = semesterToSeason(c.suggestedSemester);
                      return (
                        <TableRow
                          key={c.courseId}
                          onClick={() => setSelectedCourseId(c.courseId)}
                          className={cn(
                            'cursor-pointer',
                            isSelected && 'bg-primary/5 hover:bg-primary/5',
                          )}
                        >
                          <TableCell className="align-top">
                            <div className="font-mono text-xs font-semibold text-primary">{c.courseCode}</div>
                            <div className="mt-0.5 text-sm">{c.courseName}</div>
                          </TableCell>
                          <TableCell className="text-center align-top">
                            <Badge
                              variant="outline"
                              className={cn(
                                'h-5 px-1.5 text-[10px] font-semibold',
                                c.isCompulsory
                                  ? 'border-blue-200 bg-blue-50 text-blue-700'
                                  : 'border-violet-200 bg-violet-50 text-violet-700',
                              )}
                            >
                              {c.isCompulsory ? 'Z' : 'S'}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-center align-top tabular-nums text-muted-foreground">{c.nationalCredit}</TableCell>
                          <TableCell className="text-center align-top tabular-nums text-muted-foreground">{c.ects}</TableCell>
                          <TableCell className="text-center align-top tabular-nums text-muted-foreground">{year ?? '—'}</TableCell>
                          <TableCell className="align-top text-sm text-muted-foreground">{season ?? '—'}</TableCell>
                          <TableCell className="align-top" onClick={e => e.stopPropagation()}>
                            <PreviousCoursePicker
                              current={d.previousCourseId !== null
                                ? { id: d.previousCourseId, code: d.previousCourseCode ?? '', name: d.previousCourseName ?? '' }
                                : null}
                              excludeCourseId={c.courseId}
                              disabled={isLocked}
                              onChange={prev => setRow(c.courseId, prev)}
                            />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </CardContent>

            {/* Aksiyon barı */}
            {!isLocked && (
              <div className="flex items-center justify-between gap-3 border-t bg-muted/30 px-4 py-3">
                <p className="text-xs text-muted-foreground">
                  {hasChanges
                    ? `${changedItems.length} satırda kaydedilmemiş değişiklik var.`
                    : 'Kaydedilmemiş değişiklik yok.'}
                </p>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={resetDraft}
                    disabled={!hasChanges || saveMut.isPending}
                  >
                    <RotateCcw className="h-4 w-4" />
                    Sıfırla
                  </Button>
                  <Button
                    size="sm"
                    onClick={handleSave}
                    disabled={!hasChanges || saveMut.isPending}
                  >
                    {saveMut.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                    Kaydet{hasChanges ? ` (${changedItems.length})` : ''}
                  </Button>
                </div>
              </div>
            )}
          </Card>
        </>
      )}
    </div>
  );
}

/* ── Seçili ders detayı (aynı kutu içinde gövde — kendi kartı yok) ── */
function CourseDetailBody({
  course,
  curriculum,
  stats,
  statsLoading,
}: {
  course: IntibakCourseDto | null;
  curriculum: IntibakCurriculumDto | null;
  stats: CourseIntibakStatisticsDto | null;
  statsLoading: boolean;
}) {
  if (!course || !curriculum) {
    return (
      <div className="px-4 py-8 text-center text-sm text-muted-foreground">
        Ders yükleniyor…
      </div>
    );
  }

  const mapped = course.previousCourseId !== null;

  return (
    <div className="space-y-4 p-4">
      {/* Başlık: ders kimliği */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm font-semibold text-primary">{course.courseCode}</span>
            <Badge
              variant="outline"
              className={cn(
                'h-5 px-1.5 text-[10px] font-semibold',
                course.isCompulsory
                  ? 'border-blue-200 bg-blue-50 text-blue-700'
                  : 'border-violet-200 bg-violet-50 text-violet-700',
              )}
            >
              {course.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
            </Badge>
          </div>
          <h2 className="mt-1 text-base font-semibold leading-tight">{course.courseName}</h2>
          <p className="mt-1.5 text-xs text-muted-foreground">
            {curriculum.facultyName} · {curriculum.departmentName} · {curriculum.programName}
            <span className="mx-1.5 text-muted-foreground/40">|</span>
            {curriculum.programCode} {curriculum.version}{curriculum.name ? ` — ${curriculum.name}` : ''}
          </p>
        </div>
        <div className="flex flex-col items-end gap-1">
          <span className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Mevcut İntibak</span>
          {mapped ? (
            <span className="inline-flex items-center gap-1.5 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-1">
              <Link2 className="h-3.5 w-3.5 text-emerald-600" />
              <span className="font-mono text-xs font-semibold text-emerald-800">{course.previousCourseCode}</span>
              <span className="max-w-[220px] truncate text-xs text-emerald-700/80">{course.previousCourseName}</span>
            </span>
          ) : (
            <Badge variant="outline" className="border-amber-200 bg-amber-50 text-amber-700">
              İntibakı Yapılmamış
            </Badge>
          )}
        </div>
      </div>

      <Separator />

      {/* Öğrenci istatistikleri — sade satırlar, iki simetrik sütun */}
      <div>
        <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <Users className="h-3.5 w-3.5" />
          Öğrenci İstatistikleri
        </div>
        <div className="grid gap-x-10 sm:grid-cols-2">
          <StatLinesGroup
            title="Tüm Dönemler"
            loading={statsLoading}
            total={stats?.totalStudents ?? 0}
            passed={stats?.passedStudents ?? 0}
            failed={stats?.failedStudents ?? 0}
          />
          <StatLinesGroup
            title="Aktif Dönem"
            loading={statsLoading}
            total={stats?.activeStudents ?? 0}
            passed={stats?.activePassedStudents ?? 0}
            failed={stats?.activeFailedStudents ?? 0}
          />
        </div>
      </div>
    </div>
  );
}

function StatLinesGroup({
  title,
  loading,
  total,
  passed,
  failed,
}: {
  title: string;
  loading: boolean;
  total: number;
  passed: number;
  failed: number;
}) {
  return (
    <div className="pt-2">
      <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground/70">{title}</p>
      <StatLine label="Dersi Alan Öğrenci Sayısı" loading={loading}>
        <span className="font-semibold tabular-nums">{total}</span>
      </StatLine>
      <StatLine label="Başarılı / Başarısız" loading={loading}>
        <span className="tabular-nums">
          <span className="font-semibold text-emerald-600">{passed}</span>
          <span className="px-1 text-muted-foreground">/</span>
          <span className="font-semibold text-red-600">{failed}</span>
        </span>
      </StatLine>
    </div>
  );
}

function StatLine({
  label,
  loading,
  children,
}: {
  label: string;
  loading: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-dashed py-1.5 text-sm last:border-0">
      <span className="text-muted-foreground">{label}</span>
      {loading ? <Skeleton className="h-4 w-10" /> : children}
    </div>
  );
}

/* ── Toolbar filtre dropdown'u (etiket + select) ── */
function FilterSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <div className="flex h-9 items-center overflow-hidden rounded-md border border-input bg-background">
      <span className="border-r bg-muted/50 px-2.5 text-xs font-semibold text-muted-foreground">{label}</span>
      <select
        value={value}
        onChange={e => onChange(e.target.value)}
        className="h-full bg-transparent px-2 text-sm outline-none"
      >
        {options.map(o => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
    </div>
  );
}

// ── Önceki ders seçici (arama dropdown'u — useAdminCourses) ──
const MAX_RESULTS = 8;
const DEBOUNCE_MS = 200;

function PreviousCoursePicker({
  current,
  excludeCourseId,
  disabled,
  onChange,
}: {
  current: { id: number; code: string; name: string } | null;
  excludeCourseId: number;
  disabled?: boolean;
  onChange: (course: { id: number; code: string; name: string } | null) => void;
}) {
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const debounced = useDebounce(search, DEBOUNCE_MS);

  const shouldQuery = open && debounced.trim().length >= 2;
  const { data: results = [], isFetching } = useAdminCourses(
    shouldQuery ? { search: debounced.trim() } : undefined,
  );

  const options = useMemo(
    () => results.filter(c => c.id !== excludeCourseId).slice(0, MAX_RESULTS),
    [results, excludeCourseId],
  );

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  if (current) {
    return (
      <div className="flex items-center gap-2">
        <span className="inline-flex items-center gap-2 rounded-md border border-emerald-200 bg-emerald-50 px-2.5 py-1">
          <Link2 className="h-3.5 w-3.5 shrink-0 text-emerald-600" />
          <span className="font-mono text-xs font-semibold text-emerald-800">{current.code}</span>
          <span className="max-w-[200px] truncate text-xs text-emerald-700/80">{current.name}</span>
        </span>
        {!disabled && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 px-2 text-muted-foreground hover:text-destructive"
            onClick={() => onChange(null)}
          >
            <Link2Off className="h-3.5 w-3.5" />
            Kaldır
          </Button>
        )}
      </div>
    );
  }

  if (disabled) {
    return <span className="text-sm text-muted-foreground">—</span>;
  }

  return (
    <div ref={containerRef} className="relative max-w-md">
      <div className="relative">
        <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder="Önceki ders ara (en az 2 karakter)..."
          value={search}
          onChange={e => { setSearch(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          className="h-9 pl-8"
        />
        {isFetching && <Loader2 className="absolute right-2.5 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground" />}
      </div>
      {open && shouldQuery && (
        <div className="absolute z-20 mt-1 w-full overflow-hidden rounded-md border bg-popover shadow-md">
          {options.length === 0 ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">
              {isFetching ? 'Aranıyor...' : 'Sonuç yok.'}
            </div>
          ) : (
            <ul className="max-h-64 overflow-auto py-1">
              {options.map(o => (
                <li key={o.id}>
                  <button
                    type="button"
                    className="flex w-full items-start gap-2 px-3 py-2 text-left text-sm hover:bg-muted"
                    onClick={() => {
                      onChange({ id: o.id, code: o.code, name: o.name });
                      setSearch('');
                      setOpen(false);
                    }}
                  >
                    <span className="font-mono text-xs font-semibold">{o.code}</span>
                    <span className="flex-1 truncate">{o.name}</span>
                    {o.departmentName && <span className="text-xs text-muted-foreground">{o.departmentName}</span>}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {open && !shouldQuery && search.trim().length > 0 && (
        <div className="absolute z-20 mt-1 w-full rounded-md border bg-popover px-3 py-2 text-sm text-muted-foreground shadow-md">
          <X className="mr-1 inline h-3 w-3" /> En az 2 karakter girin.
        </div>
      )}
    </div>
  );
}
