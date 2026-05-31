import { useState } from 'react';
import {
  CalendarCheck,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Copy,
  FolderOpen,
  Printer,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import type { InstructorSection } from '../../api/instructor.api';
import { buildClipboardText } from './courseClipboard';

// ── constants ─────────────────────────────────────────────────
const DEFAULT_PAGE_SIZE = 10;

// ── Z/S mandatory badge ───────────────────────────────────────
export function MandatoryBadge({ isMandatory }: { isMandatory: boolean }) {
  return (
    <Badge
      variant="outline"
      className={
        isMandatory
          ? 'bg-emerald-50 text-emerald-700 border-emerald-200 font-mono text-[11px] px-1.5 py-0'
          : 'bg-slate-50 text-slate-500 border-slate-200 font-mono text-[11px] px-1.5 py-0'
      }
    >
      {isMandatory ? 'Z' : 'S'}
    </Badge>
  );
}

// ── pagination bar ────────────────────────────────────────────
function PaginationBar({
  filteredCount,
  totalCount,
  page,
  pageCount,
  onPageChange,
  visibleSections,
}: {
  filteredCount: number;
  totalCount: number;
  page: number;
  pageCount: number;
  onPageChange: (p: number) => void;
  visibleSections: InstructorSection[];
}) {
  const handleCopy = () => {
    void navigator.clipboard.writeText(buildClipboardText(visibleSections));
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border/80 bg-muted/20 px-4 py-2.5">
      <div className="flex items-center gap-1">
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8"
          title="Yazdır"
          onClick={() => window.print()}
        >
          <Printer className="h-4 w-4" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8"
          title="Listeyi kopyala"
          onClick={handleCopy}
        >
          <Copy className="h-4 w-4" />
        </Button>
        <span className="ml-2 text-xs text-muted-foreground tabular-nums">
          {filteredCount} / {totalCount} ders
        </span>
      </div>

      <div className="flex items-center gap-1">
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          title="İlk sayfa"
          disabled={page <= 1}
          onClick={() => onPageChange(1)}
        >
          <ChevronsLeft className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          title="Önceki sayfa"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="px-2 text-xs text-muted-foreground tabular-nums">
          Sayfa {page} / {pageCount}
        </span>
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          title="Sonraki sayfa"
          disabled={page >= pageCount}
          onClick={() => onPageChange(page + 1)}
        >
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          title="Son sayfa"
          disabled={page >= pageCount}
          onClick={() => onPageChange(pageCount)}
        >
          <ChevronsRight className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}

// ── shared instructor course list ─────────────────────────────
interface InstructorCourseTableProps {
  /** Already-filtered full list; pagination handled inside. */
  sections: InstructorSection[];
  /** Pre-filter total, for the count display. */
  totalCount: number;
  isLoading: boolean;
  /** Show the Kon column when true. */
  showKontenjan: boolean;
  selectedSectionId: number | null;
  onSelect: (sectionId: number) => void;
  pageSize?: number;
}

export function InstructorCourseTable({
  sections,
  totalCount,
  isLoading,
  showKontenjan,
  selectedSectionId,
  onSelect,
  pageSize = DEFAULT_PAGE_SIZE,
}: InstructorCourseTableProps) {
  const [page, setPage] = useState(1);

  // Reset to first page when the filtered list identity changes — derived
  // during render (React's recommended pattern over an effect+setState).
  const [prevSections, setPrevSections] = useState(sections);
  if (sections !== prevSections) {
    setPrevSections(sections);
    setPage(1);
  }

  const pageCount = Math.max(1, Math.ceil(sections.length / pageSize));
  const currentPage = Math.min(page, pageCount);
  const pageStart = (currentPage - 1) * pageSize;
  const visibleSections = sections.slice(pageStart, pageStart + pageSize);

  const handlePageChange = (p: number) => setPage(Math.min(Math.max(1, p), pageCount));

  // Column span for empty-state rows (10 base + 1 when Kon is shown).
  const isEmpty = sections.length === 0;

  return (
    <Card className="flex flex-col overflow-hidden">
      {/* Horizontally scrollable table */}
      <div className="overflow-x-auto">
        {isLoading ? (
          <div className="space-y-2 p-4">
            {[1, 2, 3].map((i) => <Skeleton key={i} className="h-12 w-full" />)}
          </div>
        ) : isEmpty ? (
          <div className="flex flex-col items-center justify-center px-4 py-12 text-center">
            <FolderOpen className="mb-3 h-10 w-10 text-muted-foreground/30" />
            <p className="text-sm font-medium text-muted-foreground">
              {totalCount === 0
                ? 'Bu dönem size atanmış ders bulunmamaktadır.'
                : 'Filtreye uyan ders bulunamadı.'}
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[960px]">
            <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
              <tr>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Şb.</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ders Kodu</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ders Adı</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">T+U</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Z</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Krd</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">AKTS</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Fakülte</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Program</th>
                {showKontenjan && (
                  <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Kon</th>
                )}
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ders Durumu</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/80">
              {visibleSections.map((s: InstructorSection) => {
                const isSelected = s.sectionId === selectedSectionId;
                return (
                  <tr
                    key={s.sectionId}
                    className={`cursor-pointer transition-colors hover:bg-primary/5 ${isSelected ? 'bg-primary/8' : ''}`}
                    onClick={() => onSelect(s.sectionId)}
                  >
                    <td className="px-4 py-3 text-center text-sm tabular-nums text-muted-foreground">{s.sectionNumber}</td>
                    <td className="px-4 py-3">
                      <span className="font-mono text-sm font-semibold text-primary">{s.courseCode}</span>
                    </td>
                    <td className="px-4 py-3 text-sm font-medium">{s.courseName}</td>
                    <td className="px-4 py-3 text-center text-sm tabular-nums text-muted-foreground">
                      {s.theoryHours}+{s.labHours}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <MandatoryBadge isMandatory={s.isMandatory} />
                    </td>
                    <td className="px-4 py-3 text-center text-sm tabular-nums">{s.nationalCredit}</td>
                    <td className="px-4 py-3 text-center text-sm tabular-nums">{s.ects}</td>
                    <td className="px-4 py-3 text-xs leading-tight">
                      {s.facultyName || <span className="text-muted-foreground">—</span>}
                    </td>
                    <td className="px-4 py-3 text-xs leading-tight text-muted-foreground">
                      {s.programName || '—'}
                    </td>
                    {showKontenjan && (
                      <td className="px-4 py-3 text-center text-sm tabular-nums text-muted-foreground">
                        {s.enrolledCount}/{s.capacity}
                      </td>
                    )}
                    <td className="px-4 py-3 text-center">
                      <Badge
                        variant="outline"
                        className={s.isFinalized
                          ? 'bg-emerald-50 text-emerald-700 border-emerald-200 inline-flex items-center gap-1'
                          : 'bg-slate-50 text-slate-600 border-slate-200 inline-flex items-center gap-1'}
                      >
                        <CalendarCheck className="h-3 w-3" />
                        {s.isFinalized ? 'Kesinleşti' : 'Aktif'}
                      </Badge>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination bar */}
      {!isLoading && !isEmpty && (
        <PaginationBar
          filteredCount={sections.length}
          totalCount={totalCount}
          page={currentPage}
          pageCount={pageCount}
          onPageChange={handlePageChange}
          visibleSections={visibleSections}
        />
      )}
    </Card>
  );
}
