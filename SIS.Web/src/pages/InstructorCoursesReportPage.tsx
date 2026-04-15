import { useMemo, useState } from 'react';
import {
  BookOpen,
  ChevronDown,
  Clock,
  Download,
  Loader2,
  Search,
  UserCheck,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
import { useDepartmentInstructors, useInstructorWorkload } from '../hooks/useCourseOpening';
import { useActiveTerm } from '../hooks/useActiveTerm';
import type { InstructorOptionDto, AssignedSectionDto } from '../types/courseOpening';

type SortKey = 'name' | 'hours' | 'courses' | 'sections';
type SortDir = 'asc' | 'desc';

/* ─── Stat Card ─── */

function StatCard({ icon: Icon, label, value, sub, color = 'blue' }: {
  icon: React.ElementType;
  label: string;
  value: string | number;
  sub?: string;
  color?: 'blue' | 'emerald' | 'amber' | 'red' | 'gray';
}) {
  const palette: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-600 border-blue-100',
    emerald: 'bg-emerald-50 text-emerald-600 border-emerald-100',
    amber: 'bg-amber-50 text-amber-600 border-amber-100',
    red: 'bg-red-50 text-red-600 border-red-100',
    gray: 'bg-gray-50 text-gray-500 border-gray-100',
  };
  return (
    <div className={cn('flex items-center gap-3 rounded-xl border px-4 py-3', palette[color])}>
      <div className="shrink-0 rounded-lg bg-white/70 p-2 shadow-sm">
        <Icon className="h-4 w-4" />
      </div>
      <div className="min-w-0">
        <p className="text-[11px] font-medium opacity-75 leading-none mb-0.5">{label}</p>
        <p className="text-lg font-bold leading-none">{value}</p>
        {sub && <p className="text-[10px] opacity-60 mt-0.5">{sub}</p>}
      </div>
    </div>
  );
}

/* ─── Sort Header ─── */

function SortHead({ field, children, className, sortKey, sortDir, onSort }: {
  field: SortKey;
  children: React.ReactNode;
  className?: string;
  sortKey: SortKey;
  sortDir: SortDir;
  onSort: (key: SortKey) => void;
}) {
  const isActive = sortKey === field;
  return (
    <TableHead
      className={cn(
        'cursor-pointer select-none transition-colors text-xs font-semibold text-gray-600',
        isActive ? 'bg-gray-100/80 text-gray-900' : 'hover:bg-gray-50',
        className,
      )}
      onClick={() => onSort(field)}
      role="columnheader"
      aria-sort={isActive ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
      tabIndex={0}
      onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSort(field); } }}
    >
      <span className="inline-flex items-center gap-1">
        {children}
        {isActive && <span className="text-[10px] text-blue-600" aria-hidden="true">{sortDir === 'asc' ? '▲' : '▼'}</span>}
      </span>
    </TableHead>
  );
}

/* ─── Workload Bar ─── */

function WorkloadBar({ assigned, max }: { assigned: number; max: number }) {
  const pct = max > 0 ? Math.round((assigned / max) * 100) : 0;
  const color = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';
  const textColor = pct >= 100 ? 'text-red-700' : pct >= 80 ? 'text-amber-700' : 'text-gray-700';

  return (
    <div className="flex items-center gap-2.5">
      <div className="w-16 h-2 rounded-full bg-gray-200 overflow-hidden">
        <div className={cn('h-full rounded-full transition-all', color)} style={{ width: `${Math.min(100, pct)}%` }} />
      </div>
      <span className={cn('text-xs font-semibold tabular-nums whitespace-nowrap', textColor)}>
        {assigned}/{max}
      </span>
    </div>
  );
}

/* ─── Main Page ─── */

export default function InstructorCoursesReportPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | ''>('');
  const [selectedDeptId, setSelectedDeptId] = useState<number | ''>('');
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(
    selectedFacultyId !== '' ? selectedFacultyId : undefined,
  );
  const { data: activeTerm } = useActiveTerm();
  const termId = activeTerm?.id ?? null;

  const deptId = selectedDeptId !== '' ? selectedDeptId : null;
  const { data: instructors = [], isLoading } = useDepartmentInstructors(deptId);

  const selectedDeptName = useMemo(() => {
    if (selectedDeptId === '') return '';
    return allDepartments.find(d => d.id === selectedDeptId)?.name ?? '';
  }, [allDepartments, selectedDeptId]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? instructors.filter(i => i.fullName.toLowerCase().includes(q)) : instructors;
  }, [instructors, search]);

  const sorted = useMemo(() => {
    return [...filtered].sort((a, b) => {
      const cmp = a.fullName.localeCompare(b.fullName, 'tr');
      return sortDir === 'asc' ? cmp : -cmp;
    });
  }, [filtered, sortDir]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortKey(key); setSortDir('asc'); }
  }

  /* Stats */
  const totalInstructors = instructors.length;
  const avgHours = totalInstructors > 0
    ? Math.round(instructors.reduce((s, i) => s + i.assignedWeeklyHours, 0) / totalInstructors)
    : 0;
  const overloaded = instructors.filter(i => i.maxWeeklyHours > 0 && i.assignedWeeklyHours > i.maxWeeklyHours).length;
  const unassigned = instructors.filter(i => i.assignedWeeklyHours === 0).length;

  /* CSV */
  function exportCsv() {
    if (!sorted.length) return;
    const header = ['Ad Soyad', 'Bölüm', 'Atanan Saat', 'Maks Saat', 'Personel ID'];
    const rows = sorted.map(i => [
      i.fullName, selectedDeptName,
      String(i.assignedWeeklyHours), String(i.maxWeeklyHours),
      String(i.instructorId),
    ]);
    const csv = [header, ...rows].map(r => r.map(c => `"${c.replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `ogretim-uyeleri-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="space-y-5">
      {/* ── Filter Bar ── */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
          <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">Fakülte</span>
          <select
            className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[180px]"
            value={selectedFacultyId}
            onChange={e => {
              setSelectedFacultyId(e.target.value !== '' ? Number(e.target.value) : '');
              setSelectedDeptId('');
              setExpandedId(null);
            }}
          >
            <option value="">Tümü</option>
            {faculties.map(f => (
              <option key={f.id} value={f.id}>{f.name}</option>
            ))}
          </select>
        </div>

        <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
          <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">Bölüm</span>
          <select
            className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[200px]"
            value={selectedDeptId}
            onChange={e => {
              setSelectedDeptId(e.target.value !== '' ? Number(e.target.value) : '');
              setExpandedId(null);
            }}
            disabled={allDepartments.length === 0}
          >
            <option value="">Bölüm Seçin</option>
            {allDepartments.map(d => (
              <option key={d.id} value={d.id}>{d.name}</option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-1.5 ml-auto">
          <div className="relative">
            <Search className="h-3.5 w-3.5 text-gray-400 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            <Input
              placeholder="Öğretim üyesi ara..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-8 h-8 w-64 text-xs rounded-lg shadow-sm"
            />
          </div>
          {sorted.length > 0 && (
            <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs rounded-lg" onClick={exportCsv}>
              <Download className="h-3 w-3" />
              CSV
            </Button>
          )}
        </div>
      </div>

      {/* ── Stat Cards ── */}
      {deptId && !isLoading && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <StatCard icon={Users} label="Öğretim Üyesi" value={totalInstructors} color="blue" />
          <StatCard
            icon={Clock}
            label="Ort. Haftalık Yük"
            value={`${avgHours} saat`}
            color={avgHours > 0 ? 'emerald' : 'gray'}
          />
          <StatCard
            icon={UserCheck}
            label="Dersi Olmayan"
            value={unassigned}
            sub={totalInstructors > 0 ? `/ ${totalInstructors} kişi` : undefined}
            color={unassigned > 0 ? 'amber' : 'emerald'}
          />
          <StatCard
            icon={BookOpen}
            label="Kapasite Aşımı"
            value={overloaded}
            color={overloaded > 0 ? 'red' : 'emerald'}
          />
        </div>
      )}

      {/* ── No Department Selected ── */}
      {!deptId && !isLoading && (
        <div className="rounded-xl border border-dashed border-gray-300 py-20 text-center">
          <Users className="h-10 w-10 text-gray-300 mx-auto mb-3" />
          <p className="text-sm text-gray-500">Listeyi görmek için bir bölüm seçin.</p>
        </div>
      )}

      {/* ── Loading ── */}
      {isLoading && (
        <div className="space-y-3">
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-xl" />)}
          </div>
          <Skeleton className="h-96 rounded-xl" />
        </div>
      )}

      {/* ── Table ── */}
      {deptId && !isLoading && (
        <>
          {sorted.length === 0 ? (
            <div className="rounded-xl border border-dashed border-gray-300 py-16 text-center">
              <Search className="h-8 w-8 text-gray-300 mx-auto mb-3" />
              <p className="text-sm text-gray-500">
                {search ? 'Eşleşen öğretim üyesi bulunamadı.' : 'Bu bölümde öğretim üyesi bulunmuyor.'}
              </p>
            </div>
          ) : (
            <Card className="overflow-hidden rounded-xl border shadow-sm">
              <div className="overflow-x-auto">
                <Table className="table-fixed">
                  <colgroup>
                    <col className="w-10" />
                    <col className="w-[10%]" />
                    <col className="w-[28%]" />
                    <col className="w-[20%]" />
                    <col className="w-[18%]" />
                    <col className="w-[10%]" />
                    <col className="w-[10%]" />
                  </colgroup>
                  <TableHeader>
                    <TableRow className="bg-gray-50/80 border-b">
                      <TableHead className="w-10" />
                      <TableHead className="text-xs font-semibold text-gray-600">Unvan</TableHead>
                      <SortHead field="name" sortKey={sortKey} sortDir={sortDir} onSort={toggleSort}>Ad Soyad</SortHead>
                      <TableHead className="text-xs font-semibold text-gray-600">Bölüm</TableHead>
                      <SortHead field="hours" sortKey={sortKey} sortDir={sortDir} onSort={toggleSort}>Haftalık Yük</SortHead>
                      <SortHead field="courses" sortKey={sortKey} sortDir={sortDir} onSort={toggleSort} className="text-center">Ders</SortHead>
                      <SortHead field="sections" sortKey={sortKey} sortDir={sortDir} onSort={toggleSort} className="text-center">Şube</SortHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sorted.map(instructor => (
                      <InstructorRow
                        key={instructor.instructorId}
                        instructor={instructor}
                        termId={termId ?? 0}
                        departmentName={selectedDeptName}
                        isExpanded={expandedId === instructor.instructorId}
                        onToggle={() => setExpandedId(c => c === instructor.instructorId ? null : instructor.instructorId)}
                      />
                    ))}
                  </TableBody>
                </Table>
              </div>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

/* ─── Instructor Row ─── */

function InstructorRow({ instructor, termId, departmentName, isExpanded, onToggle }: {
  instructor: InstructorOptionDto;
  termId: number;
  departmentName: string;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  const workloadQuery = useInstructorWorkload(
    isExpanded ? instructor.instructorId : null,
    termId,
  );

  const workload = workloadQuery.data;
  const sections = workload?.assignedSections ?? [];
  const assignedHours = workload?.assignedWeeklyHours ?? instructor.assignedWeeklyHours;
  const maxHours = workload?.maxWeeklyHours ?? instructor.maxWeeklyHours;
  const courseCount = workload ? new Set(sections.map(s => s.courseCode)).size : null;

  return (
    <>
      <TableRow
        className={cn(
          'cursor-pointer transition-colors group',
          isExpanded ? 'bg-blue-50/40' : 'hover:bg-gray-50/80',
        )}
        onClick={onToggle}
      >
        <TableCell className="px-2 py-3">
          <ChevronDown className={cn(
            'h-3.5 w-3.5 text-gray-400 transition-transform group-hover:text-gray-600',
            isExpanded && 'rotate-180 text-blue-600',
          )} />
        </TableCell>

        {/* Title */}
        <TableCell className="py-3">
          {workload?.title ? (
            <Badge variant="outline" className="text-[10px] px-1.5 py-0 rounded-md font-medium">{workload.title}</Badge>
          ) : isExpanded && workloadQuery.isLoading ? (
            <Loader2 className="h-3 w-3 animate-spin text-gray-400" />
          ) : (
            <span className="text-xs text-gray-400">—</span>
          )}
        </TableCell>

        {/* Name */}
        <TableCell className="py-3 overflow-hidden">
          <span className="text-sm font-medium text-gray-800 truncate block">{instructor.fullName}</span>
        </TableCell>

        {/* Department */}
        <TableCell className="py-3 overflow-hidden">
          <span className="text-xs text-gray-500 truncate block">{departmentName}</span>
        </TableCell>

        {/* Weekly Hours */}
        <TableCell className="py-3">
          <WorkloadBar assigned={assignedHours} max={maxHours} />
        </TableCell>

        {/* Course Count */}
        <TableCell className="py-3 text-center">
          {courseCount !== null ? (
            <span className="text-sm font-semibold text-gray-700">{courseCount}</span>
          ) : (
            <span className="text-xs text-gray-400">—</span>
          )}
        </TableCell>

        {/* Section Count */}
        <TableCell className="py-3 text-center">
          {workload ? (
            <span className="text-sm font-semibold text-gray-700">{sections.length}</span>
          ) : (
            <span className="text-xs text-gray-400">—</span>
          )}
        </TableCell>
      </TableRow>

      {/* ── Expanded Detail ── */}
      {isExpanded && (
        <TableRow className="bg-gray-50/60 hover:bg-gray-50/60">
          <TableCell colSpan={7} className="p-0">
            <div className="px-6 py-4 border-t border-gray-100">
              {workloadQuery.isLoading ? (
                <div className="flex items-center gap-2 py-4 justify-center text-gray-400">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  <span className="text-sm">Yük bilgisi yükleniyor...</span>
                </div>
              ) : workloadQuery.isError ? (
                <p className="text-sm text-red-600 py-4 text-center">Yük bilgisi alınamadı.</p>
              ) : sections.length === 0 ? (
                <div className="flex flex-col items-center py-6 text-gray-400">
                  <BookOpen className="h-8 w-8 mb-2" />
                  <p className="text-sm">Bu dönem atanmış ders yok.</p>
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

/* ─── Section Detail Table ─── */

function SectionDetailTable({ sections }: { sections: AssignedSectionDto[] }) {
  return (
    <div className="rounded-lg border bg-white overflow-hidden max-h-[300px] overflow-y-auto">
      <Table>
        <TableHeader className="sticky top-0 z-10">
          <TableRow className="bg-gray-50 text-xs shadow-[0_1px_0_0_theme(colors.gray.200)]">
            <TableHead className="text-xs font-medium text-gray-500">Ders Kodu</TableHead>
            <TableHead className="text-xs font-medium text-gray-500">Ders Adı</TableHead>
            <TableHead className="text-xs font-medium text-gray-500 text-center">Şube</TableHead>
            <TableHead className="text-xs font-medium text-gray-500 text-center">Hft. Saat</TableHead>
            <TableHead className="text-xs font-medium text-gray-500 text-center">Kontenjan</TableHead>
            <TableHead className="text-xs font-medium text-gray-500 text-center">Kayıtlı</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sections.map(sec => (
            <TableRow key={sec.sectionId} className="hover:bg-gray-50/50">
              <TableCell className="font-mono text-xs font-bold text-blue-700">{sec.courseCode}</TableCell>
              <TableCell className="text-sm text-gray-700">{sec.courseName}</TableCell>
              <TableCell className="text-center">
                <Badge variant="outline" className="font-mono text-[10px] px-1.5 py-0 rounded-md">{sec.sectionNumber}</Badge>
              </TableCell>
              <TableCell className="text-center text-xs font-semibold text-gray-700">{sec.weeklyHours}</TableCell>
              <TableCell className="text-center text-xs text-gray-600">{sec.capacity}</TableCell>
              <TableCell className="text-center">
                <span className={cn(
                  'text-xs font-semibold',
                  sec.enrolledCount >= sec.capacity ? 'text-red-600' : 'text-gray-700',
                )}>
                  {sec.enrolledCount}
                </span>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
