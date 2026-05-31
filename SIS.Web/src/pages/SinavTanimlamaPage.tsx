import { useState } from 'react';
import { FolderOpen, Loader2, Plus, Search, Trash2, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { useInstructorSections } from '../hooks/useInstructorSections';
import { useSectionExams, useDeleteSectionExam } from '../hooks/useSectionExams';
import { SinavEkleDialog } from '../components/sinav-tanimlama/SinavEkleDialog';
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

/** "Fakülte / Program" cell — shows only the non-empty parts */
function FacultyProgramCell({ facultyName, programName }: { facultyName: string; programName: string }) {
  const parts = [facultyName, programName].filter(Boolean);
  if (parts.length === 0) return <span className="text-muted-foreground">—</span>;
  return (
    <span className="text-xs leading-tight">
      {parts.length === 2 ? (
        <>
          <span className="font-medium">{facultyName}</span>
          <span className="mx-1 text-muted-foreground">·</span>
          <span className="text-muted-foreground">{programName}</span>
        </>
      ) : (
        <span>{parts[0]}</span>
      )}
    </span>
  );
}

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

// ── status legend ─────────────────────────────────────────────
function ExamStatusLegend() {
  return (
    <div className="flex items-center gap-3 border-t border-border/60 px-5 py-2.5 bg-muted/10">
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-500" />
        <span className="text-xs text-muted-foreground">İlan Edildi</span>
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-rose-500" />
        <span className="text-xs text-muted-foreground">Sonuçlandırıldı</span>
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="inline-block h-2.5 w-2.5 rounded-full bg-slate-400" />
        <span className="text-xs text-muted-foreground">Bekliyor</span>
      </span>
    </div>
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
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<SectionExam | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<SectionExam | null>(null);

  const openNew = () => { setEditTarget(null); setDialogOpen(true); };
  const openEdit = (exam: SectionExam) => { setEditTarget(exam); setDialogOpen(true); };

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
        ) : (exams.data ?? []).length === 0 ? (
          <div className="flex flex-1 flex-col items-center justify-center px-4 py-12 text-center">
            <FolderOpen className="mb-3 h-10 w-10 text-muted-foreground/30" />
            <p className="text-sm font-medium">Tanımlı sınav yok</p>
            <p className="mt-1 text-xs text-muted-foreground">
              "Sınav Ekle" butonuyla ilk sınavı ekleyin.
            </p>
          </div>
        ) : (
          <table className="w-full min-w-[640px]">
            <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
              <tr>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Sınav Adı</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Tür</th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Etki</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Tarih</th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Süre</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Derslikler</th>
                <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">İlan</th>
                <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/80">
              {(exams.data ?? []).map((exam) => (
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
                    {exam.durationMinutes != null ? `${exam.durationMinutes} dk` : '—'}
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">
                    {exam.classrooms.length > 0
                      ? exam.classrooms.map((c) => c.classroomName).join(', ')
                      : '—'}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge
                      variant="outline"
                      className={exam.isAnnouncedToStudents
                        ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                        : 'bg-slate-50 text-slate-500 border-slate-200'}
                    >
                      {exam.isAnnouncedToStudents ? 'İlan edildi' : 'Bekliyor'}
                    </Badge>
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
              ))}
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
  const { data, isLoading } = useInstructorSections();
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  const sections = data?.sections ?? [];

  const filtered = sections.filter((s) => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return `${s.courseCode} ${s.courseName}`.toLowerCase().includes(q);
  });

  const selectedSection = sections.find((s) => s.sectionId === selectedSectionId) ?? null;

  return (
    <div className="flex flex-1 flex-col gap-4">
      {/* Section list — arama + ders listesi tek kart */}
      <Card className="flex flex-col overflow-hidden">
        <div className="border-b border-border/80 bg-muted/20 p-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Ders kodu veya ders adı ile ara…"
              className="pl-9 text-[15px]"
            />
          </div>
        </div>

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
            <table className="w-full min-w-[900px]">
              <thead className="sticky top-0 z-10 border-b border-border/80 bg-muted/40">
                <tr>
                  <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Şb.</th>
                  <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ders Kodu</th>
                  <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ders Adı</th>
                  <th className="px-3 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">T+U</th>
                  <th className="px-3 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Z</th>
                  <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Krd</th>
                  <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">AKTS</th>
                  <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">Fakülte / Program</th>
                  <th className="px-3 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Kon</th>
                  <th className="px-3 py-2.5 text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">Durum</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/80">
                {filtered.map((s: InstructorSection) => {
                  const isSelected = s.sectionId === selectedSectionId;
                  return (
                    <tr
                      key={s.sectionId}
                      className={`cursor-pointer transition-colors hover:bg-primary/5 ${isSelected ? 'bg-primary/8' : ''}`}
                      onClick={() => setSelectedSectionId(s.sectionId)}
                    >
                      <td className="px-3 py-2.5 text-right text-sm tabular-nums text-muted-foreground">{s.sectionNumber}</td>
                      <td className="px-3 py-2.5">
                        <span className="font-mono text-sm font-semibold text-primary">{s.courseCode}</span>
                      </td>
                      <td className="px-3 py-2.5 text-sm font-medium">{s.courseName}</td>
                      <td className="px-3 py-2.5 text-center text-sm tabular-nums text-muted-foreground">
                        {s.theoryHours}+{s.labHours}
                      </td>
                      <td className="px-3 py-2.5 text-center">
                        <MandatoryBadge isMandatory={s.isMandatory} />
                      </td>
                      <td className="px-3 py-2.5 text-right text-sm tabular-nums">{s.nationalCredit}</td>
                      <td className="px-3 py-2.5 text-right text-sm tabular-nums">{s.ects}</td>
                      <td className="px-3 py-2.5">
                        <FacultyProgramCell facultyName={s.facultyName} programName={s.programName} />
                      </td>
                      <td className="px-3 py-2.5 text-center text-sm tabular-nums text-muted-foreground">
                        {s.enrolledCount}/{s.capacity}
                      </td>
                      <td className="px-3 py-2.5 text-center">
                        <Badge
                          variant="outline"
                          className={s.isFinalized
                            ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                            : 'bg-slate-50 text-slate-600 border-slate-200'}
                        >
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
