import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useAdminStudents } from '../../hooks/useAdminStudents';
import { useDebounce } from '../../hooks/useDebounce';
import { STUDENT_STATUS_LABELS, STUDENT_STATUS_VARIANT } from '../../lib/constants';
import { cgpaColor } from './utils';
import CreateStudentDialog from './CreateStudentDialog';
import StudentFilterBar from './components/StudentFilterBar';
import { EMPTY_FILTER_STATE, type StudentFilterState } from './components/studentFilter';

const PAGE_SIZE = 25;
const DEBOUNCE_MS = 300;
const COLUMN_COUNT = 12;

function parseHasAdvisor(value: string): boolean | undefined {
  if (value === 'true') return true;
  if (value === 'false') return false;
  return undefined;
}

function parseCgpa(value: string): number | undefined {
  if (value.trim() === '') return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function initials(firstName: string, lastName: string): string {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
}

export default function StudentSearchPage() {
  const navigate = useNavigate();

  const [filters, setFilters] = useState<StudentFilterState>(EMPTY_FILTER_STATE);
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  // Debounce only the free-text fields so we don't spam requests per keystroke.
  const debouncedSearch = useDebounce(filters.search, DEBOUNCE_MS);
  const debouncedNoFrom = useDebounce(filters.studentNoFrom, DEBOUNCE_MS);
  const debouncedNoTo = useDebounce(filters.studentNoTo, DEBOUNCE_MS);

  function patchFilters(patch: Partial<StudentFilterState>) {
    setFilters(prev => ({ ...prev, ...patch }));
    setPage(1);
  }

  function clearFilters() {
    setFilters(EMPTY_FILTER_STATE);
    setPage(1);
  }

  const { data, isLoading } = useAdminStudents({
    search: debouncedSearch || undefined,
    departmentId: filters.departmentId,
    programId: filters.programId,
    status: filters.status || undefined,
    hasAdvisor: parseHasAdvisor(filters.advisorFilter),
    minCgpa: parseCgpa(filters.minCgpa),
    maxCgpa: parseCgpa(filters.maxCgpa),
    classYear: filters.classYear ? Number(filters.classYear) : undefined,
    curriculumId: filters.curriculumId,
    studentNoFrom: debouncedNoFrom || undefined,
    studentNoTo: debouncedNoTo || undefined,
    enrollmentFrom: filters.enrollmentFrom || undefined,
    enrollmentTo: filters.enrollmentTo || undefined,
    advisorId: filters.advisorId,
    hasFailingCourse: filters.hasFailingCourse ? true : undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  function showSuccess(msg: string) {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(''), 3000);
  }

  function goToStudent(id: number) {
    navigate(`/ogrenci-rehberi/${id}`);
  }

  return (
    <div className="rounded-xl border border-border bg-white overflow-hidden shadow-sm">
      {/* (a) Filters + actions on a single row */}
      <StudentFilterBar
        filters={filters}
        onChange={patchFilters}
        onClear={clearFilters}
        advancedOpen={advancedOpen}
        onToggleAdvanced={() => setAdvancedOpen(o => !o)}
        successMessage={successMessage}
        onCreateStudent={() => setCreateModalOpen(true)}
      />

      {/* (c) Results table */}
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Foto</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground hidden xl:table-cell">T.C. Kimlik</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Öğrenci No</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ad Soyad</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground hidden lg:table-cell">Bölüm</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Program</th>
              <th className="text-center px-4 py-3 font-semibold text-muted-foreground">Sınıf</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">GNO</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Danışman</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground hidden xl:table-cell">Kayıt Tarihi</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Burs</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading ? (
              <tr>
                <td colSpan={COLUMN_COUNT} className="px-4 py-8 text-center text-muted-foreground">
                  Yükleniyor...
                </td>
              </tr>
            ) : data?.items.length === 0 ? (
              <tr>
                <td colSpan={COLUMN_COUNT} className="px-4 py-8 text-center text-muted-foreground">
                  Öğrenci bulunamadı.
                </td>
              </tr>
            ) : (
              data?.items.map(student => (
                <tr
                  key={student.id}
                  onClick={() => goToStudent(student.id)}
                  onKeyDown={e => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      goToStudent(student.id);
                    }
                  }}
                  tabIndex={0}
                  role="button"
                  className="hover:bg-muted/20 transition-colors cursor-pointer"
                >
                  <td className="px-4 py-2">
                    <div className="h-9 w-9 rounded-full bg-muted flex items-center justify-center text-xs font-semibold text-muted-foreground">
                      {initials(student.firstName, student.lastName)}
                    </div>
                  </td>
                  <td className="px-4 py-2 font-mono text-xs hidden xl:table-cell">
                    {student.nationalId ?? '—'}
                  </td>
                  <td className="px-4 py-2 font-mono text-xs">{student.studentNumber}</td>
                  <td className="px-4 py-2 font-medium text-foreground">
                    {student.firstName} {student.lastName}
                  </td>
                  <td className="px-4 py-2 text-xs text-muted-foreground hidden lg:table-cell">
                    {student.departmentName}
                  </td>
                  <td className="px-4 py-2 text-xs text-muted-foreground">{student.programName}</td>
                  <td className="px-4 py-2 text-xs text-center text-muted-foreground">
                    {student.classYear != null ? `${student.classYear}. Sınıf` : '—'}
                  </td>
                  <td className="px-4 py-2">
                    {student.transcriptAccessible === false ? (
                      <span className="text-xs font-medium text-muted-foreground">Kapalı</span>
                    ) : (
                      <span className={`font-semibold ${cgpaColor(student.cgpa)}`}>
                        {student.cgpa?.toFixed(2) ?? '—'}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-2 text-xs text-muted-foreground">{student.advisorName ?? '—'}</td>
                  <td className="px-4 py-2 text-xs hidden xl:table-cell">
                    {student.enrollmentDate
                      ? new Date(student.enrollmentDate).toLocaleDateString('tr-TR')
                      : '—'}
                  </td>
                  <td className="px-4 py-2">
                    {student.isScholarshipStudent ? (
                      <Badge variant="secondary">Burslu</Badge>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                  <td className="px-4 py-2">
                    <Badge variant={STUDENT_STATUS_VARIANT[student.status] ?? 'secondary'}>
                      {STUDENT_STATUS_LABELS[student.status] ?? student.status}
                    </Badge>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* (d) Pagination */}
      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between px-4 py-3 border-t border-border">
          <p className="text-sm text-muted-foreground">
            Toplam {data.totalCount} — Sayfa {data.page}/{data.totalPages}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              aria-label="Önceki sayfa"
              disabled={page <= 1}
              onClick={() => setPage(p => p - 1)}
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              aria-label="Sonraki sayfa"
              disabled={page >= data.totalPages}
              onClick={() => setPage(p => p + 1)}
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}

      {/* Create Student Modal — remounted on each open so form starts fresh */}
      {createModalOpen && (
        <CreateStudentDialog
          open={createModalOpen}
          onOpenChange={setCreateModalOpen}
          departmentId={filters.departmentId}
          onCreated={showSuccess}
        />
      )}
    </div>
  );
}
