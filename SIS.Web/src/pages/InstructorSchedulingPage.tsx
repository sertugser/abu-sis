import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { cn } from '@/lib/utils';
import { ROUTES } from '@/lib/constants';
import {
  AlertTriangle,
  Calendar,
  Pencil,
  Plus,
  RefreshCcw,
  Trash2,
} from 'lucide-react';
import { getSchedulingErrorMessage } from '../api/instructorScheduling.api';
import { useInstructorSections } from '../hooks/useInstructorSections';
import {
  useCreateInstructorAvailability,
  useDeleteInstructorAvailability,
  useInstructorAvailabilities,
  useUpdateInstructorAvailability,
} from '../hooks/useInstructorScheduling';
import WeeklyTimetableGrid, { type TimetableEntry } from '../components/scheduling/WeeklyTimetableGrid';
import { parseScheduleJson } from '../lib/scheduleUtils';
import type {
  InstructorAvailability,
  UpsertInstructorAvailabilityRequest,
} from '../types/scheduling';

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

export default function InstructorSchedulingPage() {
  const navigate = useNavigate();
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
  const createAvailabilityMutation = useCreateInstructorAvailability();
  const updateAvailabilityMutation = useUpdateInstructorAvailability();
  const deleteAvailabilityMutation = useDeleteInstructorAvailability();

  const sections = sectionsData?.sections ?? [];
  const availabilities = availabilityQuery.data ?? [];

  const isSavingAvailability = createAvailabilityMutation.isPending || updateAvailabilityMutation.isPending;
  const isDeletingAvailability = deleteAvailabilityMutation.isPending;

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

  const handleAvailabilityDialogChange = (open: boolean) => {
    if (!open) {
      setEditingAvailability(null);
      setAvailabilityForm(DEFAULT_AVAILABILITY_FORM);
      setAvailabilityFormError(null);
    }

    setIsAvailabilityDialogOpen(open);
  };

  const openCreateAvailabilityDialog = (dayOfWeek?: number) => {
    setEditingAvailability(null);
    setAvailabilityForm({
      ...DEFAULT_AVAILABILITY_FORM,
      dayOfWeek: String(dayOfWeek ?? 1),
    });
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
      <>
        <div className="space-y-6">
          <Card>
            <CardContent className="flex flex-col items-center justify-center gap-4 py-14 text-center">
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-muted">
                <Calendar className="h-7 w-7 text-muted-foreground" />
              </div>
              <div className="space-y-1">
                <h2 className="font-heading text-xl font-semibold text-foreground">Atanmış ders bulunamadı</h2>
                <p className="max-w-xl text-sm text-muted-foreground">
                  Bu dönem size atanmış ders bulunamadı. Müsaitlik bilgilerinizi yine de tanımlayabilirsiniz.
                </p>
              </div>
            </CardContent>
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

  return (
    <>
      <div className="space-y-6">
        {/* Availability CRUD Card */}
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

        {/* Weekly Timetable (read-only) */}
        <WeeklyTimetableGrid
          entries={buildTimetableEntries(sections, new Set())}
          availabilities={availabilities}
          title="Haftalık Ders Programı"
        />
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
  onCreate: (dayOfWeek?: number) => void;
  onEdit: (availability: InstructorAvailability) => void;
  onDeleteRequest: (availability: InstructorAvailability) => void;
}) {
  const hasWeekend = availabilities.some(a => a.dayOfWeek === 0 || a.dayOfWeek === 6);
  const activeDays = hasWeekend ? [1, 2, 3, 4, 5, 6, 0] : [1, 2, 3, 4, 5];

  return (
    <Card>
      <CardHeader className="flex items-center pb-4">
        <CardTitle className="text-lg">Haftalık Müsaitlik</CardTitle>
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
            <div className={cn("grid gap-4", hasWeekend ? "grid-cols-7" : "grid-cols-5")}>
              {activeDays.map(dayId => {
                const dayAvailabilities = availabilities
                  .filter(a => a.dayOfWeek === dayId)
                  .sort((a, b) => a.startTime.localeCompare(b.startTime));

                return (
                  <div key={dayId} className="flex flex-col rounded-xl border border-border bg-muted/10 overflow-hidden">
                    <div className="bg-muted px-4 py-3 text-center border-b border-border">
                      <h3 className="font-semibold text-sm text-foreground">{DAY_LABELS[dayId]}</h3>
                    </div>
                    <div className="p-4 flex flex-col gap-3 min-h-[160px]">
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
                        onClick={() => onCreate(dayId)}
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

function buildTimetableEntries(
  sections: Array<{
    sectionId: number;
    courseCode: string;
    courseName: string;
    classroomName: string;
    scheduleJson?: string;
  }>,
  conflictSectionIds: Set<number>,
): TimetableEntry[] {
  const entries: TimetableEntry[] = [];

  for (const section of sections) {
    const slots = parseScheduleJson(section.scheduleJson);
    if (slots.length === 0) {
      continue;
    }

    entries.push({
      id: String(section.sectionId),
      label: section.courseName,
      courseCode: section.courseCode,
      classroomName: section.classroomName || undefined,
      slots,
      hasConflict: conflictSectionIds.has(section.sectionId),
    });
  }

  return entries;
}
