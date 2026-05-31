import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ClipboardEdit, FolderOpen } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  useInstructorSections,
  useInstructorTerms,
} from '../hooks/useInstructorSections';
import { useExamsOverview } from '../hooks/useGradeData';
import { useSectionExams, useExamConfig } from '../hooks/useSectionExams';
import type { InstructorSection } from '../api/instructor.api';
import type { ExamOverviewRow, ExamOverviewComponent } from '../types/grade';
import { ROUTES } from '../lib/constants';
import {
  InstructorCourseFilters,
  FACULTY_FILTER_ALL,
} from '../components/instructor/InstructorCourseFilters';
import { InstructorCourseTable } from '../components/instructor/InstructorCourseTable';
import { computeAnnounceStatus } from '../components/instructor/examStatusUtils';
import {
  AnnounceStatusCell,
  ExamStatusLegend,
} from '../components/instructor/examStatus';

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

const DEFAULT_ANNOUNCE_DEADLINE_DAYS = 7;

const TYPE_LABELS: Record<string, string> = {
  Midterm: 'Ara Sınav',
  Final: 'Yarıyıl Sonu',
  Other: 'Diğer',
};

const TYPE_BADGE: Record<string, string> = {
  Midterm: 'bg-indigo-50 text-indigo-700 border-indigo-200',
  Final: 'bg-violet-50 text-violet-700 border-violet-200',
  Other: 'bg-slate-50 text-slate-600 border-slate-200',
};

// ── Dersin Sınavları panel (read-only → grade entry) ─────────────────────────────

interface SectionExamPanelProps {
  section: InstructorSection;
  onExamClick: (sectionId: number) => void;
}

function SectionExamPanel({ section, onExamClick }: SectionExamPanelProps) {
  const sectionId = section.sectionId;
  const exams = useSectionExams(sectionId);
  const examConfig = useExamConfig(sectionId);

  const announceDeadlineDays =
    examConfig.data?.announceDeadlineDays ?? DEFAULT_ANNOUNCE_DEADLINE_DAYS;
  // Render-stable "now" reference for the announce countdown.
  const [now] = useState(() => Date.now());

  const examRows = exams.data ?? [];

  return (
    <Card className="flex flex-col overflow-hidden">
      {/* Header */}
      <div className="border-b border-border/80 bg-muted/20 px-5 py-3">
        <p className="text-sm font-semibold">
          Dersin Sınavları —{' '}
          <span className="font-mono text-primary">{section.courseCode}</span>{' '}
          {section.courseName}
          <span className="ml-2 text-xs font-normal text-muted-foreground">
            Şb. {section.sectionNumber}
          </span>
        </p>
        <p className="text-xs text-muted-foreground">
          Not girmek için bir sınava tıklayın.
        </p>
      </div>

      {/* Table / states */}
      <div className="flex flex-1 flex-col overflow-auto">
        {exams.isLoading ? (
          <div className="space-y-2 p-4">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : exams.isError ? (
          <div className="flex flex-1 items-center justify-center p-8 text-sm text-destructive">
            Sınav verileri yüklenemedi.
          </div>
        ) : examRows.length === 0 ? (
          <div className="flex flex-1 flex-col items-center justify-center px-4 py-12 text-center">
            <FolderOpen className="mb-3 h-10 w-10 text-muted-foreground/30" />
            <p className="text-sm font-medium">Tanımlı sınav yok</p>
            <p className="mt-1 text-xs text-muted-foreground">
              Sınavlar Sınav Tanımlama sayfasından tanımlanır.
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[700px]">
            <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
              <tr>
                <th className="w-12 px-2 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  <span className="sr-only">Not Gir</span>
                </th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Sınav Adı
                </th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Tür
                </th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Etki
                </th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Tarih
                </th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  İlan Durumu
                </th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Derslikler
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/80">
              {examRows.map((exam) => {
                const status = computeAnnounceStatus(
                  exam.examDate,
                  exam.isAnnouncedToStudents,
                  announceDeadlineDays,
                  now,
                );
                return (
                  <tr
                    key={exam.id}
                    className="cursor-pointer transition-colors hover:bg-primary/5"
                    onClick={() => onExamClick(sectionId)}
                  >
                    <td className="px-2 py-3 text-center">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        title="Not Gir"
                        onClick={(e) => {
                          e.stopPropagation();
                          onExamClick(sectionId);
                        }}
                      >
                        <ClipboardEdit className="h-4 w-4" />
                        <span className="sr-only">Not Gir</span>
                      </Button>
                    </td>
                    <td className="px-4 py-3 text-sm font-medium">{exam.componentName}</td>
                    <td className="px-4 py-3">
                      <Badge variant="outline" className={TYPE_BADGE[exam.componentType] ?? ''}>
                        {TYPE_LABELS[exam.componentType] ?? exam.componentType}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-center text-sm">%{exam.weight}</td>
                    <td className="px-4 py-3 text-sm whitespace-nowrap">
                      {formatExamDate(exam.examDate)}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <AnnounceStatusCell status={status} />
                    </td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">
                      {exam.classrooms.length > 0
                        ? exam.classrooms.map((c) => c.classroomName).join(', ')
                        : '—'}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {/* Status legend */}
      <ExamStatusLegend />
    </Card>
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
          <th className="w-12 px-2 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            <span className="sr-only">Not Gir</span>
          </th>
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
              <td className="px-2 py-3 text-center">
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8"
                  title="Not Gir"
                  onClick={(e) => {
                    e.stopPropagation();
                    onRowClick(dr.sectionId);
                  }}
                >
                  <ClipboardEdit className="h-4 w-4" />
                  <span className="sr-only">Not Gir</span>
                </Button>
              </td>
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
  const [searchTerm, setSearchTerm] = useState('');
  const [showKontenjan, setShowKontenjan] = useState(false);
  const [showUnfinalized, setShowUnfinalized] = useState(false);
  const [showAllExams, setShowAllExams] = useState(false);
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  const termsQuery = useInstructorTerms();
  const sectionsQuery = useInstructorSections(selectedTermId);
  const overviewQuery = useExamsOverview(selectedTermId ?? undefined);

  const goToGradeEntry = (sectionId: number) => {
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
    if (
      searchTerm.trim() &&
      !includesCi(s.courseCode, searchTerm) &&
      !includesCi(s.courseName, searchTerm)
    ) {
      return false;
    }
    if (showUnfinalized && s.isFinalized) {
      return false;
    }
    return true;
  });

  const selectedSection =
    selectedSectionId != null
      ? allSections.find((s) => s.sectionId === selectedSectionId) ?? null
      : null;

  // ── exam overview mode ─────────────────────────────────
  // Not: ExamOverviewRow'da facultyName yok — fakülte filtresi yalnız varsayılan
  // section listesine uygulanır, "Tüm Sınavları Göster" görünümünde atlanır.

  const allOverviewRows = overviewQuery.data ?? [];

  const filteredOverviewRows = allOverviewRows.filter((r) => {
    if (
      searchTerm.trim() &&
      !includesCi(r.courseCode, searchTerm) &&
      !includesCi(r.courseName, searchTerm)
    ) {
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
      {/* ── Filtre + liste tek kart ── */}
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
          search={searchTerm}
          onSearchChange={setSearchTerm}
          showUnfinalized={showUnfinalized}
          onShowUnfinalizedChange={setShowUnfinalized}
          showAllExams={showAllExams}
          onShowAllExamsChange={setShowAllExams}
        />

        {/* Ders listesi her zaman görünür — "Tüm Sınavları Göster" toggle'ından etkilenmez. */}
        <InstructorCourseTable
          sections={filteredSections}
          totalCount={allSections.length}
          isLoading={sectionsQuery.isLoading}
          showKontenjan={showKontenjan}
          selectedSectionId={selectedSectionId}
          onSelect={setSelectedSectionId}
        />
      </Card>

      {/* ── Alt alan: "Dersin Sınavları" ── */}
      {showAllExams ? (
        <Card className="flex flex-col overflow-hidden">
          <div className="flex items-center justify-between gap-2 border-b border-border/80 bg-muted/20 px-5 py-3">
            <p className="text-sm font-semibold">Tüm Sınavlar</p>
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
          </div>
          <div className="flex flex-col overflow-auto">
            <ExamOverviewList
              rows={filteredOverviewRows}
              isLoading={overviewQuery.isLoading}
              isError={overviewQuery.isError}
              onRowClick={goToGradeEntry}
            />
          </div>
          <ExamStatusLegend />
        </Card>
      ) : selectedSection != null ? (
        /* ── Dersin Sınavları panel (when a course is selected) ── */
        <SectionExamPanel section={selectedSection} onExamClick={goToGradeEntry} />
      ) : (
        <div className="flex items-center justify-center rounded-xl border border-dashed border-border py-12 text-sm text-muted-foreground">
          Yukarıdan bir ders seçin ya da "Tüm Sınavları Göster" ile tüm sınavları
          listeleyin.
        </div>
      )}
    </div>
  );
}
