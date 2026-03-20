// SIS.Web/src/pages/GradeEntryPage.tsx
import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
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
} from 'lucide-react';
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
} from '../hooks/useGradeData';
import type { GradingCriteria, FinalizePreviewItem } from '../types/grade';

const GRADE_COLORS: Record<string, string> = {
  AA: 'text-success font-semibold',
  BA: 'text-success font-semibold',
  BB: 'text-accent font-semibold',
  CB: 'text-accent font-semibold',
  CC: 'text-accent font-semibold',
  DC: 'text-warning font-semibold',
  DD: 'text-warning font-semibold',
  FF: 'text-destructive font-semibold',
  NA: 'text-destructive font-semibold',
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

  const [selectedComponentId, setSelectedComponentId] = useState<number | null>(null);
  const [scores, setScores] = useState<Record<number, string>>({});
  const [showAddComponent, setShowAddComponent] = useState(false);
  const [newComponent, setNewComponent] = useState({ name: '', weight: '', order: '' });
  const [showCriteriaDialog, setShowCriteriaDialog] = useState(false);
  const [editCriteria, setEditCriteria] = useState<GradingCriteria | null>(null);
  const [showFinalizeDialog, setShowFinalizeDialog] = useState(false);
  const [finalizeResults, setFinalizeResults] = useState<FinalizePreviewItem[] | null>(null);

  if (gradesLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <div className="flex gap-6">
          <Skeleton className="h-96 w-64" />
          <Skeleton className="h-96 flex-1" />
        </div>
      </div>
    );
  }

  if (!gradesData) {
    return (
      <div className="text-center py-16 text-muted-foreground">
        Section bulunamadı veya erişim yetkiniz yok.
      </div>
    );
  }

  const isFinalized = gradesData.isFinalized;
  const currentCriteria = criteria ?? { aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45 };

  // Seçili bileşen yoksa ilkini seç
  const activeComponentId = selectedComponentId ?? components?.[0]?.id ?? null;

  // Kaydet
  const handleSave = () => {
    if (!activeComponentId) return;
    const grades = Object.entries(scores).map(([studentId, score]) => ({
      studentId: Number(studentId),
      score: score === '' ? null : Number(score),
    }));
    submitGradesMut.mutate({ componentId: activeComponentId, grades });
  };

  // Bileşen ekle
  const handleAddComponent = () => {
    if (!newComponent.name || !newComponent.weight) return;
    addComponentMut.mutate(
      { name: newComponent.name, weight: Number(newComponent.weight), order: Number(newComponent.order) || 0 },
      {
        onSuccess: () => {
          setShowAddComponent(false);
          setNewComponent({ name: '', weight: '', order: '' });
        },
      }
    );
  };

  // Kesinleştir
  const handleFinalize = () => {
    finalizeMut.mutate(undefined, {
      onSuccess: (data) => {
        setFinalizeResults(data.results);
      },
      onError: (err: unknown) => {
        const error = err as { response?: { data?: { error?: string } } };
        alert(error.response?.data?.error ?? 'Kesinleştirme hatası');
      },
    });
  };

  // Kesinleştirmeyi geri al
  const handleUnfinalize = () => {
    unfinalizeMut.mutate(undefined, {
      onSuccess: () => {
        setFinalizeResults(null);
        setShowFinalizeDialog(false);
      },
    });
  };

  // Harf aralığı kaydet
  const handleSaveCriteria = () => {
    if (!editCriteria) return;
    updateCriteriaMut.mutate(editCriteria, {
      onSuccess: () => setShowCriteriaDialog(false),
    });
  };

  return (
    <div className="space-y-6">
      {/* HEADER */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="sm" onClick={() => navigate('/derslerim')}>
            <ArrowLeft className="h-4 w-4 mr-1" /> Derslerim
          </Button>
          <div>
            <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
              {gradesData.courseCode} — {gradesData.courseName}
            </h1>
            <p className="text-muted-foreground text-sm mt-0.5">
              Section {sectionId} · {gradesData.students.length} öğrenci
            </p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <Badge variant={isFinalized ? 'success' : 'warning'}>
            {isFinalized ? 'Kesinleştirilmiş' : 'Kesinleştirilmemiş'}
          </Badge>
          {isFinalized ? (
            <Button variant="outline" size="sm" onClick={handleUnfinalize} disabled={unfinalizeMut.isPending}>
              <XCircle className="h-4 w-4 mr-1" /> Geri Al
            </Button>
          ) : (
            <Button size="sm" onClick={() => setShowFinalizeDialog(true)}>
              <CheckCircle className="h-4 w-4 mr-1" /> Kesinleştir
            </Button>
          )}
        </div>
      </div>

      {/* MAIN CONTENT */}
      <div className="flex gap-6">
        {/* SOL PANEL */}
        <div className="w-64 shrink-0 space-y-4">
          {/* Bileşenler */}
          <Card>
            <CardContent className="p-4">
              <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-3">
                Bileşenler
              </div>
              <div className="space-y-2">
                {components?.map(comp => (
                  <div
                    key={comp.id}
                    className={`flex items-center justify-between p-2.5 rounded-lg cursor-pointer transition-colors ${
                      activeComponentId === comp.id
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-muted/50 hover:bg-muted'
                    }`}
                    onClick={() => {
                      setSelectedComponentId(comp.id);
                      // Mevcut notları yükle
                      const newScores: Record<number, string> = {};
                      gradesData.students.forEach(s => {
                        const score = s.scores[comp.id];
                        newScores[s.studentId] = score != null ? String(score) : '';
                      });
                      setScores(newScores);
                    }}
                  >
                    <div>
                      <div className="text-sm font-medium">{comp.name}</div>
                      <div className={`text-xs ${activeComponentId === comp.id ? 'text-primary-foreground/70' : 'text-muted-foreground'}`}>
                        %{comp.weight}
                      </div>
                    </div>
                    {!isFinalized && (
                      <button
                        onClick={(e) => { e.stopPropagation(); deleteComponentMut.mutate(comp.id); }}
                        className={`p-1 rounded hover:bg-destructive/20 ${activeComponentId === comp.id ? 'text-primary-foreground/70' : 'text-muted-foreground'}`}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>
                ))}

                {!isFinalized && (
                  <button
                    onClick={() => setShowAddComponent(true)}
                    className="w-full border border-dashed border-muted-foreground/30 rounded-lg p-2.5 text-sm text-muted-foreground hover:border-primary hover:text-primary transition-colors"
                  >
                    <Plus className="h-4 w-4 inline mr-1" /> Ekle
                  </button>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Harf Aralığı */}
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between mb-3">
                <div className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                  Harf Aralığı
                </div>
                {!isFinalized && (
                  <button
                    onClick={() => { setEditCriteria({ ...currentCriteria }); setShowCriteriaDialog(true); }}
                    className="text-primary hover:underline text-xs flex items-center gap-1"
                  >
                    <Settings className="h-3 w-3" /> Düzenle
                  </button>
                )}
              </div>
              <div className="text-sm space-y-1">
                {[
                  { label: 'AA', min: currentCriteria.aaMin, color: 'text-success' },
                  { label: 'BA', min: currentCriteria.baMin, color: 'text-success' },
                  { label: 'BB', min: currentCriteria.bbMin, color: 'text-accent' },
                  { label: 'CB', min: currentCriteria.cbMin, color: 'text-accent' },
                  { label: 'CC', min: currentCriteria.ccMin, color: 'text-accent' },
                  { label: 'DC', min: currentCriteria.dcMin, color: 'text-warning' },
                  { label: 'DD', min: currentCriteria.ddMin, color: 'text-warning' },
                ].map(({ label, min, color }) => (
                  <div key={label} className="flex justify-between">
                    <span className={`font-mono font-semibold ${color}`}>{label}</span>
                    <span className="text-muted-foreground">≥ {min}</span>
                  </div>
                ))}
                <div className="flex justify-between">
                  <span className="font-mono font-semibold text-destructive">FF</span>
                  <span className="text-muted-foreground">&lt; {currentCriteria.ddMin}</span>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* SAĞ TABLO */}
        <div className="flex-1">
          <Card>
            <CardContent className="p-5">
              {!activeComponentId ? (
                <div className="text-center py-16 text-muted-foreground">
                  Bileşen ekleyerek not girişine başlayın.
                </div>
              ) : (
                <>
                  <div className="flex items-center justify-between mb-4">
                    <h2 className="text-lg font-semibold">
                      {components?.find(c => c.id === activeComponentId)?.name} Notları
                    </h2>
                    {!isFinalized && (
                      <Button size="sm" onClick={handleSave} disabled={submitGradesMut.isPending}>
                        <Save className="h-4 w-4 mr-1" />
                        {submitGradesMut.isPending ? 'Kaydediliyor...' : 'Kaydet'}
                      </Button>
                    )}
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b text-left">
                          <th className="pb-2 font-medium text-muted-foreground w-10">#</th>
                          <th className="pb-2 font-medium text-muted-foreground">Öğrenci</th>
                          <th className="pb-2 font-medium text-muted-foreground">No</th>
                          <th className="pb-2 font-medium text-muted-foreground text-center w-24">Puan</th>
                          <th className="pb-2 font-medium text-muted-foreground text-center w-16">Harf</th>
                        </tr>
                      </thead>
                      <tbody>
                        {gradesData.students.map((student, index) => {
                          const scoreStr = scores[student.studentId] ?? '';
                          const scoreNum = scoreStr !== '' ? Number(scoreStr) : null;
                          const letterGrade = scoreNum != null && !isNaN(scoreNum)
                            ? resolveGrade(Math.round(scoreNum), currentCriteria)
                            : null;

                          return (
                            <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/30">
                              <td className="py-2.5 text-muted-foreground">{index + 1}</td>
                              <td className="py-2.5 font-medium">{student.firstName} {student.lastName}</td>
                              <td className="py-2.5 font-mono text-xs">{student.studentNumber}</td>
                              <td className="py-2.5 text-center">
                                {isFinalized ? (
                                  <span className="font-mono">{scoreStr || '—'}</span>
                                ) : (
                                  <Input
                                    type="number"
                                    min={0}
                                    max={100}
                                    value={scoreStr}
                                    onChange={(e) => setScores(prev => ({ ...prev, [student.studentId]: e.target.value }))}
                                    className="w-20 mx-auto text-center h-8"
                                  />
                                )}
                              </td>
                              <td className="py-2.5 text-center">
                                <span className={letterGrade ? (GRADE_COLORS[letterGrade] ?? '') : 'text-muted-foreground'}>
                                  {letterGrade ?? '—'}
                                </span>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* BILEŞEN EKLEME DIALOG */}
      <Dialog open={showAddComponent} onOpenChange={setShowAddComponent}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Bileşen Ekle</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>İsim</Label>
              <Input
                placeholder="Vize, Final, Ödev..."
                value={newComponent.name}
                onChange={(e) => setNewComponent(prev => ({ ...prev, name: e.target.value }))}
              />
            </div>
            <div>
              <Label>Ağırlık (%)</Label>
              <Input
                type="number"
                min={1}
                max={100}
                placeholder="40"
                value={newComponent.weight}
                onChange={(e) => setNewComponent(prev => ({ ...prev, weight: e.target.value }))}
              />
            </div>
            <div>
              <Label>Sıra</Label>
              <Input
                type="number"
                min={0}
                placeholder="1"
                value={newComponent.order}
                onChange={(e) => setNewComponent(prev => ({ ...prev, order: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowAddComponent(false)}>İptal</Button>
            <Button onClick={handleAddComponent} disabled={addComponentMut.isPending}>Ekle</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* HARF ARALIĞI DÜZENLEME DIALOG */}
      <Dialog open={showCriteriaDialog} onOpenChange={setShowCriteriaDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Harf Aralığı Düzenle</DialogTitle>
          </DialogHeader>
          {editCriteria && (
            <div className="grid grid-cols-2 gap-3">
              {([
                { key: 'aaMin' as const, label: 'AA Min' },
                { key: 'baMin' as const, label: 'BA Min' },
                { key: 'bbMin' as const, label: 'BB Min' },
                { key: 'cbMin' as const, label: 'CB Min' },
                { key: 'ccMin' as const, label: 'CC Min' },
                { key: 'dcMin' as const, label: 'DC Min' },
                { key: 'ddMin' as const, label: 'DD Min' },
              ]).map(({ key, label }) => (
                <div key={key}>
                  <Label>{label}</Label>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={editCriteria[key]}
                    onChange={(e) => setEditCriteria(prev => prev ? { ...prev, [key]: Number(e.target.value) } : null)}
                  />
                </div>
              ))}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCriteriaDialog(false)}>İptal</Button>
            <Button onClick={handleSaveCriteria} disabled={updateCriteriaMut.isPending}>Kaydet</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* KESİNLEŞTİRME ÖNİZLEME DIALOG */}
      <Dialog open={showFinalizeDialog} onOpenChange={setShowFinalizeDialog}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {finalizeResults ? '✅ Kesinleştirildi' : '⚠️ Kesinleştirme Önizlemesi'}
            </DialogTitle>
          </DialogHeader>

          {!finalizeResults ? (
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Bu işlem tüm öğrencilerin ağırlıklı ortalamalarını hesaplayıp harf notlarını belirleyecektir.
                Eksik notu olan öğrenciler NA alacaktır. CGPA/SGPA güncellenecektir.
              </p>
              <div className="flex items-center gap-2 text-warning text-sm">
                <AlertTriangle className="h-4 w-4" />
                Bu işlemi daha sonra geri alabilirsiniz.
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setShowFinalizeDialog(false)}>İptal</Button>
                <Button onClick={handleFinalize} disabled={finalizeMut.isPending}>
                  {finalizeMut.isPending ? 'Hesaplanıyor...' : 'Onayla ve Kesinleştir'}
                </Button>
              </DialogFooter>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="overflow-x-auto max-h-80">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left">
                      <th className="pb-2 font-medium text-muted-foreground">Öğrenci</th>
                      <th className="pb-2 font-medium text-muted-foreground text-center">Ağ. Ort.</th>
                      <th className="pb-2 font-medium text-muted-foreground text-center">Harf Notu</th>
                    </tr>
                  </thead>
                  <tbody>
                    {finalizeResults.map(r => {
                      const student = gradesData.students.find(s => s.studentId === r.studentId);
                      return (
                        <tr key={r.studentId} className="border-b last:border-0">
                          <td className="py-2">{student ? `${student.firstName} ${student.lastName}` : `#${r.studentId}`}</td>
                          <td className="py-2 text-center font-mono">{r.weightedAverage.toFixed(1)}</td>
                          <td className="py-2 text-center">
                            <span className={GRADE_COLORS[r.letterGrade] ?? ''}>
                              {r.letterGrade}
                            </span>
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
    </div>
  );
}
