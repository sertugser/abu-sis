import { useState, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import { Progress } from '@/components/ui/progress';
import { Separator } from '@/components/ui/separator';
import { Tooltip, TooltipTrigger, TooltipContent, TooltipProvider } from '@/components/ui/tooltip';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { useAcademicOperationsContext, useGradeLockReadiness } from '@/hooks/useAcademicOperations';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '@/hooks/useAdminAcademicStructure';
import type { AccessibleDepartment } from '@/types/academicOperations';
import {
  useTermClosingStatus,
  usePromotionPreview,
  useLockGrades,
  useUnlockGrades,
  useCalculateGpa,
  usePromote,
  useRollbackPromotion,
  useOpenNewTerm,
} from '@/hooks/useTermClosing';
import type { StudentProgressionDto } from '@/api/termClosing.api';
import { cn } from '@/lib/utils';
import {
  Loader2,
  Lock,
  Calculator,
  GraduationCap,
  CalendarPlus,
  CheckCircle2,
  AlertTriangle,
  Info,
  ChevronRight,
  FileText,
  Users,
  BookOpen,
  TrendingUp,
  ArrowUpDown,
  RotateCcw,
  Shield,
} from 'lucide-react';

/* ══════════════════════════════════════════════════════════════════
   STEP CONFIG
   ══════════════════════════════════════════════════════════════════ */

const STEPS = [
  { id: 1, label: 'Not Kilitleme', icon: Lock, description: 'Notlari kesinlestir' },
  { id: 2, label: 'GPA Hesaplama', icon: Calculator, description: 'Ortalamalari hesapla' },
  { id: 3, label: 'Sinif Atlatma', icon: GraduationCap, description: 'Seviyeleri guncelle' },
  { id: 4, label: 'Yeni Donem', icon: CalendarPlus, description: 'Donemi olustur' },
];

/* ══════════════════════════════════════════════════════════════════
   MAIN PAGE
   ══════════════════════════════════════════════════════════════════ */

export default function TermClosingWizard() {
  const opsContext = useAcademicOperationsContext();
  // useMemo ile stable referans (?? [] her render'da yeni array yaratırdı)
  const departments = useMemo(
    () => opsContext.data?.accessibleDepartments ?? [],
    [opsContext.data],
  );
  const { data: faculties } = useAdminFaculties();
  const { data: structureDepartments } = useAdminDepartmentsByStructure();

  const [selectedFacultyId, setSelectedFacultyId] = useState<number | null>(null);
  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);

  // Kullanıcının erişebildiği fakülteler
  const accessibleDeptIds = useMemo(() => new Set(departments.map(d => d.departmentId)), [departments]);
  const availableFaculties = useMemo(() => {
    if (!faculties || !structureDepartments) return [];
    const facultyIds = new Set(
      structureDepartments.filter(sd => accessibleDeptIds.has(sd.id)).map(sd => sd.facultyId)
    );
    return faculties.filter(f => facultyIds.has(f.id));
  }, [faculties, structureDepartments, accessibleDeptIds]);

  // Fakülteye göre filtrelenmiş bölümler
  const filteredDepartments = useMemo(() => {
    if (!selectedFacultyId || !structureDepartments) return departments;
    const deptIdsInFaculty = new Set(
      structureDepartments.filter(sd => sd.facultyId === selectedFacultyId).map(sd => sd.id)
    );
    return departments.filter(d => deptIdsInFaculty.has(d.departmentId));
  }, [departments, structureDepartments, selectedFacultyId]);

  const deptId = useMemo(() => {
    if (selectedDeptId && filteredDepartments.some(d => d.departmentId === selectedDeptId)) return selectedDeptId;
    return filteredDepartments[0]?.departmentId ?? null;
  }, [selectedDeptId, filteredDepartments]);

  const statusQuery = useTermClosingStatus(deptId);
  const status = statusQuery.data;
  const readinessQuery = useGradeLockReadiness(deptId);
  const readiness = readinessQuery.data;

  const completedSteps = useMemo(() => {
    const set = new Set<number>();
    status?.steps.forEach((s, i) => { if (s.isCompleted) set.add(i + 1); });
    return set;
  }, [status]);

  const completedCount = completedSteps.size;
  const progressPct = (completedCount / 4) * 100;

  const [currentStep, setCurrentStep] = useState<number | null>(null);
  const effectiveStep = useMemo(() => {
    if (currentStep !== null) return currentStep;
    if (!status) return 1;
    for (let i = 0; i < status.steps.length; i++) {
      if (!status.steps[i].isCompleted) return i + 1;
    }
    return 4;
  }, [currentStep, status]);

  const handleFacultyChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setSelectedFacultyId(e.target.value ? Number(e.target.value) : null);
    setSelectedDeptId(null);
    setCurrentStep(null);
  };

  const handleDeptChange = (newDeptId: number) => {
    setSelectedDeptId(newDeptId);
    setCurrentStep(null);
  };

  const goForward = () => { if (effectiveStep < 4) setCurrentStep(effectiveStep + 1); };
  const goBack = () => { if (effectiveStep > 1) setCurrentStep(effectiveStep - 1); };

  if (!opsContext.data) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground gap-2">
        <Loader2 className="h-5 w-5 animate-spin" />
        <span>Yukleniyor...</span>
      </div>
    );
  }

  return (
    <TooltipProvider delayDuration={200}>
      <div className="space-y-6">
        {/* ── Header Bar ── */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="flex items-center rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
              <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-r border-gray-200 whitespace-nowrap">Fakülte</span>
              <select
                className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[140px]"
                value={selectedFacultyId ?? ''}
                onChange={handleFacultyChange}
              >
                <option value="">Tümü</option>
                {availableFaculties.map(f => (
                  <option key={f.id} value={f.id}>{f.name}</option>
                ))}
              </select>
              <span className="px-3 py-1.5 text-xs font-semibold text-gray-600 bg-gray-50 border-l border-r border-gray-200 whitespace-nowrap">Bölüm</span>
              <select
                className="h-8 border-0 bg-transparent px-2.5 text-xs focus:ring-0 focus:outline-none min-w-[200px]"
                value={deptId ?? ''}
                onChange={e => handleDeptChange(Number(e.target.value))}
              >
                {filteredDepartments.map((d: AccessibleDepartment) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.departmentCode} — {d.departmentName}
                  </option>
                ))}
              </select>
            </div>
            {status?.termName && (
              <Badge variant="outline" className="font-mono text-xs gap-1">
                <FileText className="h-3 w-3" />
                {status.termName}
              </Badge>
            )}
          </div>

          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Shield className="h-4 w-4" />
            <span>{opsContext.data.roleLabel === 'SystemAdministrator' ? 'Sistem Yoneticisi' : opsContext.data.roleLabel === 'Dean' ? 'Dekan' : 'Bolum Baskani'}</span>
          </div>
        </div>

        {/* ── Summary Cards ── */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <SummaryCard
            label="Toplam Sube"
            value={readiness?.totalSections ?? '—'}
            icon={BookOpen}
            color="blue"
          />
          <SummaryCard
            label="Aktif Kayit"
            value={readiness?.totalActiveRegistrations ?? '—'}
            icon={Users}
            color="violet"
          />
          <SummaryCard
            label="Eksik Not"
            value={readiness?.missingGradeCount ?? '—'}
            icon={AlertTriangle}
            color={readiness && readiness.missingGradeCount > 0 ? 'amber' : 'emerald'}
          />
          <SummaryCard
            label="Ilerleme"
            value={`${completedCount}/4`}
            icon={TrendingUp}
            color="emerald"
            sub={completedCount === 4 ? 'Tamamlandi' : `Adim ${effectiveStep}'de`}
          />
        </div>

        {/* ── Progress + Stepper ── */}
        <Card>
          <CardContent className="pt-6 pb-4 space-y-4">
            {/* Overall progress */}
            <div className="flex items-center justify-between text-sm mb-1">
              <span className="text-muted-foreground font-medium">Genel Ilerleme</span>
              <span className="font-bold text-primary">{Math.round(progressPct)}%</span>
            </div>
            <Progress
              value={progressPct}
              indicatorClassName={completedCount === 4 ? 'bg-emerald-500' : 'bg-primary'}
              className="h-2"
            />

            {/* Step indicators */}
            <div className="grid grid-cols-4 gap-2 pt-2">
              {STEPS.map((step) => {
                const isDone = completedSteps.has(step.id);
                const isCurrent = effectiveStep === step.id;
                const stepStatus = status?.steps[step.id - 1];
                const Icon = step.icon;

                return (
                  <Tooltip key={step.id}>
                    <TooltipTrigger asChild>
                      <button
                        onClick={() => setCurrentStep(step.id)}
                        className={cn(
                          'flex flex-col items-center gap-1.5 p-3 rounded-lg transition-all text-center',
                          'hover:bg-muted/50 cursor-pointer',
                          isCurrent && 'bg-primary/5 ring-1 ring-primary/20',
                        )}
                      >
                        <div className={cn(
                          'w-10 h-10 rounded-full flex items-center justify-center transition-all',
                          isDone && 'bg-emerald-500 text-white',
                          isCurrent && !isDone && 'bg-primary text-white shadow-md shadow-primary/20',
                          !isDone && !isCurrent && 'bg-muted text-muted-foreground',
                        )}>
                          {isDone ? <CheckCircle2 className="h-5 w-5" /> : <Icon className="h-4.5 w-4.5" />}
                        </div>
                        <div className="space-y-0.5">
                          <p className={cn(
                            'text-xs font-semibold leading-tight',
                            isCurrent ? 'text-primary' : isDone ? 'text-emerald-700' : 'text-muted-foreground'
                          )}>
                            {step.label}
                          </p>
                          <p className="text-[10px] text-muted-foreground leading-tight hidden sm:block">{step.description}</p>
                        </div>
                      </button>
                    </TooltipTrigger>
                    <TooltipContent side="bottom" className="text-xs max-w-52">
                      {isDone && stepStatus?.completedByName
                        ? <span>{stepStatus.completedByName} tarafindan tamamlandi{stepStatus.completedAt ? ` — ${new Date(stepStatus.completedAt).toLocaleString('tr-TR')}` : ''}</span>
                        : isDone ? 'Tamamlandi' : isCurrent ? 'Su anki adim' : 'Henuz baslanmadi'}
                    </TooltipContent>
                  </Tooltip>
                );
              })}
            </div>
          </CardContent>
        </Card>

        {/* ── Step Content ── */}
        <div className="min-h-[300px]">
          {deptId && effectiveStep === 1 && (
            <GradeLockStep key={`lock-${deptId}`} departmentId={deptId} isCompleted={completedSteps.has(1)} readiness={readiness} onNext={goForward} />
          )}
          {deptId && effectiveStep === 2 && (
            <GpaStep key={`gpa-${deptId}`} departmentId={deptId} isCompleted={completedSteps.has(2)} step1Completed={completedSteps.has(1)} onNext={goForward} onBack={goBack} />
          )}
          {deptId && effectiveStep === 3 && (
            <PromotionStep key={`promote-${deptId}`} departmentId={deptId} isCompleted={completedSteps.has(3)} step2Completed={completedSteps.has(2)} onNext={goForward} onBack={goBack} />
          )}
          {deptId && effectiveStep === 4 && (
            <NewTermStep key={`term-${deptId}`} departmentId={deptId} isCompleted={completedSteps.has(4)} step3Completed={completedSteps.has(3)} completedNotes={status?.steps[3]?.notes ?? null} onBack={goBack} />
          )}
        </div>

        {/* ── Completion Banner ── */}
        {completedCount === 4 && (
          <Alert variant="success" className="animate-fade-in">
            <CheckCircle2 className="h-5 w-5" />
            <AlertTitle>Donem Kapatma Tamamlandi</AlertTitle>
            <AlertDescription>
              Tum adimlar basariyla tamamlandi. Yeni donem acildi ve kullanima hazir.
            </AlertDescription>
          </Alert>
        )}
      </div>
    </TooltipProvider>
  );
}

/* ══════════════════════════════════════════════════════════════════
   SUMMARY CARD
   ══════════════════════════════════════════════════════════════════ */

const colorMap = {
  blue: 'border-l-blue-500 text-blue-600',
  violet: 'border-l-violet-500 text-violet-600',
  amber: 'border-l-amber-500 text-amber-600',
  emerald: 'border-l-emerald-500 text-emerald-600',
} as const;

function SummaryCard({ label, value, icon: Icon, color, sub }: {
  label: string; value: string | number; icon: React.ElementType; color: keyof typeof colorMap; sub?: string;
}) {
  const cls = colorMap[color];
  return (
    <Card className={cn('border-l-4 hover:-translate-y-0.5 hover:shadow-md transition-all', cls.split(' ')[0])}>
      <CardContent className="p-4 flex items-center gap-3">
        <div className={cn('p-2 rounded-lg bg-muted/50', cls.split(' ')[1])}>
          <Icon className="h-4.5 w-4.5" />
        </div>
        <div className="min-w-0">
          <p className="text-xs text-muted-foreground uppercase tracking-wide font-medium truncate">{label}</p>
          <p className="text-xl font-bold tabular-nums">{value}</p>
          {sub && <p className="text-[10px] text-muted-foreground">{sub}</p>}
        </div>
      </CardContent>
    </Card>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 1: GRADE LOCK
   ══════════════════════════════════════════════════════════════════ */

interface ReadinessData {
  totalSections: number;
  totalActiveRegistrations: number;
  missingGradeCount: number;
  allGradesEntered: boolean;
  hasActiveRegistrations: boolean;
  notes: string[];
}

function GradeLockStep({ departmentId, isCompleted, readiness, onNext }: {
  departmentId: number; isCompleted: boolean; readiness?: ReadinessData | null; onNext: () => void;
}) {
  const lockMutation = useLockGrades(departmentId);
  const unlockMutation = useUnlockGrades(departmentId);

  const handleLock = async () => {
    try {
      const result = await lockMutation.mutateAsync();
      if (result.success) onNext();
    } catch { /* error displayed below */ }
  };

  const missingPct = readiness && readiness.totalActiveRegistrations > 0
    ? Math.round((readiness.missingGradeCount / readiness.totalActiveRegistrations) * 100)
    : 0;
  const enteredPct = 100 - missingPct;

  return (
    <StepShell
      stepNum={1}
      title="Not Girisi Kilitleme"
      description="Tum ogretim uyeleri icin not girisi ve duzenleme islemlerini kilitleyin. Eksik notlar NA olarak atanir."
      icon={Lock}
      isCompleted={isCompleted}
      onBack={undefined}
    >
      {/* Pre-check: Readiness */}
      {!isCompleted && readiness && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <ReadinessItem
              label="Toplam Sube"
              value={readiness.totalSections}
              status="neutral"
            />
            <ReadinessItem
              label="Not Girilen"
              value={`${readiness.totalActiveRegistrations - readiness.missingGradeCount} / ${readiness.totalActiveRegistrations}`}
              status={readiness.allGradesEntered ? 'ok' : 'warning'}
            />
            <ReadinessItem
              label="Eksik Not"
              value={readiness.missingGradeCount}
              status={readiness.missingGradeCount === 0 ? 'ok' : 'warning'}
            />
          </div>

          {readiness.totalActiveRegistrations > 0 && (
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Not Giris Orani</span>
                <span className="font-semibold tabular-nums">{enteredPct}%</span>
              </div>
              <Progress
                value={enteredPct}
                indicatorClassName={enteredPct === 100 ? 'bg-emerald-500' : enteredPct > 80 ? 'bg-amber-500' : 'bg-red-500'}
              />
            </div>
          )}

          {readiness.missingGradeCount > 0 && (
            <Alert variant="warning">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Eksik Not Uyarisi</AlertTitle>
              <AlertDescription>
                {readiness.missingGradeCount} ogrencinin ({missingPct}%) notu eksik.
                Kilitleme isleminde bu ogrencilere otomatik olarak <strong>NA (Devamsiz)</strong> atanacaktir.
              </AlertDescription>
            </Alert>
          )}

          {readiness.allGradesEntered && (
            <Alert variant="success">
              <CheckCircle2 className="h-4 w-4" />
              <AlertTitle>Hazir</AlertTitle>
              <AlertDescription>Tum notlar girilmis. Kilitleme islemine gecebilirsiniz.</AlertDescription>
            </Alert>
          )}
        </div>
      )}

      {/* Completed state */}
      {isCompleted && (
        <div className="flex items-center justify-between p-3 rounded-lg bg-emerald-50 border border-emerald-200">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-emerald-600" />
            <span className="text-sm font-medium text-emerald-800">Notlar kilitlendi ve hesaplandi</span>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => unlockMutation.mutate()}
            disabled={unlockMutation.isPending}
            className="gap-1.5"
          >
            <RotateCcw className="h-3.5 w-3.5" />
            {unlockMutation.isPending ? 'Geri aliniyor...' : 'Geri Al'}
          </Button>
        </div>
      )}

      {/* Actions */}
      {!isCompleted && (
        <div className="flex items-center gap-3 pt-2">
          <ConfirmButton
            label="Notlari Kilitle"
            icon={Lock}
            isLoading={lockMutation.isPending}
            disabled={!readiness?.hasActiveRegistrations}
            confirmTitle="Notlari kilitlemek istiyor musunuz?"
            confirmDescription="Tum sube'ler kilitlenecek, eksik notlar NA olarak atanacak ve ogrenci GPA'lari yeniden hesaplanacaktir. Bu islem geri alinabilir."
            onConfirm={handleLock}
          />
        </div>
      )}

      <MutationFeedback mutation={lockMutation} />
      <MutationFeedback mutation={unlockMutation} successMsg="Kilit geri alindi. Ogretim uyeleri notlari tekrar duzenleyebilir." />
    </StepShell>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 2: GPA CALCULATION
   ══════════════════════════════════════════════════════════════════ */

function GpaStep({ departmentId, isCompleted, step1Completed, onNext, onBack }: {
  departmentId: number; isCompleted: boolean; step1Completed: boolean; onNext: () => void; onBack: () => void;
}) {
  const gpaMutation = useCalculateGpa(departmentId);

  const handleCalc = async () => {
    try {
      await gpaMutation.mutateAsync();
      onNext();
    } catch { /* shown below */ }
  };

  const result = gpaMutation.data;

  return (
    <StepShell stepNum={2} title="GPA Hesaplama" description="Tum ogrencilerin donem (ANO) ve genel (AGNO) not ortalamasini yeniden hesaplayin." icon={Calculator} isCompleted={isCompleted} onBack={onBack}>
      {!step1Completed && !isCompleted && (
        <PrerequisiteAlert stepName="Not Kilitleme (Adim 1)" />
      )}

      {result && (
        <div className="grid grid-cols-3 gap-3">
          <ReadinessItem label="Toplam Ogrenci" value={result.totalStudents} status="neutral" />
          <ReadinessItem label="Hesaplanan" value={result.recalculatedCount} status="ok" />
          <ReadinessItem label="GPA Degisen" value={result.changedCount} status={result.changedCount > 0 ? 'warning' : 'ok'} />
        </div>
      )}

      {gpaMutation.isPending && (
        <div className="flex items-center gap-3 p-4 rounded-lg bg-muted/30">
          <Loader2 className="h-5 w-5 animate-spin text-primary" />
          <div>
            <p className="text-sm font-medium">GPA hesaplaniyor...</p>
            <p className="text-xs text-muted-foreground">Ogrenci sayisina gore bu islem birkac dakika surebilir.</p>
          </div>
        </div>
      )}

      {isCompleted && !result && (
        <div className="flex items-center gap-2 p-3 rounded-lg bg-emerald-50 border border-emerald-200">
          <CheckCircle2 className="h-5 w-5 text-emerald-600" />
          <span className="text-sm font-medium text-emerald-800">GPA hesaplama tamamlandi</span>
        </div>
      )}

      {!isCompleted && step1Completed && (
        <ConfirmButton
          label="GPA Hesapla"
          icon={Calculator}
          isLoading={gpaMutation.isPending}
          confirmTitle="GPA hesaplamasini baslatmak istiyor musunuz?"
          confirmDescription="Tum ogrencilerin ANO ve AGNO degerleri yeniden hesaplanacaktir."
          onConfirm={handleCalc}
        />
      )}

      <MutationFeedback mutation={gpaMutation} />
    </StepShell>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 3: PROMOTION
   ══════════════════════════════════════════════════════════════════ */

function PromotionStep({ departmentId, isCompleted, step2Completed, onNext, onBack }: {
  departmentId: number; isCompleted: boolean; step2Completed: boolean; onNext: () => void; onBack: () => void;
}) {
  const previewQuery = usePromotionPreview(departmentId, !isCompleted && step2Completed);
  const promoteMutation = usePromote(departmentId);
  const rollbackMutation = useRollbackPromotion(departmentId);

  const preview = previewQuery.data;
  const students: StudentProgressionDto[] = preview?.results ?? [];

  const handlePromote = async () => {
    try {
      await promoteMutation.mutateAsync();
      onNext();
    } catch { /* shown below */ }
  };

  return (
    <StepShell stepNum={3} title="Sinif Atlatma" description="Ogrenci sinif seviyelerini akademik ilerleme kurallarina gore guncelleyin." icon={GraduationCap} isCompleted={isCompleted} onBack={onBack}>
      {!step2Completed && !isCompleted && (
        <PrerequisiteAlert stepName="GPA Hesaplama (Adim 2)" />
      )}

      {/* Preview Statistics */}
      {preview && (
        <div className="space-y-4">
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <ReadinessItem label="Toplam Ogrenci" value={preview.totalStudentsAnalyzed} status="neutral" />
            <ReadinessItem label="Yukselecek" value={preview.promotedCount} status="ok" />
            <ReadinessItem label="Kalacak" value={preview.stayedCount} status="neutral" />
            <ReadinessItem label="Uyari" value={preview.warningCount} status={preview.warningCount > 0 ? 'warning' : 'ok'} />
          </div>

          {preview.totalStudentsAnalyzed > 0 && (
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Terfi Orani</span>
                <span className="font-semibold tabular-nums">
                  {Math.round((preview.promotedCount / preview.totalStudentsAnalyzed) * 100)}%
                </span>
              </div>
              <Progress
                value={(preview.promotedCount / preview.totalStudentsAnalyzed) * 100}
                indicatorClassName="bg-emerald-500"
              />
            </div>
          )}
        </div>
      )}

      {previewQuery.isLoading && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Onizleme hazirlaniyor...
        </div>
      )}

      {/* Student Table */}
      {students.length > 0 && (
        <div className="rounded-lg border overflow-hidden">
          <div className="max-h-80 overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="bg-muted/60 sticky top-0 z-10">
                <tr>
                  <th className="px-3 py-2.5 text-left font-semibold text-xs uppercase tracking-wider">Ogrenci</th>
                  <th className="px-3 py-2.5 text-center font-semibold text-xs uppercase tracking-wider">Eski</th>
                  <th className="px-3 py-2.5 text-center font-semibold text-xs uppercase tracking-wider">
                    <ArrowUpDown className="h-3 w-3 inline mr-1" />
                    Yeni
                  </th>
                  <th className="px-3 py-2.5 text-right font-semibold text-xs uppercase tracking-wider">AKTS</th>
                  <th className="px-3 py-2.5 text-right font-semibold text-xs uppercase tracking-wider">AGNO</th>
                  <th className="px-3 py-2.5 text-center font-semibold text-xs uppercase tracking-wider">Durum</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {students.map(row => (
                  <tr key={row.studentId} className={cn(
                    'hover:bg-muted/20 transition-colors',
                    row.status.startsWith('Warning') && 'bg-amber-50/50',
                  )}>
                    <td className="px-3 py-2">
                      <p className="font-medium text-xs">{row.fullName}</p>
                      <p className="text-[10px] text-muted-foreground font-mono">{row.studentNumber}</p>
                    </td>
                    <td className="px-3 py-2 text-center text-xs">{row.oldLevelLabel}</td>
                    <td className="px-3 py-2 text-center">
                      <span className={cn('text-xs font-semibold', row.wasPromoted ? 'text-emerald-600' : 'text-muted-foreground')}>
                        {row.newLevelLabel}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums text-xs">{row.completedEcts}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-xs font-medium">{row.cgpa.toFixed(2)}</td>
                    <td className="px-3 py-2 text-center">
                      {row.status.startsWith('Warning') ? (
                        <Badge variant="warning" className="text-[10px]">Uyari</Badge>
                      ) : row.wasPromoted ? (
                        <Badge variant="success" className="text-[10px]">Terfi</Badge>
                      ) : (
                        <Badge variant="secondary" className="text-[10px]">Sabit</Badge>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Completed */}
      {isCompleted && (
        <div className="flex items-center justify-between p-3 rounded-lg bg-emerald-50 border border-emerald-200">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-emerald-600" />
            <span className="text-sm font-medium text-emerald-800">Sinif atlatma uygulandi</span>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => rollbackMutation.mutate()}
            disabled={rollbackMutation.isPending}
            className="gap-1.5"
          >
            <RotateCcw className="h-3.5 w-3.5" />
            {rollbackMutation.isPending ? 'Geri aliniyor...' : 'Geri Al'}
          </Button>
        </div>
      )}

      {!isCompleted && step2Completed && (
        <ConfirmButton
          label="Sinif Atlatmayi Uygula"
          icon={GraduationCap}
          isLoading={promoteMutation.isPending}
          confirmTitle="Sinif atlatma islemini uygulamak istiyor musunuz?"
          confirmDescription={preview
            ? `${preview.promotedCount} ogrenci sinif atlayacak, ${preview.stayedCount} ogrenci kalacak. Bu islem geri alinabilir.`
            : 'Ogrenci sinif seviyeleri guncellenecektir.'}
          onConfirm={handlePromote}
        />
      )}

      <MutationFeedback mutation={promoteMutation} />
      <MutationFeedback mutation={rollbackMutation} successMsg="Sinif atlatma geri alindi. Ogrenci seviyeleri eski haline donduruldu." />
    </StepShell>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 4: NEW TERM
   ══════════════════════════════════════════════════════════════════ */

function NewTermStep({ departmentId, isCompleted, step3Completed, completedNotes, onBack }: {
  departmentId: number; isCompleted: boolean; step3Completed: boolean; completedNotes: string | null; onBack: () => void;
}) {
  const openTermMutation = useOpenNewTerm(departmentId);

  const [termName, setTermName] = useState('');
  const [termYear, setTermYear] = useState(new Date().getFullYear());
  const [semester, setSemester] = useState(2);
  const [regStart, setRegStart] = useState('');
  const [regEnd, setRegEnd] = useState('');
  const [termStart, setTermStart] = useState('');
  const [termEnd, setTermEnd] = useState('');

  const handleOpen = async () => {
    try {
      await openTermMutation.mutateAsync({
        name: termName,
        year: termYear,
        semester,
        registrationStart: new Date(regStart).toISOString(),
        registrationEnd: new Date(regEnd).toISOString(),
        termStart: new Date(termStart).toISOString(),
        termEnd: new Date(termEnd).toISOString(),
      });
    } catch { /* shown below */ }
  };

  const dateErrors: string[] = [];
  if (regStart && regEnd && new Date(regEnd) <= new Date(regStart))
    dateErrors.push('Kayit bitis tarihi baslangictan sonra olmalidir.');
  if (termStart && termEnd && new Date(termEnd) <= new Date(termStart))
    dateErrors.push('Donem bitis tarihi baslangictan sonra olmalidir.');

  const canSubmit = termName && regStart && regEnd && termStart && termEnd && dateErrors.length === 0;

  return (
    <StepShell stepNum={4} title="Yeni Donem Olustur" description="Yeni akademik donemi tanimlayip kayit takvimini ayarlayin." icon={CalendarPlus} isCompleted={isCompleted} onBack={onBack}>
      {!step3Completed && !isCompleted && (
        <PrerequisiteAlert stepName="Sinif Atlatma (Adim 3)" />
      )}

      {!isCompleted && step3Completed && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <FormField label="Donem Adi" required>
              <Input placeholder="2025-2026 Bahar" value={termName} onChange={e => setTermName(e.target.value)} />
            </FormField>
            <FormField label="Yil">
              <Input type="number" value={termYear} onChange={e => setTermYear(Number(e.target.value))} />
            </FormField>
            <FormField label="Donem Tipi">
              <select className="w-full border rounded-md px-3 py-2 text-sm bg-card" value={semester} onChange={e => setSemester(Number(e.target.value))}>
                <option value={1}>Guz</option>
                <option value={2}>Bahar</option>
                <option value={3}>Yaz</option>
              </select>
            </FormField>
          </div>

          <Separator />

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FormField label="Kayit Baslangici" required>
              <Input type="date" value={regStart} onChange={e => setRegStart(e.target.value)} />
            </FormField>
            <FormField label="Kayit Bitisi" required>
              <Input type="date" value={regEnd} onChange={e => setRegEnd(e.target.value)} />
            </FormField>
            <FormField label="Donem Baslangici" required>
              <Input type="date" value={termStart} onChange={e => setTermStart(e.target.value)} />
            </FormField>
            <FormField label="Donem Bitisi" required>
              <Input type="date" value={termEnd} onChange={e => setTermEnd(e.target.value)} />
            </FormField>
          </div>

          {dateErrors.map((err, i) => (
            <Alert key={i} variant="destructive" className="py-2">
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>{err}</AlertDescription>
            </Alert>
          ))}
        </div>
      )}

      {isCompleted && (
        <Alert variant="success">
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Yeni Donem Olusturuldu</AlertTitle>
          <AlertDescription>
            {openTermMutation.data
              ? `"${openTermMutation.data.message}" (ID: ${openTermMutation.data.newTermId})`
              : completedNotes ?? 'Yeni donem basariyla olusturuldu.'}
          </AlertDescription>
        </Alert>
      )}

      {!isCompleted && step3Completed && (
        <ConfirmButton
          label="Donemi Olustur"
          icon={CalendarPlus}
          isLoading={openTermMutation.isPending}
          disabled={!canSubmit}
          confirmTitle="Yeni donemi acmak istiyor musunuz?"
          confirmDescription={`"${termName || '(isimsiz)'}" donemi olusturulacak ve kayit tarihleri ayarlanacaktir.`}
          onConfirm={handleOpen}
        />
      )}

      <MutationFeedback mutation={openTermMutation} />
    </StepShell>
  );
}

/* ══════════════════════════════════════════════════════════════════
   SHARED COMPONENTS
   ══════════════════════════════════════════════════════════════════ */

function StepShell({ stepNum, title, description, icon: Icon, isCompleted, onBack, children }: {
  stepNum: number; title: string; description: string; icon: React.ElementType; isCompleted: boolean; onBack?: () => void; children: React.ReactNode;
}) {
  return (
    <Card className="animate-fade-in">
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className={cn(
              'p-2.5 rounded-lg',
              isCompleted ? 'bg-emerald-100 text-emerald-600' : 'bg-primary/10 text-primary',
            )}>
              <Icon className="h-5 w-5" />
            </div>
            <div>
              <CardTitle className="text-base">
                Adim {stepNum}: {title}
              </CardTitle>
              <CardDescription className="mt-0.5">{description}</CardDescription>
            </div>
          </div>
          {isCompleted && <Badge variant="success">Tamamlandi</Badge>}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {children}
        {onBack && (
          <div className="pt-2">
            <Button variant="ghost" size="sm" onClick={onBack} className="gap-1 text-muted-foreground">
              <ChevronRight className="h-3.5 w-3.5 rotate-180" />
              Onceki adim
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function ReadinessItem({ label, value, status }: {
  label: string; value: string | number; status: 'ok' | 'warning' | 'neutral';
}) {
  return (
    <div className={cn(
      'rounded-lg border p-3 text-center',
      status === 'ok' && 'border-emerald-200 bg-emerald-50/50',
      status === 'warning' && 'border-amber-200 bg-amber-50/50',
      status === 'neutral' && 'bg-muted/20',
    )}>
      <p className="text-xs text-muted-foreground mb-0.5">{label}</p>
      <p className={cn(
        'text-lg font-bold tabular-nums',
        status === 'ok' && 'text-emerald-700',
        status === 'warning' && 'text-amber-700',
      )}>{value}</p>
    </div>
  );
}

function PrerequisiteAlert({ stepName }: { stepName: string }) {
  return (
    <Alert variant="info">
      <Info className="h-4 w-4" />
      <AlertTitle>Onkosul</AlertTitle>
      <AlertDescription>
        Bu adima gecmek icin once <strong>{stepName}</strong> adiminin tamamlanmasi gerekmektedir.
      </AlertDescription>
    </Alert>
  );
}

function ConfirmButton({ label, icon: Icon, isLoading, disabled, confirmTitle, confirmDescription, onConfirm }: {
  label: string; icon: React.ElementType; isLoading: boolean; disabled?: boolean; confirmTitle: string; confirmDescription: string; onConfirm: () => void;
}) {
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button disabled={isLoading || disabled} className="gap-2">
          {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Icon className="h-4 w-4" />}
          {isLoading ? 'Isleniyor...' : label}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{confirmTitle}</AlertDialogTitle>
          <AlertDialogDescription>{confirmDescription}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Iptal</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Onayla</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function FormField({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs font-medium">
        {label}
        {required && <span className="text-destructive ml-0.5">*</span>}
      </Label>
      {children}
    </div>
  );
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function MutationFeedback({ mutation, successMsg }: { mutation: any; successMsg?: string }) {
  if (mutation.isError) {
    return (
      <Alert variant="destructive" className="py-2 animate-fade-in">
        <AlertTriangle className="h-4 w-4" />
        <AlertDescription>{(mutation.error as Error)?.message ?? 'Beklenmeyen bir hata olustu.'}</AlertDescription>
      </Alert>
    );
  }
  if (mutation.isSuccess && successMsg) {
    return (
      <Alert variant="success" className="py-2 animate-fade-in">
        <CheckCircle2 className="h-4 w-4" />
        <AlertDescription>{successMsg}</AlertDescription>
      </Alert>
    );
  }
  return null;
}
