import { useMemo, useState } from 'react';
import {
  CalendarCheck,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Copy,
  FolderOpen,
  Loader2,
  Pencil,
  Plus,
  Printer,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  useInstructorSections,
  useInstructorTerms,
} from '../hooks/useInstructorSections';
import {
  useSectionExams,
  useDeleteSectionExam,
  useExamConfig,
} from '../hooks/useSectionExams';
import { SinavEkleDialog } from '../components/sinav-tanimlama/SinavEkleDialog';
import {
  InstructorCourseFilters,
  FACULTY_FILTER_ALL,
} from '../components/instructor/InstructorCourseFilters';
import type { InstructorSection } from '../api/instructor.api';
import type { SectionExam } from '../types/sectionExam';

// ── constants ─────────────────────────────────────────────────
const PAGE_SIZE = 10;
const MS_PER_HOUR = 1000 * 60 * 60;
const MS_PER_DAY = MS_PER_HOUR * 24;

// ── helpers ───────────────────────────────────────────────────
function formatExamDate(iso: string): string {
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

type AnnounceStatus =
  | { kind: 'announced' }
  | { kind: 'countdown'; days: number; hours: number }
  | { kind: 'overdue' };

/**
 * Pure announce-status helper.
 * deadline = examDate − announceDeadlineDays. Compared against `now`.
 */
function computeAnnounceStatus(
  examDateIso: string,
  isAnnounced: boolean,
  announceDeadlineDays: number,
  now: number,
): AnnounceStatus {
  if (isAnnounced) return { kind: 'announced' };

  const deadline = new Date(examDateIso).getTime() - announceDeadlineDays * MS_PER_DAY;
  const remaining = deadline - now;
  if (remaining <= 0) return { kind: 'overdue' };

  const days = Math.floor(remaining / MS_PER_DAY);
  const hours = Math.floor((remaining % MS_PER_DAY) / MS_PER_HOUR);
  return { kind: 'countdown', days, hours };
}

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

/** Z/S mandatory badge */
function MandatoryBadge({ isMandatory }: { isMandatory: boolean }) {
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

// ── delete confirm dialog ─────────────────────────────────────
function extractDeleteError(err: unknown): string {
  if (err && typeof err === 'object') {
    const response = (err as { response?: { data?: { error?: string; message?: string } } }).response;
    if (response?.data?.error) return response.data.error;
    if (response?.data?.message) return response.data.message;
  }
  return 'Sınav silinemedi.';
}

function DeleteConfirmDialog({
  target,
  sectionId,
  onOpenChange,
}: {
  target: SectionExam | null;
  sectionId: number;
  onOpenChange: (v: boolean) => void;
}) {
  const deleteMut = useDeleteSectionExam(sectionId);

  return (
    <Dialog
      open={target !== null}
      onOpenChange={(v) => {
        if (!v && deleteMut.isError) return;
        onOpenChange(v);
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Sınavı Sil</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          <span className="font-medium text-foreground">{target?.componentName}</span> sınavı
          silinecek. Bu işlem geri alınamaz.
        </p>
        {deleteMut.isError && (
          <p className="text-sm text-destructive">{extractDeleteError(deleteMut.error)}</p>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>İptal</Button>
          <Button
            variant="destructive"
            disabled={deleteMut.isPending}
            onClick={() => {
              if (!target) return;
              deleteMut.mutate(target.id, { onSuccess: () => onOpenChange(false) });
            }}
          >
            {deleteMut.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Sil
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── status legend (3-item) ────────────────────────────────────
function ExamStatusLegend() {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 border-t border-border/60 px-5 py-2.5 bg-muted/10">
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-500" />
        <span className="text-xs text-muted-foreground">İlan Edildi</span>
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-rose-500" />
        <span className="text-xs text-muted-foreground">Sonuçlandırıldı</span>
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-red-600" />
        <span className="text-xs text-muted-foreground">Sınav İlan Süresi Geçti</span>
      </span>
    </div>
  );
}

// ── announce-status cell ──────────────────────────────────────
function AnnounceStatusCell({ status }: { status: AnnounceStatus }) {
  if (status.kind === 'announced') {
    return (
      <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200">
        İlan Edildi
      </Badge>
    );
  }
  if (status.kind === 'overdue') {
    return (
      <Badge variant="outline" className="bg-red-50 text-red-700 border-red-200">
        Sınav İlan Süresi Geçti
      </Badge>
    );
  }
  return (
    <span className="text-xs text-amber-700">
      İlan için son {status.days}g {status.hours}s
    </span>
  );
}

// ── exam table sub-component ──────────────────────────────────
function ExamTable({
  sectionId,
  selectedSection,
}: {
  sectionId: number;
  selectedSection: InstructorSection;
}) {
  const exams = useSectionExams(sectionId);
  const examConfig = useExamConfig(sectionId);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<SectionExam | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<SectionExam | null>(null);

  const announceDeadlineDays = examConfig.data?.announceDeadlineDays ?? 7;
  // Capture a render-stable "now" once when the table mounts (countdown reference).
  const [now] = useState(() => Date.now());

  const openNew = () => { setEditTarget(null); setDialogOpen(true); };
  const openEdit = (exam: SectionExam) => { setEditTarget(exam); setDialogOpen(true); };

  const examRows = exams.data ?? [];

  return (
    <Card className="flex flex-col overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-border/80 bg-muted/20 px-5 py-3">
        <div>
          <p className="text-sm font-semibold">
            <span className="font-mono text-primary">{selectedSection.courseCode}</span>
            {' '}{selectedSection.courseName}
            <span className="ml-2 text-xs text-muted-foreground font-normal">
              Şb. {selectedSection.sectionNumber}
            </span>
          </p>
          <p className="text-xs text-muted-foreground">Dersin Sınavları</p>
        </div>
        <Button size="sm" className="h-8" onClick={openNew}>
          <Plus className="mr-1.5 h-3.5 w-3.5" />
          Sınav Ekle
        </Button>
      </div>

      {/* Table / states */}
      <div className="flex flex-1 flex-col overflow-auto">
        {exams.isLoading ? (
          <div className="space-y-2 p-4">
            {[1, 2, 3].map((i) => <Skeleton key={i} className="h-12 w-full" />)}
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
              "Sınav Ekle" butonuyla ilk sınavı ekleyin.
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[760px]">
            <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
              <tr>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Sınav Adı</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Tür</th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Etki</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Tarih</th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Süre (dk)</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Derslikler</th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">İlan Durumu</th>
                <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">İşlem</th>
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
                  <tr key={exam.id} className="transition-colors hover:bg-primary/5">
                    <td className="px-4 py-3 font-medium text-sm">{exam.componentName}</td>
                    <td className="px-4 py-3">
                      <Badge variant="outline" className={TYPE_BADGE[exam.componentType] ?? ''}>
                        {TYPE_LABELS[exam.componentType] ?? exam.componentType}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 text-center text-sm">%{exam.weight}</td>
                    <td className="px-4 py-3 text-sm whitespace-nowrap">{formatExamDate(exam.examDate)}</td>
                    <td className="px-4 py-3 text-center text-sm text-muted-foreground">
                      {exam.durationMinutes != null ? exam.durationMinutes : '—'}
                    </td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">
                      {exam.classrooms.length > 0
                        ? exam.classrooms.map((c) => c.classroomName).join(', ')
                        : '—'}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <AnnounceStatusCell status={status} />
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 hover:bg-primary/5 hover:text-primary"
                          title="Düzenle"
                          onClick={() => openEdit(exam)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                          title="Sil"
                          onClick={() => setDeleteTarget(exam)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
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

      {/* Dialogs */}
      <SinavEkleDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        sectionId={sectionId}
        editTarget={editTarget}
      />
      <DeleteConfirmDialog
        target={deleteTarget}
        sectionId={sectionId}
        onOpenChange={(v) => { if (!v) setDeleteTarget(null); }}
      />
    </Card>
  );
}

// ── pagination bar ────────────────────────────────────────────
function buildClipboardText(sections: InstructorSection[]): string {
  const header = ['Şb.', 'Ders Kodu', 'Ders Adı', 'Fakülte', 'Program', 'Durum'].join('\t');
  const rows = sections.map((s) =>
    [
      s.sectionNumber,
      s.courseCode,
      s.courseName,
      s.facultyName,
      s.programName,
      s.isFinalized ? 'Kesinleşti' : 'Aktif',
    ].join('\t'),
  );
  return [header, ...rows].join('\n');
}

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

// ── main page ─────────────────────────────────────────────────
export default function SinavTanimlamaPage() {
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [facultyFilter, setFacultyFilter] = useState<string>(FACULTY_FILTER_ALL);
  const [courseCodeFilter, setCourseCodeFilter] = useState('');
  const [courseNameFilter, setCourseNameFilter] = useState('');
  const [showKontenjan, setShowKontenjan] = useState(false);
  const [showUnfinalized, setShowUnfinalized] = useState(false);
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);
  const [page, setPage] = useState(1);

  const termsQuery = useInstructorTerms();
  const { data, isLoading } = useInstructorSections(selectedTermId);

  const sections = useMemo(() => data?.sections ?? [], [data]);
  const terms = termsQuery.data ?? [];

  // Distinct, non-empty faculty names (drives the Fakülte Türü select).
  const facultyOptions = useMemo(
    () =>
      Array.from(
        new Set(sections.map((s) => s.facultyName).filter((name) => name.length > 0)),
      ).sort((a, b) => a.localeCompare(b, 'tr')),
    [sections],
  );

  const filtered = useMemo(
    () =>
      sections.filter((s) => {
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
      }),
    [sections, facultyFilter, courseCodeFilter, courseNameFilter, showUnfinalized],
  );

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, pageCount);
  const pageStart = (currentPage - 1) * PAGE_SIZE;
  const visibleSections = filtered.slice(pageStart, pageStart + PAGE_SIZE);

  const selectedSection = sections.find((s) => s.sectionId === selectedSectionId) ?? null;

  // Reset to first page whenever the filter result set changes.
  const handlePageChange = (p: number) => setPage(Math.min(Math.max(1, p), pageCount));

  return (
    <div className="flex flex-1 flex-col gap-4">
      {/* Section list — Proliz filtre + ders listesi tek kart */}
      <Card className="flex flex-col overflow-hidden">
        <InstructorCourseFilters
          terms={terms}
          selectedTermId={selectedTermId}
          onTermChange={(id) => { setSelectedTermId(id); setPage(1); }}
          facultyOptions={facultyOptions}
          facultyFilter={facultyFilter}
          onFacultyChange={(v) => { setFacultyFilter(v); setPage(1); }}
          showKontenjan={showKontenjan}
          onShowKontenjanChange={setShowKontenjan}
          courseCode={courseCodeFilter}
          onCourseCodeChange={(v) => { setCourseCodeFilter(v); setPage(1); }}
          courseName={courseNameFilter}
          onCourseNameChange={(v) => { setCourseNameFilter(v); setPage(1); }}
          showUnfinalized={showUnfinalized}
          onShowUnfinalizedChange={(v) => { setShowUnfinalized(v); setPage(1); }}
          onSearch={() => { /* live filter — button is cosmetic (Proliz parity) */ }}
        />

        {/* Horizontally scrollable table */}
        <div className="overflow-x-auto">
          {isLoading ? (
            <div className="space-y-2 p-4">
              {[1, 2, 3].map((i) => <Skeleton key={i} className="h-12 w-full" />)}
            </div>
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center justify-center px-4 py-12 text-center">
              <FolderOpen className="mb-3 h-10 w-10 text-muted-foreground/30" />
              <p className="text-sm font-medium text-muted-foreground">
                {sections.length === 0
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
                      onClick={() => setSelectedSectionId(s.sectionId)}
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
        {!isLoading && filtered.length > 0 && (
          <PaginationBar
            filteredCount={filtered.length}
            totalCount={sections.length}
            page={currentPage}
            pageCount={pageCount}
            onPageChange={handlePageChange}
            visibleSections={visibleSections}
          />
        )}
      </Card>

      {/* Exam table — shown only when a section is selected */}
      {selectedSectionId != null && selectedSection != null ? (
        <ExamTable sectionId={selectedSectionId} selectedSection={selectedSection} />
      ) : selectedSectionId != null ? null : (
        <div className="flex items-center justify-center rounded-xl border border-dashed border-border py-12 text-sm text-muted-foreground">
          Yukarıdan bir ders seçerek sınavlarını görüntüleyin ve düzenleyin.
        </div>
      )}
    </div>
  );
}
