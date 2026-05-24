import { useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, CheckCircle2, Link2, Link2Off, Loader2, Save, Search, X } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useAdminCourses } from '../hooks/useAdminCourseCatalog';
import { useDebounce } from '../hooks/useDebounce';
import { useIntibakCourses, useIntibakCurricula, useSaveIntibak } from '../hooks/useIntibak';
import type { IntibakBulkItem, IntibakCourseDto } from '../api/intibak.api';

type Toast = { type: 'success' | 'error'; message: string };

export default function IntibakManagementPage() {
  const curriculaQuery = useIntibakCurricula();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [onlyUnmapped, setOnlyUnmapped] = useState(false);
  const [toast, setToast] = useState<Toast | null>(null);

  // draft: courseId -> previousCourseId|null (+ display code/name for picker)
  const [draft, setDraft] = useState<Map<number, IntibakCourseDto>>(new Map());

  const coursesQuery = useIntibakCourses(selectedId);
  const saveMut = useSaveIntibak(selectedId);

  const curricula = curriculaQuery.data ?? [];
  const courses = useMemo(() => coursesQuery.data ?? [], [coursesQuery.data]);

  // Müfredat değişince / data gelince draft'ı orijinalden başlat
  useEffect(() => {
    const next = new Map<number, IntibakCourseDto>();
    for (const c of courses) next.set(c.courseId, c);
    setDraft(next);
  }, [courses]);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), toast.type === 'error' ? 7000 : 4000);
    return () => clearTimeout(t);
  }, [toast]);

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
      if (!q) return true;
      return c.courseCode.toLocaleLowerCase('tr-TR').includes(q)
        || c.courseName.toLocaleLowerCase('tr-TR').includes(q);
    });
  }, [courses, draft, search, onlyUnmapped]);

  const mappedCount = useMemo(
    () => courses.filter(c => (draft.get(c.courseId)?.previousCourseId ?? null) !== null).length,
    [courses, draft],
  );

  const selectedCurriculum = curricula.find(c => c.id === selectedId) ?? null;
  const isLocked = selectedCurriculum?.status === 'Published' || selectedCurriculum?.status === 'Archived';

  const handleSave = () => {
    if (changedItems.length === 0 || selectedId === null) return;
    saveMut.mutate(changedItems, {
      onSuccess: data => setToast({ type: 'success', message: data.message }),
      onError: (err: unknown) =>
        setToast({ type: 'error', message: err instanceof Error ? err.message : 'Kaydetme başarısız.' }),
    });
  };

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Müfredat Ders İntibak İşlemleri</h1>
        <p className="text-sm text-muted-foreground">
          Bir müfredattaki derslerin eski müfredattaki karşılığını (önceki ders / intibak) toplu tanımlayın.
        </p>
      </div>

      {toast && (
        <Alert variant={toast.type === 'error' ? 'destructive' : 'default'} className="py-2">
          {toast.type === 'error' ? <AlertCircle className="h-4 w-4" /> : <CheckCircle2 className="h-4 w-4" />}
          <AlertDescription className="text-sm">{toast.message}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(420px,1fr)_auto] lg:items-end">
        <Card>
          <CardContent className="p-3">
            <label className="mb-1 block text-xs font-medium text-muted-foreground">Müfredat</label>
            {curriculaQuery.isLoading ? (
              <Skeleton className="h-10 w-full" />
            ) : (
              <select
                value={selectedId ?? ''}
                onChange={e => setSelectedId(e.target.value ? Number(e.target.value) : null)}
                className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="">— Müfredat seçin —</option>
                {curricula.map(c => (
                  <option key={c.id} value={c.id}>
                    {c.programCode} {c.version} — {c.departmentName} ({c.mappedCount}/{c.courseCount} intibak){c.isActive ? ' • aktif' : ''}
                  </option>
                ))}
              </select>
            )}
          </CardContent>
        </Card>

        {selectedId !== null && (
          <Button onClick={handleSave} disabled={changedItems.length === 0 || saveMut.isPending || isLocked} className="h-10">
            {saveMut.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            Kaydet{changedItems.length > 0 ? ` (${changedItems.length})` : ''}
          </Button>
        )}
      </div>

      {selectedId === null ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-16 text-center text-muted-foreground">
            <Link2 className="mb-3 h-9 w-9 opacity-40" />
            <p className="text-sm">İntibak tanımlamak için bir müfredat seçin.</p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader className="pb-2">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <CardTitle className="text-base">
                Dersler{' '}
                <span className="text-sm font-normal text-muted-foreground">
                  ({mappedCount}/{courses.length} intibak tanımlı)
                </span>
              </CardTitle>
              <div className="flex flex-wrap items-center gap-3">
                <label className="flex items-center gap-2 text-sm">
                  <Switch checked={onlyUnmapped} onCheckedChange={setOnlyUnmapped} />
                  Sadece intibakı tanımsız
                </label>
                <div className="relative">
                  <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    placeholder="Ders ara..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    className="h-9 w-56 pl-8"
                  />
                </div>
              </div>
            </div>
            {isLocked && (
              <Alert variant="warning" className="mt-2 py-2">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription className="text-xs">
                  Bu müfredat {selectedCurriculum?.status === 'Published' ? 'yayında' : 'arşivde'} — intibak salt okunur. Yeni sürüm oluşturun.
                </AlertDescription>
              </Alert>
            )}
          </CardHeader>
          <CardContent className="p-0">
            {coursesQuery.isLoading ? (
              <div className="p-4"><Skeleton className="h-[400px] w-full" /></div>
            ) : visibleCourses.length === 0 ? (
              <div className="py-16 text-center text-sm text-muted-foreground">
                {onlyUnmapped ? 'İntibakı tanımsız ders kalmadı.' : 'Eşleşen ders yok.'}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[70px] text-center">Yarıyıl</TableHead>
                    <TableHead className="w-[280px]">Ders</TableHead>
                    <TableHead>Önceki Ders (İntibak)</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {visibleCourses.map(c => {
                    const d = draft.get(c.courseId) ?? c;
                    return (
                      <TableRow key={c.courseId}>
                        <TableCell className="text-center tabular-nums text-muted-foreground">{c.suggestedSemester}</TableCell>
                        <TableCell>
                          <div className="font-mono text-xs font-semibold">{c.courseCode}</div>
                          <div className="text-sm">{c.courseName}</div>
                        </TableCell>
                        <TableCell>
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
        </Card>
      )}
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
        <Badge variant="secondary" className="gap-1 font-normal">
          <Link2 className="h-3 w-3" />
          <span className="font-mono text-xs">{current.code}</span>
          <span className="max-w-[180px] truncate text-muted-foreground">{current.name}</span>
        </Badge>
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
