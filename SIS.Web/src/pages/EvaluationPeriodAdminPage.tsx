import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { CalendarClock, Lock, Unlock, Plus, Pencil, Trash2, AlertCircle } from 'lucide-react';
import {
  useActiveEvaluationPeriod,
  useAllEvaluationQuestions,
  useCloseEvaluationPeriod,
  useCreateEvaluationQuestion,
  useDeleteEvaluationQuestion,
  useOpenEvaluationPeriod,
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

  // Open-period form state
  const [selectedTermId, setSelectedTermId] = useState<string>('');
  const [closesAt, setClosesAt] = useState<string>('');
  const [formError, setFormError] = useState<string | null>(null);

  // Close confirmation
  const [closeConfirmOpen, setCloseConfirmOpen] = useState(false);

  // Question CRUD state
  const [questionDialogOpen, setQuestionDialogOpen] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<EvaluationQuestion | null>(null);
  const [deletingQuestionId, setDeletingQuestionId] = useState<number | null>(null);

  const isOpen = periodData?.isOpen === true;
  const activePeriod = periodData?.period;

  async function handleOpenPeriod() {
    setFormError(null);
    if (!selectedTermId) {
      setFormError('Lütfen bir dönem seçin.');
      return;
    }
    if (!closesAt) {
      setFormError('Lütfen kapanış tarihini seçin.');
      return;
    }
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
    <div className="space-y-6">
      <div>
        <h1 className="font-heading text-xl font-bold">Değerlendirme Dönemi Yönetimi</h1>
        <p className="text-sm text-muted-foreground">
          Ders değerlendirme anket dönemini açın veya kapatın, soruları yönetin.
        </p>
      </div>

      {/* Period Status Section */}
      {periodLoading ? (
        <Skeleton className="h-32 rounded-xl" />
      ) : isOpen && activePeriod ? (
        <Card className="border-l-4 border-l-emerald-500">
          <CardContent className="p-5 space-y-4">
            <div className="flex items-center gap-3">
              <Unlock className="h-8 w-8 text-emerald-600" />
              <div className="flex-1">
                <p className="text-sm font-semibold">Aktif Değerlendirme Dönemi</p>
                <p className="text-xs text-muted-foreground">
                  {activePeriod.termName} · Kapanış: {formatDate(activePeriod.closesAt)}
                </p>
              </div>
              <Badge variant="success">AÇIK</Badge>
            </div>
            <div className="text-xs text-muted-foreground">
              Açılma: {formatDate(activePeriod.openedAt)}
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
          </CardContent>
        </Card>
      ) : (
        <Card className="border-l-4 border-l-slate-400">
          <CardContent className="p-5 space-y-4">
            <div className="flex items-center gap-3">
              <Lock className="h-8 w-8 text-slate-500" />
              <div className="flex-1">
                <p className="text-sm font-semibold">Aktif Değerlendirme Dönemi Yok</p>
                <p className="text-xs text-muted-foreground">
                  Öğrencilerin ders değerlendirme yapabilmesi için yeni bir dönem açın.
                </p>
              </div>
              <Badge variant="secondary">KAPALI</Badge>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2 border-t">
              <div className="space-y-1.5">
                <Label>Dönem</Label>
                <Select value={selectedTermId} onValueChange={setSelectedTermId}>
                  <SelectTrigger>
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
                <Label>Kapanış Tarihi ve Saati</Label>
                <Input
                  type="datetime-local"
                  value={closesAt}
                  onChange={(e) => setClosesAt(e.target.value)}
                />
              </div>
            </div>

            {formError && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{formError}</AlertDescription>
              </Alert>
            )}

            <Button onClick={handleOpenPeriod} disabled={openMutation.isPending}>
              <CalendarClock className="h-4 w-4 mr-1.5" />
              {openMutation.isPending ? 'Açılıyor...' : 'Değerlendirme Dönemini Aç'}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Questions Management Section */}
      <div className="pt-4">
        <div className="flex items-center justify-between mb-3 flex-wrap gap-2">
          <div>
            <h2 className="font-heading text-lg font-bold">Değerlendirme Soruları</h2>
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

        {questionsLoading ? (
          <Skeleton className="h-64 rounded-xl" />
        ) : (
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-12">Sıra</TableHead>
                    <TableHead>Soru</TableHead>
                    <TableHead>Kategori</TableHead>
                    <TableHead>Tip</TableHead>
                    <TableHead>Durum</TableHead>
                    <TableHead className="text-right">İşlem</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(questions ?? []).length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                        Henüz soru tanımlanmamış.
                      </TableCell>
                    </TableRow>
                  ) : (
                    questions
                      ?.slice()
                      .sort((a, b) => a.sortOrder - b.sortOrder)
                      .map((q) => (
                        <TableRow key={q.id}>
                          <TableCell className="text-xs text-muted-foreground">
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
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-1">
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8"
                                onClick={() => {
                                  setEditingQuestion(q);
                                  setQuestionDialogOpen(true);
                                }}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8"
                                onClick={() => setDeletingQuestionId(q.id)}
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
            </CardContent>
          </Card>
        )}
      </div>

      {/* Question Form Dialog */}
      <QuestionFormDialog
        open={questionDialogOpen}
        onOpenChange={setQuestionDialogOpen}
        editing={editingQuestion}
      />

      {/* Delete Question Confirmation */}
      <AlertDialog
        open={deletingQuestionId != null}
        onOpenChange={(open) => !open && setDeletingQuestionId(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Soruyu sil?</AlertDialogTitle>
            <AlertDialogDescription>
              Bu soru kalıcı olarak silinecek. Daha önce bu soruya verilmiş yanıtlar etkilenebilir.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>İptal</AlertDialogCancel>
            <DeleteQuestionButton
              id={deletingQuestionId}
              onDone={() => setDeletingQuestionId(null)}
            />
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Close Period Confirmation */}
      <AlertDialog open={closeConfirmOpen} onOpenChange={setCloseConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Değerlendirme dönemini kapat?</AlertDialogTitle>
            <AlertDialogDescription>
              Dönem kapatıldığında öğrenciler artık değerlendirme yapamaz. Mevcut yanıtlar
              korunur.
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
    </div>
  );
}

function DeleteQuestionButton({ id, onDone }: { id: number | null; onDone: () => void }) {
  const deleteMutation = useDeleteEvaluationQuestion();
  async function handleDelete() {
    if (id == null) return;
    try {
      await deleteMutation.mutateAsync(id);
    } finally {
      onDone();
    }
  }
  return (
    <AlertDialogAction onClick={handleDelete} disabled={deleteMutation.isPending}>
      Sil
    </AlertDialogAction>
  );
}

function QuestionFormDialog({
  open,
  onOpenChange,
  editing,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: EvaluationQuestion | null;
}) {
  const createMutation = useCreateEvaluationQuestion();
  const updateMutation = useUpdateEvaluationQuestion();

  const [text, setText] = useState('');
  const [category, setCategory] = useState<EvaluationQuestionCategory>('Course');
  const [questionType, setQuestionType] = useState<EvaluationQuestionType>('Rating');
  const [sortOrder, setSortOrder] = useState<string>('0');
  const [isActive, setIsActive] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Sync form with editing
  const [lastEditingId, setLastEditingId] = useState<number | null>(null);
  if (open && editing?.id !== lastEditingId) {
    setLastEditingId(editing?.id ?? null);
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
  }

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
