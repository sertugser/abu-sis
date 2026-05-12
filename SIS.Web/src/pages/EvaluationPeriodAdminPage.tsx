import { useEffect, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  CalendarClock,
  Lock,
  Unlock,
  Plus,
  Pencil,
  Trash2,
  AlertCircle,
  Info,
  Eye,
  EyeOff,
  RotateCcw,
  ChevronDown,
  ChevronUp,
  Clock,
} from 'lucide-react';
import {
  useActiveEvaluationPeriod,
  useAllEvaluationQuestions,
  useCloseEvaluationPeriod,
  useCreateEvaluationQuestion,
  useDeleteEvaluationQuestion,
  useOpenEvaluationPeriod,
  useRestoreEvaluationQuestion,
  useUpdateEvaluationQuestion,
} from '../hooks/useCourseEvaluation';
import { useAdminTerms } from '../hooks/useAdminTerms';
import { formatDate } from '../lib/utils';
import type {
  EvaluationQuestion,
  EvaluationQuestionCategory,
  EvaluationQuestionType,
} from '../types/courseEvaluation';

export default function EvaluationPeriodAdminPage() {
  const { data: periodData, isLoading: periodLoading } = useActiveEvaluationPeriod();
  const { data: terms } = useAdminTerms();
  const { data: questions, isLoading: questionsLoading } = useAllEvaluationQuestions();

  const openMutation = useOpenEvaluationPeriod();
  const closeMutation = useCloseEvaluationPeriod();
  const toggleActiveMutation = useUpdateEvaluationQuestion();
  const deleteQuestionMutation = useDeleteEvaluationQuestion();
  const restoreQuestionMutation = useRestoreEvaluationQuestion();

  // Open-period form state
  const [selectedTermId, setSelectedTermId] = useState<string>('');
  const [closesAt, setClosesAt] = useState<string>('');
  const [formError, setFormError] = useState<string | null>(null);

  // Confirm dialogs
  const [openPeriodConfirmOpen, setOpenPeriodConfirmOpen] = useState(false);
  const [closeConfirmOpen, setCloseConfirmOpen] = useState(false);

  // Question CRUD state
  const [questionDialogOpen, setQuestionDialogOpen] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<EvaluationQuestion | null>(null);
  const [deletingQuestion, setDeletingQuestion] = useState<EvaluationQuestion | null>(null);

  // Deleted questions section
  const [showDeleted, setShowDeleted] = useState(false);

  const isOpen = periodData?.isOpen === true;
  const activePeriod = periodData?.period;

  const liveQuestions = (questions ?? []).filter((q) => !q.isDeleted);
  const deletedQuestions = (questions ?? []).filter((q) => q.isDeleted);

  async function handleToggleActive(q: EvaluationQuestion) {
    await toggleActiveMutation.mutateAsync({
      id: q.id,
      payload: {
        text: q.text,
        category: q.category,
        questionType: q.questionType,
        sortOrder: q.sortOrder,
        isActive: !q.isActive,
      },
    });
  }

  async function handleDeleteConfirmed() {
    if (!deletingQuestion) return;
    try {
      await deleteQuestionMutation.mutateAsync(deletingQuestion.id);
    } finally {
      setDeletingQuestion(null);
    }
  }

  async function handleRestore(id: number) {
    await restoreQuestionMutation.mutateAsync(id);
  }

  function handleOpenPeriodClick() {
    setFormError(null);
    if (!selectedTermId) {
      setFormError('Lütfen bir dönem seçin.');
      return;
    }
    if (!closesAt) {
      setFormError('Lütfen kapanış tarihini seçin.');
      return;
    }
    setOpenPeriodConfirmOpen(true);
  }

  async function handleOpenPeriodConfirmed() {
    setOpenPeriodConfirmOpen(false);
    try {
      await openMutation.mutateAsync({
        termId: Number(selectedTermId),
        closesAt: new Date(closesAt).toISOString(),
      });
      setSelectedTermId('');
      setClosesAt('');
    } catch (err: unknown) {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setFormError(message ?? 'Dönem açılamadı.');
    }
  }

  async function handleClosePeriod() {
    if (!activePeriod) return;
    try {
      await closeMutation.mutateAsync(activePeriod.id);
    } finally {
      setCloseConfirmOpen(false);
    }
  }

  return (
    <div className="space-y-4">
      {periodLoading ? (
        <Skeleton className="h-96 rounded-xl" />
      ) : (
        <div className="overflow-hidden rounded-xl border border-border bg-white shadow-sm">
          {/* Period Status Section — accent left border on whole container is too heavy;
              use a slim colored bar inside instead */}
          {isOpen && activePeriod ? (
            <div className="flex flex-wrap items-center gap-3 border-l-4 border-l-emerald-500 bg-emerald-50/40 px-5 py-4">
              <Unlock className="h-7 w-7 text-emerald-600" />
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <p className="text-sm font-semibold">Aktif Değerlendirme Dönemi</p>
                  <Badge variant="success">AÇIK</Badge>
                </div>
                <p className="text-xs text-muted-foreground">
                  {activePeriod.termName} · Açılma: {formatDate(activePeriod.openedAt)} · Kapanış: {formatDate(activePeriod.closesAt)}
                </p>
              </div>
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setCloseConfirmOpen(true)}
                disabled={closeMutation.isPending}
              >
                <Lock className="h-4 w-4 mr-1.5" />
                Dönemi Kapat
              </Button>
            </div>
          ) : (
            <div className="border-l-4 border-l-slate-400 bg-slate-50/50 px-5 py-4 space-y-3">
              <div className="flex items-center gap-3">
                <Lock className="h-7 w-7 text-slate-500" />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="text-sm font-semibold">Aktif Değerlendirme Dönemi Yok</p>
                    <Badge variant="secondary">KAPALI</Badge>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    Öğrencilerin ders değerlendirme yapabilmesi için yeni bir dönem açın.
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-[1fr_1fr_auto] gap-3 items-end">
                <div className="space-y-1.5">
                  <Label className="text-xs">Dönem</Label>
                  <Select value={selectedTermId} onValueChange={setSelectedTermId}>
                    <SelectTrigger className="bg-white">
                      <SelectValue placeholder="Dönem seçin..." />
                    </SelectTrigger>
                    <SelectContent>
                      {terms?.map((t) => (
                        <SelectItem key={t.id} value={String(t.id)}>
                          {t.name} {t.isActive ? '(Aktif)' : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Kapanış Tarihi ve Saati</Label>
                  <Input
                    type="datetime-local"
                    value={closesAt}
                    onChange={(e) => setClosesAt(e.target.value)}
                    className="bg-white"
                  />
                </div>
                <Button onClick={handleOpenPeriodClick} disabled={openMutation.isPending}>
                  <CalendarClock className="h-4 w-4 mr-1.5" />
                  {openMutation.isPending ? 'Açılıyor...' : 'Dönemi Aç'}
                </Button>
              </div>

              {formError && (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{formError}</AlertDescription>
                </Alert>
              )}
            </div>
          )}

          {/* Questions Section — same container, no internal Card */}
          <div className="border-t border-slate-100">
            <div className="flex items-center justify-between gap-3 flex-wrap px-5 py-3">
              <div>
                <h2 className="font-heading text-base font-bold leading-tight">Değerlendirme Soruları</h2>
                <p className="text-xs text-muted-foreground">
                  Öğrencilere sorulacak anket sorularını yönetin.
                </p>
              </div>
              <Button
                size="sm"
                onClick={() => {
                  setEditingQuestion(null);
                  setQuestionDialogOpen(true);
                }}
              >
                <Plus className="h-4 w-4 mr-1.5" />
                Yeni Soru
              </Button>
            </div>

            {isOpen && (
              <Alert className="mx-5 mb-3 border-amber-200 bg-amber-50">
                <Clock className="h-4 w-4 text-amber-600" />
                <AlertDescription className="text-amber-800 text-xs">
                  <span className="font-medium">Dönem açık:</span> Yapacağınız değişiklikler şu anki anketi etkilemez; bir sonraki dönem açıldığında geçerli olur.
                </AlertDescription>
              </Alert>
            )}

            {questionsLoading ? (
              <div className="px-5 pb-5">
                <Skeleton className="h-48 rounded-lg" />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-12 pl-5">Sıra</TableHead>
                    <TableHead>Soru</TableHead>
                    <TableHead>Kategori</TableHead>
                    <TableHead>Tip</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="text-right pr-5">İşlem</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {liveQuestions.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                        Henüz soru tanımlanmamış.
                      </TableCell>
                    </TableRow>
                  ) : (
                    liveQuestions
                      .slice()
                      .sort((a, b) => a.sortOrder - b.sortOrder)
                      .map((q) => (
                        <TableRow key={q.id}>
                          <TableCell className="text-xs text-muted-foreground pl-5">
                            {q.sortOrder}
                          </TableCell>
                          <TableCell className="text-sm">{q.text}</TableCell>
                          <TableCell>
                            <Badge variant="outline" className="text-[10px]">
                              {q.category === 'Course' ? 'Ders' : 'Öğretim Üyesi'}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            <Badge variant="outline" className="text-[10px]">
                              {q.questionType === 'Rating' ? 'Puan' : 'Metin'}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            {q.isActive ? (
                              <Badge variant="success">Aktif</Badge>
                            ) : (
                              <Badge variant="secondary">Pasif</Badge>
                            )}
                          </TableCell>
                          <TableCell className="text-right pr-5">
                            <div className="flex justify-end gap-1">
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8"
                                onClick={() => {
                                  setEditingQuestion(q);
                                  setQuestionDialogOpen(true);
                                }}
                                title="Düzenle"
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              {q.isActive ? (
                                <Button
                                  size="icon"
                                  variant="ghost"
                                  className="h-8 w-8"
                                  disabled={toggleActiveMutation.isPending}
                                  onClick={() => handleToggleActive(q)}
                                  title="Pasife Al"
                                >
                                  <EyeOff className="h-4 w-4 text-muted-foreground" />
                                </Button>
                              ) : (
                                <Button
                                  size="icon"
                                  variant="ghost"
                                  className="h-8 w-8"
                                  disabled={toggleActiveMutation.isPending}
                                  onClick={() => handleToggleActive(q)}
                                  title="Aktife Al"
                                >
                                  <Eye className="h-4 w-4 text-green-600" />
                                </Button>
                              )}
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8"
                                onClick={() => setDeletingQuestion(q)}
                                title="Sil"
                              >
                                <Trash2 className="h-4 w-4 text-destructive" />
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      ))
                  )}
                </TableBody>
              </Table>
            )}
          </div>

          {/* Deleted questions — inside same container */}
          {!questionsLoading && deletedQuestions.length > 0 && (
            <div className="border-t border-slate-100 bg-slate-50/40">
              <button
                className="flex items-center gap-2 text-xs text-muted-foreground hover:text-foreground transition-colors w-full px-5 py-2.5"
                onClick={() => setShowDeleted((v) => !v)}
              >
                {showDeleted ? (
                  <ChevronUp className="h-3.5 w-3.5" />
                ) : (
                  <ChevronDown className="h-3.5 w-3.5" />
                )}
                Silinmiş Sorular ({deletedQuestions.length})
              </button>

              {showDeleted && (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-12 pl-5">Sıra</TableHead>
                      <TableHead>Soru</TableHead>
                      <TableHead>Kategori</TableHead>
                      <TableHead>Tip</TableHead>
                      <TableHead className="text-right pr-5">İşlem</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {deletedQuestions.map((q) => (
                      <TableRow key={q.id} className="opacity-60">
                        <TableCell className="text-xs text-muted-foreground pl-5">
                          {q.sortOrder}
                        </TableCell>
                        <TableCell className="text-sm line-through">{q.text}</TableCell>
                        <TableCell>
                          <Badge variant="outline" className="text-[10px]">
                            {q.category === 'Course' ? 'Ders' : 'Öğretim Üyesi'}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className="text-[10px]">
                            {q.questionType === 'Rating' ? 'Puan' : 'Metin'}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right pr-5">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-xs gap-1.5"
                            disabled={restoreQuestionMutation.isPending}
                            onClick={() => handleRestore(q.id)}
                          >
                            <RotateCcw className="h-3 w-3" />
                            Geri Yükle
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </div>
          )}
        </div>
      )}

      {/* Question Form Dialog */}
      <QuestionFormDialog
        key={editingQuestion ? `edit-${editingQuestion.id}` : 'new'}
        open={questionDialogOpen}
        onOpenChange={setQuestionDialogOpen}
        editing={editingQuestion}
        periodIsOpen={isOpen}
      />

      {/* Soft Delete Confirmation */}
      <AlertDialog
        open={deletingQuestion != null}
        onOpenChange={(open) => { if (!open && !deleteQuestionMutation.isPending) setDeletingQuestion(null); }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Soru silinsin mi?</AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-2 text-sm text-muted-foreground">
                <p>
                  <span className="font-medium text-foreground">{deletingQuestion?.text}</span>
                </p>
                <p>
                  Soru silinir; geçmiş yanıtlar korunur. İstediğiniz zaman geri yükleyebilirsiniz.
                </p>
                {isOpen && (
                  <p className="text-amber-700">
                    Dönem açık olduğu için bu değişiklik bir sonraki dönemde geçerli olur.
                  </p>
                )}
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteQuestionMutation.isPending}>İptal</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDeleteConfirmed}
              disabled={deleteQuestionMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteQuestionMutation.isPending ? 'Siliniyor...' : 'Sil'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Close Period Confirmation */}
      <AlertDialog open={closeConfirmOpen} onOpenChange={setCloseConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Değerlendirme dönemini kapat?</AlertDialogTitle>
            <AlertDialogDescription>
              Dönem kapatıldığında öğrenciler artık değerlendirme yapamaz. Mevcut yanıtlar korunur.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleClosePeriod} disabled={closeMutation.isPending}>
              Kapat
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Open Period Confirmation */}
      <AlertDialog open={openPeriodConfirmOpen} onOpenChange={setOpenPeriodConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Değerlendirme dönemi açılsın mı?</AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-2 text-sm text-muted-foreground">
                <p>
                  Dönem açıldığında <strong className="text-foreground">şu anda aktif olan sorular</strong> fotoğraflanır
                  ve öğrencilere bu sorular sunulur.
                </p>
                <p>
                  Dönem açık iken soru ekleyebilir, düzenleyebilir veya silebilirsiniz; bu değişiklikler
                  yalnızca bir <strong className="text-foreground">sonraki dönem</strong> açıldığında ankete yansır.
                </p>
                <p>Devam etmek istiyor musunuz?</p>
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleOpenPeriodConfirmed} disabled={openMutation.isPending}>
              Dönemi Aç
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function QuestionFormDialog({
  open,
  onOpenChange,
  editing,
  periodIsOpen,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: EvaluationQuestion | null;
  periodIsOpen: boolean;
}) {
  const createMutation = useCreateEvaluationQuestion();
  const updateMutation = useUpdateEvaluationQuestion();

  const [text, setText] = useState(editing?.text ?? '');
  const [category, setCategory] = useState<EvaluationQuestionCategory>(editing?.category ?? 'Course');
  const [questionType, setQuestionType] = useState<EvaluationQuestionType>(editing?.questionType ?? 'Rating');
  const [sortOrder, setSortOrder] = useState<string>(editing ? String(editing.sortOrder) : '0');
  const [isActive, setIsActive] = useState(editing?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    if (editing) {
      setText(editing.text);
      setCategory(editing.category);
      setQuestionType(editing.questionType);
      setSortOrder(String(editing.sortOrder));
      setIsActive(editing.isActive);
    } else {
      setText('');
      setCategory('Course');
      setQuestionType('Rating');
      setSortOrder('0');
      setIsActive(true);
    }
    setError(null);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editing?.id]);

  async function handleSubmit() {
    setError(null);
    if (!text.trim()) {
      setError('Soru metni boş olamaz.');
      return;
    }
    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          payload: {
            text: text.trim(),
            category,
            questionType,
            sortOrder: Number(sortOrder) || 0,
            isActive,
          },
        });
      } else {
        await createMutation.mutateAsync({
          text: text.trim(),
          category,
          questionType,
          sortOrder: Number(sortOrder) || 0,
        });
      }
      onOpenChange(false);
    } catch (err: unknown) {
      const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(message ?? 'İşlem başarısız.');
    }
  }

  const isPending = createMutation.isPending || updateMutation.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{editing ? 'Soruyu Düzenle' : 'Yeni Soru'}</DialogTitle>
          <DialogDescription>
            Öğrencilere sorulacak soru metnini ve tipini belirleyin.
          </DialogDescription>
        </DialogHeader>

        {periodIsOpen && (
          <Alert className="border-amber-200 bg-amber-50 py-2">
            <Info className="h-4 w-4 text-amber-600" />
            <AlertDescription className="text-amber-800 text-xs">
              Bu değişiklik bir sonraki dönemde ankete yansır.
            </AlertDescription>
          </Alert>
        )}

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label>Soru Metni</Label>
            <Textarea
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder="Örn: Öğretim üyesi dersi anlaşılır biçimde anlatıyor mu?"
              rows={3}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Kategori</Label>
              <Select
                value={category}
                onValueChange={(v) => setCategory(v as EvaluationQuestionCategory)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Course">Ders</SelectItem>
                  <SelectItem value="Instructor">Öğretim Üyesi</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label>Soru Tipi</Label>
              <Select
                value={questionType}
                onValueChange={(v) => setQuestionType(v as EvaluationQuestionType)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Rating">Puan (1-5)</SelectItem>
                  <SelectItem value="Text">Metin</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Sıra No</Label>
              <Input
                type="number"
                value={sortOrder}
                onChange={(e) => setSortOrder(e.target.value)}
              />
            </div>

            {editing && (
              <div className="space-y-1.5">
                <Label>Durum</Label>
                <Select
                  value={isActive ? 'active' : 'inactive'}
                  onValueChange={(v) => setIsActive(v === 'active')}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="active">Aktif</SelectItem>
                    <SelectItem value="inactive">Pasif</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            İptal
          </Button>
          <Button onClick={handleSubmit} disabled={isPending}>
            {isPending ? 'Kaydediliyor...' : editing ? 'Kaydet' : 'Oluştur'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
