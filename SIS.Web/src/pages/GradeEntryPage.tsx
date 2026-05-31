// SIS.Web/src/pages/GradeEntryPage.tsx
// Proliz tarzı not giriş ekranı. Sınav tanımı SALT-OKUNUR (tanım Sınav Tanımlama'da).
// Tüm mevcut hook'lar reuse edilir; yeni: Toplu Not Aktar (paste), toplu katılım, ön izleme.

import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  ArrowLeft,
  Save,
  CheckCircle,
  XCircle,
  Settings,
  Users,
  Search,
  Download,
  Upload,
  ClipboardPaste,
  Eye,
  EyeOff,
  Info,
  Printer,
} from 'lucide-react';
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
import { ScorePasteDialog } from '../components/grade/ScorePasteDialog';
import { ExcelImportDialog } from '../components/grade/ExcelImportDialog';
import { GradingCriteriaDialog } from '../components/grade/GradingCriteriaDialog';
import { FinalizeDialog } from '../components/grade/FinalizeDialog';
import { BulkAttendanceMenu, type BulkAttendanceItem } from '../components/grade/BulkAttendanceMenu';
import { getErrorMessage } from '../lib/apiError';
import { COMPONENT_TYPE_LABELS, COMPONENT_TYPE_BADGE_CLASS, resolveGrade } from '../components/grade/gradeDisplay';
import type {
  ExamAttendance,
  FinalizePreviewItem,
  GradingCriteria,
} from '../types/grade';

const DEFAULT_CRITERIA: GradingCriteria = {
  aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45,
};

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

  const [selectedComponentId, setSelectedComponentId] = useState<number | null>(null);
  const [scores, setScores] = useState<Record<number, string>>({});
  const [searchTerm, setSearchTerm] = useState('');
  const [optimisticAttendance, setOptimisticAttendance] = useState<Record<number, ExamAttendance>>({});

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
  const activeComponentId = selectedComponentId ?? components?.[0]?.id ?? null;
  const totalWeight = components?.reduce((sum, c) => sum + c.weight, 0) ?? 0;
  const activeComponent = components?.find((c) => c.id === activeComponentId);
  const hideStatistics = gradesData?.hideStatistics ?? false;
  // Mutlak/Bağıl değerlendirme göstergesi: tip kaynağında alan yok → mutlak varsayılır.
  const isRelative = false;

  const weightedAverageScores = useMemo(
    () => (gradesData?.students ?? []).map((s) => s.weightedAverage).filter((v): v is number => v != null),
    [gradesData],
  );

  useEffect(() => {
    if (!gradesData || !activeComponentId) return;
    const next: Record<number, string> = {};
    gradesData.students.forEach((student) => {
      const score = student.scores[activeComponentId];
      next[student.studentId] = score != null ? String(score) : '';
    });
    // eslint-disable-next-line react-hooks/set-state-in-effect -- fetched veriden form state'i başlatma; bilinçli senkron
    setScores(next);
  }, [gradesData, activeComponentId]);

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

  const selectComponent = (compId: number) => {
    setSelectedComponentId(compId);
    const next: Record<number, string> = {};
    gradesData.students.forEach((s) => {
      const score = s.scores[compId];
      next[s.studentId] = score != null ? String(score) : '';
    });
    setScores(next);
    setSearchTerm('');
  };

  const handleScoreChange = (studentId: number, value: string) => {
    setScores((prev) => ({ ...prev, [studentId]: value }));
  };

  const handleSave = () => {
    if (!activeComponentId) return;
    const invalidEntry = Object.entries(scores).find(([, score]) => {
      if (score === '') return false;
      const numericScore = Number(score);
      return Number.isNaN(numericScore) || numericScore < 0 || numericScore > 100;
    });
    if (invalidEntry) {
      showError('Geçersiz not', 'Notlar 0 ile 100 arasında olmalıdır.');
      return;
    }
    const grades = Object.entries(scores).map(([studentId, score]) => ({
      studentId: Number(studentId),
      score: score === '' ? null : Number(score),
    }));
    submitGradesMut.mutate(
      { componentId: activeComponentId, grades },
      { onError: (err: unknown) => showError('Kaydedilemedi', getErrorMessage(err, 'Notlar kaydedilemedi.')) },
    );
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

  const handleDownloadTemplate = () => {
    if (!activeComponentId) return;
    downloadTemplateMut.mutate(activeComponentId, {
      onSuccess: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${gradesData.courseCode}-${activeComponent?.name ?? 'grades'}-template.xlsx`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(url);
      },
      onError: () => showError('İndirilemedi', 'Excel şablonu indirilemedi.'),
    });
  };

  const handleAttendanceChange = (studentId: number, attendance: ExamAttendance) => {
    if (!activeComponentId || isFinalized) return;
    setOptimisticAttendance((prev) => ({ ...prev, [studentId]: attendance }));
    setAttendanceMut.mutate(
      { componentId: activeComponentId, items: [{ studentId, attendance }] },
      {
        onSettled: () => {
          setOptimisticAttendance((prev) => {
            const next = { ...prev };
            delete next[studentId];
            return next;
          });
        },
      },
    );
  };

  const handleBulkAttendance = (items: BulkAttendanceItem[]) => {
    if (!activeComponentId || isFinalized || items.length === 0) return;
    setOptimisticAttendance((prev) => {
      const next = { ...prev };
      items.forEach((it) => { next[it.studentId] = it.attendance; });
      return next;
    });
    setAttendanceMut.mutate(
      { componentId: activeComponentId, items },
      {
        onSettled: () => {
          setOptimisticAttendance((prev) => {
            const next = { ...prev };
            items.forEach((it) => { delete next[it.studentId]; });
            return next;
          });
        },
        onError: (err: unknown) => showError('İşaretlenemedi', getErrorMessage(err, 'Toplu katılım işaretlenemedi.')),
      },
    );
  };

  const filteredStudents = gradesData.students.filter((s) => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return `${s.studentNumber} ${s.firstName} ${s.lastName}`.toLowerCase().includes(q);
  });

  const activePublished = Boolean(activeComponent?.publishedAt);

  return (
    <div className="flex flex-col gap-4 flex-1">

      {/* ═══ HEADER — Ders Bilgileri ═══ */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="icon" onClick={() => navigate('/not-girisi')} title="Listeye dön">
              <ArrowLeft className="h-4 w-4" />
            </Button>
            <span className="font-mono text-lg font-bold text-primary">{gradesData.courseCode}</span>
            <span className="text-lg font-semibold text-foreground">{gradesData.courseName}</span>
          </div>
          <div className="flex flex-wrap items-center gap-3 mt-1 pl-12">
            {!hideStatistics && (
              <>
                <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground">
                  <Users className="h-3.5 w-3.5" />
                  {gradesData.students.length} öğrenci
                </span>
                <span className="text-muted-foreground">·</span>
              </>
            )}
            <Badge variant="secondary" className="text-xs">
              {isFinalized ? 'Kesinleştirilmiş' : 'Kesinleştirilmemiş'}
            </Badge>
            <span className="text-muted-foreground">·</span>
            <Badge variant="outline" className="text-xs" title="Değerlendirme yöntemi (admin tarafından belirlenir)">
              Değerlendirme: {isRelative ? 'Bağıl' : 'Mutlak'}
            </Badge>
            {/* Ek Bilgiler — seçili sınav */}
            {activeComponent && (
              <>
                <span className="text-muted-foreground">·</span>
                <span className="text-sm text-foreground">
                  <span className="font-medium">{activeComponent.name}</span>
                  <span className="text-muted-foreground"> (%{activeComponent.weight})</span>
                  {' — '}
                  <span className={activePublished ? 'text-emerald-600 font-medium' : 'text-muted-foreground'}>
                    {activePublished ? 'İlan Edildi' : 'İlan Edilmedi'}
                  </span>
                </span>
              </>
            )}
          </div>
        </div>

        <Link
          to="/sinav-tanimlama"
          className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-primary transition-colors"
        >
          <Info className="h-3.5 w-3.5" />
          Sınav tanımları Sınav Tanımlama'da
        </Link>
      </div>

      {/* ═══ BİLEŞEN SEÇİCİ (salt-okunur sekmeler) ═══ */}
      <div className="flex items-center gap-2 flex-wrap">
        {components?.map((comp) => {
          const isActive = activeComponentId === comp.id;
          const isPublished = Boolean(comp.publishedAt);
          return (
            <button
              key={comp.id}
              onClick={() => selectComponent(comp.id)}
              className={`relative flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium transition-all ${
                isActive ? 'bg-primary text-primary-foreground shadow-sm' : 'bg-muted/50 text-foreground hover:bg-muted'
              }`}
              title={isPublished ? `İlan edildi: ${new Date(comp.publishedAt!).toLocaleString('tr-TR')}` : 'İlan edilmedi (öğrenci notu/istatistiği göremez)'}
            >
              <span
                aria-hidden
                className={`inline-block h-2 w-2 rounded-full ${
                  isPublished ? 'bg-emerald-500' : isActive ? 'bg-primary-foreground/40' : 'bg-muted-foreground/40'
                }`}
              />
              <span>{comp.name}</span>
              <span className={`font-mono text-xs ${isActive ? 'text-primary-foreground/70' : 'text-muted-foreground'}`}>
                %{comp.weight}
              </span>
              {comp.type && comp.type !== 'Other' && (
                <span className={`text-[10px] font-medium px-1.5 py-0.5 rounded ${
                  isActive ? 'bg-primary-foreground/15 text-primary-foreground/80' : COMPONENT_TYPE_BADGE_CLASS[comp.type]
                }`}>
                  {COMPONENT_TYPE_LABELS[comp.type]}
                </span>
              )}
            </button>
          );
        })}

        <div className="ml-auto flex items-center gap-2">
          <span className={`text-sm font-mono font-semibold ${
            totalWeight === 100 ? 'text-emerald-600' : totalWeight > 100 ? 'text-red-600' : 'text-muted-foreground'
          }`}>
            Toplam: %{totalWeight}{totalWeight === 100 && ' ✓'}
          </span>
        </div>
      </div>

      {/* ═══ ARAÇ ÇUBUĞU ═══ */}
      {activeComponentId && (
        <div className="flex flex-wrap items-center gap-2">
          {!isFinalized && (
            <>
              <Button variant="outline" size="sm" onClick={() => setShowPasteDialog(true)} className="gap-1.5 text-sm">
                <ClipboardPaste className="h-4 w-4" /> Toplu Not Aktar
              </Button>
              <BulkAttendanceMenu
                students={gradesData.students}
                componentId={activeComponentId}
                onBulkSet={handleBulkAttendance}
              />
              <Button
                variant="outline"
                size="sm"
                onClick={handleDownloadTemplate}
                disabled={downloadTemplateMut.isPending}
                className="gap-1.5 text-sm"
              >
                <Download className="h-4 w-4" />
                {downloadTemplateMut.isPending ? 'İndiriliyor...' : 'Şablon'}
              </Button>
              <Button variant="outline" size="sm" onClick={() => setShowImportDialog(true)} className="gap-1.5 text-sm">
                <Upload className="h-4 w-4" /> Excel Yükle
              </Button>
            </>
          )}

          <Button
            variant={showPreviousGrade ? 'default' : 'outline'}
            size="sm"
            onClick={() => setShowPreviousGrade((v) => !v)}
            className="gap-1.5 text-sm"
          >
            <Eye className="h-4 w-4" /> Önceki Harf Notu
          </Button>
          <Button
            variant={showPreview ? 'default' : 'outline'}
            size="sm"
            onClick={() => setShowPreview((v) => !v)}
            className="gap-1.5 text-sm"
          >
            <Eye className="h-4 w-4" /> Ön İzleme
          </Button>

          <Button variant="outline" size="sm" onClick={() => window.print()} className="gap-1.5 text-sm">
            <Printer className="h-4 w-4" /> Yazdır
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={() => setShowCriteriaDialog(true)}
            className="gap-1.5 text-sm"
            disabled={isFinalized}
          >
            <Settings className="h-4 w-4" /> Harf Aralığı
          </Button>

          {canPublishComponent && (
            activePublished ? (
              <Button
                variant="outline"
                size="sm"
                onClick={() => unpublishComponentMut.mutate(activeComponentId)}
                disabled={publishComponentMut.isPending || unpublishComponentMut.isPending}
                className="gap-1.5 text-sm"
                title={`İlan: ${activeComponent?.publishedAt ? new Date(activeComponent.publishedAt).toLocaleString('tr-TR') : ''} — geri al`}
              >
                <EyeOff className="h-4 w-4" /> İlanı Geri Al
              </Button>
            ) : (
              <Button
                variant="outline"
                size="sm"
                onClick={() => publishComponentMut.mutate(activeComponentId)}
                disabled={publishComponentMut.isPending || unpublishComponentMut.isPending}
                className="gap-1.5 text-sm border-emerald-500/40 text-emerald-700 hover:bg-emerald-50"
                title="Öğrenci bu bileşenin notunu ve sınıf istatistiklerini görmeye başlar"
              >
                <Eye className="h-4 w-4" /> İlan Et
              </Button>
            )
          )}

          {isFinalized ? (
            <Button
              variant="outline"
              size="sm"
              onClick={handleUnfinalize}
              disabled={unfinalizeMut.isPending}
              className="gap-1.5 text-sm"
            >
              <XCircle className="h-4 w-4" /> Geri Al
            </Button>
          ) : (
            <Button size="sm" onClick={() => setShowFinalizeDialog(true)} className="gap-1.5 text-sm">
              <CheckCircle className="h-4 w-4" /> Kesinleştir
            </Button>
          )}
        </div>
      )}

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
          {!activeComponentId ? (
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
              {/* Arama + Kaydet */}
              <div className="flex items-center justify-between px-4 py-3 border-b bg-muted/20">
                <div className="flex items-center gap-3">
                  <div className="relative">
                    <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
                    <Input
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                      placeholder="Öğrenci ara..."
                      className="pl-9 w-64 h-9 text-sm"
                    />
                  </div>
                  <span className="text-sm text-muted-foreground">
                    {activeComponent?.name} — %{activeComponent?.weight} ağırlık
                  </span>
                </div>
                {!isFinalized && (
                  <Button size="sm" onClick={handleSave} disabled={submitGradesMut.isPending} className="gap-1.5">
                    <Save className="h-4 w-4" />
                    {submitGradesMut.isPending ? 'Kaydediliyor...' : 'Kaydet'}
                  </Button>
                )}
              </div>

              <GradeStudentTable
                students={filteredStudents}
                componentId={activeComponentId}
                scores={scores}
                optimisticAttendance={optimisticAttendance}
                isFinalized={isFinalized}
                readOnly={false}
                criteria={currentCriteria}
                showPreviousGrade={showPreviousGrade}
                showPreview={showPreview}
                isRelative={isRelative}
                onScoreChange={handleScoreChange}
                onAttendanceChange={handleAttendanceChange}
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

      <ExcelImportDialog open={showImportDialog} onOpenChange={setShowImportDialog} sectionId={id} />

      {activeComponentId && (
        <ScorePasteDialog
          open={showPasteDialog}
          onOpenChange={setShowPasteDialog}
          sectionId={id}
          componentId={activeComponentId}
        />
      )}
    </div>
  );
}
