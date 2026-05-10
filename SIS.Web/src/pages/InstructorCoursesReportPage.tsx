import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { AlertCircle, BookOpen, ChevronDown, Clock, FileText, Loader2, Search, UserCheck, Users } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import DepartmentOperationsLayout from '@/components/academic-operations/DepartmentOperationsLayout';
import { resolveInitialDepartmentId } from '@/components/academic-operations/utils';
import { cn } from '@/lib/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useDepartmentInstructors, useInstructorWorkload } from '../hooks/useCourseOpening';
import { useActiveTerm } from '../hooks/useActiveTerm';
import type { AssignedSectionDto, InstructorOptionDto } from '../types/courseOpening';

type SortKey = 'name' | 'hours';
type SortDir = 'asc' | 'desc';

export default function InstructorCoursesReportPage() {
  const contextQuery = useAcademicOperationsContext();
  const activeTermQuery = useActiveTerm();
  const activeTerm = activeTermQuery.data;
  const [departmentId, setDepartmentId] = useState<number | null>(null);
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [expandedId, setExpandedId] = useState<number | null>(null);

  useEffect(() => {
    if (departmentId !== null && contextQuery.data) {
      const exists = contextQuery.data.accessibleDepartments.some(department => department.departmentId === departmentId);
      // eslint-disable-next-line react-hooks/set-state-in-effect
      if (!exists) setDepartmentId(null);
    }
  }, [contextQuery.data, departmentId]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setExpandedId(null);
  }, [departmentId, activeTerm?.id]);

  const termId = activeTerm?.id ?? null;
  const instructorsQuery = useDepartmentInstructors(departmentId, termId, true);
  const instructors = instructorsQuery.data ?? [];

  const layoutDepartmentId = resolveInitialDepartmentId(contextQuery.data ?? null);
  const departmentOptions = contextQuery.data?.accessibleDepartments ?? [];
  const filtered = useMemo(() => {
    const query = search.trim().toLocaleLowerCase('tr-TR');
    if (!query) return instructors;

    return instructors.filter(instructor =>
      instructor.fullName.toLocaleLowerCase('tr-TR').includes(query)
      || instructor.departmentName.toLocaleLowerCase('tr-TR').includes(query)
      || instructor.departmentCode.toLocaleLowerCase('tr-TR').includes(query)
    );
  }, [instructors, search]);

  const sorted = useMemo(() => {
    return [...filtered].sort((a, b) => {
      const compare = sortKey === 'name'
        ? a.fullName.localeCompare(b.fullName, 'tr')
        : a.assignedWeeklyHours - b.assignedWeeklyHours;
      return sortDir === 'asc' ? compare : -compare;
    });
  }, [filtered, sortDir, sortKey]);

  const stats = useMemo(() => {
    const totalInstructors = instructors.length;
    const totalHours = instructors.reduce((sum, instructor) => sum + instructor.assignedWeeklyHours, 0);
    const overloaded = instructors.filter(
      instructor => instructor.maxWeeklyHours > 0 && instructor.assignedWeeklyHours > instructor.maxWeeklyHours,
    ).length;
    const unassigned = instructors.filter(instructor => instructor.assignedWeeklyHours === 0).length;

    return {
      totalInstructors,
      averageHours: totalInstructors > 0 ? Math.round(totalHours / totalInstructors) : 0,
      overloaded,
      unassigned,
    };
  }, [instructors]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) {
      setSortDir(current => (current === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  }

  function exportPdf() {
    if (sorted.length === 0) return;

    const selectedDepartment = departmentId === null
      ? 'Tümü'
      : departmentOptions.find(department => department.departmentId === departmentId);
    const departmentLabel = typeof selectedDepartment === 'string'
      ? selectedDepartment
      : selectedDepartment
        ? `${selectedDepartment.departmentCode} - ${selectedDepartment.departmentName}`
        : 'Tümü';
    const generatedAt = new Date().toLocaleString('tr-TR');
    const rows = sorted.map(instructor => `
      <tr>
        <td>${escapeHtml(instructor.fullName)}</td>
        <td>${escapeHtml(instructor.departmentName || '-')}</td>
        <td class="number">${instructor.assignedWeeklyHours}</td>
        <td class="number">${instructor.maxWeeklyHours}</td>
        <td class="number">${instructor.instructorId}</td>
      </tr>
    `).join('');

    const printWindow = window.open('', '_blank', 'width=1120,height=820');
    if (!printWindow) return;

    printWindow.document.open();
    printWindow.document.write(`
      <!doctype html>
      <html lang="tr">
        <head>
          <meta charset="utf-8" />
          <title>Ders Veren Öğretim Elemanları</title>
          <style>
            @page { size: A4 landscape; margin: 14mm; }
            * { box-sizing: border-box; }
            body { margin: 0; color: #111827; font-family: Arial, Helvetica, sans-serif; font-size: 11px; }
            h1 { margin: 0 0 8px; font-size: 20px; }
            .meta { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; margin: 12px 0 16px; }
            .meta div { border: 1px solid #d1d5db; border-radius: 6px; padding: 8px; }
            .label { display: block; margin-bottom: 3px; color: #6b7280; font-size: 9px; text-transform: uppercase; }
            table { width: 100%; border-collapse: collapse; }
            th, td { border: 1px solid #d1d5db; padding: 6px 7px; text-align: left; vertical-align: top; }
            th { background: #f3f4f6; font-size: 10px; }
            .number { text-align: right; font-variant-numeric: tabular-nums; }
            .footer { margin-top: 12px; color: #6b7280; font-size: 9px; }
          </style>
        </head>
        <body>
          <h1>Ders Veren Öğretim Elemanları</h1>
          <div class="meta">
            <div><span class="label">Bölüm</span>${escapeHtml(departmentLabel)}</div>
            <div><span class="label">Dönem</span>${escapeHtml(activeTerm?.name ?? '-')}</div>
            <div><span class="label">Kayıt</span>${sorted.length}</div>
          </div>
          <table>
            <thead>
              <tr>
                <th>Öğretim Elemanı</th>
                <th>Bölüm</th>
                <th>Atanan Saat</th>
                <th>Maks Saat</th>
                <th>Personel ID</th>
              </tr>
            </thead>
            <tbody>${rows}</tbody>
          </table>
          <div class="footer">Oluşturulma zamanı: ${escapeHtml(generatedAt)}</div>
          <script>
            window.addEventListener('load', () => {
              window.focus();
              setTimeout(() => window.print(), 150);
            });
          </script>
        </body>
      </html>
    `);
    printWindow.document.close();
  }

  return (
    <DepartmentOperationsLayout
      icon={Users}
      title="Ders Veren Öğretim Elemanları"
      description="Bölümde aktif dönemde ders yükü bulunan öğretim elemanlarını ve atandıkları şubeleri inceleyin."
      context={contextQuery.data ?? null}
      departmentId={layoutDepartmentId}
      onDepartmentChange={() => {}}
      termName={activeTerm?.name}
      isContextLoading={contextQuery.isLoading}
      contextError={contextQuery.isError ? getAcademicOperationsErrorMessage(contextQuery.error, 'Kapsam bilgileri alınamadı.') : null}
      hideHeader
      showDepartmentSelect={false}
    >
      <div className="space-y-4">
        {instructorsQuery.isError && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {getAcademicOperationsErrorMessage(instructorsQuery.error, 'Öğretim elemanı listesi yüklenemedi.')}
            </AlertDescription>
          </Alert>
        )}

        {!activeTermQuery.isLoading && termId === null && (
          <Alert variant="warning">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Aktif akademik dönem bulunamadığı için dönem bazlı ders yükü detayı gösterilemeyebilir.</AlertDescription>
          </Alert>
        )}

        <Card>
          <CardContent className="p-3">
            <div className="grid gap-2 lg:grid-cols-[minmax(220px,320px)_minmax(260px,1fr)_auto]">
              <select
                value={departmentId ?? 'all'}
                onChange={event => {
                  const value = event.target.value;
                  setDepartmentId(value === 'all' ? null : Number(value));
                }}
                className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="all">Tümü</option>
                {departmentOptions.map(department => (
                  <option key={department.departmentId} value={department.departmentId}>
                    {department.departmentCode} - {department.departmentName}
                  </option>
                ))}
              </select>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  placeholder="Öğretim elemanı ara..."
                  value={search}
                  onChange={event => setSearch(event.target.value)}
                  className="h-10 w-full pl-8"
                />
              </div>
              <Button
                type="button"
                variant="outline"
                className="h-10 justify-center lg:w-[112px]"
                onClick={exportPdf}
                disabled={sorted.length === 0}
              >
                <FileText className="h-4 w-4" />
                PDF
              </Button>
            </div>
          </CardContent>
        </Card>

        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <StatCard icon={Users} label="Öğretim Elemanı" value={stats.totalInstructors} loading={instructorsQuery.isLoading} />
          <StatCard icon={Clock} label="Ortalama Saat" value={stats.averageHours} loading={instructorsQuery.isLoading} />
          <StatCard icon={UserCheck} label="Dersi Olmayan" value={stats.unassigned} loading={instructorsQuery.isLoading} tone={stats.unassigned > 0 ? 'warning' : 'default'} />
          <StatCard icon={BookOpen} label="Kapasite Aşımı" value={stats.overloaded} loading={instructorsQuery.isLoading} tone={stats.overloaded > 0 ? 'danger' : 'default'} />
        </div>

        {instructorsQuery.isLoading ? (
          <Skeleton className="h-[430px] rounded-lg" />
        ) : sorted.length === 0 ? (
          <Card>
            <CardContent className="flex flex-col items-center justify-center py-16 text-center">
              <Users className="mb-3 h-9 w-9 text-muted-foreground/45" />
              <p className="text-sm font-medium">
                {search ? 'Eşleşen öğretim elemanı bulunamadı.' : 'Seçili kapsamda öğretim elemanı bulunmuyor.'}
              </p>
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Ders Yükü</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[48px]" />
                    <SortableHead field="name" sortKey={sortKey} sortDir={sortDir} onSort={toggleSort}>
                      Öğretim Elemanı
                    </SortableHead>
                    <TableHead>Bölüm</TableHead>
                    <SortableHead field="hours" sortKey={sortKey} sortDir={sortDir} onSort={toggleSort}>
                      Haftalık Yük
                    </SortableHead>
                    <TableHead className="w-[140px] text-right">Kalan Kapasite</TableHead>
                    <TableHead className="w-[150px]">Detay</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sorted.map(instructor => (
                    <InstructorRow
                      key={instructor.instructorId}
                      instructor={instructor}
                      departmentId={departmentId}
                      termId={termId}
                      isExpanded={expandedId === instructor.instructorId}
                      onToggle={() => setExpandedId(current => (current === instructor.instructorId ? null : instructor.instructorId))}
                    />
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )}
      </div>
    </DepartmentOperationsLayout>
  );
}

function escapeHtml(value: string | number | null | undefined) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function StatCard({
  icon: Icon,
  label,
  value,
  loading,
  tone = 'default',
}: {
  icon: LucideIcon;
  label: string;
  value: number;
  loading: boolean;
  tone?: 'default' | 'warning' | 'danger';
}) {
  const toneClass = tone === 'warning'
    ? 'border-amber-200 bg-amber-50/40'
    : tone === 'danger'
      ? 'border-red-200 bg-red-50/40'
      : undefined;

  return (
    <Card className={toneClass}>
      <CardContent className="flex items-center gap-3 p-4">
        <div className="rounded-md border bg-background p-2 text-muted-foreground">
          <Icon className="h-4 w-4" />
        </div>
        <div>
          <p className="text-xs font-medium text-muted-foreground">{label}</p>
          {loading ? <Skeleton className="mt-2 h-6 w-12" /> : <p className="mt-1 text-2xl font-semibold">{value}</p>}
        </div>
      </CardContent>
    </Card>
  );
}

function SortableHead({
  field,
  children,
  sortKey,
  sortDir,
  onSort,
}: {
  field: SortKey;
  children: ReactNode;
  sortKey: SortKey;
  sortDir: SortDir;
  onSort: (key: SortKey) => void;
}) {
  const active = sortKey === field;
  return (
    <TableHead
      role="columnheader"
      tabIndex={0}
      aria-sort={active ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
      className="cursor-pointer select-none"
      onClick={() => onSort(field)}
      onKeyDown={event => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSort(field);
        }
      }}
    >
      <span className="inline-flex items-center gap-1">
        {children}
        {active && <span className="text-[10px] text-primary">{sortDir === 'asc' ? '▲' : '▼'}</span>}
      </span>
    </TableHead>
  );
}

function InstructorRow({
  instructor,
  departmentId,
  termId,
  isExpanded,
  onToggle,
}: {
  instructor: InstructorOptionDto;
  departmentId: number | null;
  termId: number | null;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  const workloadQuery = useInstructorWorkload(
    isExpanded && termId !== null ? instructor.instructorId : null,
    termId,
    departmentId,
  );
  const workload = workloadQuery.data;
  const sections = workload?.assignedSections ?? [];
  const assignedHours = workload?.assignedWeeklyHours ?? instructor.assignedWeeklyHours;
  const maxHours = workload?.maxWeeklyHours ?? instructor.maxWeeklyHours;
  const remaining = Math.max(maxHours - assignedHours, 0);

  return (
    <>
      <TableRow className={cn('cursor-pointer', isExpanded ? 'bg-muted/40' : 'hover:bg-muted/30')} onClick={onToggle}>
        <TableCell>
          <ChevronDown className={cn('h-4 w-4 text-muted-foreground transition-transform', isExpanded && 'rotate-180 text-primary')} />
        </TableCell>
        <TableCell>
          <div className="font-medium">{instructor.fullName}</div>
          {workload?.title && <div className="mt-1 text-xs text-muted-foreground">{workload.title}</div>}
        </TableCell>
        <TableCell className="text-sm text-muted-foreground">{instructor.departmentName || '-'}</TableCell>
        <TableCell>
          <WorkloadBar assigned={assignedHours} max={maxHours} />
        </TableCell>
        <TableCell className="text-right tabular-nums">{remaining}</TableCell>
        <TableCell>
          {isExpanded && workloadQuery.isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          ) : workload ? (
            <Badge variant={sections.length > 0 ? 'info' : 'secondary'}>{sections.length} şube</Badge>
          ) : (
            <Badge variant="outline">Aç</Badge>
          )}
        </TableCell>
      </TableRow>

      {isExpanded && (
        <TableRow className="bg-muted/20 hover:bg-muted/20">
          <TableCell colSpan={6} className="p-0">
            <div className="border-t px-6 py-4">
              {termId === null ? (
                <p className="py-4 text-center text-sm text-muted-foreground">Ders yükü detayı için aktif dönem gerekli.</p>
              ) : workloadQuery.isLoading ? (
                <div className="flex items-center justify-center gap-2 py-5 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Yük bilgisi yükleniyor...
                </div>
              ) : workloadQuery.isError ? (
                <p className="py-4 text-center text-sm text-destructive">
                  {getAcademicOperationsErrorMessage(workloadQuery.error, 'Yük bilgisi alınamadı.')}
                </p>
              ) : sections.length === 0 ? (
                <div className="flex flex-col items-center py-6 text-muted-foreground">
                  <BookOpen className="mb-2 h-8 w-8" />
                  <p className="text-sm">Bu dönemde atanmış şube yok.</p>
                </div>
              ) : (
                <SectionDetailTable sections={sections} />
              )}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function WorkloadBar({ assigned, max }: { assigned: number; max: number }) {
  const percent = max > 0 ? Math.round((assigned / max) * 100) : 0;
  const tone = percent > 100 ? 'bg-red-500' : percent >= 80 ? 'bg-amber-500' : 'bg-emerald-500';

  return (
    <div className="flex min-w-[150px] items-center gap-2">
      <div className="h-2 w-20 overflow-hidden rounded-full bg-muted">
        <div className={cn('h-full rounded-full', tone)} style={{ width: `${Math.min(percent, 100)}%` }} />
      </div>
      <span className="text-sm tabular-nums">{assigned}/{max}</span>
    </div>
  );
}

function SectionDetailTable({ sections }: { sections: AssignedSectionDto[] }) {
  return (
    <div className="max-h-[300px] overflow-auto rounded-md border bg-background">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Ders Kodu</TableHead>
            <TableHead>Bölüm</TableHead>
            <TableHead>Ders Adı</TableHead>
            <TableHead className="w-[90px] text-center">Şube</TableHead>
            <TableHead className="w-[110px] text-right">Saat</TableHead>
            <TableHead className="w-[120px] text-right">Kayıt</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sections.map(section => (
            <TableRow key={section.sectionId}>
              <TableCell className="font-mono text-xs font-semibold">{section.courseCode}</TableCell>
              <TableCell className="text-sm text-muted-foreground">
                {section.departmentCode ? `${section.departmentCode} - ${section.departmentName ?? ''}` : section.departmentName ?? '-'}
              </TableCell>
              <TableCell>{section.courseName}</TableCell>
              <TableCell className="text-center">
                <Badge variant="outline">{section.sectionNumber}</Badge>
              </TableCell>
              <TableCell className="text-right tabular-nums">{section.weeklyHours}</TableCell>
              <TableCell className="text-right tabular-nums">{section.enrolledCount}/{section.capacity}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
