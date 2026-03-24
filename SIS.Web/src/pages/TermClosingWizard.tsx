import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';

/* ── Adım tanımları ── */
const STEPS = [
  { id: 1, label: 'Not Girişi Kilitle' },
  { id: 2, label: 'GPA Hesapla' },
  { id: 3, label: 'Sınıf Atla' },
  { id: 4, label: 'Yeni Dönem Aç' },
];

/* ── Mock veri ── */
interface PromotionRow {
  studentNo: string;
  oldLevel: string;
  newLevel: string;
  ects: number;
  cgpa: number;
  status: 'normal' | 'warning';
}

const MOCK_PROMOTIONS: PromotionRow[] = [
  { studentNo: '2023BM0018', oldLevel: '1.Sınıf', newLevel: '1.Sınıf', ects: 58, cgpa: 1.92, status: 'warning' },
  { studentNo: '2022BM0042', oldLevel: '2.Sınıf', newLevel: '3.Sınıf', ects: 135, cgpa: 2.85, status: 'normal' },
  { studentNo: '2021BM0005', oldLevel: '3.Sınıf', newLevel: '4.Sınıf', ects: 185, cgpa: 3.12, status: 'normal' },
  { studentNo: '2022BM0099', oldLevel: '2.Sınıf', newLevel: '2.Sınıf', ects: 115, cgpa: 1.65, status: 'warning' },
  { studentNo: '2023BM0122', oldLevel: '1.Sınıf', newLevel: '2.Sınıf', ects: 62, cgpa: 2.45, status: 'normal' },
];

/* ────────────────────── Sayfa ────────────────────── */

export default function TermClosingWizard() {
  const [currentStep, setCurrentStep] = useState(3);
  const [completedSteps, setCompletedSteps] = useState<Set<number>>(new Set([1, 2]));

  const totalStudents = 342;
  const willPromote = 298;
  const willStay = 12;
  const warningCount = 32;

  const completeStep = (stepId: number) => {
    setCompletedSteps(prev => new Set([...prev, stepId]));
    if (stepId < 4) setCurrentStep(stepId + 1);
  };

  const goBack = () => {
    if (currentStep > 1) setCurrentStep(currentStep - 1);
  };

  return (
    <div className="space-y-6">
      {/* Başlık */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
          🧙 Akademik Dönem Kapatma
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Dönem sonu işlemlerini adım adım tamamlayın
        </p>
      </div>

      {/* Adım göstergesi */}
      <Card>
        <CardContent className="py-6">
          <div className="flex items-center justify-between">
            {STEPS.map((step, idx) => {
              const isDone = completedSteps.has(step.id);
              const isCurrent = currentStep === step.id;
              const isFuture = !isDone && !isCurrent;

              return (
                <div key={step.id} className="flex items-center flex-1">
                  <div className="flex flex-col items-center flex-1">
                    <div
                      className={cn(
                        'w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold transition-colors',
                        isDone && 'bg-emerald-500 text-white',
                        isCurrent && 'bg-primary text-white ring-4 ring-primary/20',
                        isFuture && 'bg-muted text-muted-foreground',
                      )}
                    >
                      {isDone ? '✅' : `${step.id}`}
                    </div>
                    <span
                      className={cn(
                        'text-xs mt-2 text-center',
                        isCurrent ? 'font-semibold text-primary' : 'text-muted-foreground',
                      )}
                    >
                      {step.label}
                    </span>
                  </div>
                  {idx < STEPS.length - 1 && (
                    <div className={cn(
                      'h-0.5 w-full mx-2 rounded',
                      isDone ? 'bg-emerald-400' : 'bg-muted',
                    )} />
                  )}
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Adım içeriği */}
      {currentStep === 1 && (
        <StepCard
          title="Adım 1: Not Girişi Kilitle"
          description="Tüm öğretim üyeleri için not girişi ve düzenleme işlemlerini kilitleyin."
          onComplete={() => completeStep(1)}
          isCompleted={completedSteps.has(1)}
        >
          <div className="space-y-3">
            <div className="flex items-center gap-3 p-3 rounded-lg bg-amber-50 border border-amber-200">
              <span className="text-2xl">⚠️</span>
              <p className="text-sm">Bu işlem geri alınamaz. Tüm not girişlerini kalıcı olarak kilitleyecektir.</p>
            </div>
            <div className="flex gap-4 text-sm">
              <div>Toplam Ders: <strong>42</strong></div>
              <div>Not Giren Öğretim Üyesi: <strong>36</strong></div>
              <div>Eksik Not Girişi: <strong className="text-red-500">3 ders</strong></div>
            </div>
          </div>
        </StepCard>
      )}

      {currentStep === 2 && (
        <StepCard
          title="Adım 2: GPA Hesapla"
          description="Tüm öğrenciler için dönem ve genel not ortalamasını yeniden hesaplayın."
          onComplete={() => completeStep(2)}
          isCompleted={completedSteps.has(2)}
        >
          <div className="space-y-3">
            <div className="grid grid-cols-3 gap-4">
              <MiniStat label="Toplam Öğrenci" value="342" />
              <MiniStat label="Hesaplama Süresi (tahmini)" value="~15 sn" />
              <MiniStat label="Son Hesaplama" value="Henüz yapılmadı" />
            </div>
            <Progress value={completedSteps.has(2) ? 100 : 0} className="h-2" />
          </div>
        </StepCard>
      )}

      {currentStep === 3 && (
        <StepCard
          title="Adım 3: Sınıf Atlama (Akademik İlerleme)"
          description="Toplam AKTS'ye göre öğrenci sınıf seviyelerini güncelleyin."
          onComplete={() => completeStep(3)}
          isCompleted={completedSteps.has(3)}
          onBack={goBack}
        >
          <div className="space-y-4">
            {/* Özet çizgisi */}
            <div className="grid grid-cols-4 gap-4">
              <MiniStat label="Toplam Öğrenci" value={String(totalStudents)} />
              <MiniStat label="Yükselecek" value={String(willPromote)} color="text-emerald-600" />
              <MiniStat label="Kalacak" value={String(willStay)} />
              <MiniStat label="Uyarı (CGPA<1.70)" value={String(warningCount)} color="text-red-500" />
            </div>

            {/* Detay tablosu */}
            <div className="rounded-lg border overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-muted/50">
                  <tr>
                    <th className="px-4 py-2 text-left font-semibold">Öğrenci No</th>
                    <th className="px-4 py-2 text-left font-semibold">Eski Sınıf</th>
                    <th className="px-4 py-2 text-left font-semibold">Yeni Sınıf</th>
                    <th className="px-4 py-2 text-right font-semibold">AKTS</th>
                    <th className="px-4 py-2 text-left font-semibold">Durum</th>
                  </tr>
                </thead>
                <tbody>
                  {MOCK_PROMOTIONS.map(row => (
                    <tr key={row.studentNo} className="border-t">
                      <td className="px-4 py-2 font-mono">{row.studentNo}</td>
                      <td className="px-4 py-2">{row.oldLevel}</td>
                      <td className="px-4 py-2 font-semibold">{row.newLevel}</td>
                      <td className="px-4 py-2 text-right">{row.ects}</td>
                      <td className="px-4 py-2">
                        {row.status === 'warning' ? (
                          <Badge variant="warning">⚠️ CGPA: {row.cgpa.toFixed(2)}</Badge>
                        ) : (
                          <Badge variant="success">✅ Normal</Badge>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </StepCard>
      )}

      {currentStep === 4 && (
        <StepCard
          title="Adım 4: Yeni Dönem Aç"
          description="2025-2026 Bahar dönemini oluşturun ve ders kayıt takvimini ayarlayın."
          onComplete={() => completeStep(4)}
          isCompleted={completedSteps.has(4)}
          completeLabel="Dönemi Aç ve Bitir"
          onBack={goBack}
        >
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-4">
              <MiniStat label="Yeni Dönem" value="2025-2026 Bahar" />
              <MiniStat label="Kayıt Başlangıcı" value="10 Şubat 2026" />
            </div>
          </div>
        </StepCard>
      )}

      {completedSteps.size === 4 && (
        <Card className="border-emerald-300 bg-emerald-50/50">
          <CardContent className="flex items-center gap-4 p-6">
            <span className="text-4xl">🎉</span>
            <div>
              <h2 className="text-lg font-bold text-emerald-800">Dönem Kapatma Tamamlandı!</h2>
              <p className="text-sm text-emerald-700">Tüm adımlar başarıyla tamamlandı. Yeni dönem açıldı.</p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/* ── Yardımcılar ── */
interface StepCardProps {
  title: string;
  description: string;
  onComplete: () => void;
  isCompleted: boolean;
  completeLabel?: string;
  children: React.ReactNode;
  onBack?: () => void;
}

function StepCard({ title, description, onComplete, isCompleted, completeLabel, children, onBack }: StepCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <p className="text-sm text-muted-foreground">{description}</p>
      </CardHeader>
      <CardContent className="space-y-4">
        {children}
        <div className="flex gap-3 pt-4 border-t">
          {onBack && <Button variant="outline" onClick={onBack}>◀ Geri</Button>}
          {!isCompleted && (
            <Button onClick={onComplete}>{completeLabel ?? 'Onayla ve Devam Et ✅'}</Button>
          )}
          <Button variant="outline">Raporu İndir 📊</Button>
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
