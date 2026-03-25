import { useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { cn } from '@/lib/utils';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { CheckCircle2, Send, RotateCcw, AlertTriangle } from 'lucide-react';

/* ── Mock data (backend entegrasyonunda kaldırılacak) ── */
interface SurveyCourse {
  id: number;
  courseCode: string;
  courseName: string;
  ects: number;
  isRetake: boolean;
}

const MOCK_COURSES: SurveyCourse[] = [
  { id: 1, courseCode: 'BM301', courseName: 'Veritabanı Yönetimi', ects: 5, isRetake: true },
  { id: 2, courseCode: 'BM303', courseName: 'İşletim Sistemleri', ects: 5, isRetake: false },
  { id: 3, courseCode: 'BM305', courseName: 'Bilgisayar Ağları', ects: 5, isRetake: false },
  { id: 4, courseCode: 'BM307', courseName: 'Yapay Zekaya Giriş', ects: 4, isRetake: false },
  { id: 5, courseCode: 'BM309', courseName: 'Mikroişlemciler', ects: 4, isRetake: false },
  { id: 6, courseCode: 'SEC-01', courseName: 'Seçmeli Ders (Slot 1)', ects: 3, isRetake: false },
];

type Priority = 'kesin' | 'muhtemel' | 'dusunuyor' | null;

function getEctsLimit(cgpa: number): number {
  if (cgpa < 2.0) return 30;
  if (cgpa < 2.5) return 42;
  return 48;
}

const PRIORITY_META = {
  kesin: { label: 'Kesin', color: 'bg-primary', activeClass: 'bg-primary text-primary-foreground border-primary ring-2 ring-primary/20' },
  muhtemel: { label: 'Muhtemel', color: 'bg-amber-500', activeClass: 'bg-amber-50 text-amber-800 border-amber-400 ring-2 ring-amber-200' },
  dusunuyor: { label: 'Düşünüyorum', color: 'bg-slate-400', activeClass: 'bg-slate-50 text-slate-700 border-slate-400 ring-2 ring-slate-200' },
} as const;

export default function PreRegistrationSurveyPage() {
  const { data: student } = useStudentInfo();
  const cgpa = student?.cgpa ?? 2.5;
  const ectsLimit = getEctsLimit(cgpa);

  const [selections, setSelections] = useState<Record<number, Priority>>(() => {
    const init: Record<number, Priority> = {};
    MOCK_COURSES.forEach(c => {
      init[c.id] = c.isRetake ? 'kesin' : null;
    });
    return init;
  });

  const [submitted, setSubmitted] = useState(false);

  const setPriority = (courseId: number, p: Priority) => {
    setSelections(prev => ({ ...prev, [courseId]: p }));
  };

  const stats = useMemo(() => {
    let kesinEcts = 0, muhtemelEcts = 0;
    MOCK_COURSES.forEach(c => {
      const s = selections[c.id];
      if (s === 'kesin') kesinEcts += c.ects;
      else if (s === 'muhtemel') muhtemelEcts += c.ects;
    });
    const selectedEcts = kesinEcts + muhtemelEcts;
    return { kesinEcts, muhtemelEcts, selectedEcts };
  }, [selections]);

  const remaining = ectsLimit - stats.selectedEcts;
  const overLimit = remaining < 0;
  const fillPercent = Math.min((stats.selectedEcts / ectsLimit) * 100, 100);
  const kesinPercent = Math.min((stats.kesinEcts / ectsLimit) * 100, 100);

  return (
    <div className="space-y-5">
      {/* Gönderim başarı mesajı */}
      {submitted && (
        <Alert className="border-success/40 bg-success/5">
          <CheckCircle2 className="h-4 w-4 text-success" />
          <AlertDescription className="text-success font-medium">
            Anketiniz başarıyla gönderildi. Ders kayıt döneminde bu bilgiler dikkate alınacaktır.
          </AlertDescription>
        </Alert>
      )}

      {/* AKTS Bütçe Kartı */}
      <Card>
        <CardContent className="px-5 py-4 space-y-3">
          <div className="flex items-center justify-between gap-4">
            <div className="space-y-0.5">
              <h2 className="font-heading text-sm font-bold text-foreground">AKTS Bütçeniz</h2>
              <p className="text-xs text-muted-foreground">
                AGNO: <span className="font-semibold text-foreground">{cgpa.toFixed(2)}</span>
                <span className="mx-2 text-border">|</span>
                Dönem limiti: <span className="font-semibold text-foreground">{ectsLimit} AKTS</span>
                <span className="mx-2 text-border">|</span>
                Kalan: <span className={cn(
                  'font-semibold',
                  overLimit ? 'text-destructive' : remaining <= 6 ? 'text-amber-600' : 'text-foreground',
                )}>{remaining} AKTS</span>
              </p>
            </div>
            {submitted ? (
              <Button variant="outline" size="sm" className="gap-2 shrink-0" onClick={() => setSubmitted(false)}>
                <RotateCcw className="h-3.5 w-3.5" />
                Düzenle
              </Button>
            ) : (
              <Button size="sm" className="gap-2 shrink-0" onClick={() => setSubmitted(true)}>
                <Send className="h-3.5 w-3.5" />
                Gönder
              </Button>
            )}
          </div>

          {/* Progress bar */}
          <div className="space-y-1.5">
            <div className="h-3 w-full rounded-full bg-muted overflow-hidden">
              <div className="h-full flex transition-all duration-300">
                <div
                  className="bg-primary transition-all duration-300"
                  style={{ width: `${kesinPercent}%` }}
                />
                <div
                  className="bg-amber-400 transition-all duration-300"
                  style={{ width: `${Math.min(fillPercent - kesinPercent, 100 - kesinPercent)}%` }}
                />
              </div>
            </div>
            <div className="flex items-center gap-4 text-[11px] text-muted-foreground">
              <span className="flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-primary" />
                Kesin: {stats.kesinEcts} AKTS
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-amber-400" />
                Muhtemel: {stats.muhtemelEcts} AKTS
              </span>
              <span className="ml-auto">
                {stats.selectedEcts} / {ectsLimit} AKTS
              </span>
            </div>
          </div>

          {overLimit && (
            <div className="flex items-center gap-2 rounded-lg bg-destructive/10 px-3 py-2 text-xs font-medium text-destructive">
              <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
              AKTS limitini {Math.abs(remaining)} AKTS aştınız. Kayıt döneminde bu limit geçerli olacaktır.
            </div>
          )}
        </CardContent>
      </Card>

      {/* Bilgi notu — tekrar ders varsa */}
      {MOCK_COURSES.some(c => c.isRetake) && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/20 bg-destructive/[0.03] px-4 py-2.5 text-sm text-muted-foreground">
          <span className="h-2 w-2 rounded-full bg-destructive shrink-0" />
          Tekrar dersleri otomatik <span className="font-semibold text-foreground">"Kesin"</span> işaretlidir ve değiştirilemez.
        </div>
      )}

      {/* Ders listesi */}
      <Card className="overflow-hidden">
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/40">
                  <th className="px-5 py-3 text-left font-semibold text-foreground">Ders</th>
                  <th className="px-4 py-3 text-center font-semibold text-foreground w-20">AKTS</th>
                  <th className="px-4 py-3 text-center font-semibold text-foreground w-80">Alma Niyeti</th>
                </tr>
              </thead>
              <tbody>
                {MOCK_COURSES.map((course, index) => {
                  const selected = selections[course.id];
                  return (
                    <tr
                      key={course.id}
                      className={cn(
                        'border-b border-border/50 transition-colors',
                        course.isRetake && 'bg-destructive/[0.03]',
                        index % 2 === 0 && !course.isRetake && 'bg-muted/10',
                      )}
                    >
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-3">
                          <span className="font-mono text-sm font-bold text-foreground">{course.courseCode}</span>
                          <span className="text-foreground">{course.courseName}</span>
                          {course.isRetake && (
                            <Badge variant="destructive" className="text-[10px] px-2 py-0">Tekrar</Badge>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3.5 text-center font-mono font-medium">{course.ects}</td>
                      <td className="px-4 py-3.5">
                        <div className="flex items-center justify-center gap-2">
                          {(['kesin', 'muhtemel', 'dusunuyor'] as const).map(p => {
                            const meta = PRIORITY_META[p];
                            const isSelected = selected === p;
                            const isDisabled = submitted || (course.isRetake && p !== 'kesin');

                            return (
                              <button
                                key={p}
                                type="button"
                                disabled={isDisabled}
                                onClick={() => setPriority(course.id, isSelected ? null : p)}
                                className={cn(
                                  'rounded-lg border px-4 py-1.5 text-xs font-semibold transition-all',
                                  isSelected
                                    ? meta.activeClass
                                    : 'border-border bg-card text-muted-foreground hover:bg-muted hover:text-foreground',
                                  isDisabled && 'opacity-40 cursor-not-allowed',
                                )}
                              >
                                {meta.label}
                              </button>
                            );
                          })}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

    </div>
  );
}
