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
import {
  AlertCircle,
  AlertTriangle,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Download,
  Mail,
  Search,
} from 'lucide-react';

const ITEMS_PER_PAGE = 20;

type SortField = 'courseCode' | 'courseName' | 'instructorName' | 'classroomName' | 'occupancy';
type SortDir = 'asc' | 'desc';

function SortHeader({ field, children, className, sortField, sortDir, onSort }: {
  field: SortField;
  children: React.ReactNode;
  className?: string;
  sortField: SortField;
  sortDir: SortDir;
  onSort: (field: SortField) => void;
}) {
  return (
    <TableHead
      className={cn('cursor-pointer select-none hover:bg-gray-100 transition-colors', className)}
      onClick={() => onSort(field)}
    >
      <span className="inline-flex items-center gap-1">
        {children}
        {sortField === field && <span className="text-[10px]">{sortDir === 'asc' ? '\u25B2' : '\u25BC'}</span>}
      </span>
    </TableHead>
  );
}

export default function ClassListsPage() {
  const contextQuery = useAcademicOperationsContext();
  const { data: activeTerm } = useActiveTerm();
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();

  const departments = contextQuery.data?.accessibleDepartments ?? [];

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
    setSelectedDeptId(Number(e.target.value));
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

  if (contextQuery.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-10 rounded-lg" />
        <Skeleton className="h-80 rounded-xl" />
      </div>
    );
  }

  if (contextQuery.isError || !contextQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Sayfa y\u00fcklenemedi</AlertTitle>
        <AlertDescription>
          {getAcademicOperationsErrorMessage(contextQuery.error, 'Yetkili kapsam bilgileri al\u0131namad\u0131.')}
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
  const empty = sections.filter(s => s.activeStudentCount === 0);
  const hasProblems = noInstructor.length > 0 || noClassroom.length > 0 || overfull.length > 0 || empty.length > 0;

  const avgOccupancy = sections.length > 0
    ? Math.round(sections.reduce((sum, s) => sum + (s.capacity > 0 ? s.activeStudentCount / s.capacity : 0), 0) / sections.length * 100)
    : 0;

  const totalPages = Math.max(1, Math.ceil(sorted.length / ITEMS_PER_PAGE));
  const paginated = sorted.slice(page * ITEMS_PER_PAGE, (page + 1) * ITEMS_PER_PAGE);

  const handleExportCsv = () => {
    if (sections.length === 0) return;
    const header = ['Ders Kodu', 'Ders Ad\u0131', '\u015Eube', '\u00D6\u011Fretim \u00DCyesi', 'Derslik', 'Program', 'Kontenjan', 'Kay\u0131tl\u0131', 'Doluluk %'];
    const rows = sections.map(s => [
      s.courseCode,
      s.courseName,
      String(s.sectionId),
      s.instructorName ?? '',
      s.classroomName ?? '',
      s.scheduleSummary ?? '',
      String(s.capacity),
      String(s.activeStudentCount),
      s.capacity > 0 ? String(Math.round((s.activeStudentCount / s.capacity) * 100)) : '0',
    ]);
    const csv = [header, ...rows].map(r => r.map(c => `"${c.replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `sinif-listeleri-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-5">
      {/* Filter Bar */}
      <div className="flex flex-wrap items-center gap-2 text-xs">
        {availableFaculties.length > 1 && (
          <div className="flex items-center gap-1.5">
            <label className="font-medium whitespace-nowrap">Fak\u00fclte:</label>
            <select
              className="h-7 rounded-md border border-input bg-background px-2 text-xs"
              value={selectedFacultyId ?? ''}
              onChange={handleFacultyChange}
            >
              <option value="">T\u00fcm\u00fc</option>
              {availableFaculties.map(f => (
                <option key={f.id} value={f.id}>{f.name}</option>
              ))}
            </select>
          </div>
        )}

        <div className="flex items-center gap-1.5">
          <label className="font-medium whitespace-nowrap">B\u00f6l\u00fcm:</label>
          <select
            className="h-7 rounded-md border border-input bg-background px-2 text-xs"
            value={currentDeptId ?? ''}
            onChange={handleDeptChange}
          >
            {filteredDepartments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} \u2014 {d.departmentName}
              </option>
            ))}
          </select>
        </div>

        <div className="relative ml-auto">
          <Search className="h-3.5 w-3.5 text-gray-500 absolute left-2.5 top-1/2 -translate-y-1/2" />
          <Input
            value={searchTerm}
            onChange={e => { setSearchTerm(e.target.value); setPage(0); }}
            placeholder="Ders, \u00f6\u011fretim \u00fcyesi veya derslik..."
            className="pl-8 h-7 w-64 text-xs"
          />
        </div>
      </div>

      {/* Stats Bar */}
      {!classListsQuery.isLoading && classLists && (
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <p className="text-sm text-gray-500">
            <span className="font-semibold text-gray-900">{sections.length}</span> \u015Fube \u00b7{' '}
            <span className="font-semibold text-gray-900">{classLists.totalStudents}</span> \u00f6\u011Frenci \u00b7{' '}
            %{avgOccupancy} doluluk
          </p>
          <Button variant="outline" size="sm" className="h-7 gap-1 text-xs" onClick={handleExportCsv} disabled={sections.length === 0}>
            <Download className="h-3 w-3" />
            CSV
          </Button>
        </div>
      )}

      {/* Problems Banner */}
      {hasProblems && (
        <div className="flex flex-wrap gap-2 text-xs">
          {noInstructor.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 border border-amber-200 px-2 py-1 text-amber-700">
              <AlertTriangle className="h-3 w-3" />
              {noInstructor.length} \u015Fube hocas\u0131 atanmam\u0131\u015F
            </span>
          )}
          {noClassroom.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 border border-amber-200 px-2 py-1 text-amber-700">
              <AlertTriangle className="h-3 w-3" />
              {noClassroom.length} \u015Fube dersli\u011Fi atanmam\u0131\u015F
            </span>
          )}
          {overfull.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-red-50 border border-red-200 px-2 py-1 text-red-700">
              <AlertCircle className="h-3 w-3" />
              {overfull.length} \u015Fube kapasite a\u015F\u0131m\u0131nda
            </span>
          )}
          {empty.length > 0 && (
            <span className="inline-flex items-center gap-1 rounded-md bg-gray-100 border border-gray-300 px-2 py-1 text-gray-500">
              {empty.length} \u015Fube bo\u015F
            </span>
          )}
        </div>
      )}

      {/* Loading */}
      {classListsQuery.isLoading && (
        <div className="space-y-3">
          <Skeleton className="h-10 rounded-lg" />
          <Skeleton className="h-96 rounded-xl" />
        </div>
      )}

      {/* Error */}
      {classListsQuery.isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Hata</AlertTitle>
          <AlertDescription>{getAcademicOperationsErrorMessage(classListsQuery.error, 'S\u0131n\u0131f listeleri y\u00fcklenemedi.')}</AlertDescription>
        </Alert>
      )}

      {/* Empty */}
      {!classListsQuery.isLoading && !classListsQuery.isError && !classLists && (
        <Card><CardContent className="py-16 text-center text-gray-500">Veri bulunamad\u0131.</CardContent></Card>
      )}

      {/* Table */}
      {!classListsQuery.isLoading && classLists && (
        <>
          {sorted.length === 0 ? (
            <p className="text-sm text-gray-500 py-8 text-center">E\u015Fle\u015Fen \u015Fube bulunamad\u0131.</p>
          ) : (
            <Card className="overflow-hidden">
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-gray-50 text-xs">
                      <TableHead className="w-8" />
                      <SortHeader field="courseCode" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Ders Kodu</SortHeader>
                      <SortHeader field="courseName" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Ders Ad\u0131</SortHeader>
                      <SortHeader field="instructorName" sortField={sortField} sortDir={sortDir} onSort={handleSort}>\u00D6\u011Fretim \u00DCyesi</SortHeader>
                      <SortHeader field="classroomName" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Derslik</SortHeader>
                      <TableHead className="hidden lg:table-cell">Program</TableHead>
                      <SortHeader field="occupancy" className="text-center w-28" sortField={sortField} sortDir={sortDir} onSort={handleSort}>Doluluk</SortHeader>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginated.map(section => (
                      <SectionRow
                        key={section.sectionId}
                        section={section}
                        isExpanded={expandedId === section.sectionId}
                        onToggle={() => setExpandedId(c => c === section.sectionId ? null : section.sectionId)}
                      />
                    ))}
                  </TableBody>
                </Table>
              </div>
              {totalPages > 1 && <Pagination current={page} total={totalPages} count={sorted.length} onPage={setPage} />}
            </Card>
          )}
        </>
      )}
    </div>
  );
}

function SectionRow({ section, isExpanded, onToggle }: {
  section: DepartmentSectionClassList; isExpanded: boolean; onToggle: () => void;
}) {
  const [studentSearch, setStudentSearch] = useState('');
  const [emailCopied, setEmailCopied] = useState(false);

  const pct = section.capacity > 0 ? (section.activeStudentCount / section.capacity) * 100 : 0;
  const barColor = pct >= 100 ? 'bg-red-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500';
  const hasIssue = !section.instructorName || !section.classroomName || pct >= 100;
  const isEmpty = section.activeStudentCount === 0;

  const filteredStudents = useMemo(() => {
    if (!studentSearch.trim()) return section.students;
    const q = studentSearch.toLowerCase();
    return section.students.filter(
      st => st.studentName.toLowerCase().includes(q) || st.studentNumber.toLowerCase().includes(q)
    );
  }, [section.students, studentSearch]);

  const handleCopyEmails = () => {
    const emails = section.students.filter(s => s.email).map(s => s.email).join('; ');
    navigator.clipboard.writeText(emails).then(() => {
      setEmailCopied(true);
      setTimeout(() => setEmailCopied(false), 2000);
    });
  };

  return (
    <>
      <TableRow
        className={cn(
          'cursor-pointer transition-colors hover:bg-gray-100',
          (hasIssue || isEmpty) && 'bg-amber-50',
        )}
        onClick={onToggle}
      >
        <TableCell className="px-2">
          <ChevronDown className={cn('h-3.5 w-3.5 text-gray-500 transition-transform', isExpanded && 'rotate-180')} />
        </TableCell>
        <TableCell>
          <span className="font-mono text-xs font-bold text-blue-700">{section.courseCode}</span>
        </TableCell>
        <TableCell>
          <div className="flex items-center gap-2">
            <span className="text-sm">{section.courseName}</span>
            {!section.isActive && <Badge variant="secondary" className="text-[10px] px-1 py-0">Pasif</Badge>}
          </div>
        </TableCell>
        <TableCell className={cn('text-sm', !section.instructorName && 'text-amber-600 italic text-xs')}>
          {section.instructorName || 'Atanmam\u0131\u015F'}
        </TableCell>
        <TableCell className={cn('text-sm', !section.classroomName && 'text-amber-600 italic text-xs')}>
          {section.classroomName || 'Atanmam\u0131\u015F'}
        </TableCell>
        <TableCell className="hidden lg:table-cell text-xs text-gray-500">
          {section.scheduleSummary || <span className="italic">Tan\u0131mlanmam\u0131\u015F</span>}
        </TableCell>
        <TableCell>
          <div className="flex items-center gap-2 justify-center">
            <div className="w-12 h-1.5 rounded-full bg-gray-200 overflow-hidden">
              <div className={cn('h-full rounded-full', barColor)} style={{ width: `${Math.min(100, pct)}%` }} />
            </div>
            <span className="text-xs font-semibold tabular-nums whitespace-nowrap">
              {section.activeStudentCount}/{section.capacity}
            </span>
          </div>
        </TableCell>
      </TableRow>

      {isExpanded && (
        <TableRow className="bg-gray-50 hover:bg-gray-50">
          <TableCell colSpan={7} className="p-0">
            <div className="px-6 py-3">
              {section.students.length === 0 ? (
                <p className="text-sm text-gray-500 text-center py-3">Bu \u015Fubede \u00f6\u011Frenci yok.</p>
              ) : (
                <>
                  <div className="flex items-center gap-2 mb-2">
                    <div className="relative">
                      <Search className="h-3 w-3 text-gray-400 absolute left-2 top-1/2 -translate-y-1/2" />
                      <Input
                        placeholder="\u00d6\u011frenci ara..."
                        className="h-7 w-48 text-xs pl-6"
                        value={studentSearch}
                        onChange={e => setStudentSearch(e.target.value)}
                        onClick={e => e.stopPropagation()}
                      />
                    </div>
                    <Button
                      size="sm"
                      variant="outline"
                      className="h-7 text-xs"
                      onClick={e => { e.stopPropagation(); handleCopyEmails(); }}
                    >
                      <Mail className="h-3 w-3 mr-1" />
                      {emailCopied ? 'Kopyaland\u0131!' : 'E-posta Listesi Kopyala'}
                    </Button>
                    {studentSearch && (
                      <span className="text-xs text-gray-500">{filteredStudents.length} / {section.students.length} \u00f6\u011frenci</span>
                    )}
                  </div>
                  <div className="rounded-md border overflow-hidden overflow-x-auto">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-gray-50 text-xs">
                          <TableHead>No</TableHead>
                          <TableHead>Ad Soyad</TableHead>
                          <TableHead>E-posta</TableHead>
                          <TableHead className="text-center">Durum</TableHead>
                          <TableHead className="text-center">Not</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {filteredStudents.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={5} className="text-center text-xs text-gray-500 py-4">E\u015Fle\u015Fen \u00f6\u011frenci bulunamad\u0131.</TableCell>
                          </TableRow>
                        ) : (
                          filteredStudents.map(st => (
                            <TableRow key={st.studentId}>
                              <TableCell className="font-mono text-xs">{st.studentNumber}</TableCell>
                              <TableCell className="text-sm">{st.studentName}</TableCell>
                              <TableCell className="text-xs text-gray-500">
                                {st.email ? <span className="flex items-center gap-1"><Mail className="h-3 w-3" />{st.email}</span> : '\u2014'}
                              </TableCell>
                              <TableCell className="text-center">
                                <Badge variant={st.status === 'Registered' ? 'success' : 'outline'} className="text-[10px]">{st.status}</Badge>
                              </TableCell>
                              <TableCell className="text-center font-mono text-xs">
                                {st.grade ? (
                                  <span className={cn((st.grade === 'FF' || st.grade === 'NA') && 'text-red-600 font-bold')}>{st.grade}</span>
                                ) : '\u2014'}
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

function Pagination({ current, total, count, onPage }: { current: number; total: number; count: number; onPage: (p: number) => void }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 border-t text-xs text-gray-500">
      <span>{count} kay\u0131t \u00b7 sayfa {current + 1}/{total}</span>
      <div className="flex gap-1">
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0" disabled={current === 0} onClick={() => onPage(current - 1)}>
          <ChevronLeft className="h-3.5 w-3.5" />
        </Button>
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0" disabled={current >= total - 1} onClick={() => onPage(current + 1)}>
          <ChevronRight className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}
