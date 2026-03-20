import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Separator } from '@/components/ui/separator';
import { Progress } from '@/components/ui/progress';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
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
  ClipboardEdit,
  Users,
  GraduationCap,
  Weight,
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

const GRADE_STYLE: Record<string, { bg: string; text: string }> = {
  AA: { bg: 'bg-success/10', text: 'text-success' },
  BA: { bg: 'bg-success/10', text: 'text-success' },
  BB: { bg: 'bg-accent/10', text: 'text-accent' },
  CB: { bg: 'bg-accent/10', text: 'text-accent' },
  CC: { bg: 'bg-accent/10', text: 'text-accent' },
  DC: { bg: 'bg-warning/10', text: 'text-warning' },
  DD: { bg: 'bg-warning/10', text: 'text-warning' },
  FF: { bg: 'bg-destructive/10', text: 'text-destructive' },
  NA: { bg: 'bg-destructive/10', text: 'text-destructive' },
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
    <span className={`inline-flex items-center justify-center min-w-[2.25rem] px-2 py-0.5 rounded-md text-xs font-bold font-mono ${style.bg} ${style.text}`}>
      {grade}
    </span>
  );
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
  const [newComponent, setNewComponent] = useState({ name: '', weight: '' });
  const [showCriteriaDialog, setShowCriteriaDialog] = useState(false);
  const [editCriteria, setEditCriteria] = useState<GradingCriteria | null>(null);
  const [showFinalizeDialog, setShowFinalizeDialog] = useState(false);
  const [finalizeResults, setFinalizeResults] = useState<FinalizePreviewItem[] | null>(null);

  if (gradesLoading) {
    return (
      <div className="space-y-5 animate-fade-in">
        <Skeleton className="h-24 rounded-xl" />
        <div className="flex gap-5">
          <div className="w-72 shrink-0 space-y-4">
            <Skeleton className="h-48 rounded-xl" />
            <Skeleton className="h-56 rounded-xl" />
          </div>
          <Skeleton className="h-96 flex-1 rounded-xl" />
        </div>
      </div>
    );
  }

  if (!gradesData) {
    return (
      <div className="flex flex-col items-center justify-center py-24 animate-fade-in">
        <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mb-4">
          <XCircle className="h-8 w-8 text-muted-foreground" />
        </div>
        <h2 className="font-heading text-lg font-bold text-foreground">Erişim Sağlanamadı</h2>
        <p className="text-muted-foreground text-sm mt-1 mb-6">Bu section bulunamadı veya erişim yetkiniz yok.</p>
        <Button variant="outline" onClick={() => navigate('/derslerim')}>
          <ArrowLeft className="h-4 w-4 mr-1.5" /> Derslerime Dön
        </Button>
      </div>
    );
  }

  const isFinalized = gradesData.isFinalized;
  const currentCriteria = criteria ?? { aaMin: 91, baMin: 81, bbMin: 71, cbMin: 61, ccMin: 55, dcMin: 50, ddMin: 45 };
  const activeComponentId = selectedComponentId ?? components?.[0]?.id ?? null;
  const totalWeight = components?.reduce((sum, c) => sum + c.weight, 0) ?? 0;

  const handleSave = () => {
    if (!activeComponentId) return;
    const grades = Object.entries(scores).map(([studentId, score]) => ({
      studentId: Number(studentId),
      score: score === '' ? null : Number(score),
    }));
    submitGradesMut.mutate({ componentId: activeComponentId, grades });
  };

  const handleAddComponent = () => {
    if (!newComponent.name || !newComponent.weight) return;
    addComponentMut.mutate(
      { name: newComponent.name, weight: Number(newComponent.weight), order: 0 },
      {
        onSuccess: () => {
          setShowAddComponent(false);
          setNewComponent({ name: '', weight: '' });
        },
      }
    );
  };

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

  const gradeScale = [
    { label: 'AA', min: currentCriteria.aaMin, variant: 'success' as const },
    { label: 'BA', min: currentCriteria.baMin, variant: 'success' as const },
    { label: 'BB', min: currentCriteria.bbMin, variant: 'accent' as const },
    { label: 'CB', min: currentCriteria.cbMin, variant: 'accent' as const },
    { label: 'CC', min: currentCriteria.ccMin, variant: 'accent' as const },
    { label: 'DC', min: currentCriteria.dcMin, variant: 'warning' as const },
    { label: 'DD', min: currentCriteria.ddMin, variant: 'warning' as const },
  ];

  return (
    <div className="space-y-5 animate-fade-in">
      {/* ═══ HEADER CARD ═══ */}
      <Card className="overflow-hidden border-0 shadow-sm">
        <div className="bg-gradient-to-r from-primary/[0.03] via-transparent to-accent/[0.03]">
          <CardContent className="p-5">
            <div className="flex items-center justify-between">
              <div>
                <div className="flex items-center gap-3">
                  <span className="font-mono text-xl font-extrabold text-primary tracking-tight">
                    {gradesData.courseCode}
                  </span>
                  <span className="text-xl font-heading font-bold text-foreground">
                    {gradesData.courseName}
                  </span>
                </div>
                <div className="flex items-center gap-4 mt-1.5">
                  <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                    <Users className="h-3.5 w-3.5" />
                    {gradesData.students.length} öğrenci
                  </span>
                  <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                    <GraduationCap className="h-3.5 w-3.5" />
                    Section {sectionId}
                  </span>
                </div>
              </div>

              <div className="flex items-center gap-3">
                <Badge
                  variant={isFinalized ? 'success' : 'warning'}
                  className="px-3 py-1 text-xs"
                >
                  {isFinalized ? 'Kesinleştirilmiş' : 'Kesinleştirilmemiş'}
                </Badge>
                {isFinalized ? (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleUnfinalize}
                    disabled={unfinalizeMut.isPending}
                    className="gap-1.5"
                  >
                    <XCircle className="h-4 w-4" /> Geri Al
                  </Button>
                ) : (
                  <Button
                    size="sm"
                    onClick={() => setShowFinalizeDialog(true)}
                    className="gap-1.5"
                  >
                    <CheckCircle className="h-4 w-4" /> Kesinleştir
                  </Button>
                )}
              </div>
            </div>
          </CardContent>
        </div>
      </Card>

      {/* ═══ MAIN LAYOUT ═══ */}
      <div className="flex gap-5">
        {/* ─── SOL PANEL ─── */}
        <div className="w-72 shrink-0 space-y-4 stagger-children">
          {/* Bileşenler */}
          <Card>
            <CardHeader className="pb-3 px-4 pt-4">
              <div className="flex items-center justify-between">
                <CardTitle className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Bileşenler
                </CardTitle>
                <div className="flex items-center gap-1.5">
                  <Weight className="h-3 w-3 text-muted-foreground" />
                  <span className={`text-xs font-mono font-bold ${totalWeight === 100 ? 'text-success' : totalWeight > 100 ? 'text-destructive' : 'text-muted-foreground'}`}>
                    %{totalWeight}
                  </span>
                </div>
              </div>
              <Progress
                value={Math.min(totalWeight, 100)}
                className="h-1.5 mt-2"
              />
            </CardHeader>
            <CardContent className="px-4 pb-4 pt-0">
              <div className="space-y-1.5">
                {components?.map(comp => {
                  const isActive = activeComponentId === comp.id;
                  return (
                    <div
                      key={comp.id}
                      onClick={() => {
                        setSelectedComponentId(comp.id);
                        const newScores: Record<number, string> = {};
                        gradesData.students.forEach(s => {
                          const score = s.scores[comp.id];
                          newScores[s.studentId] = score != null ? String(score) : '';
                        });
                        setScores(newScores);
                      }}
                      className={`group flex items-center justify-between px-3 py-2.5 rounded-lg cursor-pointer transition-all duration-200 ${
                        isActive
                          ? 'bg-primary text-primary-foreground shadow-sm'
                          : 'hover:bg-muted/80'
                      }`}
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <div className={`w-1 h-8 rounded-full shrink-0 transition-colors ${isActive ? 'bg-primary-foreground/30' : 'bg-primary/20'}`} />
                        <div className="min-w-0">
                          <div className="text-sm font-semibold truncate">{comp.name}</div>
                          <div className={`text-xs ${isActive ? 'text-primary-foreground/60' : 'text-muted-foreground'}`}>
                            Ağırlık: %{comp.weight}
                          </div>
                        </div>
                      </div>
                      {!isFinalized && (
                        <button
                          onClick={(e) => { e.stopPropagation(); deleteComponentMut.mutate(comp.id); }}
                          className={`p-1.5 rounded-md opacity-0 group-hover:opacity-100 transition-all ${
                            isActive
                              ? 'hover:bg-primary-foreground/15 text-primary-foreground/60 hover:text-primary-foreground'
                              : 'hover:bg-destructive/10 text-muted-foreground hover:text-destructive'
                          }`}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      )}
                    </div>
                  );
                })}

                {!isFinalized && (
                  <button
                    onClick={() => setShowAddComponent(true)}
                    className="w-full flex items-center justify-center gap-1.5 border border-dashed border-muted-foreground/25 rounded-lg py-2.5 text-sm text-muted-foreground hover:border-primary/50 hover:text-primary hover:bg-primary/[0.02] transition-all duration-200"
                  >
                    <Plus className="h-4 w-4" />
                    Bileşen Ekle
                  </button>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Harf Aralığı */}
          <Card>
            <CardHeader className="pb-3 px-4 pt-4">
              <div className="flex items-center justify-between">
                <CardTitle className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Harf Aralığı
                </CardTitle>
                {!isFinalized && (
                  <button
                    onClick={() => { setEditCriteria({ ...currentCriteria }); setShowCriteriaDialog(true); }}
                    className="inline-flex items-center gap-1 text-xs text-primary hover:text-primary-light transition-colors"
                  >
                    <Settings className="h-3 w-3" />
                    Düzenle
                  </button>
                )}
              </div>
            </CardHeader>
            <CardContent className="px-4 pb-4 pt-0">
              <div className="space-y-1">
                {gradeScale.map(({ label, min, variant }) => (
                  <div key={label} className="flex items-center justify-between py-1">
                    <Badge variant={variant} className="min-w-[2.25rem] justify-center font-mono text-xs font-bold">
                      {label}
                    </Badge>
                    <span className="text-xs text-muted-foreground font-mono">
                      ≥ {min}
                    </span>
                  </div>
                ))}
                <Separator className="my-1.5" />
                <div className="flex items-center justify-between py-1">
                  <Badge variant="destructive" className="min-w-[2.25rem] justify-center font-mono text-xs font-bold">
                    FF
                  </Badge>
                  <span className="text-xs text-muted-foreground font-mono">
                    &lt; {currentCriteria.ddMin}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* ─── SAĞ PANEL: NOT TABLOSU ─── */}
        <div className="flex-1 min-w-0">
          <Card className="h-full">
            {!activeComponentId ? (
              /* Empty State */
              <div className="flex flex-col items-center justify-center py-20 px-8">
                <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-primary/5 to-accent/5 border border-primary/10 flex items-center justify-center mb-5">
                  <ClipboardEdit className="h-9 w-9 text-primary/40" />
                </div>
                <h3 className="font-heading text-lg font-bold text-foreground">
                  Not Girişine Hazır
                </h3>
                <p className="text-sm text-muted-foreground text-center mt-2 max-w-sm leading-relaxed">
                  Sol panelden bir not bileşeni seçin veya yeni bileşen ekleyerek not girişine başlayın.
                </p>
                {!isFinalized && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setShowAddComponent(true)}
                    className="mt-5 gap-1.5"
                  >
                    <Plus className="h-4 w-4" />
                    İlk Bileşeni Ekle
                  </Button>
                )}
              </div>
            ) : (
              <>
                {/* Tablo Header */}
                <CardHeader className="pb-0 px-5 pt-5">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="w-1.5 h-6 rounded-full bg-primary" />
                      <CardTitle className="text-base font-heading">
                        {components?.find(c => c.id === activeComponentId)?.name} Notları
                      </CardTitle>
                      <Badge variant="outline" className="text-xs font-mono">
                        %{components?.find(c => c.id === activeComponentId)?.weight}
                      </Badge>
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
                </CardHeader>

                <CardContent className="p-5 pt-4">
                  <div className="rounded-lg border overflow-hidden">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-muted/30 hover:bg-muted/30">
                          <TableHead className="w-12 text-center font-semibold">#</TableHead>
                          <TableHead className="font-semibold">Ad Soyad</TableHead>
                          <TableHead className="font-semibold">Öğrenci No</TableHead>
                          <TableHead className="w-28 text-center font-semibold">Puan</TableHead>
                          <TableHead className="w-20 text-center font-semibold">Harf</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {gradesData.students.map((student, index) => {
                          const scoreStr = scores[student.studentId] ?? '';
                          const scoreNum = scoreStr !== '' ? Number(scoreStr) : null;
                          const letterGrade = scoreNum != null && !isNaN(scoreNum)
                            ? resolveGrade(Math.round(scoreNum), currentCriteria)
                            : null;

                          return (
                            <TableRow key={student.studentId}>
                              <TableCell className="text-center text-muted-foreground font-mono text-xs">
                                {index + 1}
                              </TableCell>
                              <TableCell className="font-medium">
                                {student.firstName} {student.lastName}
                              </TableCell>
                              <TableCell className="font-mono text-xs text-muted-foreground">
                                {student.studentNumber}
                              </TableCell>
                              <TableCell className="text-center">
                                {isFinalized ? (
                                  <span className="font-mono text-sm font-semibold">{scoreStr || '—'}</span>
                                ) : (
                                  <Input
                                    type="number"
                                    min={0}
                                    max={100}
                                    value={scoreStr}
                                    onChange={(e) => setScores(prev => ({ ...prev, [student.studentId]: e.target.value }))}
                                    className="w-20 mx-auto text-center h-8 font-mono"
                                  />
                                )}
                              </TableCell>
                              <TableCell className="text-center">
                                {letterGrade ? <GradeChip grade={letterGrade} /> : <span className="text-muted-foreground text-sm">—</span>}
                              </TableCell>
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                  </div>

                  {gradesData.students.length === 0 && (
                    <div className="text-center py-12 text-muted-foreground text-sm">
                      Bu sectionda kayıtlı öğrenci bulunmamaktadır.
                    </div>
                  )}
                </CardContent>
              </>
            )}
          </Card>
        </div>
      </div>

      {/* ═══ DIALOGS ═══ */}

      {/* Bileşen Ekleme */}
      <Dialog open={showAddComponent} onOpenChange={setShowAddComponent}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Yeni Bileşen Ekle</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 pt-2">
            <div className="space-y-2">
              <Label className="text-sm font-medium">Bileşen Adı</Label>
              <Input
                placeholder="Vize, Final, Ödev, Proje..."
                value={newComponent.name}
                onChange={(e) => setNewComponent(prev => ({ ...prev, name: e.target.value }))}
              />
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
            <Button variant="outline" onClick={() => setShowAddComponent(false)}>İptal</Button>
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
            <DialogTitle>Harf Aralığı Düzenle</DialogTitle>
          </DialogHeader>
          {editCriteria && (
            <div className="space-y-3 pt-2">
              <p className="text-xs text-muted-foreground">Her harf notu için minimum puan değerini belirleyin. Değerler azalan sırada olmalıdır.</p>
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
                <><CheckCircle className="h-5 w-5 text-success" /> Kesinleştirildi</>
              ) : (
                <><AlertTriangle className="h-5 w-5 text-warning" /> Kesinleştirme Onayı</>
              )}
            </DialogTitle>
          </DialogHeader>

          {!finalizeResults ? (
            <div className="space-y-4 pt-1">
              <div className="rounded-lg bg-warning/5 border border-warning/20 p-4">
                <p className="text-sm text-foreground leading-relaxed">
                  Bu işlem tüm öğrencilerin ağırlıklı ortalamalarını hesaplayıp harf notlarını belirleyecektir.
                  Eksik notu olan öğrenciler <span className="font-mono font-bold text-destructive">NA</span> alacaktır.
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
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/30 hover:bg-muted/30">
                      <TableHead className="font-semibold">Öğrenci</TableHead>
                      <TableHead className="text-center font-semibold">Ağ. Ortalama</TableHead>
                      <TableHead className="text-center font-semibold">Harf Notu</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {finalizeResults.map(r => {
                      const student = gradesData.students.find(s => s.studentId === r.studentId);
                      return (
                        <TableRow key={r.studentId}>
                          <TableCell className="font-medium">
                            {student ? `${student.firstName} ${student.lastName}` : `#${r.studentId}`}
                          </TableCell>
                          <TableCell className="text-center font-mono text-sm">
                            {r.weightedAverage.toFixed(1)}
                          </TableCell>
                          <TableCell className="text-center">
                            <GradeChip grade={r.letterGrade} />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
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
