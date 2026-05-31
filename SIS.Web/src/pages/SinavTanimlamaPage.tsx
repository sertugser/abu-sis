import { useMemo, useState } from 'react';
import {
  FolderOpen,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { useInstructorSections } from '../hooks/useInstructorSections';
import {
  useSectionExams,
  useDeleteSectionExam,
  useExamConfig,
} from '../hooks/useSectionExams';
import { SinavEkleDialog } from '../components/sinav-tanimlama/SinavEkleDialog';
import { InstructorCourseTable } from '../components/instructor/InstructorCourseTable';
import { computeAnnounceStatus } from '../components/instructor/examStatusUtils';
import {
  AnnounceStatusCell,
  ExamStatusLegend,
} from '../components/instructor/examStatus';
import type { InstructorSection } from '../api/instructor.api';
import type { SectionExam } from '../types/sectionExam';

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

// ── main page ─────────────────────────────────────────────────
export default function SinavTanimlamaPage() {
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  const { data, isLoading } = useInstructorSections();
  const sections = useMemo(() => data?.sections ?? [], [data]);

  const selectedSection = sections.find((s) => s.sectionId === selectedSectionId) ?? null;

  return (
    <div className="flex flex-1 flex-col gap-4">
      {/* Section list (Proliz görünümü — ders listesi, filtre yok) */}
      <InstructorCourseTable
        sections={sections}
        totalCount={sections.length}
        isLoading={isLoading}
        showKontenjan={true}
        selectedSectionId={selectedSectionId}
        onSelect={setSelectedSectionId}
      />

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
