import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useAcademicOperationsContext } from '../hooks/useAcademicOperations';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { useCourseIntentionSummary, useCourseIntentionSurveys, useSetCourseIntentionSurveyState, useUpsertCourseIntentionSurvey } from '../hooks/useCourseIntention';
import { getApiErrorMessage } from '../lib/apiError';
import type { CourseIntentionSurveyStatus } from '../types/courseIntention';
import { BarChart3, CalendarClock, CheckCircle2, Loader2, SlidersHorizontal } from 'lucide-react';

export default function CourseIntentionManagementPage() {
  const { data: activeTerm } = useActiveTerm();
  const contextQuery = useAcademicOperationsContext();
  const surveysQuery = useCourseIntentionSurveys(activeTerm?.id);
  const createSurvey = useUpsertCourseIntentionSurvey();
  const setSurveyState = useSetCourseIntentionSurveyState();

  const [selectedSurveyId, setSelectedSurveyId] = useState<number | null>(null);
  const [selectedDepartmentId, setSelectedDepartmentId] = useState<number | null>(null);
  const [title, setTitle] = useState('');
  const [openAt, setOpenAt] = useState('');
  const [closeAt, setCloseAt] = useState('');
  const [feedback, setFeedback] = useState<string | null>(null);

  const departments = contextQuery.data?.accessibleDepartments ?? [];
  const selectedSurvey = useMemo(
    () => (surveysQuery.data ?? []).find(item => item.id === selectedSurveyId) ?? null,
    [selectedSurveyId, surveysQuery.data]
  );

  useEffect(() => {
    if (!selectedDepartmentId && departments.length) {
      setSelectedDepartmentId(departments[0].departmentId);
    }
  }, [departments, selectedDepartmentId]);

  useEffect(() => {
    if (surveysQuery.data?.length && !selectedSurveyId) {
      setSelectedSurveyId(surveysQuery.data[0].id);
    }
  }, [selectedSurveyId, surveysQuery.data]);

  useEffect(() => {
    if (!selectedSurvey) {
      setTitle(activeTerm ? `${activeTerm.name} Niyet Anketi` : '');
      setOpenAt('');
      setCloseAt('');
      return;
    }

    setTitle(selectedSurvey.title);
    setOpenAt(toDateTimeLocalValue(selectedSurvey.openAtUtc));
    setCloseAt(toDateTimeLocalValue(selectedSurvey.closeAtUtc));
  }, [activeTerm, selectedSurvey]);

  const summaryQuery = useCourseIntentionSummary(selectedSurveyId, selectedDepartmentId);

  const handleSaveSurvey = async () => {
    if (!activeTerm || !openAt || !closeAt) return;

    setFeedback(null);
    try {
      const result = await createSurvey.mutateAsync({
        termId: activeTerm.id,
        title: title.trim() || undefined,
        openAtUtc: new Date(openAt).toISOString(),
        closeAtUtc: new Date(closeAt).toISOString(),
      });
      setSelectedSurveyId(result.id);
      setFeedback('Niyet anketi ayarlari kaydedildi.');
    } catch (error) {
      setFeedback(getApiErrorMessage(error, 'Anket kaydedilemedi.'));
    }
  };

  const handleSetState = async (surveyId: number, status: CourseIntentionSurveyStatus) => {
    setFeedback(null);
    try {
      setSelectedSurveyId(surveyId);
      await setSurveyState.mutateAsync({
        surveyId,
        request: {
          status,
          isManualOverride: true,
        },
      });
      setFeedback(`Anket durumu ${status} olarak guncellendi.`);
    } catch (error) {
      setFeedback(getApiErrorMessage(error, 'Anket durumu guncellenemedi.'));
    }
  };

  return (
    <div className="space-y-6">
      <Card className="overflow-hidden border-none shadow-sm">
        <div className="bg-[#003366] px-6 py-6 text-white">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <div className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.08em]">
                <CalendarClock className="h-3.5 w-3.5" />
                Yonetim
              </div>
              <h1 className="mt-3 font-heading text-2xl font-extrabold tracking-tight">Niyet anketi yonetimi</h1>
              <p className="mt-1 max-w-3xl text-sm text-white/75">
                Survey takvimini ac, kapat ve ozet talepleri ham veri olarak bolum bazinda incele.
              </p>
            </div>

            {activeTerm && (
              <Badge variant="info" className="w-fit border-white/20 bg-white/10 text-white">
                Aktif donem: {activeTerm.name}
              </Badge>
            )}
          </div>
        </div>
      </Card>

      <Tabs defaultValue="manage" className="space-y-4">
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="manage">Anket Ayarlari</TabsTrigger>
          <TabsTrigger value="summary">Talep Ozeti</TabsTrigger>
        </TabsList>

        <TabsContent value="manage" className="space-y-4">
          <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle>Anket zamanlamasi</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {!activeTerm ? (
                  <Alert>
                    <AlertTitle>Aktif donem bulunamadi</AlertTitle>
                    <AlertDescription>Survey olusturmak icin once aktif bir donem tanimlanmali.</AlertDescription>
                  </Alert>
                ) : (
                  <>
                    <div className="space-y-2">
                      <p className="text-sm font-medium text-foreground">Baslik</p>
                      <Input value={title} onChange={event => setTitle(event.target.value)} placeholder={`${activeTerm.name} Niyet Anketi`} />
                    </div>

                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="space-y-2">
                        <p className="text-sm font-medium text-foreground">Acilis</p>
                        <Input type="datetime-local" value={openAt} onChange={event => setOpenAt(event.target.value)} />
                      </div>
                      <div className="space-y-2">
                        <p className="text-sm font-medium text-foreground">Kapanis</p>
                        <Input type="datetime-local" value={closeAt} onChange={event => setCloseAt(event.target.value)} />
                      </div>
                    </div>

                    <Button
                      onClick={handleSaveSurvey}
                      disabled={createSurvey.isPending || !openAt || !closeAt}
                      className="w-full gap-2 bg-[#003366] hover:bg-[#002244]"
                    >
                      {createSurvey.isPending ? (
                        <>
                          <Loader2 className="h-4 w-4 animate-spin" />
                          Kaydediliyor...
                        </>
                      ) : (
                        <>
                          <CheckCircle2 className="h-4 w-4" />
                          Anketi Kaydet
                        </>
                      )}
                    </Button>

                    {feedback && (
                      <Alert variant={feedback.toLocaleLowerCase('tr').includes('kaydedildi') || feedback.toLocaleLowerCase('tr').includes('guncellendi') ? 'success' : 'destructive'}>
                        <AlertDescription>{feedback}</AlertDescription>
                      </Alert>
                    )}
                  </>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-3">
                <CardTitle>Mevcut anketler</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {surveysQuery.isLoading ? (
                  <Skeleton className="h-72 rounded-xl" />
                ) : surveysQuery.isError ? (
                  <Alert variant="destructive">
                    <AlertDescription>{getApiErrorMessage(surveysQuery.error, 'Anket listesi getirilemedi.')}</AlertDescription>
                  </Alert>
                ) : !(surveysQuery.data ?? []).length ? (
                  <Alert>
                    <AlertDescription>Aktif donem icin anket tanimli degil. Yukaridaki formdan ilk surveyi olusturabilirsin.</AlertDescription>
                  </Alert>
                ) : (
                  (surveysQuery.data ?? []).map(item => (
                    <div key={item.id} className="rounded-2xl border border-border p-4">
                      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                        <div className="space-y-2">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-semibold text-foreground">{item.title}</span>
                            <Badge variant={item.status === 'Open' ? 'success' : item.status === 'Scheduled' ? 'warning' : 'secondary'}>
                              {statusLabel(item.status)}
                            </Badge>
                            {item.isManualOverride && <Badge variant="outline">Manual Override</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">{item.termName}</p>
                          <p className="text-sm text-muted-foreground">
                            {formatDateTime(item.openAtUtc)} - {formatDateTime(item.closeAtUtc)}
                          </p>
                        </div>

                        <div className="flex flex-wrap gap-2">
                          <Button variant="outline" size="sm" onClick={() => setSelectedSurveyId(item.id)}>
                            Sec
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => handleSetState(item.id, 'Scheduled')} disabled={setSurveyState.isPending}>
                            Planla
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => handleSetState(item.id, 'Open')} disabled={setSurveyState.isPending}>
                            Ac
                          </Button>
                          <Button variant="outline" size="sm" onClick={() => handleSetState(item.id, 'Closed')} disabled={setSurveyState.isPending}>
                            Kapat
                          </Button>
                        </div>
                      </div>
                    </div>
                  ))
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="summary" className="space-y-4">
          <Card>
            <CardHeader className="pb-3">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <CardTitle>Ham talep ozeti</CardTitle>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Forecast ile birlestirilmeden yalnizca survey yanitlarinin toplamini gosterir.
                  </p>
                </div>

                <div className="flex flex-wrap gap-3">
                  <select
                    value={selectedSurveyId ?? ''}
                    onChange={event => setSelectedSurveyId(event.target.value ? Number(event.target.value) : null)}
                    className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
                  >
                    <option value="">Anket sec</option>
                    {(surveysQuery.data ?? []).map(item => (
                      <option key={item.id} value={item.id}>{item.title}</option>
                    ))}
                  </select>

                  <select
                    value={selectedDepartmentId ?? ''}
                    onChange={event => setSelectedDepartmentId(event.target.value ? Number(event.target.value) : null)}
                    className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
                  >
                    <option value="">Tum bolumler</option>
                    {departments.map(department => (
                      <option key={department.departmentId} value={department.departmentId}>{department.departmentName}</option>
                    ))}
                  </select>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {contextQuery.isLoading ? (
                <Skeleton className="h-14 rounded-xl" />
              ) : summaryQuery.isLoading ? (
                <Skeleton className="h-96 rounded-xl" />
              ) : summaryQuery.isError ? (
                <Alert variant="destructive">
                  <AlertDescription>{getApiErrorMessage(summaryQuery.error, 'Anket ozeti getirilemedi.')}</AlertDescription>
                </Alert>
              ) : !summaryQuery.data ? (
                <Alert>
                  <SlidersHorizontal className="h-4 w-4" />
                  <AlertTitle>Ozet hazir degil</AlertTitle>
                  <AlertDescription>Bir anket secildiginde bu alanda ders bazli toplam niyet verisi listelenecek.</AlertDescription>
                </Alert>
              ) : (
                <>
                  <div className="grid gap-4 md:grid-cols-3">
                    <SummaryCard label="Yanitlayan ogrenci" value={`${summaryQuery.data.totalRespondingStudents}`} />
                    <SummaryCard label="Ders sayisi" value={`${summaryQuery.data.courses.length}`} />
                    <SummaryCard label="Donem" value={summaryQuery.data.termName} />
                  </div>

                  <div className="rounded-2xl border border-border">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Ders</TableHead>
                          <TableHead>Kesin</TableHead>
                          <TableHead>Belki</TableHead>
                          <TableHead>Gun Tercihleri</TableHead>
                          <TableHead>Saat Tercihleri</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {summaryQuery.data.courses.map(item => (
                          <TableRow key={item.courseId}>
                            <TableCell>
                              <div className="space-y-1">
                                <p className="font-mono text-xs font-semibold text-primary">{item.courseCode}</p>
                                <p className="font-medium text-foreground">{item.courseName}</p>
                              </div>
                            </TableCell>
                            <TableCell>{item.certainCount}</TableCell>
                            <TableCell>{item.maybeCount}</TableCell>
                            <TableCell>{item.preferredDays.join(', ') || '—'}</TableCell>
                            <TableCell>{item.preferredTimeRanges.join(', ') || '—'}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function SummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border bg-muted/20 p-4">
      <div className="flex items-center gap-2">
        <BarChart3 className="h-4 w-4 text-primary" />
        <p className="text-sm font-medium text-muted-foreground">{label}</p>
      </div>
      <p className="mt-3 text-xl font-extrabold text-foreground">{value}</p>
    </div>
  );
}

function statusLabel(status: CourseIntentionSurveyStatus) {
  switch (status) {
    case 'Scheduled':
      return 'Planli';
    case 'Open':
      return 'Acik';
    case 'Closed':
      return 'Kapali';
    default:
      return status;
  }
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function toDateTimeLocalValue(value: string) {
  const date = new Date(value);
  const offset = date.getTimezoneOffset();
  const local = new Date(date.getTime() - offset * 60 * 1000);
  return local.toISOString().slice(0, 16);
}
