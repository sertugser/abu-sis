import { useEffect, useState, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
  Plus,
  Trash2,
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
  BarChart2,
} from 'lucide-react';
import { computeScoreStats, letterDistribution, passFailCounts } from '@/lib/gradeStats';
import {
  useGrades,
  useComponents,
  useGradingCriteria,
  useAddComponent,
  useDeleteComponent,
  useSubmitGrades,
  useFinalize,
  useUnfinalize,
  useUpdateGradingCriteria,
  useDownloadTemplate,
  usePreviewImport,
  useApplyImport,
} from '../hooks/useGradeData';
import type {
  GradeComponentType,
  GradingCriteria,
  FinalizePreviewItem,
  ImportPreviewResponse,
} from '../types/grade';

/* ── Bileşen türü etiketleri ── */
const COMPONENT_TYPE_LABELS: Record<GradeComponentType, string> = {
  Other: 'Diğer',
  Midterm: 'Ara Sınav',
  Final: 'Final',
};

const COMPONENT_TYPE_BADGE_CLASS: Record<GradeComponentType, string> = {
  Other: 'bg-slate-100 text-slate-600',
  Midterm: 'bg-blue-50 text-blue-600',
  Final: 'bg-purple-50 text-purple-700',
};

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

/* ── Score histogram buckets ── */
const HISTOGRAM_BUCKETS: { label: string; min: number; max: number }[] = [
  { label: '0–49', min: 0, max: 49 },
  { label: '50–59', min: 50, max: 59 },
  { label: '60–69', min: 60, max: 69 },
  { label: '70–79', min: 70, max: 79 },
  { label: '80–89', min: 80, max: 89 },
  { label: '90–100', min: 90, max: 100 },
];

const LETTER_ORDER = ['AA', 'BA', 'BB', 'CB', 'CC', 'DC', 'DD', 'FF', 'NA'] as const;

/* ── Letter distribution mini-row ── */
function LetterDistBar({ dist, total }: { dist: Record<string, number>; total: number }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {LETTER_ORDER.map((letter) => {
        const n = dist[letter] ?? 0;
        if (n === 0) return null;
        const style = GRADE_STYLE[letter];
        return (
          <span
            key={letter}
            className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-semibold font-mono ${style?.bg ?? 'bg-muted'} ${style?.text ?? 'text-foreground'}`}
            title={`${Math.round((n / total) * 100)}%`}
          >
            {letter}
            <span className="opacity-75">{n}</span>
          </span>
        );
      })}
    </div>
  );
}

/* ── Class statistics panel ── */
interface ClassStatsPanelProps {
  scores: number[];
  resolve: (score: number) => string;
}

function ClassStatsPanel({ scores, resolve }: ClassStatsPanelProps) {
  const stats = useMemo(() => computeScoreStats(scores), [scores]);
  const dist = useMemo(() => letterDistribution(scores, resolve), [scores, resolve]);
  const pf = useMemo(() => passFailCounts(scores, resolve), [scores, resolve]);

  if (!stats) return null;

  const bucketCounts = HISTOGRAM_BUCKETS.map((b) => ({
    label: b.label,
    count: scores.filter((s) => s >= b.min && s <= b.max).length,
  }));
  const maxBucketCount = Math.max(...bucketCounts.map((b) => b.count), 1);

  const passRate = stats.count > 0 ? Math.round((pf.pass / stats.count) * 100) : 0;

  return (
    <Card className="mb-4">
      <CardHeader className="pb-2 pt-4 px-4">
        <CardTitle className="text-sm font-semibold flex items-center gap-2 text-foreground">
          <BarChart2 className="h-4 w-4 text-primary" />
          Sınıf İstatistikleri
          <span className="ml-auto text-xs text-muted-foreground font-normal">
            {stats.count} notlu öğrenci
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="px-4 pb-4 space-y-4">
        {/* Key metrics */}
        <div className="grid grid-cols-3 gap-3 sm:grid-cols-6">
          {[
            { label: 'Ortalama', value: stats.mean.toFixed(1) },
            { label: 'Std. Sapma', value: stats.stdDev.toFixed(1) },
            { label: 'Medyan', value: stats.median.toFixed(1) },
            { label: 'En Düşük', value: stats.min.toFixed(0) },
            { label: 'En Yüksek', value: stats.max.toFixed(0) },
            { label: 'Geçme %', value: `%${passRate}` },
          ].map(({ label, value }) => (
            <div key={label} className="rounded-lg bg-muted/40 px-3 py-2 text-center">
              <div className="text-[11px] text-muted-foreground leading-none mb-1">{label}</div>
              <div className="text-sm font-bold font-mono text-foreground">{value}</div>
            </div>
          ))}
        </div>

        {/* Histogram */}
        <div>
          <p className="text-[11px] text-muted-foreground mb-1.5 uppercase tracking-wide">Puan Dağılımı</p>
          <div className="flex items-end gap-1.5 h-16">
            {bucketCounts.map(({ label, count }) => {
              const heightPct = Math.round((count / maxBucketCount) * 100);
              return (
                <div key={label} className="flex flex-col items-center gap-0.5 flex-1">
                  <span className="text-[10px] text-muted-foreground font-mono leading-none">{count > 0 ? count : ''}</span>
                  <div className="w-full rounded-sm bg-primary/20 flex items-end" style={{ height: '40px' }}>
                    <div
                      className="w-full rounded-sm bg-primary/60 transition-all"
                      style={{ height: `${heightPct}%` }}
                    />
                  </div>
                  <span className="text-[9px] text-muted-foreground leading-none">{label}</span>
                </div>
              );
            })}
          </div>
        </div>

        {/* Letter distribution */}
        <div>
          <p className="text-[11px] text-muted-foreground mb-1.5 uppercase tracking-wide">Harf Dağılımı</p>
          <LetterDistBar dist={dist} total={stats.count} />
          <div className="mt-2 flex items-center gap-3 text-xs text-muted-foreground">
            <span className="text-emerald-600 font-medium">Geçen: {pf.pass}</span>
            <span className="text-red-600 font-medium">Kalan: {pf.fail}</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

/* ── Live letter-distribution preview for criteria dialog ── */
interface CriteriaPreviewProps {
  scores: number[];
  criteria: import('../types/grade').GradingCriteria;
}

function CriteriaPreview({ scores, criteria }: CriteriaPreviewProps) {
  const resolve = (s: number) => resolveGrade(s, criteria);
  const dist = useMemo(() => letterDistribution(scores, resolve), [scores, criteria]);
  const pf = useMemo(() => passFailCounts(scores, resolve), [scores, criteria]);
  const total = scores.length;

  if (total === 0) return null;

  return (
    <div className="rounded-md border bg-muted/30 px-4 py-3 space-y-2">
      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
        Bu Eşiklerle Dağılım Önizlemesi ({total} öğrenci)
      </p>
      <LetterDistBar dist={dist} total={total} />
      <div className="flex items-center gap-3 text-xs">
        <span className="text-emerald-600 font-semibold">Geçen: {pf.pass}</span>
        <span className="text-red-600 font-semibold">Kalan: {pf.fail}</span>
        {total > 0 && (
          <span className="text-muted-foreground ml-auto">
            Geçme oranı: %{Math.round((pf.pass / total) * 100)}
          </span>
        )}
      </div>
    </div>
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

  const addComponentMut = useAddComponent(id);
  const deleteComponentMut = useDeleteComponent(id);
  const submitGradesMut = useSubmitGrades(id);
  const finalizeMut = useFinalize(id);
  const unfinalizeMut = useUnfinalize(id);
  const updateCriteriaMut = useUpdateGradingCriteria(id);
  const downloadTemplateMut = useDownloadTemplate(id);
const previewImportMut = usePreviewImport(id);
const applyImportMut = useApplyImport(id);

  const [selectedComponentId, setSelectedComponentId] = useState<number | null>(null);
  const [scores, setScores] = useState<Record<number, string>>({});
  const [searchTerm, setSearchTerm] = useState('');
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [newComponent, setNewComponent] = useState<{ name: string; weight: string; type: GradeComponentType }>({ name: '', weight: '', type: 'Other' });
  const [showCriteriaDialog, setShowCriteriaDialog] = useState(false);
  const [editCriteria, setEditCriteria] = useState<GradingCriteria | null>(null);
  const [showFinalizeDialog, setShowFinalizeDialog] = useState(false);
  const [finalizeResults, setFinalizeResults] = useState<FinalizePreviewItem[] | null>(null);
  const [showImportDialog, setShowImportDialog] = useState(false);
const [selectedFile, setSelectedFile] = useState<File | null>(null);
const [previewResult, setPreviewResult] = useState<ImportPreviewResponse | null>(null);

  const isFinalized = gradesData?.isFinalized ?? false;
  const currentCriteria = criteria ?? {
    aaMin: 91,
    baMin: 81,
    bbMin: 71,
    cbMin: 61,
    ccMin: 55,
    dcMin: 50,
    ddMin: 45,
  };
  const activeComponentId = selectedComponentId ?? components?.[0]?.id ?? null;
  const totalWeight = components?.reduce((sum, c) => sum + c.weight, 0) ?? 0;
  const activeComponent = components?.find(c => c.id === activeComponentId);
  const hideStatistics = gradesData?.hideStatistics ?? false;

  // Collect non-null weightedAverage values for statistics (available after grades are saved)
  const weightedAverageScores = useMemo(
    () => (gradesData?.students ?? []).map(s => s.weightedAverage).filter((v): v is number => v != null),
    [gradesData],
  );

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
  };

  const handleSave = () => {
  if (!activeComponentId) return;

  const invalidEntry = Object.entries(scores).find(([, score]) => {
    if (score === '') return false;
    const numericScore = Number(score);
    return Number.isNaN(numericScore) || numericScore < 0 || numericScore > 100;
  });

  if (invalidEntry) {
    alert('Notlar 0 ile 100 arasında olmalıdır.');
    return;
  }

  const grades = Object.entries(scores).map(([studentId, score]) => ({
    studentId: Number(studentId),
    score: score === '' ? null : Number(score),
  }));

  submitGradesMut.mutate(
    { componentId: activeComponentId, grades },
    {
      onError: (err: unknown) => {
        const error = err as { response?: { data?: { error?: string } } };
        alert(error.response?.data?.error ?? 'Notlar kaydedilemedi.');
      },
    }
  );
};

  const handleAddComponent = () => {
    if (!newComponent.name || !newComponent.weight) return;
    addComponentMut.mutate(
      { name: newComponent.name, weight: Number(newComponent.weight), order: 0, type: newComponent.type },
      {
        onSuccess: () => {
          setShowAddDialog(false);
          setNewComponent({ name: '', weight: '', type: 'Other' });
        },
      }
    );
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
              {!hideStatistics && (
                <>
                  <span className="inline-flex items-center gap-1.5 text-sm text-muted-foreground">
                    <Users className="h-3.5 w-3.5" />
                    {gradesData.students.length} öğrenci
                  </span>
                  <span className="text-muted-foreground">·</span>
                </>
              )}
             <Badge
                  variant="secondary"
                  className="text-xs"
>
                {isFinalized ? 'Kesinleştirilmiş' : 'Kesinleştirilmemiş'}
              </Badge>
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
            <Button
              size="sm"
              onClick={() => setShowFinalizeDialog(true)}
              className="gap-1.5 text-sm"
            >
              <CheckCircle className="h-4 w-4" /> Kesinleştir
            </Button>
          )}
        </div>
      </div>

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
              {comp.type && comp.type !== 'Other' && (
                <span className={`text-[10px] font-medium px-1.5 py-0.5 rounded ${
                  isActive
                    ? 'bg-primary-foreground/15 text-primary-foreground/80'
                    : COMPONENT_TYPE_BADGE_CLASS[comp.type]
                }`}>
                  {COMPONENT_TYPE_LABELS[comp.type]}
                </span>
              )}
              {!isFinalized && (
                <button
                  onClick={(e) => { e.stopPropagation(); deleteComponentMut.mutate(comp.id); }}
                  className={`ml-1 p-0.5 rounded transition-colors ${
                    isActive
                      ? 'hover:bg-primary-foreground/15 text-primary-foreground/50 hover:text-primary-foreground'
                      : 'hover:bg-destructive/10 text-muted-foreground/50 hover:text-destructive'
                  }`}
                  title="Sil"
                >
                  <Trash2 className="h-3 w-3" />
                </button>
              )}
            </button>
          );
        })}

        {!isFinalized && (
          <button
            onClick={() => setShowAddDialog(true)}
            className="flex items-center gap-1.5 px-4 py-2.5 rounded-lg text-sm font-medium border border-dashed border-muted-foreground/30 text-muted-foreground hover:border-primary/50 hover:text-primary hover:bg-primary/5 transition-all"
          >
            <Plus className="h-4 w-4" />
            Ekle
          </button>
        )}

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

      {/* ═══ CLASS STATISTICS PANEL ═══ */}
      {!hideStatistics && weightedAverageScores.length > 0 && (
        <ClassStatsPanel
          scores={weightedAverageScores}
          resolve={(s) => resolveGrade(Math.round(s), currentCriteria)}
        />
      )}

      {/* ═══ MAIN CONTENT — Grade Table ═══ */}
      <Card className="flex-1">
        <CardContent className="p-0">
          {!activeComponentId ? (
            /* ── Empty state — no exam types defined ── */
            <div className="flex flex-col items-center justify-center py-20">
              <p className="text-base font-medium text-foreground mb-2">Henüz değerlendirme eklenmemiş</p>
              <p className="text-sm text-muted-foreground mb-5">
                Vize, Final, Ödev gibi değerlendirme türlerini ekleyerek not girişine başlayın.
              </p>
              {!isFinalized && (
                <Button variant="outline" onClick={() => setShowAddDialog(true)} className="gap-1.5">
                  <Plus className="h-4 w-4" /> İlk Değerlendirmeyi Ekle
                </Button>
              )}
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
                      onChange={e => setSearchTerm(e.target.value)}
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
                    {filteredStudents.map((student, index) => {
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
                            {index + 1}
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
                            <GradeChip grade={displayedLetterGrade} />
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

      {/* Değerlendirme Ekleme */}
      <Dialog open={showAddDialog} onOpenChange={setShowAddDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Yeni Değerlendirme Ekle</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 pt-2">
            <div className="space-y-2">
              <Label className="text-sm font-medium">Değerlendirme Adı</Label>
              <Input
                placeholder="Vize, Final, Ödev, Proje..."
                value={newComponent.name}
                onChange={(e) => setNewComponent(prev => ({ ...prev, name: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label className="text-sm font-medium">Tür</Label>
              <Select
                value={newComponent.type}
                onValueChange={(val) => setNewComponent(prev => ({ ...prev, type: val as GradeComponentType }))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Other">Diğer</SelectItem>
                  <SelectItem value="Midterm">Ara Sınav</SelectItem>
                  <SelectItem value="Final">Final</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label className="text-sm font-medium">Ağırlık (%)</Label>
              <Input
                type="number"
                min={1}
                max={100}
                placeholder="40"
                value={newComponent.weight}
                onChange={(e) => setNewComponent(prev => ({ ...prev, weight: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter className="pt-2">
            <Button variant="outline" onClick={() => setShowAddDialog(false)}>İptal</Button>
            <Button onClick={handleAddComponent} disabled={addComponentMut.isPending} className="gap-1.5">
              <Plus className="h-4 w-4" /> Ekle
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Harf Aralığı Düzenleme */}
      <Dialog open={showCriteriaDialog} onOpenChange={setShowCriteriaDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Harf Notu Aralıkları</DialogTitle>
          </DialogHeader>
          {editCriteria && (
            <div className="space-y-4 pt-2">
              {criteria?.editable === false && (
                <div className="rounded-md bg-muted px-4 py-3 text-sm text-muted-foreground">
                  Bu dersin harf aralıkları sınav yönergesinde kilitli — değiştirilemez.
                </div>
              )}

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
                      disabled={criteria?.editable === false}
                    />
                  </div>
                ))}
              </div>

              {/* Live distribution preview — shown for all (editable or locked) when scores exist */}
              {!hideStatistics && weightedAverageScores.length > 0 && (
                <CriteriaPreview
                  scores={weightedAverageScores}
                  criteria={editCriteria}
                />
              )}
            </div>
          )}
          <DialogFooter className="pt-2">
            <Button variant="outline" onClick={() => setShowCriteriaDialog(false)}>İptal</Button>
            <Button
              onClick={handleSaveCriteria}
              disabled={updateCriteriaMut.isPending || criteria?.editable === false}
            >
              Kaydet
            </Button>
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
                  {!hideStatistics && (
                    <> Tüm öğrencilerin <span className="font-semibold">CGPA/SGPA</span> değerleri güncellenecektir.</>
                  )}
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
                      {!hideStatistics && (
                        <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Ağ. Ortalama</th>
                      )}
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Harf Notu</th>
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
                          {!hideStatistics && (
                            <td className="py-2.5 px-4 text-center font-mono">
                              {r.weightedAverage.toFixed(1)}
                            </td>
                          )}
                          <td className="py-2.5 px-4 text-center">
                            <GradeChip grade={r.letterGrade} />
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
                Önce aktif bileşen için şablonu indirip doldurun, sonra burada yükleyin.
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
                          <tr key={row.rowNumber} className="border-b last:border-0">
                            <td className="px-3 py-2 font-mono">{row.rowNumber}</td>
                            <td className="px-3 py-2">
                              <div className="font-medium">
                                {row.firstName} {row.lastName}
                              </div>
                              <div className="text-xs text-muted-foreground font-mono">
                                {row.studentNumber}
                              </div>
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
