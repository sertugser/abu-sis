import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import {
  useAcademicOperationsContext,
  useDepartmentClassLists,
} from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
import type { DepartmentSectionClassList } from '../types/academicOperations';
import { STATUS_LABELS, STATUS_VARIANT } from '../lib/constants';
import {
  AlertCircle,
  BookOpen,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Download,
  Mail,
  Search,
  UserX,
  Users,
  MapPin,
} from 'lucide-react';

const ITEMS_PER_PAGE = 20;

type SortField = 'courseCode' | 'courseName' | 'instructorName' | 'classroomName' | 'occupancy';
type SortDir = 'asc' | 'desc';

/* ─── Sort Header ─── */

function SortHeader({ field, children, className, sortField, sortDir, onSort }: {
  field: SortField;
  children: React.ReactNode;
  className?: string;
  sortField: SortField;
  sortDir: SortDir;
  onSort: (field: SortField) => void;
}) {
  const isActive = sortField === field;
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

/* ─── Occupancy Bar ─── */

function OccupancyBar({ active, capacity }: { active: number; capacity: number }) {
  const pct = capacity > 0 ? (active / capacity) * 100 : 0;
  const color = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : pct > 0 ? 'bg-emerald-500' : 'bg-gray-300';
  const textColor = pct >= 100 ? 'text-red-700' : pct >= 80 ? 'text-amber-700' : 'text-gray-700';

  return (
    <div className="flex items-center gap-2.5">
      <div className="w-16 h-2 rounded-full bg-gray-200 overflow-hidden">
        <div className={cn('h-full rounded-full transition-all', color)} style={{ width: `${Math.min(100, pct)}%` }} />
      </div>
      <span className={cn('text-xs font-semibold tabular-nums whitespace-nowrap', textColor)}>
        {active}/{capacity}
      </span>
    </div>
  );
}

/* ─── Main Page ─── */

export default function ClassListsPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();

  const accessibleDepartments = contextQuery.data?.accessibleDepartments;
  const departments = useMemo(() => accessibleDepartments ?? [], [accessibleDepartments]);

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [sortField, setSortField] = useState<SortField>('courseCode');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [page, setPage] = useState(0);

  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchTerm), 400);
    return () => clearTimeout(t);
  }, [searchTerm]);

  const accessibleDeptIds = useMemo(() => new Set(departments.map(d => d.departmentId)), [departments]);

  const availableFaculties = useMemo(() => {
    if (!faculties || !structureDepartments) return [];
    const facultyIds = new Set(
      structureDepartments
        .filter(sd => accessibleDeptIds.has(sd.id))
        .map(sd => sd.facultyId)
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId || !structureDepartments) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments
        .filter(sd => sd.facultyId === selectedFacultyId)
        .map(sd => sd.id)
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  const currentDeptId = useMemo(() => {
    if (selectedDeptId && filteredDepartments.some(d => d.departmentId === selectedDeptId)) {
      return selectedDeptId;
    }
    return filteredDepartments[0]?.departmentId ?? null;
  }, [selectedDeptId, filteredDepartments]);

  const handleFacultyChange = useCallback((e: ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value ? Number(e.target.value) : null;
    setSelectedFacultyId(val);
    setSelectedDeptId(null);
    setPage(0);
  }, []);

  const handleDeptChange = useCallback((e: ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value ? Number(e.target.value) : null;
    setSelectedDeptId(val);
    setPage(0);
  }, []);

  const classListsQuery = useDepartmentClassLists(currentDeptId, activeTerm?.id, true, debouncedSearch || undefined);

  const handleSort = useCallback((field: SortField) => {
    if (sortField === field) {
      setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDir('asc');
    }
    setPage(0);
  }, [sortField]);

  /* ── Loading ── */
  if (contextQuery.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-72" />
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-xl" />)}
        </div>
        <Skeleton className="h-96 rounded-xl" />
      </div>
    );
  }

  /* ── Error ── */
  if (contextQuery.isError || !contextQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sayfa yüklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri alınamadı.')}
        </AlertDescription>
      </Alert>
    );
  }

  const classLists = classListsQuery.data;
  const sections = classLists?.sections ?? [];

  const sorted = [...sections].sort((a, b) => {
    const dir = sortDir === 'asc' ? 1 : -1;
    switch (sortField) {
      case 'courseCode': return dir * a.courseCode.localeCompare(b.courseCode);
      case 'courseName': return dir * a.courseName.localeCompare(b.courseName);
      case 'instructorName': return dir * (a.instructorName ?? '').localeCompare(b.instructorName ?? '');
      case 'classroomName': return dir * (a.classroomName ?? '').localeCompare(b.classroomName ?? '');
      case 'occupancy': {
        const aPct = a.capacity > 0 ? a.activeStudentCount / a.capacity : 0;
        const bPct = b.capacity > 0 ? b.activeStudentCount / b.capacity : 0;
        return dir * (aPct - bPct);
      }
      default: return 0;
    }
  });

  const noInstructor = sections.filter(s => !s.instructorName);
  const noClassroom = sections.filter(s => !s.classroomName);
  const overfull = sections.filter(s => s.capacity > 0 && s.activeStudentCount > s.capacity);

  const totalCapacity = sections.reduce((sum, s) => sum + s.capacity, 0);
  const totalEnrolled = sections.reduce((sum, s) => sum + s.activeStudentCount, 0);
  const avgOccupancy = totalCapacity > 0 ? Math.round((totalEnrolled / totalCapacity) * 100) : 0;

  const totalPages = Math.max(1, Math.ceil(sorted.length / ITEMS_PER_PAGE));
  const paginated = sorted.slice(page * ITEMS_PER_PAGE, (page + 1) * ITEMS_PER_PAGE);

  /* ── CSV Exports ── */
  const handleExportCsv = () => {
    if (sorted.length === 0) return;
    const header = ['Ders Kodu', 'Ders Adı', 'Şube', 'Öğretim Üyesi', 'Derslik', 'Program', 'Kontenjan', 'Kayıtlı', 'Doluluk %'];
    const rows = sorted.map(s => [
      s.courseCode, s.courseName, String(s.sectionId),
      s.instructorName ?? '', s.classroomName ?? '', s.scheduleSummary ?? '',
      String(s.capacity), String(s.activeStudentCount),
      s.capacity > 0 ? String(Math.round((s.activeStudentCount / s.capacity) * 100)) : '0',
    ]);
    downloadCsv([header, ...rows], `sinif-listeleri-${new Date().toISOString().slice(0, 10)}.csv`);
  };

  const handleExportDetailedCsv = () => {
    if (sorted.length === 0) return;
    const header = ['Ders Kodu', 'Ders Adı', 'Şube', 'Öğrenci No', 'Öğrenci Adı', 'E-posta', 'Kayıt Durumu', 'Not'];
    const rows: string[][] = [];
    for (const s of sorted) {
      if (s.students.length === 0) {
        rows.push([s.courseCode, s.courseName, String(s.sectionId), '', '', '', '', '']);
      } else {
        for (const st of s.students) {
          rows.push([
            s.courseCode, s.courseName, String(s.sectionId),
            st.studentNumber, st.studentName, st.email ?? '',
            STATUS_LABELS[st.status] ?? st.status, st.grade ?? '',
          ]);
        }
      }
    }
    downloadCsv([header, ...rows], `sinif-listeleri-detayli-${new Date().toISOString().slice(0, 10)}.csv`);
  };

  /* ── Computed: should we show amber rows at all? ── */
  const issueRatio = sections.length > 0 ? noInstructor.length / sections.length : 0;
  const suppressAmberRows = issueRatio > 0.8; // If >80% have issues, amber becomes noise

  return (
    <div className="space-y-5">
      {/* ── Filter Bar ── */}
      <div className="flex flex-wrap items-center gap-3">
        {availableFaculties.length > 1 && (
          <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
            <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">Fakülte</span>
            <select
              className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[140px]"
              value={selectedFacultyId ?? ''}
              onChange={handleFacultyChange}
            >
              <option value="">Tümü</option>
              {availableFaculties.map(f => (
                <option key={f.id} value={f.id}>{f.name}</option>
              ))}
            </select>
          </div>
        )}

        <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
          <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">Bölüm</span>
          <select
            className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[200px]"
            value={currentDeptId ?? ''}
            onChange={handleDeptChange}
          >
            {filteredDepartments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-1.5 ml-auto">
          <div className="relative">
            <Search className="h-3.5 w-3.5 text-gray-400 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none" />
            <Input
              value={searchTerm}
              onChange={e => { setSearchTerm(e.target.value); setPage(0); }}
              placeholder="Ders kodu, ders adı veya derslik..."
              className="pl-8 h-8 w-72 text-xs rounded-lg shadow-sm"
            />
          </div>
          {sections.length > 0 && (
            <>
              <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs rounded-lg" onClick={handleExportCsv}>
                <Download className="h-3 w-3" />
                CSV
              </Button>
              <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs rounded-lg" onClick={handleExportDetailedCsv}>
                <Download className="h-3 w-3" />
                Detaylı CSV
              </Button>
            </>
          )}
        </div>
      </div>

      {/* ── Stat Cards ── */}
      {!classListsQuery.isLoading && classLists && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <StatCard icon={ClipboardList} label="Toplam Şube" value={sections.length} color="blue" />
          <StatCard icon={Users} label="Kayıtlı Öğrenci" value={classLists.totalStudents} sub={totalCapacity > 0 ? `/ ${totalCapacity} kontenjan` : undefined} color="emerald" />
          <StatCard
            icon={BookOpen}
            label="Ortalama Doluluk"
            value={`%${avgOccupancy}`}
            color={avgOccupancy >= 80 ? 'amber' : avgOccupancy > 0 ? 'emerald' : 'gray'}
          />
          <StatCard
            icon={noInstructor.length > 0 ? UserX : MapPin}
            label="Eksik Atama"
            value={noInstructor.length + noClassroom.length}
            sub={noInstructor.length > 0 ? `${noInstructor.length} hoca, ${noClassroom.length} derslik` : `${noClassroom.length} derslik`}
            color={noInstructor.length + noClassroom.length > 0 ? 'amber' : 'emerald'}
          />
        </div>
      )}

      {/* ── Problem Alerts ── */}
      {overfull.length > 0 && (
        <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <AlertCircle className="h-3.5 w-3.5 shrink-0" />
          <span><strong>{overfull.length} şube</strong> kapasite aşımında</span>
        </div>
      )}


      {/* ── Loading ── */}
      {classListsQuery.isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-10 rounded-lg" />
          <Skeleton className="h-96 rounded-xl" />
        </div>
      )}

      {/* ── Error ── */}
      {classListsQuery.isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Hata</AlertTitle>
          <AlertDescription>{getAcademicOperationsErrorMessage(classListsQuery.error, 'Sınıf listeleri yüklenemedi.')}</AlertDescription>
        </Alert>
      )}

      {/* ── Empty ── */}
      {!classListsQuery.isLoading && !classListsQuery.isError && !classLists && (
        <Card><CardContent className="py-16 text-center text-gray-500">Veri bulunamadı.</CardContent></Card>
      )}

      {/* ── Table ── */}
      {!classListsQuery.isLoading && classLists && (
        <>
          {sorted.length === 0 ? (
            <div className="rounded-xl border border-dashed border-gray-300 py-16 text-center">
              <Search className="h-8 w-8 text-gray-300 mx-auto mb-3" />
              <p className="text-sm text-gray-500">Eşleşen şube bulunamadı.</p>
            </div>
          ) : (
            <Card className="overflow-hidden rounded-xl border shadow-sm">
              <div className="overflow-x-auto">
                <Table className="table-fixed">
                  <colgroup>
                    <col className="w-10" />
                    <col className="w-[10%]" />
                    <col className="w-[28%]" />
                    <col className="w-[18%]" />
                    <col className="w-[10%]" />
                    <col className="w-[20%] hidden lg:table-column" />
                    <col className="w-[14%]" />
                  </colgroup>
                  <TableHeader>
                    <TableRow className="bg-gray-50/80 border-b">
                      <TableHead className="w-10" />
                      <SortHeader field="courseCode" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Ders Kodu</SortHeader>
                      <SortHeader field="courseName" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Ders Adı</SortHeader>
                      <SortHeader field="instructorName" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Öğretim Üyesi</SortHeader>
                      <SortHeader field="classroomName" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Derslik</SortHeader>
                      <TableHead className="hidden lg:table-cell text-xs font-semibold text-gray-600">Program</TableHead>
                      <SortHeader field="occupancy" className="text-right" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Doluluk</SortHeader>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginated.map((section, idx) => {
                      const prevCode = idx > 0 ? paginated[idx - 1].courseCode : null;
                      const isFirstInGroup = section.courseCode !== prevCode;

                      return (
                        <SectionRow
                          key={section.sectionId}
                          section={section}
                          isExpanded={expandedId === section.sectionId}
                          onToggle={() => setExpandedId(c => c === section.sectionId ? null : section.sectionId)}
                          isFirstInGroup={isFirstInGroup}
                          suppressAmber={suppressAmberRows}
                        />
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
              {totalPages > 1 && <Pagination current={page} total={totalPages} count={sorted.length} onPage={p => { setPage(p); setExpandedId(null); }} />}
            </Card>
          )}
        </>
      )}
    </div>
  );
}

/* ─── Section Row ─── */

function SectionRow({ section, isExpanded, onToggle, isFirstInGroup, suppressAmber }: {
  section: DepartmentSectionClassList;
  isExpanded: boolean;
  onToggle: () => void;
  isFirstInGroup: boolean;
  suppressAmber: boolean;
}) {
  const [studentSearch, setStudentSearch] = useState('');
  const [emailCopied, setEmailCopied] = useState(false);
  const [studentSort, setStudentSort] = useState<{ field: 'studentNumber' | 'studentName' | 'status' | 'grade'; dir: SortDir }>({ field: 'studentName', dir: 'asc' });

  const pct = section.capacity > 0 ? (section.activeStudentCount / section.capacity) * 100 : 0;
  const hasIssue = !section.instructorName || !section.classroomName || pct >= 100;

  const handleStudentSort = (field: typeof studentSort.field) => {
    setStudentSort(prev => prev.field === field ? { field, dir: prev.dir === 'asc' ? 'desc' : 'asc' } : { field, dir: 'asc' });
  };

  const filteredStudents = useMemo(() => {
    let list = section.students;
    if (studentSearch.trim()) {
      const q = studentSearch.toLowerCase();
      list = list.filter(
        st => st.studentName.toLowerCase().includes(q) || st.studentNumber.toLowerCase().includes(q)
      );
    }
    const dir = studentSort.dir === 'asc' ? 1 : -1;
    return [...list].sort((a, b) => {
      switch (studentSort.field) {
        case 'studentNumber': return dir * a.studentNumber.localeCompare(b.studentNumber);
        case 'studentName': return dir * a.studentName.localeCompare(b.studentName);
        case 'status': return dir * a.status.localeCompare(b.status);
        case 'grade': return dir * (a.grade ?? '').localeCompare(b.grade ?? '');
        default: return 0;
      }
    });
  }, [section.students, studentSearch, studentSort]);

  const handleCopyEmails = () => {
    const emails = section.students.filter(s => s.email).map(s => s.email).join('; ');
    if (!emails) return;
    navigator.clipboard.writeText(emails).then(() => {
      setEmailCopied(true);
      setTimeout(() => setEmailCopied(false), 2000);
    }).catch(() => {
      const textarea = document.createElement('textarea');
      textarea.value = emails;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
      setEmailCopied(true);
      setTimeout(() => setEmailCopied(false), 2000);
    });
  };

  return (
    <>
      <TableRow
        className={cn(
          'cursor-pointer transition-colors group',
          isFirstInGroup && 'border-t border-gray-200',
          isExpanded ? 'bg-blue-50/40' : 'hover:bg-gray-50/80',
          !suppressAmber && hasIssue && !isExpanded && 'bg-amber-50/40',
        )}
        onClick={onToggle}
      >
        <TableCell className="px-2 py-3">
          <ChevronDown className={cn(
            'h-3.5 w-3.5 text-gray-400 transition-transform group-hover:text-gray-600',
            isExpanded && 'rotate-180 text-blue-600',
          )} />
        </TableCell>

        {/* Course Code — only show text for first in group */}
        <TableCell className="py-3">
          {isFirstInGroup ? (
            <span className="font-mono text-xs font-bold text-blue-700">{section.courseCode}</span>
          ) : (
            <span className="font-mono text-xs text-gray-400">{section.courseCode}</span>
          )}
        </TableCell>

        {/* Course Name */}
        <TableCell className="py-3 overflow-hidden">
          <div className="flex items-center gap-2 min-w-0">
            <span className={cn('text-sm truncate', !isFirstInGroup && 'text-gray-500')}>{section.courseName}</span>
            {!section.isActive && <Badge variant="secondary" className="text-[10px] px-1.5 py-0 rounded-full shrink-0">Pasif</Badge>}
          </div>
        </TableCell>

        {/* Instructor */}
        <TableCell className="py-3 overflow-hidden">
          {section.instructorName ? (
            <span className="text-sm text-gray-700 truncate block">{section.instructorName}</span>
          ) : (
            <span className="inline-flex items-center gap-1 text-xs text-amber-600">
              <UserX className="h-3 w-3 shrink-0" />
              Atanmamış
            </span>
          )}
        </TableCell>

        {/* Classroom */}
        <TableCell className="py-3">
          {section.classroomName ? (
            <Badge variant="outline" className="font-mono text-[11px] px-2 py-0.5 rounded-md bg-white">{section.classroomName}</Badge>
          ) : (
            <span className="inline-flex items-center gap-1 text-xs text-amber-600">
              <MapPin className="h-3 w-3 shrink-0" />
              Atanmamış
            </span>
          )}
        </TableCell>

        {/* Schedule */}
        <TableCell className="hidden lg:table-cell py-3 overflow-hidden">
          {section.scheduleSummary ? (
            <span className="text-xs text-gray-600 truncate block">{section.scheduleSummary}</span>
          ) : (
            <span className="text-xs text-gray-400">—</span>
          )}
        </TableCell>

        {/* Occupancy */}
        <TableCell className="py-3">
          <div className="flex justify-end">
            <OccupancyBar active={section.activeStudentCount} capacity={section.capacity} />
          </div>
        </TableCell>
      </TableRow>

      {/* ── Expanded Detail ── */}
      {isExpanded && (
        <TableRow className="bg-gray-50/60 hover:bg-gray-50/60">
          <TableCell colSpan={7} className="p-0">
            <div className="px-6 py-4 border-t border-gray-100">
              {section.students.length === 0 ? (
                <div className="flex flex-col items-center py-6 text-gray-400">
                  <Users className="h-8 w-8 mb-2" />
                  <p className="text-sm">Bu şubede henüz kayıtlı öğrenci yok.</p>
                </div>
              ) : (
                <>
                  <div className="flex items-center justify-between gap-2 mb-3">
                    <div className="flex items-center gap-2">
                      <div className="relative">
                        <Search className="h-3 w-3 text-gray-400 absolute left-2 top-1/2 -translate-y-1/2 pointer-events-none" />
                        <Input
                          placeholder="Öğrenci ara..."
                          className="h-7 w-52 text-xs pl-6 rounded-lg"
                          value={studentSearch}
                          onChange={e => setStudentSearch(e.target.value)}
                          onClick={e => e.stopPropagation()}
                        />
                      </div>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 text-xs rounded-lg"
                        onClick={e => { e.stopPropagation(); handleCopyEmails(); }}
                      >
                        <Mail className="h-3 w-3 mr-1" />
                        {emailCopied ? 'Kopyalandı!' : 'E-postaları Kopyala'}
                      </Button>
                    </div>
                    <span className="text-xs text-gray-500 tabular-nums shrink-0">
                      {studentSearch ? `${filteredStudents.length} / ` : ''}{section.students.length} öğrenci
                    </span>
                  </div>
                  <div className="rounded-lg border bg-white overflow-hidden max-h-[400px] overflow-y-auto">
                    <Table>
                      <TableHeader className="sticky top-0 z-10">
                        <TableRow className="bg-gray-50 text-xs shadow-[0_1px_0_0_theme(colors.gray.200)]">
                          <StudentSortHead field="studentNumber" label="No" sort={studentSort} onSort={handleStudentSort} />
                          <StudentSortHead field="studentName" label="Ad Soyad" sort={studentSort} onSort={handleStudentSort} />
                          <TableHead className="text-xs font-medium text-gray-500">E-posta</TableHead>
                          <StudentSortHead field="status" label="Durum" sort={studentSort} onSort={handleStudentSort} className="text-center" />
                          <StudentSortHead field="grade" label="Not" sort={studentSort} onSort={handleStudentSort} className="text-center" />
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {filteredStudents.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={5} className="text-center text-xs text-gray-500 py-6">Eşleşen öğrenci bulunamadı.</TableCell>
                          </TableRow>
                        ) : (
                          filteredStudents.map(st => (
                            <TableRow key={st.studentId} className="hover:bg-gray-50/50">
                              <TableCell className="font-mono text-xs text-gray-600">{st.studentNumber}</TableCell>
                              <TableCell className="text-sm font-medium text-gray-800">{st.studentName}</TableCell>
                              <TableCell className="text-xs text-gray-500">
                                {st.email ? (
                                  <span className="inline-flex items-center gap-1">
                                    <Mail className="h-3 w-3 text-gray-400" />
                                    {st.email}
                                  </span>
                                ) : (
                                  <span className="text-gray-300">—</span>
                                )}
                              </TableCell>
                              <TableCell className="text-center">
                                <Badge variant={STATUS_VARIANT[st.status] ?? 'outline'} className="text-[10px]">
                                  {STATUS_LABELS[st.status] ?? st.status}
                                </Badge>
                              </TableCell>
                              <TableCell className="text-center font-mono text-xs">
                                {st.grade ? (
                                  <span className={cn(
                                    'inline-flex items-center justify-center w-7 h-5 rounded font-bold text-[11px]',
                                    (st.grade === 'FF' || st.grade === 'NA') ? 'bg-red-100 text-red-700' : 'text-gray-700',
                                  )}>{st.grade}</span>
                                ) : <span className="text-gray-300">—</span>}
                              </TableCell>
                            </TableRow>
                          ))
                        )}
                      </TableBody>
                    </Table>
                  </div>
                </>
              )}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

/* ─── Student Sort Header ─── */

function StudentSortHead({ field, label, sort, onSort, className }: {
  field: 'studentNumber' | 'studentName' | 'status' | 'grade';
  label: string;
  sort: { field: string; dir: SortDir };
  onSort: (field: 'studentNumber' | 'studentName' | 'status' | 'grade') => void;
  className?: string;
}) {
  const isActive = sort.field === field;
  return (
    <TableHead
      className={cn('cursor-pointer select-none text-xs font-medium text-gray-500 hover:text-gray-700 hover:bg-gray-100/50 transition-colors', className)}
      onClick={e => { e.stopPropagation(); onSort(field); }}
    >
      <span className="inline-flex items-center gap-1">
        {label}
        {isActive && <span className="text-[10px] text-blue-500">{sort.dir === 'asc' ? '▲' : '▼'}</span>}
      </span>
    </TableHead>
  );
}

/* ─── Pagination ─── */

function Pagination({ current, total, count, onPage }: { current: number; total: number; count: number; onPage: (p: number) => void }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 border-t bg-gray-50/50 text-xs text-gray-500">
      <span>{count} kayıt · sayfa {current + 1}/{total}</span>
      <div className="flex gap-1">
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0 rounded-lg" disabled={current === 0} onClick={() => onPage(current - 1)}>
          <ChevronLeft className="h-3.5 w-3.5" />
        </Button>
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0 rounded-lg" disabled={current >= total - 1} onClick={() => onPage(current + 1)}>
          <ChevronRight className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}

/* ─── CSV Helper ─── */

function downloadCsv(data: string[][], filename: string) {
  const csv = data.map(r => r.map(c => `"${c.replace(/"/g, '""')}"`).join(',')).join('\n');
  const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
