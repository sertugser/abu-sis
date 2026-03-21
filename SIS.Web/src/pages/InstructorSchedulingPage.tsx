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
        <PageHeader />
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
        <PageHeader />
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
        <PageHeader />

        <Card className="overflow-hidden">
          <CardHeader className="gap-4 border-b border-border bg-muted/30">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div className="space-y-3">
                <div>
                  <CardTitle>Section Seçimi</CardTitle>
                  <CardDescription>
                    Çakışma analizi ve öneri işlemleri yalnızca size atanmış dersler için çalışır.
                  </CardDescription>
                </div>
                <div className="relative">
                  <select
                    value={selectedSection?.sectionId ?? ''}
                    onChange={handleSectionChange}
                    className="min-w-[280px] appearance-none rounded-lg border border-border bg-card px-4 py-2 pr-10 text-sm font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                  >
                    {sections.map(section => (
                      <option key={section.sectionId} value={section.sectionId}>
                        {section.courseCode} — {section.courseName}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                <Badge variant="outline">{sectionsData.term}</Badge>
                <Badge variant={selectedSection?.isActive ? 'success' : 'secondary'}>
                  {selectedSection?.isActive ? 'Aktif Section' : 'Pasif Section'}
                </Badge>
                <Badge variant="outline">
                  {sectionsData.totalSections} ders / {sectionsData.totalStudents} öğrenci
                </Badge>
                <Badge
                  variant={availabilityQuery.isLoading ? 'outline' : availabilities.length > 0 ? 'success' : 'warning'}
                >
                  {availabilityQuery.isLoading
                    ? 'Müsaitlik yükleniyor'
                    : `${availabilities.length} müsaitlik kuralı`}
                </Badge>
              </div>
            </div>
          </CardHeader>

          {selectedSection && (
            <CardContent className="grid gap-4 p-6 md:grid-cols-2 xl:grid-cols-4">
              <SummaryItem
                icon={<BookOpen className="h-4 w-4 text-primary" />}
                label="Ders"
                value={`${selectedSection.courseCode} — ${selectedSection.courseName}`}
                detail={`${selectedSection.theoryHours}T + ${selectedSection.labHours}U · ${selectedSection.ects} AKTS`}
              />
              <SummaryItem
                icon={<Calendar className="h-4 w-4 text-primary" />}
                label="Mevcut Program"
                value={selectedSection.schedule || 'Henüz tanımlanmadı'}
                detail="Doğrulama mevcut schedule kaydı üzerinden yapılır."
              />
              <SummaryItem
                icon={<Building2 className="h-4 w-4 text-primary" />}
                label="Derslik"
                value={selectedSection.classroomName || 'Henüz atanmadı'}
                detail="Öneri akışı kapasiteye en yakın uygun dersliği arar."
              />
              <SummaryItem
                icon={<Users className="h-4 w-4 text-primary" />}
                label="Doluluk"
                value={`${selectedSection.activeStudents}/${selectedSection.capacity}`}
                detail={`${selectedSection.availableSeats} boş kontenjan`}
              />
            </CardContent>
          )}
        </Card>

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
              <CardHeader className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                  <CardTitle>Çakışma Analizi</CardTitle>
                  <CardDescription>
                    Seçili section&apos;ın mevcut programı; derslik, öğretim üyesi, müsaitlik, kapasite ve haftalık yük kurallarıyla karşılaştırılır.
                  </CardDescription>
                </div>
                <Button onClick={handleValidate} disabled={validateMutation.isPending}>
                  {validateMutation.isPending ? (
                    <>
                      <RefreshCcw className="h-4 w-4 animate-spin" />
                      Doğrulanıyor
                    </>
                  ) : (
                    <>
                      <CheckCircle2 className="h-4 w-4" />
                      Mevcut Programı Doğrula
                    </>
                  )}
                </Button>
              </CardHeader>
              <CardContent className="space-y-4">
                {!validateMutation.isPending && !validateMutation.data && !validationErrorMessage && (
                  <EmptyPanelState
                    title="Analiz henüz çalıştırılmadı"
                    description="Mevcut ders programınızın başka section’larla çakışıp çakışmadığını görmek için doğrulamayı başlatın."
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
              <CardHeader className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                  <CardTitle>Program Önerisi</CardTitle>
                  <CardDescription>
                    Sistem, size ait section için müsaitlik pencerelerine ve mevcut term schedule’larına göre conflict olmayan bir öneri üretmeye çalışır.
                  </CardDescription>
                </div>
                <Button onClick={handleSuggest} disabled={suggestMutation.isPending}>
                  {suggestMutation.isPending ? (
                    <>
                      <RefreshCcw className="h-4 w-4 animate-spin" />
                      Öneri Hazırlanıyor
                    </>
                  ) : (
                    <>
                      <Sparkles className="h-4 w-4" />
                      Uygun Program Öner
                    </>
                  )}
                </Button>
              </CardHeader>
              <CardContent className="space-y-4">
                {!suggestMutation.isPending && !suggestMutation.data && !suggestionErrorMessage && (
                  <EmptyPanelState
                    title="Henüz öneri üretilmedi"
                    description="Öneri akışı; kapasiteye uygun derslikleri ve instructor availability pencerelerini kullanarak deterministik bir çözüm dener."
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
                          <CardDescription>
                            Sonuçlar yalnızca bilgilendirme içindir; kalıcı bir atama yapılmaz.
                          </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          <div className="grid gap-4 md:grid-cols-2">
                            <SummaryItem
                              icon={<Building2 className="h-4 w-4 text-primary" />}
                              label="Önerilen Derslik"
                              value={suggestMutation.data.suggestedClassroomName || 'Öneri bulunamadı'}
                              detail={suggestMutation.data.suggestedClassroomId
                                ? `Derslik No: ${suggestMutation.data.suggestedClassroomId}`
                                : 'Uygun derslik eşleşmesi yok'}
                            />
                            <SummaryItem
                              icon={<Clock3 className="h-4 w-4 text-primary" />}
                              label="Haftalık Yük Etkisi"
                              value={formatWeeklyImpact(suggestMutation.data.estimatedWeeklyHoursImpact)}
                              detail="Yeni önerinin mevcut haftalık yük üzerindeki tahmini etkisi"
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

function PageHeader() {
  return (
    <div className="space-y-2">
      <div className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 text-xs text-muted-foreground">
        <Calendar className="h-3.5 w-3.5" />
        Öğretim Üyesi Paneli / Program Planlama
      </div>
      <div>
        <h1 className="font-heading text-2xl font-extrabold tracking-tight text-foreground">Program Planlama</h1>
        <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
          Section programınızı doğrulayın, conflict detaylarını görün, haftalık müsaitliğinizi yönetin ve uygun derslik/saat önerileri alın.
        </p>
      </div>
    </div>
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
      <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {icon}
        {label}
      </div>
      <p className="text-sm font-semibold text-foreground">{value}</p>
      <p className="mt-1 text-xs text-muted-foreground">{detail}</p>
    </div>
  );
}

function EmptyPanelState({ title, description }: { title: string; description: string }) {
  return (
    <div className="rounded-xl border border-dashed border-border bg-muted/20 px-5 py-10 text-center">
      <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-muted">
        <Sparkles className="h-5 w-5 text-muted-foreground" />
      </div>
      <h3 className="font-heading text-lg font-semibold text-foreground">{title}</h3>
      <p className="mx-auto mt-2 max-w-2xl text-sm text-muted-foreground">{description}</p>
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
        <CardTitle>Conflict Listesi</CardTitle>
        <CardDescription>
          Her kayıt, seçili section için tespit edilen ayrı bir conflict tipini gösterir.
        </CardDescription>
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
  return (
    <Card>
      <CardHeader className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <CardTitle>Müsaitlik Yönetimi</CardTitle>
          <CardDescription>
            Program önerileri bu haftalık pencereler içinden üretilir. Tercihli işaretlenen kayıtlar aynı derecede uygun seçenekler arasında önce denenir.
          </CardDescription>
        </div>
        <Button onClick={onCreate}>
          <Plus className="h-4 w-4" />
          Müsaitlik Ekle
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
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="h-32 rounded-xl" />
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
          <Alert variant="warning">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Henüz müsaitlik tanımı yok</AlertTitle>
            <AlertDescription className="space-y-3">
              <p>
                Program önerisinin çalışabilmesi için haftalık uygunluk pencerelerinizi tanımlayın. İsterseniz birkaç sabit pencereyle başlayıp sonradan düzenleyebilirsiniz.
              </p>
              <Button variant="outline" onClick={onCreate}>
                <Plus className="h-4 w-4" />
                İlk Müsaitliği Ekle
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {!isLoading && !errorMessage && availabilities.length > 0 && (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {availabilities.map(availability => (
              <AvailabilityRuleCard
                key={availability.id}
                availability={availability}
                onEdit={() => onEdit(availability)}
                onDelete={() => onDeleteRequest(availability)}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function AvailabilityRuleCard({
  availability,
  onEdit,
  onDelete,
}: {
  availability: InstructorAvailability;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-semibold text-foreground">
            {formatDay(availability.dayOfWeek)} · {formatTimeRange(availability.startTime, availability.endTime)}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {availability.isAvailable
              ? 'Bu aralıkta öneri üretilebilir.'
              : 'Bu aralıkta planlama yapılmaması gerekir.'}
          </p>
        </div>
        <div className="flex gap-2">
          <Badge variant={availability.isAvailable ? 'success' : 'secondary'}>
            {availability.isAvailable ? 'Uygun' : 'Kapalı'}
          </Badge>
          {availability.isPreferred && (
            <Badge variant="outline">Tercihli</Badge>
          )}
        </div>
      </div>

      <div className="mt-4 flex gap-2">
        <Button variant="outline" size="sm" onClick={onEdit}>
          <Pencil className="h-3.5 w-3.5" />
          Düzenle
        </Button>
        <Button variant="ghost" size="sm" onClick={onDelete}>
          <Trash2 className="h-3.5 w-3.5" />
          Sil
        </Button>
      </div>
    </div>
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
        <CardTitle>Availability Özeti</CardTitle>
        <CardDescription>
          Öneri motoru bu pencereleri kullanır. Daha fazla kontrol için üstteki müsaitlik yönetimi kartını kullanabilirsiniz.
        </CardDescription>
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
          <Alert variant="warning">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Müsaitlik tanımı bulunamadı</AlertTitle>
            <AlertDescription className="space-y-3">
              <p>
                Availability kaydı olmadan sistem conflict olmayan öneri üretmekte zorlanır.
              </p>
              <Button variant="outline" onClick={onCreate}>
                <Plus className="h-4 w-4" />
                Müsaitlik Ekle
              </Button>
            </AlertDescription>
          </Alert>
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
          <DialogTitle>{isEditing ? 'Müsaitlik Kaydını Düzenle' : 'Yeni Müsaitlik Ekle'}</DialogTitle>
          <DialogDescription>
            Haftalık müsaitlik pencerelerinizi tanımlayarak öneri motorunun derslik ve saat kombinasyonlarını daha isabetli üretmesini sağlayabilirsiniz.
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
