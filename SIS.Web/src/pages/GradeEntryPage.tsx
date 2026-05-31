// SIS.Web/src/pages/GradeEntryPage.tsx
// Proliz tarzı not giriş ekranı. Sınav tanımı SALT-OKUNUR (tanım Sınav Tanımlama'da).
// Tüm bileşenler aynı anda düzenlenebilir kolon; Kaydet değişen tüm bileşenleri tek tek gönderir.

import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ArrowLeft, XCircle, Search, Info } from 'lucide-react';
import {
  useGrades,
  useComponents,
  useGradingCriteria,
  useSubmitGrades,
  useFinalize,
  useUnfinalize,
  useUpdateGradingCriteria,
  useDownloadTemplate,
  useSetExamAttendance,
  usePublishComponent,
  useUnpublishComponent,
} from '../hooks/useGradeData';
import { usePermissions } from '../context/PermissionContext';
import { useToast } from '@/components/ui/global-toast';
import { ButunlemePanel, MazeretPanel } from '../components/grade/MakeupSections';
import { ClassStatsPanel } from '../components/grade/GradeStatsPanel';
import { GradeStudentTable } from '../components/grade/GradeStudentTable';
import { GradeReportDocument } from '../components/grade/GradeReportDocument';
import { GradeCourseHeader } from '../components/grade/GradeCourseHeader';
import { GradeToolbar } from '../components/grade/GradeToolbar';
import { ScorePasteDialog } from '../components/grade/ScorePasteDialog';
import { ExcelImportDialog } from '../components/grade/ExcelImportDialog';
import { GradingCriteriaDialog } from '../components/grade/GradingCriteriaDialog';
import { FinalizeDialog } from '../components/grade/FinalizeDialog';
import type { BulkAttendanceItem } from '../components/grade/BulkAttendanceMenu';
import { getErrorMessage } from '../lib/apiError';
import { resolveGrade } from '../components/grade/gradeDisplay';
import type {
  ExamAttendance,
  FinalizePreviewItem,
  GradeComponent,
  GradingCriteria,
  StudentScore,
} from '../types/grade';

const DEFAULT_CRITERIA: GradingCriteria = {
  aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
};

/** Düzenlenmiş puanlar: componentId → (studentId → string). */
type ScoreEdits = Record<number, Record<number, string>>;
/** İyimser katılım: componentId → (studentId → attendance). */
type AttendanceEdits = Record<number, Record<number, ExamAttendance>>;

function isInvalidScore(score: string): boolean {
  if (score === '') return false;
  const n = Number(score);
  return Number.isNaN(n) || n < 0 || n > 100;
}

export default function GradeEntryPage() {
  const { sectionId } = useParams<{ sectionId: string }>();
  const navigate = useNavigate();
  const id = Number(sectionId);

  const { data: gradesData, isLoading: gradesLoading } = useGrades(id);
  const { data: components } = useComponents(id);
  const { data: criteria } = useGradingCriteria(id);

  const submitGradesMut = useSubmitGrades(id);
  const finalizeMut = useFinalize(id);
  const unfinalizeMut = useUnfinalize(id);
  const updateCriteriaMut = useUpdateGradingCriteria(id);
  const downloadTemplateMut = useDownloadTemplate(id);
  const setAttendanceMut = useSetExamAttendance(id);
  const publishComponentMut = usePublishComponent(id);
  const unpublishComponentMut = useUnpublishComponent(id);

  const { hasPermission } = usePermissions();
  const { showError } = useToast();
  const canPublishComponent = hasPermission('menu.not-girisi.publish');

  const [edits, setEdits] = useState<ScoreEdits>({});
  const [isSavingAll, setIsSavingAll] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [optimisticAttendance, setOptimisticAttendance] = useState<AttendanceEdits>({});

  const [showCriteriaDialog, setShowCriteriaDialog] = useState(false);
  const [showFinalizeDialog, setShowFinalizeDialog] = useState(false);
  const [finalizeResults, setFinalizeResults] = useState<FinalizePreviewItem[] | null>(null);
  const [showImportDialog, setShowImportDialog] = useState(false);
  const [showPasteDialog, setShowPasteDialog] = useState(false);
  const [showPreviousGrade, setShowPreviousGrade] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  // Tracks whether we have already auto-enabled showPreviousGrade for a finalized section,
  // so a deliberate user toggle-off is not immediately overridden on re-render.
  const autoEnabledPreviousGradeRef = useRef(false);

  const isFinalized = gradesData?.isFinalized ?? false;
  const currentCriteria = criteria ?? DEFAULT_CRITERIA;
  const componentList: GradeComponent[] = useMemo(() => components ?? [], [components]);
  const hasComponents = componentList.length > 0;
  const totalWeight = componentList.reduce((sum, c) => sum + c.weight, 0);
  const publishedCount = componentList.filter((c) => Boolean(c.publishedAt)).length;
  const hideStatistics = gradesData?.hideStatistics ?? false;
  // Mutlak/Bağıl değerlendirme göstergesi: tip kaynağında alan yok → mutlak varsayılır.
  const isRelative = false;

  const weightedAverageScores = useMemo(
    () => (gradesData?.students ?? []).map((s) => s.weightedAverage).filter((v): v is number => v != null),
    [gradesData],
  );

  // Yeni veri yüklenince düzenleme tamponunu temizle (server değerleri tablo tarafından gösterilir).
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- fetched veri değişince edit tamponunu sıfırla; bilinçli senkron
    setEdits({});
  }, [gradesData]);

  // Auto-enable the letter-grade column the first time we learn the section is finalized.
  // Guarded by a ref so a subsequent user toggle-off is not overridden on re-render.
  useEffect(() => {
    if (gradesData?.isFinalized && !autoEnabledPreviousGradeRef.current) {
      autoEnabledPreviousGradeRef.current = true;
      setShowPreviousGrade(true);
    }
  }, [gradesData?.isFinalized]);

  if (gradesLoading) {
    return (
      <div className="flex flex-col gap-4 flex-1">
        <div className="h-16 rounded-xl bg-muted animate-pulse" />
        <div className="h-12 rounded-xl bg-muted animate-pulse" />
        <div className="h-96 flex-1 rounded-xl bg-muted animate-pulse" />
      </div>
    );
  }

  if (!gradesData) {
    return (
      <div className="flex flex-col items-center justify-center py-24">
        <XCircle className="h-12 w-12 text-muted-foreground mb-4" />
        <p className="text-lg font-medium text-foreground">Erişim Sağlanamadı</p>
        <p className="text-sm text-muted-foreground mt-1 mb-6">
          Bu ders bulunamadı veya erişim yetkiniz yok.
        </p>
        <Button variant="outline" onClick={() => navigate('/derslerim')}>
          <ArrowLeft className="h-4 w-4 mr-1.5" /> Derslerime Dön
        </Button>
      </div>
    );
  }

  const handleScoreChange = (componentId: number, studentId: number, value: string) => {
    setEdits((prev) => ({
      ...prev,
      [componentId]: { ...prev[componentId], [studentId]: value },
    }));
  };

  /** Bir bileşenin değişen puanlarını, server değerleriyle karşılaştırarak çıkarır. */
  const collectChangedGrades = (componentId: number): StudentScore[] => {
    const componentEdits = edits[componentId];
    if (!componentEdits) return [];
    const changed: StudentScore[] = [];
    for (const [studentIdStr, value] of Object.entries(componentEdits)) {
      const studentId = Number(studentIdStr);
      const student = gradesData.students.find((s) => s.studentId === studentId);
      if (!student) continue;
      const persisted = student.scores[componentId];
      const persistedStr = persisted != null ? String(persisted) : '';
      if (value === persistedStr) continue;
      changed.push({ studentId, score: value === '' ? null : Number(value) });
    }
    return changed;
  };

  const handleSave = async () => {
    if (!components || components.length === 0) return;

    // Tüm düzenlemeler 0-100 aralığında mı?
    for (const componentEdits of Object.values(edits)) {
      for (const value of Object.values(componentEdits)) {
        if (isInvalidScore(value)) {
          showError('Geçersiz not', 'Notlar 0 ile 100 arasında olmalıdır.');
          return;
        }
      }
    }

    const componentsToSave = components
      .map((comp) => ({ componentId: comp.id, grades: collectChangedGrades(comp.id) }))
      .filter((item) => item.grades.length > 0);

    if (componentsToSave.length === 0) return;

    setIsSavingAll(true);
    try {
      for (const item of componentsToSave) {
        await submitGradesMut.mutateAsync(item);
      }
      setEdits({});
    } catch (err: unknown) {
      showError('Kaydedilemedi', getErrorMessage(err, 'Notlar kaydedilemedi.'));
    } finally {
      setIsSavingAll(false);
    }
  };

  const handleFinalize = () => {
    finalizeMut.mutate(undefined, {
      onSuccess: (data) => setFinalizeResults(data.results),
      onError: (err: unknown) => showError('Kesinleştirme hatası', getErrorMessage(err, 'Kesinleştirme hatası')),
    });
  };

  const handleUnfinalize = () => {
    unfinalizeMut.mutate(undefined, {
      onSuccess: () => {
        setFinalizeResults(null);
        setShowFinalizeDialog(false);
      },
      onError: (err: unknown) => showError('Geri alınamadı', getErrorMessage(err, 'Geri alma hatası')),
    });
  };

  const handleSaveCriteria = (next: GradingCriteria) => {
    updateCriteriaMut.mutate(next, {
      onSuccess: () => setShowCriteriaDialog(false),
      onError: (err: unknown) => showError('Kaydedilemedi', getErrorMessage(err, 'Harf aralıkları kaydedilemedi.')),
    });
  };

  const handleDownloadTemplate = (componentId: number) => {
    const component = componentList.find((c) => c.id === componentId);
    downloadTemplateMut.mutate(componentId, {
      onSuccess: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${gradesData.courseCode}-${component?.name ?? 'grades'}-template.xlsx`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(url);
      },
      onError: () => showError('İndirilemedi', 'Excel şablonu indirilemedi.'),
    });
  };

  const handleAttendanceChange = (componentId: number, studentId: number, attendance: ExamAttendance) => {
    if (isFinalized) return;
    setOptimisticAttendance((prev) => ({
      ...prev,
      [componentId]: { ...prev[componentId], [studentId]: attendance },
    }));
    setAttendanceMut.mutate(
      { componentId, items: [{ studentId, attendance }] },
      {
        onSettled: () => {
          setOptimisticAttendance((prev) => {
            const forComponent = { ...prev[componentId] };
            delete forComponent[studentId];
            return { ...prev, [componentId]: forComponent };
          });
        },
        onError: (err: unknown) => showError('İşaretlenemedi', getErrorMessage(err, 'Katılım işaretlenemedi.')),
      },
    );
  };

  const handleBulkAttendance = (componentId: number, items: BulkAttendanceItem[]) => {
    if (isFinalized || items.length === 0) return;
    setOptimisticAttendance((prev) => {
      const forComponent = { ...prev[componentId] };
      items.forEach((it) => { forComponent[it.studentId] = it.attendance; });
      return { ...prev, [componentId]: forComponent };
    });
    setAttendanceMut.mutate(
      { componentId, items },
      {
        onSettled: () => {
          setOptimisticAttendance((prev) => {
            const forComponent = { ...prev[componentId] };
            items.forEach((it) => { delete forComponent[it.studentId]; });
            return { ...prev, [componentId]: forComponent };
          });
        },
        onError: (err: unknown) => showError('İşaretlenemedi', getErrorMessage(err, 'Toplu katılım işaretlenemedi.')),
      },
    );
  };

  const handlePublishComponent = (componentId: number) => {
    publishComponentMut.mutate(componentId, {
      onError: (err: unknown) => showError('İlan edilemedi', getErrorMessage(err, 'Bileşen ilan edilemedi.')),
    });
  };

  const handleUnpublishComponent = (componentId: number) => {
    unpublishComponentMut.mutate(componentId, {
      onError: (err: unknown) => showError('Geri alınamadı', getErrorMessage(err, 'İlan geri alınamadı.')),
    });
  };

  const filteredStudents = gradesData.students.filter((s) => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return `${s.studentNumber} ${s.firstName} ${s.lastName}`.toLowerCase().includes(q);
  });

  return (
    <>
    <div className="flex flex-col gap-4 flex-1 print:hidden">

      {/* ═══ Tek kart: Ders Bilgileri + Araç çubuğu ═══ */}
      <div className="rounded-xl border bg-card divide-y">
      <GradeCourseHeader
        courseCode={gradesData.courseCode}
        courseName={gradesData.courseName}
        studentCount={gradesData.students.length}
        hideStatistics={hideStatistics}
        isFinalized={isFinalized}
        isRelative={isRelative}
        totalWeight={totalWeight}
        publishedCount={publishedCount}
        componentCount={componentList.length}
        onBack={() => navigate('/not-girisi')}
      />

      {/* ═══ ARAÇ ÇUBUĞU ═══ */}
      {hasComponents && (
        <div className="px-4 py-3">
        <GradeToolbar
          students={gradesData.students}
          components={componentList}
          isFinalized={isFinalized}
          showPreview={showPreview}
          showPreviousGrade={showPreviousGrade}
          isSaving={isSavingAll || submitGradesMut.isPending}
          isDownloadingTemplate={downloadTemplateMut.isPending}
          isUnfinalizePending={unfinalizeMut.isPending}
          onTogglePreview={() => setShowPreview((v) => !v)}
          onTogglePreviousGrade={() => setShowPreviousGrade((v) => !v)}
          onOpenCriteria={() => setShowCriteriaDialog(true)}
          onPrint={() => window.print()}
          onOpenPaste={() => setShowPasteDialog(true)}
          onBulkAttendance={handleBulkAttendance}
          onDownloadTemplate={handleDownloadTemplate}
          onOpenImport={() => setShowImportDialog(true)}
          onSave={handleSave}
          onFinalize={() => setShowFinalizeDialog(true)}
          onUnfinalize={handleUnfinalize}
        />
        </div>
      )}
      </div>

      {/* ═══ SINIF İSTATİSTİKLERİ ═══ */}
      {!hideStatistics && weightedAverageScores.length > 0 && (
        <ClassStatsPanel
          scores={weightedAverageScores}
          resolve={(s) => resolveGrade(Math.round(s), currentCriteria)}
        />
      )}

      {/* ═══ ANA İÇERİK — Not Tablosu ═══ */}
      <Card className="flex-1">
        <CardContent className="p-0">
          {!hasComponents ? (
            <div className="flex flex-col items-center justify-center py-20 text-center">
              <p className="text-base font-medium text-foreground mb-2">Henüz değerlendirme tanımlanmamış</p>
              <p className="text-sm text-muted-foreground mb-5 max-w-md">
                Sınav/değerlendirme türleri Sınav Tanımlama ekranından tanımlanır. Tanım yapıldıktan sonra not girişine başlayabilirsiniz.
              </p>
              <Button variant="outline" asChild className="gap-1.5">
                <Link to="/sinav-tanimlama">
                  <Info className="h-4 w-4" /> Sınav Tanımlama'ya Git
                </Link>
              </Button>
            </div>
          ) : (
            <>
              {/* Arama */}
              <div className="flex items-center px-4 py-3 border-b bg-muted/20">
                <div className="relative">
                  <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
                  <Input
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    placeholder="Öğrenci ara..."
                    className="pl-9 w-64 h-9 text-sm"
                  />
                </div>
              </div>

              <GradeStudentTable
                students={filteredStudents}
                components={componentList}
                scores={edits}
                optimisticAttendance={optimisticAttendance}
                isFinalized={isFinalized}
                readOnly={false}
                criteria={currentCriteria}
                showPreviousGrade={showPreviousGrade}
                showPreview={showPreview}
                isRelative={isRelative}
                canPublishComponent={canPublishComponent}
                isPublishPending={publishComponentMut.isPending || unpublishComponentMut.isPending}
                onScoreChange={handleScoreChange}
                onAttendanceChange={handleAttendanceChange}
                onPublishComponent={handlePublishComponent}
                onUnpublishComponent={handleUnpublishComponent}
              />

              {filteredStudents.length === 0 && searchTerm.trim() && (
                <div className="py-12 text-center text-muted-foreground text-sm">
                  Aramayla eşleşen öğrenci bulunamadı.
                </div>
              )}
              {gradesData.students.length === 0 && (
                <div className="py-12 text-center text-muted-foreground text-sm">
                  Bu derste kayıtlı öğrenci bulunmamaktadır.
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* ═══ BÜTÜNLEME & MAZERET (finalize sonrası) ═══ */}
      {isFinalized && (
        <>
          <ButunlemePanel sectionId={id} />
          <MazeretPanel sectionId={id} />
        </>
      )}

      {/* ═══ DIALOGS ═══ */}
      <GradingCriteriaDialog
        open={showCriteriaDialog}
        onOpenChange={setShowCriteriaDialog}
        initialCriteria={currentCriteria}
        locked={criteria?.editable === false}
        hideStatistics={hideStatistics}
        previewScores={weightedAverageScores}
        isSaving={updateCriteriaMut.isPending}
        onSave={handleSaveCriteria}
      />

      <FinalizeDialog
        open={showFinalizeDialog}
        onOpenChange={setShowFinalizeDialog}
        results={finalizeResults}
        students={gradesData.students}
        hideStatistics={hideStatistics}
        isPending={finalizeMut.isPending}
        onConfirm={handleFinalize}
        onClose={() => { setShowFinalizeDialog(false); setFinalizeResults(null); }}
      />

      <ExcelImportDialog
        open={showImportDialog}
        onOpenChange={setShowImportDialog}
        sectionId={id}
        components={componentList}
        isDownloadingTemplate={downloadTemplateMut.isPending}
        onDownloadTemplate={handleDownloadTemplate}
      />

      <ScorePasteDialog
        open={showPasteDialog}
        onOpenChange={setShowPasteDialog}
        sectionId={id}
        components={componentList}
      />
    </div>

    {/* ═══ YAZDIR — resmi not listesi belgesi (sadece print'te görünür) ═══ */}
    {hasComponents && (
      <GradeReportDocument
        courseCode={gradesData.courseCode}
        courseName={gradesData.courseName}
        students={gradesData.students}
        components={componentList}
        criteria={currentCriteria}
        isFinalized={isFinalized}
      />
    )}
    </>
  );
}
