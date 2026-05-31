import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { FolderOpen } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  useInstructorSections,
  useInstructorTerms,
} from '../hooks/useInstructorSections';
import { useExamsOverview } from '../hooks/useGradeData';
import type { InstructorSection } from '../api/instructor.api';
import type { ExamOverviewRow, ExamOverviewComponent } from '../types/grade';
import { ROUTES } from '../lib/constants';
import {
  InstructorCourseFilters,
  FACULTY_FILTER_ALL,
} from '../components/instructor/InstructorCourseFilters';

// ── helpers ────────────────────────────────────────────────────────────────────

function formatExamDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function includesCi(value: string, query: string): boolean {
  return value.toLowerCase().includes(query.trim().toLowerCase());
}

// ── section list view (default) ────────────────────────────────────────────────

interface SectionListProps {
  sections: InstructorSection[];
  totalCount: number;
  isLoading: boolean;
  isError: boolean;
  showKontenjan: boolean;
  onRowClick: (sectionId: number) => void;
}

function SectionList({
  sections,
  totalCount,
  isLoading,
  isError,
  showKontenjan,
  onRowClick,
}: SectionListProps) {
  if (isLoading) {
    return (
      <div className="space-y-2 p-4">
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex flex-1 items-center justify-center p-8 text-sm text-destructive">
        Ders verileri yüklenemedi.
      </div>
    );
  }

  if (sections.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center px-4 py-12 text-center">
        <FolderOpen className="mb-3 h-10 w-10 text-muted-foreground/30" />
        <p className="text-sm font-medium text-muted-foreground">
          {totalCount === 0
            ? 'Bu dönem size atanmış ders bulunmamaktadır.'
            : 'Filtreye uyan ders bulunamadı.'}
        </p>
      </div>
    );
  }

  return (
    <table className="w-full min-w-[560px]">
      <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
        <tr>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Şb.
          </th>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Ders Kodu
          </th>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Ders Adı
          </th>
          <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            AKTS
          </th>
          <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Öğrenci
          </th>
          {showKontenjan && (
            <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Kon
            </th>
          )}
          <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Durum
          </th>
        </tr>
      </thead>
      <tbody className="divide-y divide-border/80">
        {sections.map((s: InstructorSection) => (
          <tr
            key={s.sectionId}
            className="cursor-pointer transition-colors hover:bg-primary/5"
            onClick={() => onRowClick(s.sectionId)}
          >
            <td className="px-4 py-3 text-sm text-muted-foreground">{s.sectionNumber}</td>
            <td className="px-4 py-3">
              <span className="font-mono text-sm font-semibold text-primary">{s.courseCode}</span>
            </td>
            <td className="px-4 py-3 text-sm font-medium">{s.courseName}</td>
            <td className="px-4 py-3 text-center text-sm">{s.ects}</td>
            <td className="px-4 py-3 text-center text-sm">{s.enrolledCount}</td>
            {showKontenjan && (
              <td className="px-4 py-3 text-center text-sm text-muted-foreground">
                {s.enrolledCount}/{s.capacity}
              </td>
            )}
            <td className="px-4 py-3 text-center">
              <Badge
                variant="outline"
                className={
                  s.isFinalized
                    ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                    : 'bg-slate-50 text-slate-600 border-slate-200'
                }
              >
                {s.isFinalized ? 'Sonuçlandı' : 'Bekliyor'}
              </Badge>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

// ── exam overview view ─────────────────────────────────────────────────────────

interface ExamOverviewListProps {
  rows: ExamOverviewRow[];
  isLoading: boolean;
  isError: boolean;
  onRowClick: (sectionId: number) => void;
}

function ExamOverviewList({ rows, isLoading, isError, onRowClick }: ExamOverviewListProps) {
  if (isLoading) {
    return (
      <div className="space-y-2 p-4">
        {[1, 2, 3, 4].map((i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex flex-1 items-center justify-center p-8 text-sm text-destructive">
        Sınav verileri yüklenemedi.
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center px-4 py-12 text-center">
        <FolderOpen className="mb-3 h-10 w-10 text-muted-foreground/30" />
        <p className="text-sm font-medium text-muted-foreground">
          Filtreye uyan sınav bulunamadı.
        </p>
      </div>
    );
  }

  // Expand each row into display rows — one per component, or one placeholder if empty
  type DisplayRow = {
    key: string;
    sectionId: number;
    sectionNumber: number;
    courseCode: string;
    courseName: string;
    isFinalized: boolean;
    component: ExamOverviewComponent | null;
  };

  const displayRows: DisplayRow[] = [];

  for (const row of rows) {
    if (row.components.length === 0) {
      displayRows.push({
        key: `${row.sectionId}-none`,
        sectionId: row.sectionId,
        sectionNumber: row.sectionNumber,
        courseCode: row.courseCode,
        courseName: row.courseName,
        isFinalized: row.isFinalized,
        component: null,
      });
    } else {
      for (const comp of row.components) {
        displayRows.push({
          key: `${row.sectionId}-${comp.id}`,
          sectionId: row.sectionId,
          sectionNumber: row.sectionNumber,
          courseCode: row.courseCode,
          courseName: row.courseName,
          isFinalized: row.isFinalized,
          component: comp,
        });
      }
    }
  }

  return (
    <table className="w-full min-w-[700px]">
      <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
        <tr>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Şb.
          </th>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Ders Kodu
          </th>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Ders Adı
          </th>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Sınav Adı
          </th>
          <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Etki Oranı
          </th>
          <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Durum
          </th>
          <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Tarih
          </th>
        </tr>
      </thead>
      <tbody className="divide-y divide-border/80">
        {displayRows.map((dr) => {
          const { component } = dr;

          let statusBadge: React.ReactNode;
          if (component === null) {
            statusBadge = (
              <Badge
                variant="outline"
                className="bg-slate-50 text-slate-500 border-slate-200 italic"
              >
                Sınav Tanımlı Değil
              </Badge>
            );
          } else if (dr.isFinalized) {
            statusBadge = (
              <Badge
                variant="outline"
                className="bg-emerald-50 text-emerald-700 border-emerald-200"
              >
                Sonuçlandırıldı
              </Badge>
            );
          } else if (component.publishedAt !== null) {
            statusBadge = (
              <Badge
                variant="outline"
                className="bg-blue-50 text-blue-700 border-blue-200"
              >
                İlan Edildi
              </Badge>
            );
          } else {
            statusBadge = (
              <Badge
                variant="outline"
                className="bg-slate-50 text-slate-600 border-slate-200"
              >
                Bekliyor
              </Badge>
            );
          }

          return (
            <tr
              key={dr.key}
              className="cursor-pointer transition-colors hover:bg-primary/5"
              onClick={() => onRowClick(dr.sectionId)}
            >
              <td className="px-4 py-3 text-sm text-muted-foreground">{dr.sectionNumber}</td>
              <td className="px-4 py-3">
                <span className="font-mono text-sm font-semibold text-primary">
                  {dr.courseCode}
                </span>
              </td>
              <td className="px-4 py-3 text-sm font-medium">{dr.courseName}</td>
              <td className="px-4 py-3 text-sm">
                {component !== null ? (
                  component.name
                ) : (
                  <span className="text-muted-foreground italic">—</span>
                )}
              </td>
              <td className="px-4 py-3 text-center text-sm">
                {component !== null ? `%${component.weight}` : '—'}
              </td>
              <td className="px-4 py-3 text-center">{statusBadge}</td>
              <td className="px-4 py-3 text-sm whitespace-nowrap">
                {component !== null ? formatExamDate(component.examDate) : '—'}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

// ── main page ──────────────────────────────────────────────────────────────────

export default function NotGirisiListPage() {
  const navigate = useNavigate();

  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [facultyFilter, setFacultyFilter] = useState<string>(FACULTY_FILTER_ALL);
  const [courseCodeFilter, setCourseCodeFilter] = useState('');
  const [courseNameFilter, setCourseNameFilter] = useState('');
  const [showKontenjan, setShowKontenjan] = useState(false);
  const [showUnfinalized, setShowUnfinalized] = useState(false);
  const [showAllExams, setShowAllExams] = useState(false);

  const termsQuery = useInstructorTerms();
  const sectionsQuery = useInstructorSections(selectedTermId);
  const overviewQuery = useExamsOverview(selectedTermId ?? undefined);

  const handleRowClick = (sectionId: number) => {
    navigate(`${ROUTES.GRADE_ENTRY}/${sectionId}`);
  };

  // ── section list mode (default) ────────────────────────

  const allSections = sectionsQuery.data?.sections ?? [];

  // Distinct faculty names from loaded sections (drives the Fakülte Türü select).
  const facultyOptions = Array.from(
    new Set(allSections.map((s) => s.facultyName).filter((name) => name.length > 0)),
  ).sort((a, b) => a.localeCompare(b, 'tr'));

  const filteredSections = allSections.filter((s) => {
    if (facultyFilter !== FACULTY_FILTER_ALL && s.facultyName !== facultyFilter) {
      return false;
    }
    if (courseCodeFilter.trim() && !includesCi(s.courseCode, courseCodeFilter)) {
      return false;
    }
    if (courseNameFilter.trim() && !includesCi(s.courseName, courseNameFilter)) {
      return false;
    }
    if (showUnfinalized && s.isFinalized) {
      return false;
    }
    return true;
  });

  // ── exam overview mode ─────────────────────────────────
  // Not: ExamOverviewRow'da facultyName yok — fakülte filtresi yalnız varsayılan
  // section listesine uygulanır, "Tüm Sınavları Göster" görünümünde atlanır.

  const allOverviewRows = overviewQuery.data ?? [];

  const filteredOverviewRows = allOverviewRows.filter((r) => {
    if (courseCodeFilter.trim() && !includesCi(r.courseCode, courseCodeFilter)) {
      return false;
    }
    if (courseNameFilter.trim() && !includesCi(r.courseName, courseNameFilter)) {
      return false;
    }
    if (showUnfinalized && r.isFinalized) {
      return false;
    }
    return true;
  });

  const terms = termsQuery.data ?? [];
  return (
    <div className="flex flex-1 flex-col gap-4">
      {/* ── Content card (filtreler + liste tek kart) ── */}
      <Card className="flex flex-col overflow-hidden">
        <InstructorCourseFilters
          terms={terms}
          selectedTermId={selectedTermId}
          onTermChange={setSelectedTermId}
          facultyOptions={facultyOptions}
          facultyFilter={facultyFilter}
          onFacultyChange={setFacultyFilter}
          showKontenjan={showKontenjan}
          onShowKontenjanChange={setShowKontenjan}
          courseCode={courseCodeFilter}
          onCourseCodeChange={setCourseCodeFilter}
          courseName={courseNameFilter}
          onCourseNameChange={setCourseNameFilter}
          showUnfinalized={showUnfinalized}
          onShowUnfinalizedChange={setShowUnfinalized}
          showAllExams={showAllExams}
          onShowAllExamsChange={setShowAllExams}
        />

        <div className="flex flex-col overflow-auto">
          {showAllExams ? (
            <ExamOverviewList
              rows={filteredOverviewRows}
              isLoading={overviewQuery.isLoading}
              isError={overviewQuery.isError}
              onRowClick={handleRowClick}
            />
          ) : (
            <SectionList
              sections={filteredSections}
              totalCount={allSections.length}
              isLoading={sectionsQuery.isLoading}
              isError={sectionsQuery.isError}
              showKontenjan={showKontenjan}
              onRowClick={handleRowClick}
            />
          )}
        </div>
      </Card>

      {/* ── Sınav Tanımlama hint (when in overview mode) ── */}
      {showAllExams && (
        <p className="text-xs text-muted-foreground">
          Sınav eklemek veya düzenlemek için{' '}
          <Button
            variant="link"
            size="sm"
            className="h-auto p-0 text-xs"
            onClick={() => navigate(ROUTES.SINAV_TANIMLAMA)}
          >
            Sınav Tanımlama
          </Button>{' '}
          sayfasını kullanın.
        </p>
      )}
    </div>
  );
}
