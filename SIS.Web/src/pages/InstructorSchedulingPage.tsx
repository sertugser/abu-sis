import { useEffect, useState, type ChangeEvent, type ReactNode } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
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
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { ROUTES } from '@/lib/constants';
import {
  AlertCircle,
  AlertTriangle,
  BookOpen,
  Building2,
  Calendar,
  CheckCircle2,
  Clock3,
  Pencil,
  Plus,
  RefreshCcw,
  Sparkles,
  Trash2,
  Users,
} from 'lucide-react';
import { getSchedulingErrorMessage } from '../api/instructorScheduling.api';
import { useInstructorSections } from '../hooks/useInstructorSections';
import {
  useCreateInstructorAvailability,
  useDeleteInstructorAvailability,
  useInstructorAvailabilities,
  useSuggestInstructorSchedule,
  useUpdateInstructorAvailability,
  useValidateInstructorSchedule,
} from '../hooks/useInstructorScheduling';
import type {
  InstructorAvailability,
  ScheduleSlot,
  SchedulingConflict,
  UpsertInstructorAvailabilityRequest,
} from '../types/scheduling';

type PanelKey = 'analysis' | 'suggestion';

type AvailabilityFormState = {
  dayOfWeek: string;
  startTime: string;
  endTime: string;
  isAvailable: boolean;
  isPreferred: boolean;
};

const DAY_LABELS: Record<number, string> = {
  0: 'Pazar',
  1: 'Pazartesi',
  2: 'Salı',
  3: 'Çarşamba',
  4: 'Perşembe',
  5: 'Cuma',
  6: 'Cumartesi',
};

const DEFAULT_AVAILABILITY_FORM: AvailabilityFormState = {
  dayOfWeek: '1',
  startTime: '09:00',
  endTime: '12:00',
  isAvailable: true,
  isPreferred: true,
};

const CONFLICT_META: Record<string, { label: string; variant: 'destructive' | 'warning' | 'outline' }> = {
  ClassroomConflict: { label: 'Derslik Çakışması', variant: 'destructive' },
  InstructorConflict: { label: 'Öğretim Üyesi Çakışması', variant: 'destructive' },
  CapacityConflict: { label: 'Kapasite Sorunu', variant: 'warning' },
  InstructorAvailabilityConflict: { label: 'Müsaitlik Dışı', variant: 'warning' },
  WeeklyLoadConflict: { label: 'Haftalık Yük Aşımı', variant: 'warning' },
  InvalidScheduleJson: { label: 'Program Verisi Sorunu', variant: 'outline' },
};

export default function InstructorSchedulingPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activePanel, setActivePanel] = useState<PanelKey>('analysis');
  const [isAvailabilityDialogOpen, setIsAvailabilityDialogOpen] = useState(false);
  const [editingAvailability, setEditingAvailability] = useState<InstructorAvailability | null>(null);
  const [availabilityForm, setAvailabilityForm] = useState<AvailabilityFormState>(DEFAULT_AVAILABILITY_FORM);
  const [availabilityFormError, setAvailabilityFormError] = useState<string | null>(null);
  const [pendingDeleteAvailability, setPendingDeleteAvailability] = useState<InstructorAvailability | null>(null);

  const {
    data: sectionsData,
    isLoading,
    isError,
    error,
    refetch,
  } = useInstructorSections();

  const availabilityQuery = useInstructorAvailabilities();
  const validateMutation = useValidateInstructorSchedule();
  const suggestMutation = useSuggestInstructorSchedule();
  const createAvailabilityMutation = useCreateInstructorAvailability();
  const updateAvailabilityMutation = useUpdateInstructorAvailability();
  const deleteAvailabilityMutation = useDeleteInstructorAvailability();

  const selectedSectionId = Number(searchParams.get('sectionId'));
  const sections = sectionsData?.sections ?? [];
  const selectedSection = sections.find(section => section.sectionId === selectedSectionId) ?? sections[0] ?? null;
  const availabilities = availabilityQuery.data ?? [];

  const resetValidation = validateMutation.reset;
  const resetSuggestion = suggestMutation.reset;
  const isSavingAvailability = createAvailabilityMutation.isPending || updateAvailabilityMutation.isPending;
  const isDeletingAvailability = deleteAvailabilityMutation.isPending;

  useEffect(() => {
    if (!selectedSection) {
      return;
    }

    const currentSectionParam = searchParams.get('sectionId');
    if (currentSectionParam === String(selectedSection.sectionId)) {
      return;
    }

    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('sectionId', String(selectedSection.sectionId));
    setSearchParams(nextSearchParams, { replace: true });
  }, [searchParams, selectedSection, setSearchParams]);

  useEffect(() => {
    resetValidation();
    resetSuggestion();
  }, [selectedSection?.sectionId, resetValidation, resetSuggestion]);

  const sectionsErrorMessage = isError
    ? getSchedulingErrorMessage(error, 'Ders programı bilgileri yüklenemedi.')
    : null;

  const availabilityErrorMessage = availabilityQuery.isError
    ? getSchedulingErrorMessage(availabilityQuery.error, 'Müsaitlik bilgileri alınamadı.')
    : null;

  const availabilityMutationErrorMessage = createAvailabilityMutation.isError
    ? getSchedulingErrorMessage(createAvailabilityMutation.error, 'Müsaitlik kaydı eklenemedi.')
    : updateAvailabilityMutation.isError
      ? getSchedulingErrorMessage(updateAvailabilityMutation.error, 'Müsaitlik kaydı güncellenemedi.')
      : deleteAvailabilityMutation.isError
        ? getSchedulingErrorMessage(deleteAvailabilityMutation.error, 'Müsaitlik kaydı silinemedi.')
        : null;

  const validationErrorMessage = validateMutation.isError
    ? getSchedulingErrorMessage(validateMutation.error, 'Program doğrulaması tamamlanamadı.')
    : null;

  const suggestionErrorMessage = suggestMutation.isError
    ? getSchedulingErrorMessage(suggestMutation.error, 'Program önerisi alınamadı.')
    : null;

  const handleSectionChange = (event: ChangeEvent<HTMLSelectElement>) => {
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('sectionId', event.target.value);
    setSearchParams(nextSearchParams);
  };

  const handleValidate = () => {
    if (!selectedSection) {
      return;
    }

    validateMutation.mutate({ sectionId: selectedSection.sectionId });
  };

  const handleSuggest = () => {
    if (!selectedSection) {
      return;
    }

    suggestMutation.mutate({ sectionId: selectedSection.sectionId });
  };

  const handleAvailabilityDialogChange = (open: boolean) => {
    if (!open) {
      setEditingAvailability(null);
      setAvailabilityForm(DEFAULT_AVAILABILITY_FORM);
      setAvailabilityFormError(null);
    }

    setIsAvailabilityDialogOpen(open);
  };

  const openCreateAvailabilityDialog = () => {
    setEditingAvailability(null);
    setAvailabilityForm(DEFAULT_AVAILABILITY_FORM);
    setAvailabilityFormError(null);
    setIsAvailabilityDialogOpen(true);
  };

  const openEditAvailabilityDialog = (availability: InstructorAvailability) => {
    setEditingAvailability(availability);
    setAvailabilityForm({
      dayOfWeek: String(availability.dayOfWeek),
      startTime: normalizeTimeForInput(availability.startTime),
      endTime: normalizeTimeForInput(availability.endTime),
      isAvailable: availability.isAvailable,
      isPreferred: availability.isPreferred,
    });
    setAvailabilityFormError(null);
    setIsAvailabilityDialogOpen(true);
  };

  const handleAvailabilityFieldChange = (
    field: keyof AvailabilityFormState,
    value: string | boolean,
  ) => {
    setAvailabilityForm(current => {
      const next = {
        ...current,
        [field]: value,
      };

      if (field === 'isAvailable' && !value) {
        next.isPreferred = false;
      }

      return next;
    });
    setAvailabilityFormError(null);
  };

  const handleAvailabilitySubmit = () => {
    const payload = toAvailabilityPayload(availabilityForm);
    if (typeof payload === 'string') {
      setAvailabilityFormError(payload);
      return;
    }

    const onSuccess = () => {
      handleAvailabilityDialogChange(false);
      resetValidation();
      resetSuggestion();
    };

    if (editingAvailability) {
      updateAvailabilityMutation.mutate(
        {
          availabilityId: editingAvailability.id,
          payload,
        },
        { onSuccess },
      );
      return;
    }

    createAvailabilityMutation.mutate(payload, { onSuccess });
  };

  const handleDeleteAvailability = () => {
    if (!pendingDeleteAvailability) {
      return;
    }

    deleteAvailabilityMutation.mutate(pendingDeleteAvailability.id, {
      onSuccess: () => {
        setPendingDeleteAvailability(null);
        resetValidation();
        resetSuggestion();
      },
    });
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-48 rounded-xl" />
        <Skeleton className="h-48 rounded-xl" />
        <Skeleton className="h-12 w-80 rounded-xl" />
        <Skeleton className="h-72 rounded-xl" />
      </div>
    );
  }

  if (sectionsErrorMessage) {
    return (
      <div className="space-y-6">

        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Program ekranı yüklenemedi</AlertTitle>
          <AlertDescription className="space-y-4">
            <p>{sectionsErrorMessage}</p>
            <div className="flex flex-wrap gap-3">
              <Button variant="outline" onClick={() => refetch()}>
                <RefreshCcw className="h-4 w-4" />
                Yeniden Dene
              </Button>
              <Button variant="ghost" onClick={() => navigate(ROUTES.INSTRUCTOR_SECTIONS)}>
                Derslerime Dön
              </Button>
            </div>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  if (!sectionsData || sections.length === 0) {
    return (
      <div className="space-y-6">

        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-4 py-14 text-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-muted">
              <Calendar className="h-7 w-7 text-muted-foreground" />
            </div>
            <div className="space-y-1">
              <h2 className="font-heading text-xl font-semibold text-foreground">Atanmış ders bulunamadı</h2>
              <p className="max-w-xl text-sm text-muted-foreground">
                Bu dönem size atanmış section olmadığı için program analizi veya öneri üretimi yapılamıyor.
              </p>
            </div>
            <Button variant="outline" onClick={() => navigate('/')}>
              Ana Sayfaya Dön
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <>
      <div className="space-y-6">
        {/* Section selector bar */}
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-center gap-4">
            <select
              value={selectedSection?.sectionId ?? ''}
              onChange={handleSectionChange}
              className="min-w-[300px] appearance-none rounded-lg border border-border bg-card px-4 py-2 text-[15px] font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            >
              {sections.map(section => (
                <option key={section.sectionId} value={section.sectionId}>
                  {section.courseCode} — {section.courseName}
                </option>
              ))}
            </select>
          </div>

          {selectedSection && (
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline" className="text-sm py-1 px-3">
                <Calendar className="h-3.5 w-3.5 mr-1.5" />
                {selectedSection.schedule || 'Program belirlenmedi'}
              </Badge>
              <Badge variant="outline" className="text-sm py-1 px-3">
                <Building2 className="h-3.5 w-3.5 mr-1.5" />
                {selectedSection.classroomName || 'Derslik atanmadı'}
              </Badge>
              <Badge variant="outline" className="text-sm py-1 px-3">
                <Users className="h-3.5 w-3.5 mr-1.5" />
                {selectedSection.activeStudents}/{selectedSection.capacity}
              </Badge>
              <Badge variant="outline" className="text-sm py-1 px-3">
                {selectedSection.ects} AKTS
              </Badge>
            </div>
          )}
        </div>

        {/* Availability - FULL WIDTH */}
        <AvailabilityManagerCard
          availabilities={availabilities}
          isLoading={availabilityQuery.isLoading}
          errorMessage={availabilityErrorMessage}
          mutationErrorMessage={availabilityMutationErrorMessage}
          onRetry={() => availabilityQuery.refetch()}
          onCreate={openCreateAvailabilityDialog}
          onEdit={openEditAvailabilityDialog}
          onDeleteRequest={setPendingDeleteAvailability}
        />

        {/* Analysis / Suggestion tabs - FULL WIDTH */}
        <div className="space-y-4">
        <div className="inline-flex rounded-xl border border-border bg-card p-1">
          <PanelButton
            isActive={activePanel === 'analysis'}
            label="Çakışma Analizi"
            onClick={() => setActivePanel('analysis')}
          />
          <PanelButton
            isActive={activePanel === 'suggestion'}
            label="Program Önerisi"
            onClick={() => setActivePanel('suggestion')}
          />
        </div>

        {activePanel === 'analysis' && selectedSection && (
          <div className="space-y-4">
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle>Çakışma Analizi</CardTitle>
                <Button onClick={handleValidate} disabled={validateMutation.isPending} size="sm">
                  {validateMutation.isPending ? (
                    <>
                      <RefreshCcw className="h-4 w-4 animate-spin" />
                      Doğrulanıyor
                    </>
                  ) : (
                    <>
                      <CheckCircle2 className="h-4 w-4" />
                      Doğrula
                    </>
                  )}
                </Button>
              </CardHeader>
              <CardContent className="space-y-4">
                {!validateMutation.isPending && !validateMutation.data && !validationErrorMessage && (
                  <EmptyPanelState
                    title="Analiz henüz çalıştırılmadı"
                    description="Programınızı doğrulamak için yukarıdaki butonu kullanın."
                  />
                )}

                {validateMutation.isPending && (
                  <div className="space-y-3">
                    <Skeleton className="h-24 rounded-xl" />
                    <Skeleton className="h-64 rounded-xl" />
                  </div>
                )}

                {validationErrorMessage && (
                  <Alert variant="destructive">
                    <AlertTriangle className="h-4 w-4" />
                    <AlertTitle>Doğrulama tamamlanamadı</AlertTitle>
                    <AlertDescription>{validationErrorMessage}</AlertDescription>
                  </Alert>
                )}

                {validateMutation.data && (
                  <>
                    <NotesAlert notes={validateMutation.data.notes} />

                    <Alert variant={validateMutation.data.isValid ? 'success' : 'warning'}>
                      {validateMutation.data.isValid ? (
                        <CheckCircle2 className="h-4 w-4" />
                      ) : (
                        <AlertCircle className="h-4 w-4" />
                      )}
                      <AlertTitle>
                        {validateMutation.data.isValid
                          ? 'Program geçerli görünüyor'
                          : `${validateMutation.data.conflicts.length} adet conflict bulundu`}
                      </AlertTitle>
                      <AlertDescription>
                        {validateMutation.data.isValid
                          ? 'Seçili section için mevcut kurallar altında conflict tespit edilmedi.'
                          : 'Conflict listesini inceleyerek derslik, saat veya müsaitlik kaynaklı sorunları görebilirsiniz.'}
                      </AlertDescription>
                    </Alert>

                    {validateMutation.data.conflicts.length > 0 && (
                      <ConflictTable conflicts={validateMutation.data.conflicts} />
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          </div>
        )}

        {activePanel === 'suggestion' && selectedSection && (
          <div className="space-y-4">
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle>Program Önerisi</CardTitle>
                <Button onClick={handleSuggest} disabled={suggestMutation.isPending} size="sm">
                  {suggestMutation.isPending ? (
                    <>
                      <RefreshCcw className="h-4 w-4 animate-spin" />
                      Hazırlanıyor
                    </>
                  ) : (
                    <>
                      <Sparkles className="h-4 w-4" />
                      Öneri Al
                    </>
                  )}
                </Button>
              </CardHeader>
              <CardContent className="space-y-4">
                {!suggestMutation.isPending && !suggestMutation.data && !suggestionErrorMessage && (
                  <EmptyPanelState
                    title="Henüz öneri üretilmedi"
                    description="Müsaitliğinize uygun program önerisi almak için butonu kullanın."
                  />
                )}

                {suggestMutation.isPending && (
                  <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
                    <Skeleton className="h-72 rounded-xl" />
                    <Skeleton className="h-72 rounded-xl" />
                  </div>
                )}

                {suggestionErrorMessage && (
                  <Alert variant="destructive">
                    <AlertTriangle className="h-4 w-4" />
                    <AlertTitle>Öneri alınamadı</AlertTitle>
                    <AlertDescription>{suggestionErrorMessage}</AlertDescription>
                  </Alert>
                )}

                {suggestMutation.data && (
                  <div className="grid gap-4 xl:grid-cols-[1.25fr_0.75fr]">
                    <div className="space-y-4">
                      <NotesAlert notes={suggestMutation.data.notes} />

                      <Alert variant={suggestMutation.data.isFeasible ? 'success' : 'warning'}>
                        {suggestMutation.data.isFeasible ? (
                          <CheckCircle2 className="h-4 w-4" />
                        ) : (
                          <AlertCircle className="h-4 w-4" />
                        )}
                        <AlertTitle>
                          {suggestMutation.data.isFeasible
                            ? 'Conflict olmayan bir öneri bulundu'
                            : 'Uygun çözüm bulunamadı'}
                        </AlertTitle>
                        <AlertDescription>
                          {suggestMutation.data.isFeasible
                            ? 'Önerilen derslik ve slotlar mevcut kurallar altında conflict üretmiyor.'
                            : 'Sistem, mevcut müsaitlik ve term schedule’ları içinde geçerli bir kombinasyon bulamadı.'}
                        </AlertDescription>
                      </Alert>

                      <Card>
                        <CardHeader>
                          <CardTitle>Öneri Özeti</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          <div className="grid gap-4 md:grid-cols-2">
                            <SummaryItem
                              icon={<Building2 className="h-4 w-4 text-primary" />}
                              label="Önerilen Derslik"
                              value={suggestMutation.data.suggestedClassroomName || 'Bulunamadı'}
                              detail=""
                            />
                            <SummaryItem
                              icon={<Clock3 className="h-4 w-4 text-primary" />}
                              label="Haftalık Yük Etkisi"
                              value={formatWeeklyImpact(suggestMutation.data.estimatedWeeklyHoursImpact)}
                              detail=""
                            />
                          </div>

                          <div className="space-y-3">
                            <div className="flex items-center gap-2">
                              <Calendar className="h-4 w-4 text-primary" />
                              <h3 className="text-sm font-semibold text-foreground">Önerilen Slotlar</h3>
                            </div>

                            {suggestMutation.data.suggestedSlots.length > 0 ? (
                              <div className="flex flex-wrap gap-2">
                                {suggestMutation.data.suggestedSlots.map((slot, index) => (
                                  <Badge key={`${slot.day}-${slot.startTime}-${slot.endTime}-${index}`} variant="outline" className="px-3 py-1">
                                    {formatSlot(slot)}
                                  </Badge>
                                ))}
                              </div>
                            ) : (
                              <p className="text-sm text-muted-foreground">
                                Bu section için conflict olmayan slot üretilemedi.
                              </p>
                            )}
                          </div>
                        </CardContent>
                      </Card>
                    </div>

                    <AvailabilitySummaryCard
                      availabilities={availabilities}
                      isLoading={availabilityQuery.isLoading}
                      errorMessage={availabilityErrorMessage}
                      onCreate={openCreateAvailabilityDialog}
                    />
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        )}
        </div>{/* end analysis/suggestion */}
      </div>

      <AvailabilityDialog
        isOpen={isAvailabilityDialogOpen}
        isPending={isSavingAvailability}
        errorMessage={availabilityFormError ?? availabilityMutationErrorMessage}
        isEditing={Boolean(editingAvailability)}
        form={availabilityForm}
        onOpenChange={handleAvailabilityDialogChange}
        onFieldChange={handleAvailabilityFieldChange}
        onSubmit={handleAvailabilitySubmit}
      />

      <AlertDialog
        open={Boolean(pendingDeleteAvailability)}
        onOpenChange={(open) => {
          if (!open && !isDeletingAvailability) {
            setPendingDeleteAvailability(null);
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Müsaitlik kaydı silinsin mi?</AlertDialogTitle>
            <AlertDialogDescription>
              {pendingDeleteAvailability
                ? `${formatDay(pendingDeleteAvailability.dayOfWeek)} ${formatTimeRange(
                  pendingDeleteAvailability.startTime,
                  pendingDeleteAvailability.endTime,
                )} penceresi kaldırılacak.`
                : 'Bu işlem geri alınamaz.'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeletingAvailability}>İptal</AlertDialogCancel>
            <AlertDialogAction onClick={handleDeleteAvailability} disabled={isDeletingAvailability}>
              {isDeletingAvailability ? 'Siliniyor' : 'Sil'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}


function PanelButton({
  isActive,
  label,
  onClick,
}: {
  isActive: boolean;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'rounded-lg px-4 py-2 text-sm font-medium transition-colors',
        isActive
          ? 'bg-primary text-primary-foreground shadow-sm'
          : 'text-muted-foreground hover:bg-muted hover:text-foreground',
      )}
    >
      {label}
    </button>
  );
}

function SummaryItem({
  icon,
  label,
  value,
  detail,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <div className="rounded-xl border border-border bg-card p-4">
      <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {icon}
        {label}
      </div>
      <p className="text-sm font-semibold text-foreground">{value}</p>
      {detail && <p className="mt-1 text-xs text-muted-foreground">{detail}</p>}
    </div>
  );
}

function EmptyPanelState({ title, description }: { title: string; description: string }) {
  return (
    <div className="rounded-xl border border-dashed border-border bg-muted/10 px-5 py-8 text-center">
      <p className="text-sm font-medium text-muted-foreground">{title}</p>
      <p className="mt-1 text-xs text-muted-foreground">{description}</p>
    </div>
  );
}

function NotesAlert({ notes }: { notes: string[] }) {
  if (notes.length === 0) {
    return null;
  }

  return (
    <Alert variant="info">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>Sistem notları</AlertTitle>
      <AlertDescription>
        <ul className="space-y-1">
          {notes.map(note => (
            <li key={note}>{note}</li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  );
}

function ConflictTable({ conflicts }: { conflicts: SchedulingConflict[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Çakışma Listesi</CardTitle>
      </CardHeader>
      <CardContent>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Conflict</TableHead>
              <TableHead>Gün / Saat</TableHead>
              <TableHead>Derslik</TableHead>
              <TableHead>İlgili Section</TableHead>
              <TableHead>Açıklama</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {conflicts.map((conflict, index) => {
              const meta = CONFLICT_META[conflict.conflictType] ?? {
                label: conflict.conflictType,
                variant: 'outline' as const,
              };

              return (
                <TableRow key={`${conflict.conflictType}-${conflict.relatedSectionId}-${index}`}>
                  <TableCell>
                    <div className="space-y-2">
                      <Badge variant={meta.variant}>{meta.label}</Badge>
                      <div className="text-xs text-muted-foreground">
                        {conflict.courseCode} — {conflict.courseName}
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="text-sm text-foreground">
                    {formatConflictTime(conflict)}
                  </TableCell>
                  <TableCell className="text-sm text-foreground">
                    {conflict.classroomName || 'Belirtilmedi'}
                  </TableCell>
                  <TableCell className="text-sm text-foreground">
                    {conflict.relatedSectionId ? `Section ${conflict.relatedSectionId}` : '—'}
                  </TableCell>
                  <TableCell className="max-w-[460px] text-sm text-muted-foreground">
                    {conflict.message}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

function AvailabilityManagerCard({
  availabilities,
  isLoading,
  errorMessage,
  mutationErrorMessage,
  onRetry,
  onCreate,
  onEdit,
  onDeleteRequest,
}: {
  availabilities: InstructorAvailability[];
  isLoading: boolean;
  errorMessage: string | null;
  mutationErrorMessage: string | null;
  onRetry: () => void;
  onCreate: () => void;
  onEdit: (availability: InstructorAvailability) => void;
  onDeleteRequest: (availability: InstructorAvailability) => void;
}) {
  const hasWeekend = availabilities.some(a => a.dayOfWeek === 0 || a.dayOfWeek === 6);
  const activeDays = hasWeekend ? [1, 2, 3, 4, 5, 6, 0] : [1, 2, 3, 4, 5];
  
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Haftalık Müsaitlik</CardTitle>
        <Button onClick={onCreate} size="sm">
          <Plus className="h-4 w-4" />
          Ekle
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {mutationErrorMessage && (
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Müsaitlik kaydı işlenemedi</AlertTitle>
            <AlertDescription>{mutationErrorMessage}</AlertDescription>
          </Alert>
        )}

        {isLoading && (
          <div className="grid grid-cols-5 gap-4">
            {Array.from({ length: 5 }).map((_, index) => (
              <div key={index} className="space-y-3">
                <Skeleton className="h-10 rounded-md w-full mb-4" />
                <Skeleton className="h-24 rounded-lg" />
                <Skeleton className="h-24 rounded-lg hidden md:block" />
              </div>
            ))}
          </div>
        )}

        {!isLoading && errorMessage && (
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Müsaitlik bilgileri alınamadı</AlertTitle>
            <AlertDescription className="space-y-4">
              <p>{errorMessage}</p>
              <Button variant="outline" onClick={onRetry}>
                <RefreshCcw className="h-4 w-4" />
                Yeniden Dene
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {!isLoading && !errorMessage && availabilities.length === 0 && (
          <div className="py-8 text-center text-muted-foreground text-sm">
            Henüz müsaitlik tanımı yok. Eklemek için yukarıdaki butonu kullanın.
          </div>
        )}

        {!isLoading && !errorMessage && availabilities.length > 0 && (
          <div>
            <div className={cn("grid gap-3", hasWeekend ? "grid-cols-7" : "grid-cols-5")}>
              {activeDays.map(dayId => {
                const dayAvailabilities = availabilities
                  .filter(a => a.dayOfWeek === dayId)
                  .sort((a, b) => a.startTime.localeCompare(b.startTime));

                return (
                  <div key={dayId} className="flex flex-col rounded-xl border border-border bg-muted/10 overflow-hidden">
                    <div className="bg-muted p-3 text-center border-b border-border">
                      <h3 className="font-semibold text-sm text-foreground">{DAY_LABELS[dayId]}</h3>
                    </div>
                    <div className="p-3 flex flex-col gap-3 min-h-[150px]">
                      {dayAvailabilities.length > 0 ? (
                        dayAvailabilities.map(availability => (
                          <div 
                            key={availability.id} 
                            className={cn(
                              "rounded-lg p-3 text-sm border shadow-sm flex flex-col relative group cursor-pointer transition-all hover:scale-[1.02]",
                              availability.isAvailable 
                                ? availability.isPreferred 
                                  ? "bg-primary/5 border-primary/30 hover:bg-primary/10" 
                                  : "bg-card border-border hover:bg-muted/50" 
                                : "bg-muted/50 border-border opacity-70"
                            )}
                            onClick={() => onEdit(availability)}
                          >
                            <div className="mb-1">
                              <span className="font-medium text-foreground">
                                {formatTimeRange(availability.startTime, availability.endTime)}
                              </span>
                            </div>
                            <div className="text-xs text-muted-foreground mt-1">
                              <div className="flex flex-wrap gap-1.5 mt-1">
                                <Badge variant={availability.isAvailable ? 'success' : 'secondary'} className="text-[10px] px-1.5 py-0 h-5">
                                  {availability.isAvailable ? 'Uygun' : 'Kapalı'}
                                </Badge>
                                {availability.isPreferred && (
                                  <Badge variant="outline" className="text-[10px] px-1.5 py-0 h-5 border-primary/50 text-primary hover:bg-transparent">Tercihli</Badge>
                                )}
                              </div>
                            </div>
                            
                            <div className="mt-2 flex gap-1">
                              <Button
                                variant="secondary"
                                size="sm"
                                className="h-7 px-2 flex-1 text-xs"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  onEdit(availability);
                                }}
                              >
                                <Pencil className="h-3 w-3 mr-1" /> Düzenle
                              </Button>
                              <Button
                                variant="destructive"
                                size="sm"
                                className="h-7 px-2 flex-none text-xs"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  onDeleteRequest(availability);
                                }}
                              >
                                <Trash2 className="h-3 w-3 mr-1" /> Sil
                              </Button>
                            </div>
                          </div>
                        ))
                      ) : (
                        <div className="h-full flex items-center justify-center text-xs text-muted-foreground opacity-40 flex-1 min-h-[60px]">
                          —
                        </div>
                      )}
                      
                      <Button 
                        variant="ghost" 
                        size="sm" 
                        className="w-full h-8 text-xs text-muted-foreground hover:text-primary border border-dashed border-transparent hover:border-primary/30 mt-auto"
                        onClick={() => onCreate()}
                      >
                        <Plus className="h-3 w-3 mr-1" /> Ekle
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function AvailabilitySummaryCard({
  availabilities,
  isLoading,
  errorMessage,
  onCreate,
}: {
  availabilities: InstructorAvailability[];
  isLoading: boolean;
  errorMessage: string | null;
  onCreate: () => void;
}) {
  return (
    <Card className="h-fit">
      <CardHeader>
        <CardTitle>Müsaitlik Özeti</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading && (
          <>
            <Skeleton className="h-24 rounded-xl" />
            <Skeleton className="h-24 rounded-xl" />
          </>
        )}

        {!isLoading && errorMessage && (
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Müsaitlik özeti yüklenemedi</AlertTitle>
            <AlertDescription>{errorMessage}</AlertDescription>
          </Alert>
        )}

        {!isLoading && !errorMessage && availabilities.length === 0 && (
          <div className="py-6 text-center text-muted-foreground text-sm">
            Müsaitlik tanımı yok.
          </div>
        )}

        {!isLoading && !errorMessage && availabilities.length > 0 && (
          <>
            <div className="flex flex-wrap gap-2">
              <Badge variant="success">{availabilities.length} aktif kural</Badge>
              <Badge variant="outline">
                {availabilities.filter(availability => availability.isPreferred).length} tercihli pencere
              </Badge>
            </div>

            {availabilities.slice(0, 6).map(availability => (
              <div
                key={availability.id}
                className="rounded-xl border border-border bg-muted/20 p-4"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <p className="text-sm font-semibold text-foreground">
                      {formatDay(availability.dayOfWeek)} · {formatTimeRange(availability.startTime, availability.endTime)}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {availability.isPreferred ? 'Öncelikli pencere' : 'Standart pencere'}
                    </p>
                  </div>
                  <Badge variant={availability.isPreferred ? 'outline' : 'success'}>
                    {availability.isPreferred ? 'Tercihli' : 'Uygun'}
                  </Badge>
                </div>
              </div>
            ))}

            {availabilities.length > 6 && (
              <p className="text-xs text-muted-foreground">
                Tüm kayıtları görmek ve düzenlemek için üstteki yönetim alanını kullanın.
              </p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function AvailabilityDialog({
  isOpen,
  isPending,
  errorMessage,
  isEditing,
  form,
  onOpenChange,
  onFieldChange,
  onSubmit,
}: {
  isOpen: boolean;
  isPending: boolean;
  errorMessage: string | null;
  isEditing: boolean;
  form: AvailabilityFormState;
  onOpenChange: (open: boolean) => void;
  onFieldChange: (field: keyof AvailabilityFormState, value: string | boolean) => void;
  onSubmit: () => void;
}) {
  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Müsaitlik Düzenle' : 'Müsaitlik Ekle'}</DialogTitle>
          <DialogDescription>
            Haftalık uygunluk pencerenizi belirleyin.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="availability-day">Gün</Label>
              <select
                id="availability-day"
                value={form.dayOfWeek}
                onChange={event => onFieldChange('dayOfWeek', event.target.value)}
                className="flex h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring"
              >
                {Object.entries(DAY_LABELS)
                  .filter(([key]) => Number(key) >= 1 && Number(key) <= 5)
                  .map(([key, label]) => (
                    <option key={key} value={key}>
                      {label}
                    </option>
                  ))}
              </select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="availability-start">Başlangıç</Label>
              <Input
                id="availability-start"
                type="time"
                value={form.startTime}
                onChange={event => onFieldChange('startTime', event.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="availability-end">Bitiş</Label>
              <Input
                id="availability-end"
                type="time"
                value={form.endTime}
                onChange={event => onFieldChange('endTime', event.target.value)}
              />
            </div>
          </div>

          <div className="grid gap-3 rounded-xl border border-border bg-muted/20 p-4">
            <label className="flex items-start gap-3">
              <input
                type="checkbox"
                checked={form.isAvailable}
                onChange={event => onFieldChange('isAvailable', event.target.checked)}
                className="mt-1 h-4 w-4 rounded border-border"
              />
              <div>
                <p className="text-sm font-medium text-foreground">Bu pencere uygun kabul edilsin</p>
                <p className="text-xs text-muted-foreground">
                  Kapalı işaretlerseniz sistem bu saat aralığında öneri üretmez.
                </p>
              </div>
            </label>

            <label className="flex items-start gap-3">
              <input
                type="checkbox"
                checked={form.isPreferred}
                disabled={!form.isAvailable}
                onChange={event => onFieldChange('isPreferred', event.target.checked)}
                className="mt-1 h-4 w-4 rounded border-border"
              />
              <div>
                <p className="text-sm font-medium text-foreground">Tercihli pencere</p>
                <p className="text-xs text-muted-foreground">
                  Aynı derecede uygun seçenekler arasında önce bu pencere denenir.
                </p>
              </div>
            </label>
          </div>

          {errorMessage && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Form kaydedilemedi</AlertTitle>
              <AlertDescription>{errorMessage}</AlertDescription>
            </Alert>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            İptal
          </Button>
          <Button onClick={onSubmit} disabled={isPending}>
            {isPending ? 'Kaydediliyor' : isEditing ? 'Güncelle' : 'Kaydet'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function formatConflictTime(conflict: SchedulingConflict) {
  if (conflict.day === null || conflict.startTime === null || conflict.endTime === null) {
    return 'Saat bilgisi bulunmuyor';
  }

  return `${formatDay(conflict.day)} · ${formatTimeRange(conflict.startTime, conflict.endTime)}`;
}

function formatSlot(slot: ScheduleSlot) {
  return `${formatDay(slot.day)} ${formatTimeRange(slot.startTime, slot.endTime)}`;
}

function formatDay(day: number | null | undefined) {
  if (day === null || day === undefined) {
    return 'Belirtilmedi';
  }

  return DAY_LABELS[day] ?? `Gün ${day}`;
}

function formatTimeRange(startTime: string | null | undefined, endTime: string | null | undefined) {
  return `${formatTime(startTime)}-${formatTime(endTime)}`;
}

function formatTime(value: string | null | undefined) {
  if (!value) {
    return '—';
  }

  return value.slice(0, 5);
}

function formatWeeklyImpact(impact: number) {
  if (impact === 0) {
    return 'Değişim yok';
  }

  const formatted = Math.abs(impact).toLocaleString('tr-TR', {
    minimumFractionDigits: Number.isInteger(impact) ? 0 : 1,
    maximumFractionDigits: 2,
  });

  return `${impact > 0 ? '+' : '-'}${formatted} saat`;
}

function normalizeTimeForInput(value: string) {
  return value.slice(0, 5);
}

function toAvailabilityPayload(form: AvailabilityFormState): UpsertInstructorAvailabilityRequest | string {
  const dayOfWeek = Number(form.dayOfWeek);
  if (Number.isNaN(dayOfWeek)) {
    return 'Geçerli bir gün seçilmelidir.';
  }

  if (!form.startTime || !form.endTime) {
    return 'Başlangıç ve bitiş saati zorunludur.';
  }

  if (form.endTime <= form.startTime) {
    return 'Bitiş saati başlangıç saatinden sonra olmalıdır.';
  }

  if (form.isPreferred && !form.isAvailable) {
    return 'Tercihli pencere yalnızca uygun işaretlenen saatlerde tanımlanabilir.';
  }

  return {
    dayOfWeek,
    startTime: form.startTime,
    endTime: form.endTime,
    isAvailable: form.isAvailable,
    isPreferred: form.isPreferred,
  };
}
