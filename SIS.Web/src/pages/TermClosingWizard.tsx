import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { cn } from '@/lib/utils';
import { useAcademicOperationsContext } from '@/hooks/useAcademicOperations';
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
import { Loader2 } from 'lucide-react';

const STEPS = [
  { id: 1, label: 'Not Girisi Kilitle' },
  { id: 2, label: 'GPA Hesapla' },
  { id: 3, label: 'Sinif Atla' },
  { id: 4, label: 'Yeni Donem Ac' },
];

export default function TermClosingWizard() {
  const opsContext = useAcademicOperationsContext();
  const departments = opsContext.data?.accessibleDepartments ?? [];

  const [selectedDeptId, setSelectedDeptId] = useState<number | null>(null);
  const deptId = selectedDeptId ?? opsContext.data?.defaultDepartmentId ?? departments[0]?.departmentId ?? null;

  const statusQuery = useTermClosingStatus(deptId);
  const status = statusQuery.data;

  // Determine current step from backend status
  const completedSteps = new Set<number>();
  status?.steps.forEach((s, i) => {
    if (s.isCompleted) completedSteps.add(i + 1);
  });

  const [currentStep, setCurrentStep] = useState<number | null>(null);
  // Auto-detect which step user should be on
  const effectiveStep = currentStep ?? (() => {
    if (!status) return 1;
    for (let i = 0; i < status.steps.length; i++) {
      if (!status.steps[i].isCompleted) return i + 1;
    }
    return 4; // all done
  })();

  const goBack = () => {
    if (effectiveStep > 1) setCurrentStep(effectiveStep - 1);
  };

  const goForward = () => {
    if (effectiveStep < 4) setCurrentStep(effectiveStep + 1);
  };

  if (!opsContext.data) {
    return <div className="text-center py-12 text-muted-foreground">Yukleniyor...</div>;
  }

  return (
    <div className="space-y-6">
      {/* Department selector for multi-dept managers */}
      {departments.length > 1 && (
        <div className="flex items-center gap-3">
          <Label>Bolum:</Label>
          <select
            className="border rounded px-3 py-1.5 text-sm"
            value={deptId ?? ''}
            onChange={e => { setSelectedDeptId(Number(e.target.value)); setCurrentStep(null); }}
          >
            {departments.map((d: AccessibleDepartment) => (
              <option key={d.departmentId} value={d.departmentId}>{d.departmentName}</option>
            ))}
          </select>
        </div>
      )}

      {/* Step indicator */}
      <Card>
        <CardContent className="py-6">
          <div className="flex items-center justify-between">
            {STEPS.map((step, idx) => {
              const isDone = completedSteps.has(step.id);
              const isCurrent = effectiveStep === step.id;
              const isFuture = !isDone && !isCurrent;

              return (
                <div key={step.id} className="flex items-center flex-1">
                  <div
                    className="flex flex-col items-center flex-1 cursor-pointer"
                    onClick={() => setCurrentStep(step.id)}
                  >
                    <div
                      className={cn(
                        'w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold transition-colors',
                        isDone && 'bg-emerald-500 text-white',
                        isCurrent && 'bg-primary text-white ring-4 ring-primary/20',
                        isFuture && 'bg-muted text-muted-foreground',
                      )}
                    >
                      {isDone ? '\u2713' : `${step.id}`}
                    </div>
                    <span className={cn(
                      'text-xs mt-2 text-center',
                      isCurrent ? 'font-semibold text-primary' : 'text-muted-foreground',
                    )}>
                      {step.label}
                    </span>
                  </div>
                  {idx < STEPS.length - 1 && (
                    <div className={cn('h-0.5 w-full mx-2 rounded', isDone ? 'bg-emerald-400' : 'bg-muted')} />
                  )}
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Step content */}
      {deptId && effectiveStep === 1 && (
        <GradeLockStep departmentId={deptId} isCompleted={completedSteps.has(1)} onNext={goForward} />
      )}
      {deptId && effectiveStep === 2 && (
        <GpaStep departmentId={deptId} isCompleted={completedSteps.has(2)} onNext={goForward} onBack={goBack} />
      )}
      {deptId && effectiveStep === 3 && (
        <PromotionStep departmentId={deptId} isCompleted={completedSteps.has(3)} onNext={goForward} onBack={goBack} />
      )}
      {deptId && effectiveStep === 4 && (
        <NewTermStep departmentId={deptId} isCompleted={completedSteps.has(4)} onBack={goBack} />
      )}

      {completedSteps.size === 4 && (
        <Card className="border-emerald-300 bg-emerald-50/50">
          <CardContent className="flex items-center gap-4 p-6">
            <span className="text-4xl">&#127881;</span>
            <div>
              <h2 className="text-lg font-bold text-emerald-800">Donem Kapatma Tamamlandi!</h2>
              <p className="text-sm text-emerald-700">Tum adimlar basariyla tamamlandi. Yeni donem acildi.</p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 1: GRADE LOCK
   ══════════════════════════════════════════════════════════════════ */

function GradeLockStep({ departmentId, isCompleted, onNext }: { departmentId: number; isCompleted: boolean; onNext: () => void }) {
  const lockMutation = useLockGrades(departmentId);
  const unlockMutation = useUnlockGrades(departmentId);

  const handleLock = async () => {
    const result = await lockMutation.mutateAsync();
    if (result.success) onNext();
  };

  return (
    <StepCard
      title="Adim 1: Not Girisi Kilitle"
      description="Tum ogretim uyeleri icin not girisi ve duzenleme islemlerini kilitleyin."
      actionLabel="Notlari Kilitle"
      isLoading={lockMutation.isPending}
      isCompleted={isCompleted}
      confirmTitle="Notlari kilitlemek istiyor musunuz?"
      confirmDescription="Bu islem tum sube'leri kilitleyecek ve notlari hesaplayacaktir. Eksik not girisi olan ogrenciler NA alacaktir. Bu islem geri alinabilir."
      onAction={handleLock}
    >
      <div className="space-y-3">
        {!isCompleted && (
          <div className="flex items-center gap-3 p-3 rounded-lg bg-amber-50 border border-amber-200">
            <span className="text-2xl">&#9888;&#65039;</span>
            <p className="text-sm">Bu islem tum section'lari kilitleyecek ve notlari hesaplayacaktir. Eksik not girisi olan ogrenciler NA alacaktir.</p>
          </div>
        )}
        {isCompleted && (
          <div className="flex items-center justify-between">
            <Badge variant="success">Kilitlendi</Badge>
            <Button
              variant="outline"
              size="sm"
              onClick={() => unlockMutation.mutate()}
              disabled={unlockMutation.isPending}
            >
              Kilidi Ac (Geri Al)
            </Button>
          </div>
        )}
        {lockMutation.isError && (
          <p className="text-sm text-red-500">{(lockMutation.error as Error).message}</p>
        )}
      </div>
    </StepCard>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 2: GPA CALCULATION
   ══════════════════════════════════════════════════════════════════ */

function GpaStep({ departmentId, isCompleted, onNext, onBack }: { departmentId: number; isCompleted: boolean; onNext: () => void; onBack: () => void }) {
  const gpaMutation = useCalculateGpa(departmentId);

  const handleCalc = async () => {
    await gpaMutation.mutateAsync();
    onNext();
  };

  const result = gpaMutation.data;

  return (
    <StepCard
      title="Adim 2: GPA Hesapla"
      description="Tum ogrenciler icin donem ve genel not ortalamasini yeniden hesaplayin."
      actionLabel="GPA Hesapla"
      isLoading={gpaMutation.isPending}
      isCompleted={isCompleted}
      onBack={onBack}
      confirmTitle="GPA hesaplamasini baslatmak istiyor musunuz?"
      confirmDescription="Tum ogrencilerin donem ve genel not ortalamalari yeniden hesaplanacaktir. Bu islem geri alinabilir."
      onAction={handleCalc}
    >
      <div className="space-y-3">
        {result && (
          <div className="grid grid-cols-3 gap-4">
            <MiniStat label="Toplam Ogrenci" value={String(result.totalStudents)} />
            <MiniStat label="Hesaplanan" value={String(result.recalculatedCount)} />
            <MiniStat label="Degisiklik" value={String(result.changedCount)} color={result.changedCount > 0 ? 'text-amber-600' : undefined} />
          </div>
        )}
        {gpaMutation.isPending && (
          <div className="flex items-center gap-2">
            <Loader2 className="h-4 w-4 animate-spin text-primary" />
            <span className="text-sm text-muted-foreground">GPA hesaplaniyor...</span>
          </div>
        )}
        {isCompleted && !result && <Badge variant="success">Hesaplandi</Badge>}
      </div>
    </StepCard>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 3: PROMOTION
   ══════════════════════════════════════════════════════════════════ */

function PromotionStep({ departmentId, isCompleted, onNext, onBack }: { departmentId: number; isCompleted: boolean; onNext: () => void; onBack: () => void }) {
  const previewQuery = usePromotionPreview(departmentId, !isCompleted);
  const promoteMutation = usePromote(departmentId);
  const rollbackMutation = useRollbackPromotion(departmentId);

  const preview = previewQuery.data;

  const handlePromote = async () => {
    await promoteMutation.mutateAsync();
    onNext();
  };

  const students: StudentProgressionDto[] = preview?.results ?? [];

  return (
    <StepCard
      title="Adim 3: Sinif Atlama (Akademik Ilerleme)"
      description="Toplam AKTS'ye gore ogrenci sinif seviyelerini guncelleyin."
      actionLabel="Sinif Atlamayi Uygula"
      isLoading={promoteMutation.isPending}
      isCompleted={isCompleted}
      onBack={onBack}
      confirmTitle="Sinif atlama islemini uygulamak istiyor musunuz?"
      confirmDescription={`${preview ? `${preview.promotedCount} ogrenci sinif atlayacak, ${preview.stayedCount} ogrenci mevcut sinifinda kalacak.` : 'Ogrenci sinif seviyeleri AKTS toplamina gore guncellenecektir.'} Bu islem geri alinabilir.`}
      onAction={handlePromote}
    >
      <div className="space-y-4">
        {preview && (
          <div className="grid grid-cols-4 gap-4">
            <MiniStat label="Toplam Ogrenci" value={String(preview.totalStudentsAnalyzed)} />
            <MiniStat label="Yukselecek" value={String(preview.promotedCount)} color="text-emerald-600" />
            <MiniStat label="Kalacak" value={String(preview.stayedCount)} />
            <MiniStat label="Uyari (CGPA<1.70)" value={String(preview.warningCount)} color="text-red-500" />
          </div>
        )}

        {previewQuery.isLoading && <p className="text-sm text-muted-foreground">Onizleme yukleniyor...</p>}

        {students.length > 0 && (
          <div className="rounded-lg border overflow-hidden max-h-96 overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="bg-muted/50 sticky top-0">
                <tr>
                  <th className="px-4 py-2 text-left font-semibold">Ogrenci No</th>
                  <th className="px-4 py-2 text-left font-semibold">Ad Soyad</th>
                  <th className="px-4 py-2 text-left font-semibold">Eski Sinif</th>
                  <th className="px-4 py-2 text-left font-semibold">Yeni Sinif</th>
                  <th className="px-4 py-2 text-right font-semibold">AKTS</th>
                  <th className="px-4 py-2 text-left font-semibold">Durum</th>
                </tr>
              </thead>
              <tbody>
                {students.map(row => (
                  <tr key={row.studentId} className="border-t">
                    <td className="px-4 py-2 font-mono text-xs">{row.studentNumber}</td>
                    <td className="px-4 py-2">{row.fullName}</td>
                    <td className="px-4 py-2">{row.oldLevelLabel}</td>
                    <td className="px-4 py-2 font-semibold">{row.newLevelLabel}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{row.completedEcts}</td>
                    <td className="px-4 py-2">
                      {row.status.startsWith('Warning') ? (
                        <Badge variant="warning">CGPA: {row.cgpa.toFixed(2)}</Badge>
                      ) : (
                        <Badge variant="success">Normal</Badge>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {isCompleted && (
          <div className="flex items-center justify-between">
            <Badge variant="success">Uyguland\u0131</Badge>
            <Button
              variant="outline"
              size="sm"
              onClick={() => rollbackMutation.mutate()}
              disabled={rollbackMutation.isPending}
            >
              Geri Al
            </Button>
          </div>
        )}
      </div>
    </StepCard>
  );
}

/* ══════════════════════════════════════════════════════════════════
   STEP 4: NEW TERM
   ══════════════════════════════════════════════════════════════════ */

function NewTermStep({ departmentId, isCompleted, onBack }: { departmentId: number; isCompleted: boolean; onBack: () => void }) {
  const openTermMutation = useOpenNewTerm(departmentId);

  const [termName, setTermName] = useState('');
  const [termYear, setTermYear] = useState(new Date().getFullYear());
  const [semester, setSemester] = useState(2); // Spring
  const [regStart, setRegStart] = useState('');
  const [regEnd, setRegEnd] = useState('');
  const [termStart, setTermStart] = useState('');
  const [termEnd, setTermEnd] = useState('');

  const handleOpen = async () => {
    await openTermMutation.mutateAsync({
      name: termName,
      year: termYear,
      semester,
      registrationStart: new Date(regStart).toISOString(),
      registrationEnd: new Date(regEnd).toISOString(),
      termStart: new Date(termStart).toISOString(),
      termEnd: new Date(termEnd).toISOString(),
    });
  };

  const canSubmit = termName && regStart && regEnd && termStart && termEnd;

  return (
    <StepCard
      title="Adim 4: Yeni Donem Ac"
      description="Yeni akademik donemi olusturun ve ders kayit takvimini ayarlayin."
      actionLabel="Donemi Ac"
      isLoading={openTermMutation.isPending}
      isCompleted={isCompleted}
      onBack={onBack}
      actionDisabled={!canSubmit}
      confirmTitle="Yeni donemi acmak istiyor musunuz?"
      confirmDescription={`"${termName || '(isimsiz)'}" adli yeni donem olusturulacaktir. Kayit tarihleri ve donem takvimi ayarlanacaktir.`}
      onAction={handleOpen}
    >
      <div className="space-y-4">
        {!isCompleted && (
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label>Donem Adi</Label>
              <Input placeholder="2025-2026 Bahar" value={termName} onChange={e => setTermName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Yil</Label>
              <Input type="number" value={termYear} onChange={e => setTermYear(Number(e.target.value))} />
            </div>
            <div className="space-y-1.5">
              <Label>Donem Tipi</Label>
              <select className="w-full border rounded px-3 py-2 text-sm" value={semester} onChange={e => setSemester(Number(e.target.value))}>
                <option value={1}>Guz</option>
                <option value={2}>Bahar</option>
                <option value={3}>Yaz</option>
              </select>
            </div>
            <div />
            <div className="space-y-1.5">
              <Label>Kayit Baslangici</Label>
              <Input type="date" value={regStart} onChange={e => setRegStart(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Kayit Bitisi</Label>
              <Input type="date" value={regEnd} onChange={e => setRegEnd(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Donem Baslangici</Label>
              <Input type="date" value={termStart} onChange={e => setTermStart(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Donem Bitisi</Label>
              <Input type="date" value={termEnd} onChange={e => setTermEnd(e.target.value)} />
            </div>
          </div>
        )}

        {isCompleted && openTermMutation.data && (
          <Badge variant="success">Yeni donem acildi (ID: {openTermMutation.data.newTermId})</Badge>
        )}

        {openTermMutation.isError && (
          <p className="text-sm text-red-500">{(openTermMutation.error as Error).message}</p>
        )}
      </div>
    </StepCard>
  );
}

/* ══════════════════════════════════════════════════════════════════
   SHARED COMPONENTS
   ══════════════════════════════════════════════════════════════════ */

interface StepCardProps {
  title: string;
  description: string;
  onAction: () => void;
  actionLabel: string;
  isLoading: boolean;
  isCompleted: boolean;
  confirmTitle: string;
  confirmDescription: string;
  onBack?: () => void;
  actionDisabled?: boolean;
  children: React.ReactNode;
}

function StepCard({ title, description, onAction, actionLabel, isLoading, isCompleted, confirmTitle, confirmDescription, onBack, actionDisabled, children }: StepCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <p className="text-sm text-muted-foreground">{description}</p>
      </CardHeader>
      <CardContent className="space-y-4">
        {children}
        <div className="flex gap-3 pt-4 border-t">
          {onBack && <Button variant="outline" onClick={onBack}>&laquo; Geri</Button>}
          {!isCompleted && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button disabled={isLoading || actionDisabled}>
                  {isLoading ? (
                    <span className="flex items-center gap-2">
                      <Loader2 className="h-4 w-4 animate-spin" />
                      Isleniyor...
                    </span>
                  ) : actionLabel}
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>{confirmTitle}</AlertDialogTitle>
                  <AlertDialogDescription>{confirmDescription}</AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Iptal</AlertDialogCancel>
                  <AlertDialogAction onClick={onAction}>Onayla</AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function MiniStat({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="rounded-lg border bg-muted/20 p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={cn('text-lg font-bold', color)}>{value}</p>
    </div>
  );
}
