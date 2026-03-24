import { useState, type ChangeEvent, type ReactNode } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  AlertCircle,
  AlertTriangle,
  Building2,
  Calendar,
  CheckCircle2,
  RefreshCcw,
  Users,
  XCircle,
  Zap,
} from 'lucide-react';
import WeeklyTimetableGrid, { type TimetableEntry } from '../components/scheduling/WeeklyTimetableGrid';
import { getAcademicOperationsErrorMessage } from '../api/academicOperations.api';
import { getSchedulingManagementErrorMessage } from '../api/scheduling.api';
import {
  useAcademicOperationsContext,
  useAcademicPlanningSummary,
  useDepartmentClassLists,
} from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useBatchAutoSchedule } from '../hooks/useScheduling';
import { parseScheduleJson } from '../lib/scheduleUtils';
import type { DepartmentClassList } from '../types/academicOperations';
import type { BatchAutoScheduleItem, ScheduleSlot, SchedulingConflict } from '../types/scheduling';

const DAY_LABELS: Record<number, string> = {
  0: 'Paz',
  1: 'Pzt',
  2: 'Sal',
  3: 'Çar',
  4: 'Per',
  5: 'Cum',
  6: 'Cmt',
};

export default function SchedulingDashboardPage() {
  const contextQuery = useAcademicOperationsContext();
  const activeTermQuery = useActiveTerm();
  const batchMutation = useBatchAutoSchedule();

  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const canViewAllDepartments = contextQuery.data?.canViewAllDepartments ?? false;

  const [selectedDepartmentId, setSelectedDepartmentId] = useState<number | null | undefined>(undefined);

  const effectiveDepartmentId = selectedDepartmentId === undefined
    ? contextQuery.data?.defaultDepartmentId ?? null
    : selectedDepartmentId;

  const classListsQuery = useDepartmentClassLists(
    effectiveDepartmentId,
    activeTermQuery.data?.id,
    effectiveDepartmentId !== null,
  );
  const planningSummaryQuery = useAcademicPlanningSummary(
    effectiveDepartmentId,
    activeTermQuery.data?.id,
    effectiveDepartmentId !== null,
  );

  const contextErrorMessage = contextQuery.isError
    ? getAcademicOperationsErrorMessage(contextQuery.error, 'Yetki bilgileri yüklenemedi.')
    : null;

  const classListErrorMessage = classListsQuery.isError
    ? getAcademicOperationsErrorMessage(classListsQuery.error, 'Bölüm ders programı alınamadı.')
    : null;

  const batchErrorMessage = batchMutation.isError
    ? getSchedulingManagementErrorMessage(batchMutation.error, 'Toplu programlama sırasında bir hata oluştu.')
    : null;

  const classLists = classListsQuery.data ?? null;
  const timetableEntries = classLists
    ? buildDepartmentTimetableEntries(
      classLists,
      collectConflictSectionIds(planningSummaryQuery.data?.scheduleValidation.conflicts ?? []),
    )
    : [];
  const scheduledSectionCount = timetableEntries.length;
  const hasAccessibleDepartments = canViewAllDepartments || departments.length > 0;

  const handleDepartmentChange = (event: ChangeEvent<HTMLSelectElement>) => {
    const value = event.target.value;
    setSelectedDepartmentId(value ? Number(value) : null);
  };

  const handleBatchSchedule = () => {
    batchMutation.mutate({
      termId: activeTermQuery.data?.id,
      departmentId: effectiveDepartmentId,
    });
  };

  if (contextQuery.isLoading || activeTermQuery.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-80" />
        <Skeleton className="h-40 rounded-xl" />
        <Skeleton className="h-[420px] rounded-xl" />
      </div>
    );
  }

  if (contextErrorMessage) {
    return (
      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>Toplu programlama ekranı yüklenemedi</AlertTitle>
        <AlertDescription>{contextErrorMessage}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Toplu Programlama</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Programsız section&apos;ları öncelik sırasına göre otomatik olarak planlayın.
          </p>
        </div>

        {activeTermQuery.data && (
          <Badge variant="outline" className="w-fit px-3 py-1 text-sm">
            <Calendar className="mr-1.5 h-3.5 w-3.5" />
            {activeTermQuery.data.name}
          </Badge>
        )}
      </div>

      {contextQuery.data?.notes?.length ? (
        <NotesAlert title="Yetki notları" notes={contextQuery.data.notes} />
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Programlama Ayarları</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {!hasAccessibleDepartments && (
            <Alert variant="warning">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Erişilebilir bölüm bulunamadı</AlertTitle>
              <AlertDescription>
                Bu kullanıcı için toplu programlama yapılabilecek bir bölüm tanımlı değil.
              </AlertDescription>
            </Alert>
          )}

          <div className="flex flex-col gap-4 lg:flex-row lg:items-end">
            <div className="max-w-sm flex-1 space-y-2">
              <label className="text-sm font-medium text-foreground">Bölüm</label>
              <select
                value={effectiveDepartmentId ?? ''}
                onChange={handleDepartmentChange}
                disabled={!hasAccessibleDepartments || batchMutation.isPending}
                className="flex h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring disabled:cursor-not-allowed disabled:opacity-60"
              >
                {canViewAllDepartments && <option value="">Tüm bölümler</option>}
                {departments.map(department => (
                  <option key={department.departmentId} value={department.departmentId}>
                    {department.departmentName}
                  </option>
                ))}
              </select>
            </div>

            <Button
              onClick={handleBatchSchedule}
              disabled={!hasAccessibleDepartments || batchMutation.isPending}
              size="lg"
              className="min-w-[220px]"
            >
              {batchMutation.isPending ? (
                <>
                  <RefreshCcw className="h-4 w-4 animate-spin" />
                  Programlanıyor...
                </>
              ) : (
                <>
                  <Zap className="h-4 w-4" />
                  Toplu Programla
                </>
              )}
            </Button>
          </div>

          <p className="text-sm text-muted-foreground">
            İşlem, aktif dönemdeki programsız ve öğretim üyesi atanmış section&apos;ları yoğunluktan başlayarak öneri motoruyla planlar.
          </p>
        </CardContent>
      </Card>

      {batchErrorMessage && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Toplu programlama başarısız</AlertTitle>
          <AlertDescription>{batchErrorMessage}</AlertDescription>
        </Alert>
      )}

      <DepartmentTimetablePanel
        departmentId={effectiveDepartmentId}
        canViewAllDepartments={canViewAllDepartments}
        isLoading={classListsQuery.isLoading}
        errorMessage={classListErrorMessage}
        classLists={classLists}
        timetableEntries={timetableEntries}
        scheduledSectionCount={scheduledSectionCount}
        onRetry={() => classListsQuery.refetch()}
      />

      {batchMutation.isPending && (
        <div className="space-y-4">
          <Skeleton className="h-24 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      )}

      {batchMutation.data && (
        <BatchResultView result={batchMutation.data} />
      )}
    </div>
  );
}

function DepartmentTimetablePanel({
  departmentId,
  canViewAllDepartments,
  isLoading,
  errorMessage,
  classLists,
  timetableEntries,
  scheduledSectionCount,
  onRetry,
}: {
  departmentId: number | null;
  canViewAllDepartments: boolean;
  isLoading: boolean;
  errorMessage: string | null;
  classLists: DepartmentClassList | null;
  timetableEntries: TimetableEntry[];
  scheduledSectionCount: number;
  onRetry: () => void;
}) {
  if (departmentId === null) {
    return (
      <Alert variant="info">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Bölüm seçimi bekleniyor</AlertTitle>
        <AlertDescription>
          {canViewAllDepartments
            ? 'Departman bazlı haftalık görünüm için lütfen bir bölüm seçin.'
            : 'Bu kullanıcı için görüntülenecek bölüm bulunamadı.'}
        </AlertDescription>
      </Alert>
    );
  }

  if (isLoading) {
    return <Skeleton className="h-[520px] rounded-xl" />;
  }

  if (errorMessage) {
    return (
      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>Bölüm programı alınamadı</AlertTitle>
        <AlertDescription className="space-y-4">
          <p>{errorMessage}</p>
          <Button variant="outline" onClick={onRetry}>
            <RefreshCcw className="h-4 w-4" />
            Yeniden Dene
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  if (!classLists) {
    return null;
  }

  return (
    <div className="space-y-4">
      {classLists.notes.length > 0 && (
        <NotesAlert title="Bölüm programı notları" notes={classLists.notes} />
      )}

      <div className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          label="Toplam Section"
          value={classLists.totalSections}
          icon={<Calendar className="h-5 w-5 text-muted-foreground" />}
        />
        <SummaryCard
          label="Programlı Section"
          value={scheduledSectionCount}
          icon={<Building2 className="h-5 w-5 text-green-600" />}
          variant="success"
        />
        <SummaryCard
          label="Toplam Öğrenci"
          value={classLists.totalStudents}
          icon={<Users className="h-5 w-5 text-muted-foreground" />}
        />
      </div>

      <WeeklyTimetableGrid
        entries={timetableEntries}
        title={`${classLists.departmentName} — Haftalık Ders Programı`}
      />
    </div>
  );
}

function BatchResultView({
  result,
}: {
  result: {
    totalSections: number;
    scheduledCount: number;
    failedCount: number;
    skippedCount: number;
    results: BatchAutoScheduleItem[];
    notes: string[];
  };
}) {
  const successfulEntries: TimetableEntry[] = result.results
    .filter(item => item.success && item.slots && item.slots.length > 0)
    .map(item => ({
      id: String(item.sectionId),
      label: item.courseName,
      courseCode: item.courseCode,
      classroomName: item.classroomName ?? undefined,
      slots: item.slots ?? [],
    }));

  return (
    <div className="space-y-4">
      {result.notes.length > 0 && (
        <NotesAlert title="Toplu programlama notları" notes={result.notes} />
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard
          label="Toplam Aday"
          value={result.totalSections}
          icon={<Calendar className="h-5 w-5 text-muted-foreground" />}
        />
        <SummaryCard
          label="Başarılı"
          value={result.scheduledCount}
          icon={<CheckCircle2 className="h-5 w-5 text-green-600" />}
          variant="success"
        />
        <SummaryCard
          label="Başarısız"
          value={result.failedCount}
          icon={<XCircle className="h-5 w-5 text-red-600" />}
          variant="destructive"
        />
        <SummaryCard
          label="Atlanan"
          value={result.skippedCount}
          icon={<AlertCircle className="h-5 w-5 text-amber-600" />}
          variant="warning"
        />
      </div>

      {result.totalSections === 0 ? (
        <Alert variant="info">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Programsız section bulunamadı</AlertTitle>
          <AlertDescription>Seçilen kapsamda işlenecek ek bir section yok.</AlertDescription>
        </Alert>
      ) : (
        <Alert variant={result.failedCount === 0 ? 'success' : result.scheduledCount > 0 ? 'warning' : 'destructive'}>
          {result.failedCount === 0 ? (
            <CheckCircle2 className="h-4 w-4" />
          ) : (
            <AlertCircle className="h-4 w-4" />
          )}
          <AlertTitle>
            {result.failedCount === 0
              ? 'Toplu programlama tamamlandı'
              : `${result.scheduledCount}/${result.totalSections} section başarıyla programlandı`}
          </AlertTitle>
          <AlertDescription>
            {result.failedCount > 0
              ? `${result.failedCount} section için uygun kombinasyon bulunamadı veya uygulama aşamasında doğrulama hatası oluştu.`
              : 'İşlenen tüm sectionlar başarıyla programa alındı.'}
          </AlertDescription>
        </Alert>
      )}

      {result.results.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Sonuç Detayları</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Durum</TableHead>
                  <TableHead>Ders Kodu</TableHead>
                  <TableHead>Ders Adı</TableHead>
                  <TableHead>Derslik</TableHead>
                  <TableHead>Program</TableHead>
                  <TableHead>Açıklama</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {result.results.map(item => (
                  <TableRow key={item.sectionId}>
                    <TableCell>
                      <Badge variant={item.success ? 'success' : 'destructive'}>
                        {item.success ? 'Başarılı' : 'Başarısız'}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-medium">{item.courseCode}</TableCell>
                    <TableCell>{item.courseName}</TableCell>
                    <TableCell>{item.classroomName ?? '—'}</TableCell>
                    <TableCell>
                      {item.slots && item.slots.length > 0 ? (
                        <div className="flex flex-wrap gap-1">
                          {item.slots.map((slot, index) => (
                            <Badge key={`${item.sectionId}-${index}`} variant="outline" className="text-xs">
                              {formatSlot(slot)}
                            </Badge>
                          ))}
                        </div>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell className="max-w-[320px] text-sm text-muted-foreground">
                      {item.failureReason ?? '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {successfulEntries.length > 0 && (
        <WeeklyTimetableGrid
          entries={successfulEntries}
          title="Bu İşlemde Programlanan Section'lar"
        />
      )}
    </div>
  );
}

function NotesAlert({ title, notes }: { title: string; notes: string[] }) {
  return (
    <Alert variant="info">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>{title}</AlertTitle>
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

function SummaryCard({
  label,
  value,
  icon,
  variant,
}: {
  label: string;
  value: number;
  icon: ReactNode;
  variant?: 'success' | 'destructive' | 'warning';
}) {
  return (
    <Card>
      <CardContent className="flex items-center gap-4 pt-6">
        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-muted">
          {icon}
        </div>
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className={`text-2xl font-bold ${
            variant === 'success'
              ? 'text-green-600'
              : variant === 'destructive'
                ? 'text-red-600'
                : variant === 'warning'
                  ? 'text-amber-600'
                  : 'text-foreground'
          }`}>
            {value}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

function buildDepartmentTimetableEntries(
  classLists: DepartmentClassList,
  conflictSectionIds: Set<number>,
): TimetableEntry[] {
  const entries: TimetableEntry[] = [];

  for (const section of classLists.sections) {
    const slots = parseScheduleJson(section.scheduleJson);
    if (slots.length === 0) {
      continue;
    }

    entries.push({
      id: String(section.sectionId),
      label: section.courseName,
      courseCode: section.courseCode,
      classroomName: section.classroomName || undefined,
      instructorName: section.instructorName || undefined,
      slots,
      hasConflict: conflictSectionIds.has(section.sectionId),
    });
  }

  return entries;
}

function collectConflictSectionIds(conflicts: SchedulingConflict[]) {
  const sectionIds = new Set<number>();

  for (const conflict of conflicts) {
    if (typeof conflict.sectionId === 'number' && conflict.sectionId > 0) {
      sectionIds.add(conflict.sectionId);
    }

    if (typeof conflict.relatedSectionId === 'number' && conflict.relatedSectionId > 0) {
      sectionIds.add(conflict.relatedSectionId);
    }
  }

  return sectionIds;
}

function formatSlot(slot: ScheduleSlot) {
  const day = DAY_LABELS[slot.day] ?? `G${slot.day}`;
  const start = typeof slot.startTime === 'string' ? slot.startTime.slice(0, 5) : '';
  const end = typeof slot.endTime === 'string' ? slot.endTime.slice(0, 5) : '';
  return `${day} ${start}-${end}`;
}
