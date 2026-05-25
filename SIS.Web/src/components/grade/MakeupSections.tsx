// SIS.Web/src/components/grade/MakeupSections.tsx
// Bütünleme ve Mazeret sınav not giriş panelleri.
// Yalnızca section kesinleştirilmişse gösterilir.

import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  AlertTriangle,
  CheckCircle,
  ChevronDown,
  ChevronRight,
  RefreshCw,
} from 'lucide-react';
import {
  useButunlemeRoster,
  useFinalizeButunleme,
  useFinalizeMazeret,
  useMazeretRoster,
  useSetButunlemeScores,
  useSetMazeretScores,
} from '../../hooks/useGradeData';
import type {
  ButunlemeFinalizeResult,
  ButunlemeScoreItem,
  MazeretFinalizeResult,
  MazeretScoreItem,
} from '../../types/grade';

/* ── shared helpers ────────────────────────────────────── */

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
  I:  { bg: 'bg-amber-50', text: 'text-amber-600' },
};

function GradeChip({ grade }: { grade: string | null | undefined }) {
  if (!grade) return <span className="text-muted-foreground">—</span>;
  const style = GRADE_STYLE[grade];
  return (
    <span
      className={`inline-flex items-center justify-center min-w-[2.5rem] px-2.5 py-1 rounded-md text-xs font-bold font-mono ${
        style ? `${style.bg} ${style.text}` : 'bg-muted text-foreground'
      }`}
    >
      {grade}
    </span>
  );
}

function ScoreCell({ value }: { value: number | null | undefined }) {
  if (value == null) return <span className="text-muted-foreground font-mono text-xs">—</span>;
  return <span className="font-mono text-sm">{value}</span>;
}

function FinalizeResultTable({
  results,
  studentMap,
}: {
  results: (ButunlemeFinalizeResult | MazeretFinalizeResult)[];
  studentMap: Map<number, string>;
}) {
  if (results.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-2">
        Güncellenecek not bulunamadı.
      </p>
    );
  }
  return (
    <div className="rounded-lg border overflow-hidden max-h-60 overflow-y-auto mt-3">
      <table className="w-full text-sm">
        <thead className="sticky top-0 bg-background border-b">
          <tr>
            <th className="text-left py-2 px-4 font-medium text-muted-foreground">Öğrenci</th>
            <th className="text-center py-2 px-4 font-medium text-muted-foreground">Eski Not</th>
            <th className="text-center py-2 px-4 font-medium text-muted-foreground">Yeni Not</th>
            <th className="text-center py-2 px-4 font-medium text-muted-foreground">Ağ. Ort.</th>
          </tr>
        </thead>
        <tbody>
          {results.map((r) => (
            <tr key={r.studentId} className="border-b last:border-0">
              <td className="py-2 px-4 font-medium">
                {studentMap.get(r.studentId) ?? `#${r.studentId}`}
              </td>
              <td className="py-2 px-4 text-center">
                <GradeChip grade={r.oldGrade} />
              </td>
              <td className="py-2 px-4 text-center">
                <GradeChip grade={r.newGrade} />
              </td>
              <td className="py-2 px-4 text-center font-mono text-xs text-muted-foreground">
                {r.weightedAverage.toFixed(1)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════
   BÜTÜNLEME PANEL
   ══════════════════════════════════════════════════════════ */

interface ButunlemePanelProps {
  sectionId: number;
}

export function ButunlemePanel({ sectionId }: ButunlemePanelProps) {
  const [open, setOpen] = useState(false);
  // local editable makeup scores: studentId → score string
  const [pendingScores, setPendingScores] = useState<Record<number, string>>({});
  const [finalizeResults, setFinalizeResults] = useState<ButunlemeFinalizeResult[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading, refetch } = useButunlemeRoster(sectionId, open);
  const saveScoresMut = useSetButunlemeScores(sectionId);
  const finalizeMut = useFinalizeButunleme(sectionId);

  const students = data?.students ?? [];
  const isEmpty = students.length === 0;

  // Build student display map for result table
  const studentMap = new Map(
    students.map((s) => [s.studentId, `${s.firstName} ${s.lastName}`])
  );

  const handleScoreChange = (studentId: number, value: string) => {
    setPendingScores((prev) => ({ ...prev, [studentId]: value }));
  };

  const handleSaveScores = () => {
    setError(null);
    const items: ButunlemeScoreItem[] = [];

    for (const [sid, raw] of Object.entries(pendingScores)) {
      if (raw === '' || raw == null) continue;
      const num = Number(raw);
      if (Number.isNaN(num) || num < 0 || num > 100) {
        setError('Tüm bütünleme puanları 0-100 arasında olmalıdır.');
        return;
      }
      items.push({ studentId: Number(sid), makeupScore: num });
    }

    if (items.length === 0) {
      setError('Kaydedilecek puan girilmedi.');
      return;
    }

    saveScoresMut.mutate(items, {
      onSuccess: () => {
        setPendingScores({});
        refetch();
      },
      onError: (err: unknown) => {
        const e = err as { response?: { data?: { error?: string } } };
        setError(e.response?.data?.error ?? 'Puanlar kaydedilemedi.');
      },
    });
  };

  const handleFinalize = () => {
    setError(null);
    finalizeMut.mutate(undefined, {
      onSuccess: (res) => {
        setFinalizeResults(res.results);
      },
      onError: (err: unknown) => {
        const e = err as { response?: { data?: { error?: string } } };
        setError(e.response?.data?.error ?? 'Sonuçlandırma başarısız.');
      },
    });
  };

  return (
    <Card>
      <CardHeader className="pb-2 pt-4 px-4">
        <CardTitle
          className="text-sm font-semibold flex items-center gap-2 cursor-pointer select-none"
          onClick={() => setOpen((v) => !v)}
        >
          {open ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
          Bütünleme
          {data && (
            <Badge variant="secondary" className="ml-1 text-xs font-normal">
              {students.length} öğrenci
            </Badge>
          )}
        </CardTitle>
      </CardHeader>

      {open && (
        <CardContent className="px-4 pb-4 space-y-4">
          {isLoading && (
            <p className="text-sm text-muted-foreground animate-pulse">Yükleniyor...</p>
          )}

          {!isLoading && isEmpty && (
            <p className="text-sm text-muted-foreground">
              Bütünlemeye uygun öğrenci yok (FF + bütünleme hakkı olan).
            </p>
          )}

          {!isLoading && !isEmpty && (
            <>
              <div className="overflow-x-auto rounded-lg border">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b bg-muted/30">
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Öğrenci No</th>
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Ad Soyad</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Mevcut Not</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Vize(ler)</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Final</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground w-32">Büt. Puanı</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.map((s) => {
                      const finalScore = s.finalScores[0];
                      const currentMakeup =
                        pendingScores[s.studentId] ??
                        (finalScore?.makeupScore != null
                          ? String(finalScore.makeupScore)
                          : '');

                      return (
                        <tr
                          key={s.studentId}
                          className="border-b last:border-0 hover:bg-muted/20 transition-colors"
                        >
                          <td className="py-2.5 px-4 font-mono text-sm">{s.studentNumber}</td>
                          <td className="py-2.5 px-4 font-medium">
                            {s.firstName} {s.lastName}
                          </td>
                          <td className="py-2.5 px-4 text-center">
                            <GradeChip grade={s.currentGrade} />
                          </td>
                          <td className="py-2.5 px-4 text-center">
                            {s.vizeScores.length === 0 ? (
                              <span className="text-muted-foreground text-xs">—</span>
                            ) : (
                              <div className="flex flex-wrap justify-center gap-1.5">
                                {s.vizeScores.map((vs) => (
                                  <span
                                    key={vs.componentId}
                                    className="text-xs text-muted-foreground"
                                    title={vs.componentName}
                                  >
                                    {vs.componentName}: <ScoreCell value={vs.score} />
                                  </span>
                                ))}
                              </div>
                            )}
                          </td>
                          <td className="py-2.5 px-4 text-center">
                            {s.finalScores.length === 0 ? (
                              <span className="text-muted-foreground text-xs">—</span>
                            ) : (
                              <div className="flex flex-wrap justify-center gap-1.5">
                                {s.finalScores.map((fs) => (
                                  <span key={fs.componentId} className="text-xs text-muted-foreground">
                                    <ScoreCell value={fs.score} />
                                  </span>
                                ))}
                              </div>
                            )}
                          </td>
                          <td className="py-2.5 px-4 text-center">
                            <Input
                              type="number"
                              min={0}
                              max={100}
                              value={currentMakeup}
                              onChange={(e) => handleScoreChange(s.studentId, e.target.value)}
                              className="w-24 mx-auto text-center h-9 font-mono text-sm"
                              placeholder="—"
                            />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {error && (
                <div className="flex items-center gap-2 rounded-md bg-red-50 border border-red-200 px-4 py-2.5 text-sm text-red-700">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  {error}
                </div>
              )}

              {finalizeResults && (
                <div className="rounded-md bg-emerald-50 border border-emerald-200 px-4 py-3">
                  <div className="flex items-center gap-2 text-sm font-medium text-emerald-700 mb-1">
                    <CheckCircle className="h-4 w-4" />
                    Bütünleme sonuçlandırıldı
                  </div>
                  <FinalizeResultTable results={finalizeResults} studentMap={studentMap} />
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-1">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleSaveScores}
                  disabled={saveScoresMut.isPending}
                  className="gap-1.5"
                >
                  <RefreshCw className={`h-4 w-4 ${saveScoresMut.isPending ? 'animate-spin' : ''}`} />
                  {saveScoresMut.isPending ? 'Kaydediliyor...' : 'Puanları Kaydet'}
                </Button>
                <Button
                  size="sm"
                  onClick={handleFinalize}
                  disabled={finalizeMut.isPending}
                  className="gap-1.5"
                >
                  <CheckCircle className="h-4 w-4" />
                  {finalizeMut.isPending ? 'İşleniyor...' : 'Bütünleme Sonuçlandır'}
                </Button>
              </div>
            </>
          )}
        </CardContent>
      )}
    </Card>
  );
}

/* ══════════════════════════════════════════════════════════
   MAZERET PANEL
   ══════════════════════════════════════════════════════════ */

interface MazeretPanelProps {
  sectionId: number;
}

export function MazeretPanel({ sectionId }: MazeretPanelProps) {
  const [open, setOpen] = useState(false);
  // keyed by `${studentId}-${componentId}`
  const [pendingScores, setPendingScores] = useState<Record<string, string>>({});
  const [finalizeResults, setFinalizeResults] = useState<MazeretFinalizeResult[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading, refetch } = useMazeretRoster(sectionId, open);
  const saveScoresMut = useSetMazeretScores(sectionId);
  const finalizeMut = useFinalizeMazeret(sectionId);

  const students = data?.students ?? [];
  const isEmpty = students.length === 0;

  const studentMap = new Map(
    students.map((s) => [s.studentId, `${s.firstName} ${s.lastName}`])
  );

  const makeKey = (studentId: number, componentId: number) => `${studentId}-${componentId}`;

  const handleScoreChange = (studentId: number, componentId: number, value: string) => {
    setPendingScores((prev) => ({ ...prev, [makeKey(studentId, componentId)]: value }));
  };

  const handleSaveScores = () => {
    setError(null);
    const items: MazeretScoreItem[] = [];

    for (const [key, raw] of Object.entries(pendingScores)) {
      if (raw === '' || raw == null) continue;
      const num = Number(raw);
      if (Number.isNaN(num) || num < 0 || num > 100) {
        setError('Tüm mazeret puanları 0-100 arasında olmalıdır.');
        return;
      }
      const [sid, cid] = key.split('-').map(Number);
      items.push({ studentId: sid, componentId: cid, makeupScore: num });
    }

    if (items.length === 0) {
      setError('Kaydedilecek puan girilmedi.');
      return;
    }

    saveScoresMut.mutate(items, {
      onSuccess: () => {
        setPendingScores({});
        refetch();
      },
      onError: (err: unknown) => {
        const e = err as { response?: { data?: { error?: string } } };
        setError(e.response?.data?.error ?? 'Puanlar kaydedilemedi.');
      },
    });
  };

  const handleFinalize = () => {
    setError(null);
    finalizeMut.mutate(undefined, {
      onSuccess: (res) => {
        setFinalizeResults(res.results);
      },
      onError: (err: unknown) => {
        const e = err as { response?: { data?: { error?: string } } };
        setError(e.response?.data?.error ?? 'Sonuçlandırma başarısız.');
      },
    });
  };

  return (
    <Card>
      <CardHeader className="pb-2 pt-4 px-4">
        <CardTitle
          className="text-sm font-semibold flex items-center gap-2 cursor-pointer select-none"
          onClick={() => setOpen((v) => !v)}
        >
          {open ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
          Mazeret
          {data && (
            <Badge variant="secondary" className="ml-1 text-xs font-normal">
              {students.length} öğrenci
            </Badge>
          )}
        </CardTitle>
      </CardHeader>

      {open && (
        <CardContent className="px-4 pb-4 space-y-4">
          {isLoading && (
            <p className="text-sm text-muted-foreground animate-pulse">Yükleniyor...</p>
          )}

          {!isLoading && isEmpty && (
            <p className="text-sm text-muted-foreground">
              Mazerete uygun öğrenci yok (I notu + mazeret hakkı olan).
            </p>
          )}

          {!isLoading && !isEmpty && (
            <>
              <div className="overflow-x-auto rounded-lg border">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b bg-muted/30">
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Öğrenci No</th>
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Ad Soyad</th>
                      <th className="text-center py-2.5 px-4 font-medium text-muted-foreground">Mevcut Not</th>
                      <th className="text-left py-2.5 px-4 font-medium text-muted-foreground">Mazeretli Bileşen(ler)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.map((s) => (
                      <tr
                        key={s.studentId}
                        className="border-b last:border-0 hover:bg-muted/20 transition-colors"
                      >
                        <td className="py-2.5 px-4 font-mono text-sm align-top">{s.studentNumber}</td>
                        <td className="py-2.5 px-4 font-medium align-top">
                          {s.firstName} {s.lastName}
                        </td>
                        <td className="py-2.5 px-4 text-center align-top">
                          <GradeChip grade={s.currentGrade} />
                        </td>
                        <td className="py-2.5 px-4 align-top">
                          <div className="flex flex-col gap-2">
                            {s.excusedComponents.map((ec) => {
                              const key = makeKey(s.studentId, ec.componentId);
                              const current =
                                pendingScores[key] ??
                                (ec.makeupScore != null ? String(ec.makeupScore) : '');
                              return (
                                <div
                                  key={ec.componentId}
                                  className="flex items-center gap-3 flex-wrap"
                                >
                                  <span className="text-xs font-medium text-foreground min-w-[6rem]">
                                    {ec.componentName}
                                  </span>
                                  <span className="text-xs text-muted-foreground">
                                    Orij: <ScoreCell value={ec.originalScore} />
                                  </span>
                                  <Input
                                    type="number"
                                    min={0}
                                    max={100}
                                    value={current}
                                    onChange={(e) =>
                                      handleScoreChange(s.studentId, ec.componentId, e.target.value)
                                    }
                                    className="w-24 text-center h-8 font-mono text-sm"
                                    placeholder="Mazeret"
                                  />
                                </div>
                              );
                            })}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {error && (
                <div className="flex items-center gap-2 rounded-md bg-red-50 border border-red-200 px-4 py-2.5 text-sm text-red-700">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  {error}
                </div>
              )}

              {finalizeResults && (
                <div className="rounded-md bg-emerald-50 border border-emerald-200 px-4 py-3">
                  <div className="flex items-center gap-2 text-sm font-medium text-emerald-700 mb-1">
                    <CheckCircle className="h-4 w-4" />
                    Mazeret sonuçlandırıldı
                  </div>
                  <FinalizeResultTable results={finalizeResults} studentMap={studentMap} />
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-1">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleSaveScores}
                  disabled={saveScoresMut.isPending}
                  className="gap-1.5"
                >
                  <RefreshCw className={`h-4 w-4 ${saveScoresMut.isPending ? 'animate-spin' : ''}`} />
                  {saveScoresMut.isPending ? 'Kaydediliyor...' : 'Puanları Kaydet'}
                </Button>
                <Button
                  size="sm"
                  onClick={handleFinalize}
                  disabled={finalizeMut.isPending}
                  className="gap-1.5"
                >
                  <CheckCircle className="h-4 w-4" />
                  {finalizeMut.isPending ? 'İşleniyor...' : 'Mazeret Sonuçlandır'}
                </Button>
              </div>
            </>
          )}
        </CardContent>
      )}
    </Card>
  );
}
