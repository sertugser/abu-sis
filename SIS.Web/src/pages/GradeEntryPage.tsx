import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Separator } from '@/components/ui/separator';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  ArrowLeft,
  Save,
  CheckCircle,
  XCircle,
  Settings,
  AlertTriangle,
  Users,
  Search,
  Download,
  Upload,
  FileSpreadsheet,
} from 'lucide-react';
import {
  useGrades,
  useComponents,
  useGradingCriteria,
  useSubmitGrades,
  useFinalize,
  usePublishGrades,
  useUnfinalize,
  useUnpublishGrades,
  useUpdateGradingCriteria,
  useDownloadTemplate,
  usePreviewImport,
  useApplyImport,
} from '../hooks/useGradeData';
import type {
  GradingCriteria,
  FinalizePreviewItem,
  ImportPreviewResponse,
} from '../types/grade';

/* ── Harf notu stilleri ── */
const GRADE_STYLE: Record<string, { bg: string; text: string }> = {
  AA: { bg: 'bg-emerald-100', text: 'text-emerald-700' },
  BA: { bg: 'bg-emerald-50', text: 'text-emerald-600' },
  BB: { bg: 'bg-blue-50', text: 'text-blue-600' },
  CB: { bg: 'bg-blue-50', text: 'text-blue-500' },
  CC: { bg: 'bg-sky-50', text: 'text-sky-600' },
  DC: { bg: 'bg-amber-50', text: 'text-amber-600' },
  DD: { bg: 'bg-orange-50', text: 'text-orange-600' },
  FF: { bg: 'bg-red-50', text: 'text-red-600' },
  NA: { bg: 'bg-red-50', text: 'text-red-600' },
};

const STUDENTS_PER_PAGE = 10;

function resolveGrade(score: number, criteria: GradingCriteria): string {
  if (score >= criteria.aaMin) return 'AA';
  if (score >= criteria.baMin) return 'BA';
  if (score >= criteria.bbMin) return 'BB';
  if (score >= criteria.cbMin) return 'CB';
  if (score >= criteria.ccMin) return 'CC';
  if (score >= criteria.dcMin) return 'DC';
  if (score >= criteria.ddMin) return 'DD';
  return 'FF';
}

function GradeChip({ grade }: { grade: string }) {
  const style = GRADE_STYLE[grade];
  if (!style) return <span className="text-muted-foreground">—</span>;
  return (
    <span className={`inline-flex items-center justify-center min-w-[2.5rem] px-2.5 py-1 rounded-md text-xs font-bold font-mono ${style.bg} ${style.text}`}>
      {grade}
    </span>
  );
}

/* ══════════════════════════════════════════════════════════ */

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
  const publishGradesMut = usePublishGrades(id);
  const unpublishGradesMut = useUnpublishGrades(id);
  const updateCriteriaMut = useUpdateGradingCriteria(id);
  const downloadTemplateMut = useDownloadTemplate(id);
const previewImportMut = usePreviewImport(id);
const applyImportMut = useApplyImport(id);

  const [selectedComponentId, setSelectedComponentId] = useState<number | null>(null);
  const [scores, setScores] = useState<Record<number, string>>({});
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [showCriteriaDialog, setShowCriteriaDialog] = useState(false);
  const [editCriteria, setEditCriteria] = useState<GradingCriteria | null>(null);
  const [showFinalizeDialog, setShowFinalizeDialog] = useState(false);
  const [showCurveDialog, setShowCurveDialog] = useState(false);
  const [curveStep, setCurveStep] = useState<'pass' | 'ranges'>('pass');
  const [passMarkText, setPassMarkText] = useState('');
  const [curveCriteria, setCurveCriteria] = useState<GradingCriteria | null>(null);
  const [finalizeResults, setFinalizeResults] = useState<FinalizePreviewItem[] | null>(null);
  const [showImportDialog, setShowImportDialog] = useState(false);
const [selectedFile, setSelectedFile] = useState<File | null>(null);
const [previewResult, setPreviewResult] = useState<ImportPreviewResponse | null>(null);

  const isFinalized = gradesData?.isFinalized ?? false;
  const isPublished = gradesData?.areGradesPublished ?? false;
  const currentCriteria = criteria ?? {
    aaMin: 91,
    baMin: 81,
    bbMin: 71,
    cbMin: 61,
    ccMin: 55,
    dcMin: 50,
    ddMin: 45,
    calculationMethod: 'Absolute' as const,
  };
  const calculationMethod = gradesData?.calculationMethod ?? currentCriteria.calculationMethod ?? 'Absolute';
  const isRelativeGrading = String(calculationMethod).toLowerCase() === 'relative';
  const activeComponentId = selectedComponentId ?? components?.[0]?.id ?? null;
  const totalWeight = components?.reduce((sum, c) => sum + c.weight, 0) ?? 0;
  const activeComponent = components?.find(c => c.id === activeComponentId);

  useEffect(() => {
    if (!gradesData || !activeComponentId) return;

    const newScores: Record<number, string> = {};
    gradesData.students.forEach((student) => {
      const score = student.scores[activeComponentId];
      newScores[student.studentId] = score != null ? String(score) : '';
    });

    // eslint-disable-next-line react-hooks/set-state-in-effect -- initializing editable form state from fetched data; intentional sync
    setScores(newScores);
  }, [gradesData, activeComponentId]);

  /* ── Loading ── */
  if (gradesLoading) {
    return (
      <div className="flex flex-col gap-4 flex-1">
        <Skeleton className="h-16 rounded-xl" />
        <Skeleton className="h-12 rounded-xl" />
        <Skeleton className="h-96 flex-1 rounded-xl" />
      </div>
    );
  }

  /* ── Error / Not Found ── */
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

  /* ── Handlers ── */
  const selectComponent = (compId: number) => {
    setSelectedComponentId(compId);
    const newScores: Record<number, string> = {};
    gradesData.students.forEach(s => {
      const score = s.scores[compId];
      newScores[s.studentId] = score != null ? String(score) : '';
    });
    setScores(newScores);
    setSearchTerm('');
    setCurrentPage(1);
  };

  const handleSave = () => {
  if (!activeComponentId) return;

  const gradeEntries = gradesData.students.map((student) => ({
    studentId: student.studentId,
    score: scores[student.studentId] ?? '',
  }));

  const invalidEntry = gradeEntries.find(({ score }) => {
    if (score === '') return false;
    const numericScore = Number(score);
    return Number.isNaN(numericScore) || numericScore < 0 || numericScore > 100;
  });

  if (invalidEntry) {
    alert('Notlar 0 ile 100 arasında olmalıdır.');
    return;
  }

  const grades = gradeEntries.map(({ studentId, score }) => ({
    studentId,
    score: score === '' ? null : Number(score),
  }));

  const nextScoresByStudent = new Map(
    gradesData.students.map((student) => [
      student.studentId,
      activeComponentId ? student.scores[activeComponentId] ?? null : null,
    ])
  );

  grades.forEach((grade) => {
    nextScoresByStudent.set(grade.studentId, grade.score);
  });

  const savedTotal = Array.from(nextScoresByStudent.values()).filter((score) => score != null).length;

  submitGradesMut.mutate(
    { componentId: activeComponentId, grades },
    {
      onSuccess: () => {
        alert(
          `${activeComponent?.name ?? 'Bu bilesen'} icin toplam ${savedTotal}/${gradesData.students.length} kisinin notu kaydedildi.`
        );
      },
      onError: (err: unknown) => {
        const error = err as { response?: { data?: { error?: string } } };
        alert(error.response?.data?.error ?? 'Notlar kaydedilemedi.');
      },
    }
  );
};

  const runFinalize = (showResultsDialog = true) => {
    finalizeMut.mutate(undefined, {
      onSuccess: (data) => {
        setFinalizeResults(data.results);
        setShowCurveDialog(false);
        setShowFinalizeDialog(showResultsDialog);
      },
      onError: (err: unknown) => {
        const error = err as { response?: { data?: { error?: string } } };
        alert(error.response?.data?.error ?? 'Kesinleştirme hatası');
      },
    });
  };

  const handleFinalize = () => {
    finalizeMut.mutate(undefined, {
      onSuccess: (data) => setFinalizeResults(data.results),
      onError: (err: unknown) => {
        const error = err as { response?: { data?: { error?: string } } };
        alert(error.response?.data?.error ?? 'Kesinleştirme hatası');
      },
    });
  };

  const handleUnfinalize = () => {
    unfinalizeMut.mutate(undefined, {
      onSuccess: () => {
        setFinalizeResults(null);
        setShowFinalizeDialog(false);
      },
    });
  };

  const handleSaveCriteria = () => {
    if (!editCriteria) return;
    updateCriteriaMut.mutate(editCriteria, {
      onSuccess: () => setShowCriteriaDialog(false),
    });
  };
  const handleDownloadTemplate = () => {
  downloadTemplateMut.mutate(undefined, {
    onSuccess: (blob) => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${gradesData.courseCode}-grades-template.xlsx`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    },
    onError: () => {
      alert('Excel şablonu indirilemedi.');
    },
  });
};

const handlePreviewImport = () => {
  if (!selectedFile) {
    alert('Lütfen bir Excel dosyası seçin.');
    return;
  }

  previewImportMut.mutate(selectedFile, {
    onSuccess: (data) => {
      setPreviewResult(data);
    },
    onError: (err: unknown) => {
      const error = err as { response?: { data?: { error?: string } } };
      alert(error.response?.data?.error ?? 'Önizleme alınamadı.');
    },
  });
};

const handleApplyImport = () => {
  if (!selectedFile) {
    alert('Lütfen bir Excel dosyası seçin.');
    return;
  }

  applyImportMut.mutate(selectedFile, {
    onSuccess: (data) => {
      alert(
        `İçe aktarma tamamlandı.\n` +
        `Eklenen: ${data.inserted}\n` +
        `Güncellenen: ${data.updated}\n` +
        `Atlanan: ${data.skipped}`
      );

      setShowImportDialog(false);
      setSelectedFile(null);
      setPreviewResult(null);
    },
    onError: (err: unknown) => {
      const error = err as { response?: { data?: { error?: string } } };
      alert(error.response?.data?.error ?? 'İçe aktarma başarısız.');
    },
  });
};

  /* ── Filter students ── */
  const filteredStudents = gradesData.students.filter(s => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return `${s.studentNumber} ${s.firstName} ${s.lastName}`.toLowerCase().includes(q);
  });

  const totalPages = Math.max(1, Math.ceil(filteredStudents.length / STUDENTS_PER_PAGE));
  const effectivePage = Math.min(currentPage, totalPages);
  const pageStart = (effectivePage - 1) * STUDENTS_PER_PAGE;
  const pageEnd = Math.min(pageStart + STUDENTS_PER_PAGE, filteredStudents.length);
  const pagedStudents = filteredStudents.slice(pageStart, pageEnd);

  const studentWeightedAverages = (() => {
    if (!components?.length) return [];

    return gradesData.students
      .map((student) => {
        let total = 0;
        for (const component of components) {
          const score = student.scores[component.id];
          if (score == null) return null;
          total += score * component.weight / 100;
        }
        return Math.round(total * 100) / 100;
      })
      .filter((value): value is number => value !== null);
  })();

  const getStudentWeightedAverage = (student: (typeof gradesData.students)[number]) => {
    if (!components?.length) return null;

    let total = 0;
    for (const component of components) {
      const score = student.scores[component.id];
      if (score == null) return null;
      total += score * component.weight / 100;
    }

    return Math.round(total * 100) / 100;
  };

  const allGradesComplete = components?.length
    ? studentWeightedAverages.length === gradesData.students.length && gradesData.students.length > 0
    : false;

  const classAverage = studentWeightedAverages.length
    ? studentWeightedAverages.reduce((sum, value) => sum + value, 0) / studentWeightedAverages.length
    : 0;

  const suggestedPassMark = clamp(Math.round(classAverage * 0.75), 35, 60);

  const startRelativeFinalize = () => {
    if (!allGradesComplete) {
      alert('Çan eğrisi ve kesinleştirme için tüm öğrencilerin tüm not bileşenleri girilmiş olmalıdır.');
      return;
    }

    const nextCriteria = buildCriteriaFromPassMark(suggestedPassMark, 'Relative');
    setPassMarkText(String(suggestedPassMark));
    setCurveCriteria(nextCriteria);
    setCurveStep('pass');
    setShowCurveDialog(true);
  };

  const approveCurvePassMark = () => {
    const passMark = Number(passMarkText);
    if (!Number.isFinite(passMark) || passMark < 1 || passMark > 60) {
      alert('Geçme notu 1 ile 60 arasında olmalıdır.');
      return;
    }

    setCurveCriteria(buildCriteriaFromPassMark(passMark, 'Relative'));
    setCurveStep('ranges');
  };

  const applyCurveAndFinalize = () => {
    if (!curveCriteria) return;
    updateCriteriaMut.mutate(curveCriteria, {
      onSuccess: () => runFinalize(false),
      onError: (err: unknown) => {
        const error = err as { response?: { data?: { error?: string } } };
        alert(error.response?.data?.error ?? 'Harf aralıkları kaydedilemedi.');
      },
    });
  };

  /* ── Grade scale data ── */
  const gradeScale = [
    { label: 'AA', min: currentCriteria.aaMin },
    { label: 'BA', min: currentCriteria.baMin },
    { label: 'BB', min: currentCriteria.bbMin },
    { label: 'CB', min: currentCriteria.cbMin },
    { label: 'CC', min: currentCriteria.ccMin },
    { label: 'DC', min: currentCriteria.dcMin },
    { label: 'DD', min: currentCriteria.ddMin },
    { label: 'FF', min: 0 },
  ];

  return (
    <div className="flex flex-col gap-4 flex-1">

      {/* ═══ HEADER BAR ═══ */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <div>
            <div className="flex items-center gap-3">
              <span className="font-mono text-lg font-bold text-primary">{gradesData.courseCode}</span>
              <span className="text-lg font-semibold text-foreground">{gradesData.courseName}</span>
            </div>
            <div className="flex items-center gap-3 mt-0.5">
              <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground">
                <Users className="h-3.5 w-3.5" />
                {gradesData.students.length} öğrenci
              </span>
              <span className="text-muted-foreground">·</span>
             <Badge
                  variant="secondary"
                  className="text-xs"
>
                {isFinalized ? 'Kesinleştirilmiş' : 'Kesinleştirilmemiş'}
              </Badge>
              {isFinalized && (
                <Badge variant={isPublished ? 'success' : 'outline'} className="text-xs">
                  {isPublished ? 'Ogrencilere acik' : 'Ogrencilere kapali'}
                </Badge>
              )}
            </div>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {!isFinalized && activeComponentId && (
            <>
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

              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowImportDialog(true)}
                className="gap-1.5 text-sm"
              >
                <Upload className="h-4 w-4" />
                Excel Yükle
              </Button>

            </>
          )}

          <Button
            variant="outline"
            size="sm"
            onClick={() => { setEditCriteria({ ...currentCriteria }); setShowCriteriaDialog(true); }}
            className="gap-1.5 text-sm"
            disabled={isFinalized}
          >
            <Settings className="h-4 w-4" />
            Harf Aralığı
          </Button>

          {isFinalized ? (
            isPublished ? (
              <Button
                variant="outline"
                size="sm"
                onClick={() => unpublishGradesMut.mutate()}
                disabled={unpublishGradesMut.isPending}
                className="gap-1.5 text-sm"
              >
                <XCircle className="h-4 w-4" /> Gosterimi Geri Al
              </Button>
            ) : (
              <>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleUnfinalize}
                  disabled={unfinalizeMut.isPending}
                  className="gap-1.5 text-sm"
                >
                  <XCircle className="h-4 w-4" /> Sonucu Geri Al
                </Button>
                <Button
                  size="sm"
                  onClick={() => publishGradesMut.mutate()}
                  disabled={publishGradesMut.isPending}
                  className="gap-1.5 text-sm"
                >
                  <CheckCircle className="h-4 w-4" /> Ogrencilerle Paylas
                </Button>
              </>
            )
          ) : (
            <div className="flex flex-col items-end gap-1">
            <Button
              size="sm"
              onClick={() => {
                if (isRelativeGrading) {
                  startRelativeFinalize();
                } else {
                  setFinalizeResults(null);
                  setShowFinalizeDialog(true);
                }
              }}
              disabled={!allGradesComplete || finalizeMut.isPending}
              title={!allGradesComplete ? 'Kesinlestirmek icin tum ogrencilerin tum notlari girilmeli.' : undefined}
              className="gap-1.5 text-sm"
            >
              <CheckCircle className="h-4 w-4" /> Kesinleştir
            </Button>
            {!allGradesComplete && (
              <span className="text-xs text-muted-foreground">
                Tum notlar girilince aktif olur.
              </span>
            )}
            </div>
          )}
        </div>
      </div>

      {isFinalized && (
        <div className="rounded-lg border bg-muted/20 px-4 py-3">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div className="text-sm font-semibold text-foreground">
                {isRelativeGrading ? 'Çan Eğrisi Özeti' : 'Mutlak Değerlendirme Özeti'}
              </div>
              <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
                {isRelativeGrading && (
                <span>
                  Sınıf ortalaması:{' '}
                  <span className="font-mono font-semibold text-foreground">
                    {studentWeightedAverages.length > 0 ? classAverage.toFixed(2) : '-'}
                  </span>
                </span>
                )}
                {isRelativeGrading && (
                <span>
                  Geçme notu:{' '}
                  <span className="font-mono font-semibold text-foreground">{currentCriteria.ddMin}</span>
                </span>
                )}
                <span>
                  Değerlendirme:{' '}
                  <span className="font-medium text-foreground">
                    {isRelativeGrading ? 'Çan eğrisi' : 'Mutlak sistem'}
                  </span>
                </span>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              {gradeScale.slice(0, 7).map(({ label, min }) => (
                <div key={label} className="flex items-center gap-1.5 rounded-md border bg-background px-2 py-1">
                  <GradeChip grade={label} />
                  <span className="text-xs font-mono text-muted-foreground">≥ {min}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* ═══ TAB BAR — Exam Types ═══ */}
      <div className="flex items-center gap-2 flex-wrap">
        {components?.map(comp => {
          const isActive = activeComponentId === comp.id;
          return (
            <button
              key={comp.id}
              onClick={() => selectComponent(comp.id)}
              className={`relative flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium transition-all ${
                isActive
                  ? 'bg-primary text-primary-foreground shadow-sm'
                  : 'bg-muted/50 text-foreground hover:bg-muted'
              }`}
            >
              <span>{comp.name}</span>
              <span className={`font-mono text-xs ${isActive ? 'text-primary-foreground/70' : 'text-muted-foreground'}`}>
                %{comp.weight}
              </span>
            </button>
          );
        })}

        {/* Weight indicator */}
        <div className="ml-auto flex items-center gap-2">
          <span className={`text-sm font-mono font-semibold ${
            totalWeight === 100 ? 'text-emerald-600' : totalWeight > 100 ? 'text-red-600' : 'text-muted-foreground'
          }`}>
            Toplam: %{totalWeight}
            {totalWeight === 100 && ' ✓'}
          </span>
        </div>
      </div>

      {/* ═══ MAIN CONTENT — Grade Table ═══ */}
      <Card className="flex-1">
        <CardContent className="p-0">
          {!activeComponentId ? (
            <div className="flex flex-col items-center justify-center py-20">
              <p className="text-base font-medium text-foreground mb-2">Henüz değerlendirme eklenmemiş</p>
              <p className="text-sm text-muted-foreground mb-5">
                Değerlendirme türleri sınav müfredatı ekranından tanımlanır.
              </p>
            </div>
          ) : (
            <>
              {/* ── Search + Save toolbar ── */}
              <div className="flex items-center justify-between px-4 py-3 border-b bg-muted/20">
                <div className="flex items-center gap-3">
                  <div className="relative">
                    <Search className="h-4 w-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
                    <Input
                      value={searchTerm}
                      onChange={(e) => {
                        setSearchTerm(e.target.value);
                        setCurrentPage(1);
                      }}
                      placeholder="Öğrenci ara..."
                      className="pl-9 w-64 h-9 text-sm"
                    />
                  </div>
                  <span className="text-sm text-muted-foreground">
                    {activeComponent?.name} — %{activeComponent?.weight} ağırlık
                  </span>
                </div>
                {!isFinalized && (
                  <Button
                    size="sm"
                    onClick={handleSave}
                    disabled={submitGradesMut.isPending}
                    className="gap-1.5"
                  >
                    <Save className="h-4 w-4" />
                    {submitGradesMut.isPending ? 'Kaydediliyor...' : 'Kaydet'}
                  </Button>
                )}
              </div>

              {/* ── Grade Table ── */}
              <div className="overflow-x-auto">
                <table className="w-full text-[15px]">
                  <thead>
                    <tr className="border-b bg-muted/30">
                      <th className="text-center py-3 px-4 font-medium text-muted-foreground w-14">#</th>
                      <th className="text-left py-3 px-4 font-medium text-muted-foreground">Öğrenci No</th>
                      <th className="text-left py-3 px-4 font-medium text-muted-foreground">Ad Soyad</th>
                      <th className="text-center py-3 px-4 font-medium text-muted-foreground w-32">Puan</th>
                      <th className="text-center py-3 px-4 font-medium text-muted-foreground w-24">Harf</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedStudents.map((student, index) => {
                     const persistedScore = activeComponentId ? student.scores[activeComponentId] : null;
                     const scoreStr = scores[student.studentId] ?? (persistedScore != null ? String(persistedScore) : '');
                     const scoreNum = scoreStr !== '' ? Number(scoreStr) : null;
                      const isValidScore =
                        scoreNum != null &&
                        !Number.isNaN(scoreNum) &&
                         scoreNum >= 0 &&
                         scoreNum <= 100;

                      const previewLetterGrade = isValidScore
                          ? resolveGrade(Math.round(scoreNum), currentCriteria)
                          : null;

                      const displayedLetterGrade = isFinalized
                          ? student.letterGrade ?? null
                          : previewLetterGrade;

                      return (
                        <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/20 transition-colors">
                          <td className="py-3 px-4 text-center text-muted-foreground font-mono text-sm">
                            {pageStart + index + 1}
                          </td>
                          <td className="py-3 px-4 font-mono text-sm">
                            {student.studentNumber}
                          </td>
                          <td className="py-3 px-4 font-medium">
                            {student.firstName} {student.lastName}
                          </td>
                          <td className="py-3 px-4 text-center">
                            {isFinalized ? (
                              <span className="font-mono text-sm font-semibold">{scoreStr || '—'}</span>
                            ) : (
                              <Input
                               type="number"
                               min={0}
                               max={100}
                               value={scoreStr}
                               onChange={(e) => setScores(prev => ({ ...prev, [student.studentId]: e.target.value }))}
                               className={`w-24 mx-auto text-center h-9 font-mono text-[15px] ${
                               scoreStr !== '' && !isValidScore ? 'border-red-500 focus-visible:ring-red-500' : ''
                                }`}
                             />
                            )}
                          </td>
                          <td className="py-3 px-4 text-center">
                            {displayedLetterGrade ? (
                              <div className="flex flex-col items-center gap-1">
                                <GradeChip grade={displayedLetterGrade} />
                                {isFinalized && student.barrierFailureReason && (
                                  <span className="max-w-44 text-xs leading-snug text-red-600">
                                    {student.barrierFailureReason}
                                  </span>
                                )}
                              </div>
                            ) : (
                            <span className="text-muted-foreground">—</span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {filteredStudents.length > 0 && (
                <div className="flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3 text-sm">
                  <span className="text-muted-foreground">
                    {pageStart + 1}-{pageEnd} / {filteredStudents.length} öğrenci
                  </span>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                      disabled={effectivePage === 1}
                    >
                      Önceki
                    </Button>
                    <span className="font-mono text-xs text-muted-foreground">
                      {effectivePage}/{totalPages}
                    </span>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
                      disabled={effectivePage === totalPages}
                    >
                      Sonraki
                    </Button>
                  </div>
                </div>
              )}

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

      {/* ═══ DIALOGS ═══ */}

      {/* Harf Aralığı Düzenleme */}
      <Dialog open={showCriteriaDialog} onOpenChange={setShowCriteriaDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Harf Notu Aralıkları</DialogTitle>
          </DialogHeader>
          {editCriteria && (
            <div className="space-y-4 pt-2">
              <p className="text-sm text-muted-foreground">
                Her harf notu için minimum puan değerini belirleyin.
              </p>

              {/* Current scale preview */}
              <div className="grid grid-cols-4 gap-2">
                {gradeScale.map(({ label, min }) => (
                  <div key={label} className="flex items-center gap-2 py-1">
                    <GradeChip grade={label} />
                    <span className="text-xs text-muted-foreground font-mono">≥ {min}</span>
                  </div>
                ))}
              </div>

              <Separator />

              {/* Editable fields */}
              <div className="grid grid-cols-2 gap-3">
                {([
                  { key: 'aaMin' as const, label: 'AA' },
                  { key: 'baMin' as const, label: 'BA' },
                  { key: 'bbMin' as const, label: 'BB' },
                  { key: 'cbMin' as const, label: 'CB' },
                  { key: 'ccMin' as const, label: 'CC' },
                  { key: 'dcMin' as const, label: 'DC' },
                  { key: 'ddMin' as const, label: 'DD' },
                ]).map(({ key, label }) => (
                  <div key={key} className="flex items-center gap-2">
                    <Label className="w-8 text-sm font-mono font-bold shrink-0">{label}</Label>
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      value={editCriteria[key]}
                      onChange={(e) => setEditCriteria(prev => prev ? { ...prev, [key]: Number(e.target.value) } : null)}
                      className="font-mono"
                    />
                  </div>
                ))}
              </div>
            </div>
          )}
          <DialogFooter className="pt-2">
            <Button variant="outline" onClick={() => setShowCriteriaDialog(false)}>İptal</Button>
            <Button onClick={handleSaveCriteria} disabled={updateCriteriaMut.isPending}>Kaydet</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

            {/* Kesinleştirme */}
      <Dialog open={showFinalizeDialog} onOpenChange={setShowFinalizeDialog}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              {finalizeResults ? (
                <><CheckCircle className="h-5 w-5 text-emerald-500" /> Notlar Kesinleştirildi</>
              ) : (
                <><AlertTriangle className="h-5 w-5 text-amber-500" /> Kesinleştirme Onayı</>
              )}
            </DialogTitle>
          </DialogHeader>

          {!finalizeResults ? (
            <div className="space-y-4 pt-1">
              <div className="rounded-lg bg-amber-50 border border-amber-200 p-4">
                <p className="text-sm text-foreground leading-relaxed">
                  Bu işlem tüm öğrencilerin ağırlıklı ortalamalarını hesaplayıp harf notlarını belirleyecektir.
                  Eksik notu olan öğrenciler <span className="font-mono font-bold text-red-600">NA</span> alacaktır.
                  Tüm öğrencilerin <span className="font-semibold">CGPA/SGPA</span> değerleri güncellenecektir.
                </p>
              </div>
              <p className="text-xs text-muted-foreground">
                Bu işlemi daha sonra geri alabilirsiniz.
              </p>
              <DialogFooter>
                <Button variant="outline" onClick={() => setShowFinalizeDialog(false)}>İptal</Button>
                <Button onClick={handleFinalize} disabled={finalizeMut.isPending} className="gap-1.5">
                  <CheckCircle className="h-4 w-4" />
                  {finalizeMut.isPending ? 'Hesaplanıyor...' : 'Onayla ve Kesinleştir'}
                </Button>
              </DialogFooter>
            </div>
          ) : (
            <div className="space-y-4 pt-1">
              <div className="rounded-lg border overflow-hidden max-h-80 overflow-y-auto">
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-background">
                    <tr className="border-b">
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Öğrenci</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Ağ. Ortalama</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Harf Notu</th>
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Not</th>
                    </tr>
                  </thead>
                  <tbody>
                    {finalizeResults.map(r => {
                      const student = gradesData.students.find(s => s.studentId === r.studentId);
                      return (
                        <tr key={r.studentId} className="border-b last:border-0">
                          <td className="py-2.5 px-4 font-medium">
                            {student ? `${student.firstName} ${student.lastName}` : `#${r.studentId}`}
                          </td>
                          <td className="py-2.5 px-4 text-center font-mono">
                            {r.weightedAverage.toFixed(1)}
                          </td>
                          <td className="py-2.5 px-4 text-center">
                            <GradeChip grade={r.letterGrade} />
                          </td>
                          <td className="py-2.5 px-4 text-xs text-red-600">
                            {r.barrierFailureReason || '—'}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => { setShowFinalizeDialog(false); setFinalizeResults(null); }}>
                  Kapat
                </Button>
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Çan Eğrisi Onayı */}
      <Dialog open={showCurveDialog} onOpenChange={setShowCurveDialog}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {curveStep === 'pass' ? 'Çan Eğrisi Geçme Notu' : 'Çan Eğrisi Harf Aralıkları'}
            </DialogTitle>
          </DialogHeader>

          {curveStep === 'pass' ? (
            <div className="space-y-4 pt-2">
              <div className="rounded-lg border bg-muted/20 p-4 text-sm">
                <div className="font-medium text-foreground">Çan eğrisi hesaplandı.</div>
                <div className="mt-1 text-muted-foreground">
                  Sınıf ortalaması <span className="font-mono font-semibold text-foreground">{classAverage.toFixed(2)}</span>.
                  Ortalama bazlı önerilen geçme notu <span className="font-mono font-semibold text-foreground">{suggestedPassMark}</span>.
                </div>
                <div className="mt-3 font-medium">
                  Geçme notu {passMarkText || suggestedPassMark} olsun mu?
                </div>
              </div>

              <div className="flex items-end gap-3">
                <div className="space-y-2">
                  <Label className="text-sm font-medium">Geçme notu</Label>
                  <Input
                    type="number"
                    min={1}
                    max={60}
                    value={passMarkText}
                    onChange={(event) => setPassMarkText(event.target.value)}
                    className="w-32 font-mono"
                  />
                </div>
                <Button
                  variant="outline"
                  onClick={() => setPassMarkText(String(suggestedPassMark))}
                >
                  Öneriyi Kullan
                </Button>
              </div>
            </div>
          ) : (
            curveCriteria && (
              <div className="space-y-4 pt-2">
                <div className="rounded-lg border bg-muted/20 p-4 text-sm">
                  Geçme notu <span className="font-mono font-semibold">{curveCriteria.ddMin}</span> kabul edildi.
                  Harf aralıkları aşağıdaki gibi olsun mu?
                </div>

                <div className="grid grid-cols-2 gap-3">
                  {([
                    { key: 'aaMin' as const, label: 'AA' },
                    { key: 'baMin' as const, label: 'BA' },
                    { key: 'bbMin' as const, label: 'BB' },
                    { key: 'cbMin' as const, label: 'CB' },
                    { key: 'ccMin' as const, label: 'CC' },
                    { key: 'dcMin' as const, label: 'DC' },
                    { key: 'ddMin' as const, label: 'DD' },
                  ]).map(({ key, label }) => (
                    <div key={key} className="flex items-center gap-2">
                      <Label className="w-8 text-sm font-mono font-bold shrink-0">{label}</Label>
                      <Input
                        type="number"
                        min={0}
                        max={100}
                        value={curveCriteria[key]}
                        onChange={(event) =>
                          setCurveCriteria((prev) => prev ? { ...prev, [key]: Number(event.target.value), calculationMethod: 'Relative' } : null)
                        }
                        className="font-mono"
                      />
                    </div>
                  ))}
                </div>

                <div className="rounded-lg border overflow-hidden">
                  <div className="px-4 py-2.5 border-b bg-muted/20 text-sm font-medium">
                    Öğrenci harf notu ön izlemesi
                  </div>
                  <div className="max-h-64 overflow-y-auto">
                    <table className="w-full text-sm">
                      <thead className="sticky top-0 bg-background">
                        <tr className="border-b">
                          <th className="text-left py-2 px-4 font-medium text-muted-foreground">Öğrenci</th>
                          <th className="text-center py-2 px-4 font-medium text-muted-foreground">Ağ. Ortalama</th>
                          <th className="text-center py-2 px-4 font-medium text-muted-foreground">Harf</th>
                        </tr>
                      </thead>
                      <tbody>
                        {gradesData.students.map((student) => {
                          const average = getStudentWeightedAverage(student);
                          const letter = average == null ? null : resolveGrade(Math.round(average), curveCriteria);

                          return (
                            <tr key={student.studentId} className="border-b last:border-0">
                              <td className="py-2 px-4">
                                <div className="font-medium">{student.firstName} {student.lastName}</div>
                                <div className="text-xs text-muted-foreground font-mono">{student.studentNumber}</div>
                              </td>
                              <td className="py-2 px-4 text-center font-mono">
                                {average == null ? '-' : average.toFixed(2)}
                              </td>
                              <td className="py-2 px-4 text-center">
                                {letter ? <GradeChip grade={letter} /> : <span className="text-muted-foreground">-</span>}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            )
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCurveDialog(false)}>İptal</Button>
            {curveStep === 'pass' ? (
              <Button onClick={approveCurvePassMark}>
                Geçme Notunu Onaylıyorum
              </Button>
            ) : (
              <Button onClick={applyCurveAndFinalize} disabled={updateCriteriaMut.isPending || finalizeMut.isPending}>
                Harf Aralıklarını Onaylıyorum
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Excel Import Dialog */}
      <Dialog open={showImportDialog} onOpenChange={setShowImportDialog}>
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <FileSpreadsheet className="h-5 w-5 text-emerald-600" />
              Excel ile Not İçe Aktar
            </DialogTitle>
          </DialogHeader>

          <div className="space-y-4 pt-2">
            <div className="space-y-2">
              <Label className="text-sm font-medium">Excel Dosyası (.xlsx)</Label>
              <Input
                type="file"
                accept=".xlsx"
                onChange={(e) => {
                  const file = e.target.files?.[0] ?? null;
                  setSelectedFile(file);
                  setPreviewResult(null);
                }}
              />
              <p className="text-xs text-muted-foreground">
                Tek ders şablonunda her sınav bileşeninin yanındaki New Score alanlarını doldurun, sonra burada yükleyin.
              </p>
            </div>

            {previewResult && (
              <div className="rounded-lg border overflow-hidden">
                <div className="flex flex-wrap gap-2 p-4 border-b bg-muted/20">
                  <Badge variant="secondary">Toplam: {previewResult.totalRows}</Badge>
                  <Badge variant="secondary">Geçerli: {previewResult.validCount}</Badge>
                  <Badge variant="secondary">Uyarı: {previewResult.warningCount}</Badge>
                  <Badge variant="secondary">Hata: {previewResult.errorCount}</Badge>
                  <Badge variant="secondary">Boş: {previewResult.emptyCount}</Badge>
                </div>
                {previewResult.errorCount > 0 && (
  <div className="px-4 py-3 border-b bg-red-50 text-red-700 text-sm">
    Hatalı satırlar içe aktarma sırasında atlanacaktır. Yalnızca geçerli satırlar kaydedilir.
  </div>
)}

                <div className="max-h-80 overflow-auto">
                  <table className="w-full text-sm">
                    <thead className="sticky top-0 bg-background border-b">
                      <tr>
                        <th className="text-left px-3 py-2 font-medium text-muted-foreground">Satır</th>
                        <th className="text-left px-3 py-2 font-medium text-muted-foreground">Öğrenci</th>
                        <th className="text-left px-3 py-2 font-medium text-muted-foreground">Bileşen</th>
                        <th className="text-center px-3 py-2 font-medium text-muted-foreground">Mevcut</th>
                        <th className="text-center px-3 py-2 font-medium text-muted-foreground">Yeni</th>
                        <th className="text-center px-3 py-2 font-medium text-muted-foreground">Durum</th>
                        <th className="text-left px-3 py-2 font-medium text-muted-foreground">Mesaj</th>
                      </tr>
                    </thead>
                    <tbody>
                      {previewResult.rows.map((row) => {
                        const statusStyle =
                          row.status === 'Valid'
                            ? 'bg-emerald-50 text-emerald-700'
                            : row.status === 'Update'
                              ? 'bg-amber-50 text-amber-700'
                              : row.status === 'Warning'
                                ? 'bg-orange-50 text-orange-700'
                                : row.status === 'Error'
                                  ? 'bg-red-50 text-red-700'
                                  : 'bg-muted text-muted-foreground';

                        return (
                          <tr key={`${row.rowNumber}-${row.componentId ?? row.componentName ?? 'row'}`} className="border-b last:border-0">
                            <td className="px-3 py-2 font-mono">{row.rowNumber}</td>
                            <td className="px-3 py-2">
                              <div className="font-medium">
                                {row.firstName} {row.lastName}
                              </div>
                              <div className="text-xs text-muted-foreground font-mono">
                                {row.studentNumber}
                              </div>
                            </td>
                            <td className="px-3 py-2 text-xs font-medium">
                              {row.componentName || '—'}
                            </td>
                            <td className="px-3 py-2 text-center font-mono">
                              {row.currentScore ?? '—'}
                            </td>
                            <td className="px-3 py-2 text-center font-mono">
                              {(row.newScore ?? row.rawNewScore) || '—'}
                            </td>
                            <td className="px-3 py-2 text-center">
                              <span className={`inline-flex px-2 py-1 rounded-md text-xs font-medium ${statusStyle}`}>
                                {row.status}
                              </span>
                            </td>
                            <td className="px-3 py-2 text-xs text-muted-foreground">
                              {row.message}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setShowImportDialog(false);
                setSelectedFile(null);
                setPreviewResult(null);
              }}
            >
              Kapat
            </Button>

            <Button
              variant="outline"
              onClick={handlePreviewImport}
              disabled={!selectedFile || previewImportMut.isPending}
              className="gap-1.5"
            >
              <FileSpreadsheet className="h-4 w-4" />
              {previewImportMut.isPending ? 'Önizleniyor...' : 'Önizleme Al'}
            </Button>

            <Button
              onClick={handleApplyImport}
              disabled={
  !selectedFile ||
  !previewResult ||
  previewResult.errorCount > 0 ||
  applyImportMut.isPending
}
              className="gap-1.5"
            >
              <Upload className="h-4 w-4" />
              {applyImportMut.isPending ? 'İçe Aktarılıyor...' : 'Onayla ve İçe Aktar'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function buildCriteriaFromPassMark(passMark: number, calculationMethod: 'Absolute' | 'Relative' = 'Relative'): GradingCriteria {
  const ddMin = clamp(Math.round(passMark), 1, 60);
  return {
    ddMin,
    dcMin: clamp(ddMin + 5, 2, 76),
    ccMin: clamp(ddMin + 10, 3, 82),
    cbMin: clamp(ddMin + 16, 4, 88),
    bbMin: clamp(ddMin + 26, 5, 94),
    baMin: clamp(ddMin + 36, 6, 98),
    aaMin: clamp(ddMin + 46, 7, 100),
    calculationMethod,
  };
}
