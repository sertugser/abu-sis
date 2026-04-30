import { useEffect, useMemo, useState, type KeyboardEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  AlertTriangle,
  CalendarDays,
  CheckCircle2,
  ClipboardList,
  Clock,
  Copy,
  Download,
  FileCheck2,
  Pencil,
  QrCode,
  Save,
  Search,
  Timer,
  Users,
  X,
  XCircle,
} from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useInstructorSections } from '../hooks/useInstructorSections';
import {
  useAttendanceMatrix,
  useAttendanceSession,
  useCloseQrAttendance,
  useCreateAttendanceSession,
  useOpenQrAttendance,
  useSaveAttendanceRecords,
} from '../hooks/useAttendance';
import { downloadAttendanceQrPdf } from '../lib/attendanceQrPdf';
import { getApiErrorMessage } from '../lib/apiError';
import { createQrCodeMatrix, getQrCodeSize } from '../lib/qrCode';
import type {
  AttendanceEntrySource,
  AttendanceMatrixStudent,
  AttendanceMatrixWeek,
  AttendanceSession,
  AttendanceStatus,
} from '../api/attendance.api';

const STATUS_LABELS: Record<AttendanceStatus, string> = {
  Present: 'Var',
  Absent: 'Yok',
  Late: 'Geç',
  Excused: 'Mazeretli',
};

const STATUS_STYLES: Record<AttendanceStatus, string> = {
  Present: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  Absent: 'bg-red-50 text-red-700 border-red-200',
  Late: 'bg-amber-50 text-amber-700 border-amber-200',
  Excused: 'bg-sky-50 text-sky-700 border-sky-200',
};

const FALLBACK_WEEK_NUMBERS = Array.from({ length: 14 }, (_, index) => index + 1);

type EditableStatus = AttendanceStatus | '';

export default function AttendanceEntryPage() {
  const [searchParams] = useSearchParams();
  const initialSectionId = Number(searchParams.get('sectionId')) || null;
  const { data: sectionsData, isLoading: sectionsLoading } = useInstructorSections();
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(initialSectionId);
  const [selectedWeekNumber, setSelectedWeekNumber] = useState(1);
  const [manualWeekSelection, setManualWeekSelection] = useState(false);
  const [activePage, setActivePage] = useState<'entry' | 'history'>('entry');
  const [sessionDate, setSessionDate] = useState(formatDateInput(new Date()));
  const [topic, setTopic] = useState('');
  const [sessionId, setSessionId] = useState<number | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [historySearchTerm, setHistorySearchTerm] = useState('');
  const [historyWeekNumber, setHistoryWeekNumber] = useState<number | null>(null);
  const [statuses, setStatuses] = useState<Record<number, EditableStatus>>({});
  const [dirtyStudentIds, setDirtyStudentIds] = useState<Set<number>>(new Set());
  const [qrDurationHours, setQrDurationHours] = useState('0');
  const [qrDurationMinutes, setQrDurationMinutes] = useState('10');
  const [copied, setCopied] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  const { data: matrix, isLoading: matrixLoading } = useAttendanceMatrix(selectedSectionId);
  const { data: session } = useAttendanceSession(sessionId);
  const createSessionMut = useCreateAttendanceSession();
  const saveRecordsMut = useSaveAttendanceRecords(sessionId);
  const openQrMut = useOpenQrAttendance(sessionId);
  const closeQrMut = useCloseQrAttendance(sessionId);

  const selectedSection = sectionsData?.sections.find(section => section.sectionId === selectedSectionId) ?? null;
  const selectedMatrixWeek = matrix?.weeks.find(week => week.weekNumber === selectedWeekNumber) ?? null;
  const currentWeekNumber = matrix?.currentWeekNumber ?? null;
  const selectedWeekDisplayDate = selectedMatrixWeek?.sessionDate ?? selectedMatrixWeek?.suggestedSessionDate ?? null;
  const weekNumbers = matrix?.weeks.length
    ? matrix.weeks.map(week => week.weekNumber)
    : FALLBACK_WEEK_NUMBERS;
  const qrUrl = session?.token ? `${window.location.origin}/yoklama/qr/${session.token}` : '';
  const remainingMs = session?.qrExpiresAt ? Math.max(0, new Date(session.qrExpiresAt).getTime() - now) : 0;
  const isQrActive = Boolean(session?.isQrCurrentlyActive && remainingMs > 0);

  const sessionStudentMap = useMemo(
    () => new Map((session?.students ?? []).map(student => [student.studentId, student])),
    [session?.students]
  );

  const filteredStudents = useMemo(() => {
    const students = matrix?.students ?? [];
    const query = searchTerm.trim().toLowerCase();
    if (!query) return students;

    return students.filter(student =>
      `${student.studentNumber} ${student.firstName} ${student.lastName} ${student.email}`
        .toLowerCase()
        .includes(query)
    );
  }, [matrix?.students, searchTerm]);

  const historyWeeks = useMemo(
    () => (matrix?.weeks ?? [])
      .filter(week => week.sessionId)
      .sort((a, b) => b.weekNumber - a.weekNumber),
    [matrix?.weeks]
  );
  const selectedHistoryWeek = historyWeeks.find(week => week.weekNumber === historyWeekNumber)
    ?? historyWeeks[0]
    ?? null;



  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!matrix?.weeks.length) return;

    const preferredWeekNumber = !manualWeekSelection && matrix.currentWeekNumber
      ? matrix.currentWeekNumber
      : selectedWeekNumber;

    if (matrix.weeks.some(week => week.weekNumber === preferredWeekNumber)) {
      if (preferredWeekNumber !== selectedWeekNumber) {
        setSelectedWeekNumber(preferredWeekNumber);
      }
      return;
    }

    setSelectedWeekNumber(matrix.currentWeekNumber ?? matrix.weeks[0].weekNumber);
  }, [manualWeekSelection, matrix?.currentWeekNumber, matrix?.weeks, selectedWeekNumber]);

  useEffect(() => {
    if (!selectedSectionId) {
      setSessionId(null);
      setStatuses({});
      setDirtyStudentIds(new Set());
      return;
    }

    setSessionId(selectedMatrixWeek?.sessionId ?? null);
    setStatuses({});
    setDirtyStudentIds(new Set());
    setTopic(selectedMatrixWeek?.topic ?? '');
    setSessionDate(getDefaultWeekDateInput(selectedMatrixWeek));
  }, [
    selectedSectionId,
    selectedWeekNumber,
    selectedMatrixWeek?.sessionId,
    selectedMatrixWeek?.sessionDate,
    selectedMatrixWeek?.suggestedSessionDate,
    selectedMatrixWeek?.topic,
  ]);

  useEffect(() => {
    if (!session) return;

    setTopic(session.topic);
    setSessionDate(formatDateInput(new Date(session.sessionDate)));
    setStatuses(previous => {
      const next: Record<number, EditableStatus> = {};
      session.students.forEach(student => {
        next[student.studentId] = dirtyStudentIds.has(student.studentId)
          ? previous[student.studentId] ?? student.status ?? ''
          : student.status ?? '';
      });
      return next;
    });
  }, [session, dirtyStudentIds]);

  if (sectionsLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-96 rounded-xl" />
      </div>
    );
  }

  const handleSectionChange = (value: string) => {
    setSelectedSectionId(Number(value));
    setManualWeekSelection(false);
  };

  const handleWeekChange = (value: string) => {
    setSelectedWeekNumber(Number(value));
    setManualWeekSelection(true);
  };

  const handleOpenSession = () => {
    if (!selectedSectionId) return;

    createSessionMut.mutate(
      {
        courseSectionId: selectedSectionId,
        weekNumber: selectedWeekNumber,
        sessionDate: `${sessionDate}T00:00:00.000Z`,
        topic,
      },
      {
        onSuccess: data => {
          setSessionId(data.id);
          setDirtyStudentIds(new Set());
        },
        onError: error => alert(getApiErrorMessage(error, 'Yoklama oturumu açılamadı.')),
      }
    );
  };

  const setStudentStatus = (studentId: number, status: AttendanceStatus) => {
    setStatuses(previous => ({ ...previous, [studentId]: status }));
    setDirtyStudentIds(previous => {
      const next = new Set(previous);
      next.add(studentId);
      return next;
    });
  };

  const markAll = (status: AttendanceStatus) => {
    if (!session) return;

    setStatuses(Object.fromEntries(session.students.map(student => [student.studentId, status])));
    setDirtyStudentIds(new Set(session.students.map(student => student.studentId)));
  };

  const handleSave = () => {
    if (!session) return;

    const records = session.students
      .map(student => ({ studentId: student.studentId, status: statuses[student.studentId] }))
      .filter((record): record is { studentId: number; status: AttendanceStatus } => Boolean(record.status));

    if (records.length === 0) {
      alert('Kaydedilecek yoklama durumu seçilmedi.');
      return;
    }

    saveRecordsMut.mutate(
      { records },
      {
        onSuccess: () => setDirtyStudentIds(new Set()),
        onError: () => alert('Yoklama kaydedilemedi.'),
      }
    );
  };

  const handleOpenQr = () => {
    if (!session) return;
    const minutes = normalizeQrDuration(qrDurationHours, qrDurationMinutes);
    const normalized = splitQrDuration(minutes);
    setQrDurationHours(String(normalized.hours));
    setQrDurationMinutes(String(normalized.minutes));

    openQrMut.mutate(
      { durationMinutes: minutes },
      { onError: error => alert(getApiErrorMessage(error, 'QR yoklama açılamadı.')) }
    );
  };

  const handleQrDurationKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter') return;

    event.preventDefault();
    handleOpenQr();
  };

  const handleCopy = async () => {
    if (!qrUrl || !navigator.clipboard) return;
    await navigator.clipboard.writeText(qrUrl);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  const handleDownloadQrPdf = () => {
    if (!session || !qrUrl) return;

    try {
      downloadAttendanceQrPdf(session, qrUrl);
    } catch {
      alert('QR PDF oluşturulamadı.');
    }
  };

  return (
    <Tabs
      value={activePage}
      onValueChange={value => setActivePage(value as 'entry' | 'history')}
      className="flex flex-col gap-4"
    >
      <Card className="border-border shadow-sm">
        <CardContent className="p-4">
          <div className="flex flex-col gap-4 xl:flex-row xl:items-end">
            <div className="grid flex-1 gap-3 md:grid-cols-[1.4fr_130px_160px_1fr]">
              <div className="space-y-2">
                <Label>Ders / Şube</Label>
                <Select
                  value={selectedSectionId ? String(selectedSectionId) : ''}
                  onValueChange={handleSectionChange}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Ders ve şube seç" />
                  </SelectTrigger>
                  <SelectContent>
                    {(sectionsData?.sections ?? []).map(section => (
                      <SelectItem key={section.sectionId} value={String(section.sectionId)}>
                        {section.courseCode} - Şube {section.sectionNumber ?? section.sectionId} - {section.courseName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Hafta</Label>
                <Select
                  value={String(selectedWeekNumber)}
                  onValueChange={handleWeekChange}
                  disabled={!selectedSectionId}
                >
                  <SelectTrigger>
                    <WeekSelectTriggerLabel
                      weekNumber={selectedWeekNumber}
                      week={selectedMatrixWeek}
                      isCurrent={selectedWeekNumber === currentWeekNumber}
                    />
                  </SelectTrigger>
                  <SelectContent className="min-w-64">
                    {weekNumbers.map(weekNumber => (
                      <SelectItem key={weekNumber} value={String(weekNumber)} className="py-2">
                        <WeekSelectOption
                          weekNumber={weekNumber}
                          week={matrix?.weeks.find(week => week.weekNumber === weekNumber)}
                          isSelected={weekNumber === selectedWeekNumber}
                          isCurrent={weekNumber === currentWeekNumber}
                        />
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Tarih</Label>
                <Input
                  type="date"
                  value={sessionDate}
                  onChange={event => setSessionDate(event.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label>Konu</Label>
                <Input
                  value={topic}
                  onChange={event => setTopic(event.target.value)}
                  maxLength={200}
                  placeholder="Ders konusu"
                />
              </div>
            </div>

            <Button
              type="button"
              onClick={handleOpenSession}
              disabled={!selectedSectionId || createSessionMut.isPending}
              className="gap-2 xl:w-auto"
            >
              <FileCheck2 className="h-4 w-4" />
              {createSessionMut.isPending
                ? 'Açılıyor...'
                : selectedMatrixWeek?.sessionId
                  ? 'Haftayı Güncelle'
                  : 'Yoklamayı Aç'}
            </Button>
          </div>

          {selectedSection && (
            <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
              <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                <span className="font-medium text-foreground">{selectedSection.courseName}</span>
                <Badge variant="outline">{selectedSection.activeStudents} öğrenci</Badge>
              </div>
              <TabsList>
                <TabsTrigger value="entry" className="gap-2">
                  <FileCheck2 className="h-4 w-4" />
                  Yoklama Girişi
                </TabsTrigger>
                <TabsTrigger value="history" className="gap-2">
                  <ClipboardList className="h-4 w-4" />
                  Geçmiş Yoklamalar
                </TabsTrigger>
              </TabsList>
            </div>
          )}
        </CardContent>
      </Card>

      {!selectedSection && (
        <Alert>
          <Clock className="h-4 w-4" />
          <AlertDescription>Yoklama tablosu için ders ve şube seçin.</AlertDescription>
        </Alert>
      )}

      {selectedSection && (
        <>
          <TabsContent value="entry" className="mt-0 space-y-4">
            {!session && !matrixLoading && (
              <Alert>
                <Clock className="h-4 w-4" />
                <AlertDescription>{selectedWeekNumber}. hafta için yoklama oturumu açılmadı.</AlertDescription>
              </Alert>
            )}

            <Tabs defaultValue="manual" className="space-y-3">
              <TabsList>
                <TabsTrigger value="manual">Manuel Yoklama</TabsTrigger>
                <TabsTrigger value="qr">QR Yoklama</TabsTrigger>
              </TabsList>

              <TabsContent value="manual" className="mt-0">
                <Card>
                  <CardContent className="p-0">
                    <div className="flex flex-col gap-3 border-b bg-muted/20 p-4 xl:flex-row xl:items-center xl:justify-between">
                      <div className="relative w-full xl:w-80">
                        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                        <Input
                          value={searchTerm}
                          onChange={event => setSearchTerm(event.target.value)}
                          placeholder="Öğrenci ara..."
                          className="pl-9"
                        />
                      </div>

                      <div className="flex flex-wrap items-center gap-2">
                        {selectedWeekDisplayDate && (
                          <Badge variant="outline">{formatDisplayDate(selectedWeekDisplayDate)}</Badge>
                        )}
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => markAll('Present')}
                          disabled={!session}
                        >
                          Tümünü Var
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => markAll('Absent')}
                          disabled={!session}
                        >
                          Tümünü Yok
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          onClick={handleSave}
                          disabled={!session || saveRecordsMut.isPending}
                          className="gap-2"
                        >
                          <Save className="h-4 w-4" />
                          {saveRecordsMut.isPending ? 'Kaydediliyor...' : 'Kaydet'}
                        </Button>
                      </div>
                    </div>

                    {matrixLoading ? (
                      <div className="space-y-3 p-4">
                        <Skeleton className="h-10 w-full" />
                        <Skeleton className="h-10 w-full" />
                        <Skeleton className="h-10 w-full" />
                      </div>
                    ) : (
                      <div className="overflow-x-auto">
                        <table className="w-full min-w-[1180px] text-sm">
                          <thead>
                            <tr className="border-b bg-muted/30">
                              <th className="sticky left-0 z-20 min-w-72 bg-muted/30 px-4 py-3 text-left font-medium text-muted-foreground">
                                Öğrenci
                              </th>
                              {weekNumbers.map(weekNumber => (
                                <th
                                  key={weekNumber}
                                  className={`min-w-32 px-3 py-3 text-center font-medium text-muted-foreground ${
                                    weekNumber === selectedWeekNumber ? 'bg-sky-50 text-sky-800' : ''
                                  }`}
                                >
                                  <div className="flex flex-col items-center gap-1">
                                    <span>{weekNumber}. Hafta</span>
                                    <div className="flex flex-wrap justify-center gap-1">
                                      {weekNumber === currentWeekNumber && (
                                        <span className="rounded border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-700">
                                          Güncel
                                        </span>
                                      )}
                                      <WeekStateMini week={matrix?.weeks.find(week => week.weekNumber === weekNumber)} />
                                    </div>
                                  </div>
                                </th>
                              ))}
                            </tr>
                          </thead>
                          <tbody>
                            {filteredStudents.map(student => (
                              <AttendanceMatrixRow
                                key={student.studentId}
                                student={student}
                                weekNumbers={weekNumbers}
                                selectedWeekNumber={selectedWeekNumber}
                                session={session}
                                sessionStudentMap={sessionStudentMap}
                                statuses={statuses}
                                onStatusChange={setStudentStatus}
                              />
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}

                    {!matrixLoading && filteredStudents.length === 0 && (
                      <div className="py-12 text-center text-sm text-muted-foreground">Eşleşen öğrenci bulunamadı.</div>
                    )}
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="qr" className="mt-0">
                {session ? (
                  <QrPanel
                    session={session}
                    qrUrl={qrUrl}
                    durationHours={qrDurationHours}
                    setDurationHours={setQrDurationHours}
                    durationMinutes={qrDurationMinutes}
                    setDurationMinutes={setQrDurationMinutes}
                    isQrActive={isQrActive}
                    remainingMs={remainingMs}
                    copied={copied}
                    onOpenQr={handleOpenQr}
                    onDurationKeyDown={handleQrDurationKeyDown}
                    onCloseQr={() =>
                      closeQrMut.mutate(undefined, {
                        onError: error => alert(getApiErrorMessage(error, 'QR kapatılamadı.')),
                      })
                    }
                    onCopy={handleCopy}
                    onDownloadPdf={handleDownloadQrPdf}
                    isOpening={openQrMut.isPending}
                    isClosing={closeQrMut.isPending}
                  />
                ) : (
                  <Alert>
                    <Clock className="h-4 w-4" />
                    <AlertDescription>{selectedWeekNumber}. hafta için yoklama oturumu açılmadı.</AlertDescription>
                  </Alert>
                )}
              </TabsContent>
            </Tabs>
          </TabsContent>

          <TabsContent value="history" className="mt-0">
            <AttendanceHistoryPanel
              weeks={historyWeeks}
              selectedWeek={selectedHistoryWeek}
              students={matrix?.students ?? []}
              searchTerm={historySearchTerm}
              onSearchTermChange={setHistorySearchTerm}
              onWeekChange={setHistoryWeekNumber}
              onEditWeek={weekNumber => {
                setSelectedWeekNumber(weekNumber);
                setManualWeekSelection(true);
                setActivePage('entry');
              }}
              isLoading={matrixLoading}
            />
        </TabsContent>
        </>
      )}
    </Tabs>
  );
}

function AttendanceHistoryPanel({
  weeks,
  students,
  searchTerm,
  onSearchTermChange,
  onEditWeek,
  isLoading,
}: {
  weeks: AttendanceMatrixWeek[];
  selectedWeek?: AttendanceMatrixWeek | null;
  students: AttendanceMatrixStudent[];
  searchTerm: string;
  onSearchTermChange: (value: string) => void;
  onWeekChange: (weekNumber: number) => void;
  onEditWeek: (weekNumber: number) => void;
  isLoading: boolean;
}) {
  const [absentPanelWeek, setAbsentPanelWeek] = useState<number | null>(null);

  const sortedWeeks = useMemo(
    () => [...weeks].sort((a, b) => a.weekNumber - b.weekNumber),
    [weeks]
  );

  const filteredStudents = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();
    if (!query) return students;

    return students.filter(student =>
      `${student.studentNumber} ${student.firstName} ${student.lastName} ${student.email}`
        .toLowerCase()
        .includes(query)
    );
  }, [searchTerm, students]);

  const totals = weeks.reduce(
    (acc, week) => {
      acc.present += week.presentCount;
      acc.absent += week.absentCount;
      acc.late += week.lateCount;
      acc.excused += week.excusedCount;
      return acc;
    },
    { present: 0, absent: 0, late: 0, excused: 0 }
  );

  // Compute absent students per week for the popup
  const absentStudentsForWeek = useMemo(() => {
    if (absentPanelWeek === null) return [];
    return students.filter(student => {
      const weekData = student.weeks?.[absentPanelWeek];
      return !weekData || weekData.status === 'Absent';
    });
  }, [absentPanelWeek, students]);

  // Compute per-student absent count across all history weeks
  const studentAbsentCounts = useMemo(() => {
    const counts: Record<number, number> = {};
    students.forEach(student => {
      let absentCount = 0;
      sortedWeeks.forEach(week => {
        const weekData = student.weeks?.[week.weekNumber];
        if (weekData?.status === 'Absent') absentCount++;
      });
      counts[student.studentId] = absentCount;
    });
    return counts;
  }, [students, sortedWeeks]);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-12 rounded-xl" />
        <Skeleton className="h-80 rounded-xl" />
      </div>
    );
  }

  if (weeks.length === 0) {
    return (
      <Alert>
        <ClipboardList className="h-4 w-4" />
        <AlertDescription>Henüz geçmiş yoklama kaydı bulunmuyor.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="relative flex gap-4">
      {/* Main matrix table */}
      <Card className={`flex-1 min-w-0 transition-all ${absentPanelWeek !== null ? 'lg:mr-[340px]' : ''}`}>
        <CardContent className="p-0">
          <div className="flex flex-col gap-3 border-b bg-muted/20 p-4 xl:flex-row xl:items-center xl:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              <CardTitle className="flex items-center gap-2 text-base">
                <CalendarDays className="h-4 w-4" />
                Geçmiş Yoklama Matrisi
              </CardTitle>
              <Badge variant="secondary">{sortedWeeks.length} hafta</Badge>
              <Badge variant="outline">{students.length} öğrenci</Badge>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Badge className="border-emerald-200 bg-emerald-50 text-emerald-700" variant="outline">
                {totals.present} var
              </Badge>
              <Badge className="border-red-200 bg-red-50 text-red-700" variant="outline">
                {totals.absent} yok
              </Badge>
              <Badge className="border-amber-200 bg-amber-50 text-amber-700" variant="outline">
                {totals.late} geç
              </Badge>
              <Badge className="border-sky-200 bg-sky-50 text-sky-700" variant="outline">
                {totals.excused} mazeretli
              </Badge>
            </div>
          </div>

          <div className="border-b p-4">
            <div className="relative w-full xl:w-80">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchTerm}
                onChange={event => onSearchTermChange(event.target.value)}
                placeholder="Öğrenci ara..."
                className="pl-9"
              />
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/30">
                  <th className="sticky left-0 z-20 min-w-52 bg-muted/30 px-4 py-3 text-left font-medium text-muted-foreground">
                    Öğrenci
                  </th>
                  {sortedWeeks.map(week => (
                    <th
                      key={week.weekNumber}
                      className="min-w-20 px-2 py-3 text-center font-medium text-muted-foreground"
                    >
                      <div className="flex flex-col items-center gap-1">
                        <span className="text-xs">{week.weekNumber}. H</span>
                        {week.sessionDate && (
                          <span className="text-[10px] text-muted-foreground/70">
                            {new Date(week.sessionDate).toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' })}
                          </span>
                        )}
                        <button
                          type="button"
                          onClick={() => onEditWeek(week.weekNumber)}
                          className="inline-flex h-6 w-6 items-center justify-center rounded border border-slate-200 bg-white text-slate-500 transition-colors hover:bg-slate-50 hover:text-slate-800"
                          title="Haftayı düzenle"
                        >
                          <Pencil className="h-3 w-3" />
                        </button>
                        {week.absentCount > 0 && (
                          <button
                            type="button"
                            onClick={() => setAbsentPanelWeek(
                              absentPanelWeek === week.weekNumber ? null : week.weekNumber
                            )}
                            className={`rounded px-1.5 py-0.5 text-[10px] font-medium transition-colors cursor-pointer ${
                              absentPanelWeek === week.weekNumber
                                ? 'bg-red-200 text-red-800 border border-red-300'
                                : 'bg-red-50 text-red-600 hover:bg-red-100'
                            }`}
                            title="Devamsız öğrencileri görüntüle"
                          >
                            {week.absentCount} yok
                          </button>
                        )}
                      </div>
                    </th>
                  ))}
                  <th className="min-w-20 px-2 py-3 text-center font-medium text-muted-foreground">
                    <div className="flex flex-col items-center gap-0.5">
                      <AlertTriangle className="h-3.5 w-3.5 text-red-500" />
                      <span className="text-[10px]">Devamsız</span>
                    </div>
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredStudents.map(student => {
                  const absentCount = studentAbsentCounts[student.studentId] ?? 0;
                  return (
                    <tr key={student.studentId} className="border-b last:border-0 hover:bg-muted/20">
                      <td className="sticky left-0 z-10 bg-card px-4 py-2">
                        <div className="font-medium text-sm">{student.firstName} {student.lastName}</div>
                        <div className="text-[11px] text-muted-foreground font-mono">{student.studentNumber}</div>
                      </td>
                      {sortedWeeks.map(week => {
                        const weekData = student.weeks?.[week.weekNumber];
                        return (
                          <td key={week.weekNumber} className="px-2 py-2 text-center align-middle">
                            <HistoryStatusDot status={weekData?.status} />
                          </td>
                        );
                      })}
                      <td className="px-2 py-2 text-center align-middle">
                        {absentCount > 0 ? (
                          <span className="inline-flex items-center justify-center rounded-full bg-red-50 border border-red-200 text-red-700 text-xs font-semibold min-w-6 h-6 px-1.5">
                            {absentCount}
                          </span>
                        ) : (
                          <span className="inline-flex items-center justify-center rounded-full bg-emerald-50 border border-emerald-200 text-emerald-700 text-xs font-semibold min-w-6 h-6 px-1.5">
                            0
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {filteredStudents.length === 0 && (
            <div className="py-12 text-center text-sm text-muted-foreground">Eşleşen öğrenci bulunamadı.</div>
          )}
        </CardContent>
      </Card>

      {/* Absent students slide-over panel */}
      {absentPanelWeek !== null && (
        <div className="lg:absolute lg:right-0 lg:top-0 lg:bottom-0 lg:w-[330px] z-30">
          <Card className="h-full border-red-200 shadow-lg">
            <CardHeader className="p-3 pb-2 border-b bg-red-50/50">
              <div className="flex items-center justify-between">
                <CardTitle className="flex items-center gap-2 text-sm text-red-800">
                  <XCircle className="h-4 w-4" />
                  {absentPanelWeek}. Hafta — Devamsızlar
                </CardTitle>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => setAbsentPanelWeek(null)}
                  className="h-7 w-7 p-0"
                >
                  <X className="h-4 w-4" />
                </Button>
              </div>
              <CardDescription className="text-xs">
                {absentStudentsForWeek.length} öğrenci devamsız
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0 overflow-y-auto" style={{ maxHeight: 'calc(100vh - 300px)' }}>
              {absentStudentsForWeek.length === 0 ? (
                <div className="p-4 text-center text-sm text-muted-foreground">Devamsız öğrenci yok.</div>
              ) : (
                <div className="divide-y">
                  {absentStudentsForWeek.map((student, idx) => (
                    <div key={student.studentId} className="flex items-center gap-3 px-3 py-2 hover:bg-red-50/30">
                      <span className="flex items-center justify-center rounded-full bg-red-100 text-red-700 text-[10px] font-bold min-w-5 h-5">
                        {idx + 1}
                      </span>
                      <div className="min-w-0 flex-1">
                        <div className="text-sm font-medium truncate">{student.firstName} {student.lastName}</div>
                        <div className="text-[11px] text-muted-foreground font-mono">{student.studentNumber}</div>
                      </div>
                      <Badge variant="outline" className="border-red-200 bg-red-50 text-red-700 text-[10px] shrink-0">
                        {studentAbsentCounts[student.studentId] ?? 0} toplam
                      </Badge>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}

function HistoryStatusDot({ status }: { status?: AttendanceStatus | null }) {
  if (!status) {
    return <span className="inline-block h-3 w-3 rounded-full bg-slate-200" title="Kayıt yok" />;
  }

  const config: Record<AttendanceStatus, { bg: string; title: string }> = {
    Present: { bg: 'bg-emerald-500', title: 'Var' },
    Absent: { bg: 'bg-red-500', title: 'Yok' },
    Late: { bg: 'bg-amber-500', title: 'Geç' },
    Excused: { bg: 'bg-sky-500', title: 'Mazeretli' },
  };

  const { bg, title } = config[status];
  return <span className={`inline-block h-3 w-3 rounded-full ${bg}`} title={title} />;
}

function WeekSelectTriggerLabel({
  weekNumber,
  week,
  isCurrent,
}: {
  weekNumber: number;
  week?: AttendanceMatrixWeek | null;
  isCurrent: boolean;
}) {
  return (
    <span className="flex min-w-0 items-center gap-2">
      <span className="truncate">{weekNumber}. Hafta</span>
      {isCurrent && (
        <span className="shrink-0 rounded border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-700">
          Güncel
        </span>
      )}
      <WeekStateMini week={week} />
    </span>
  );
}

function WeekSelectOption({
  weekNumber,
  week,
  isSelected,
  isCurrent,
}: {
  weekNumber: number;
  week?: AttendanceMatrixWeek;
  isSelected: boolean;
  isCurrent: boolean;
}) {
  return (
    <span className="flex w-full min-w-0 items-center justify-between gap-3">
      <span className="font-medium">{weekNumber}. Hafta</span>
      <span className="flex shrink-0 items-center gap-1">
        {isSelected && (
          <span className="rounded border border-sky-200 bg-sky-50 px-1.5 py-0.5 text-[10px] font-medium text-sky-700">
            Seçili
          </span>
        )}
        {isCurrent && (
          <span className="rounded border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-700">
            Güncel
          </span>
        )}
        <WeekStateMini week={week} showEmpty />
      </span>
    </span>
  );
}

function WeekStateMini({
  week,
  showEmpty = false,
}: {
  week?: AttendanceMatrixWeek | null;
  showEmpty?: boolean;
}) {
  const state = getWeekState(week);

  if (state === 'taken') {
    return (
      <span className="rounded border border-emerald-200 bg-emerald-50 px-1.5 py-0.5 text-[10px] font-medium text-emerald-700">
        Alındı
      </span>
    );
  }

  if (state === 'opened') {
    return (
      <span className="rounded border border-sky-200 bg-sky-50 px-1.5 py-0.5 text-[10px] font-medium text-sky-700">
        Açıldı
      </span>
    );
  }

  if (!showEmpty) return null;

  return (
    <span className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 text-[10px] font-medium text-slate-500">
      Boş
    </span>
  );
}

function getWeekState(week?: AttendanceMatrixWeek | null): 'taken' | 'opened' | 'empty' {
  if (!week?.sessionId) return 'empty';

  const recordCount = getWeekRecordCount(week);
  return recordCount > 0 ? 'taken' : 'opened';
}

function getWeekRecordCount(week?: AttendanceMatrixWeek | null) {
  if (!week) return 0;
  return week.presentCount + week.absentCount + week.lateCount + week.excusedCount;
}

function AttendanceMatrixRow({
  student,
  weekNumbers,
  selectedWeekNumber,
  session,
  sessionStudentMap,
  statuses,
  onStatusChange,
}: {
  student: AttendanceMatrixStudent;
  weekNumbers: number[];
  selectedWeekNumber: number;
  session?: AttendanceSession;
  sessionStudentMap: Map<number, AttendanceSession['students'][number]>;
  statuses: Record<number, EditableStatus>;
  onStatusChange: (studentId: number, status: AttendanceStatus) => void;
}) {
  return (
    <tr className="border-b last:border-0 hover:bg-muted/20">
      <td className="sticky left-0 z-10 bg-card px-4 py-3">
        <div className="font-medium">{student.firstName} {student.lastName}</div>
        <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
          <span className="font-mono">{student.studentNumber}</span>
          <span>{student.email}</span>
        </div>
      </td>
      {weekNumbers.map(weekNumber => {
        const isEditableWeek = Boolean(session && weekNumber === selectedWeekNumber);
        const matrixStatus = student.weeks?.[weekNumber];
        const sessionStudent = sessionStudentMap.get(student.studentId);
        const status = isEditableWeek ? statuses[student.studentId] : matrixStatus?.status;
        const source = isEditableWeek ? sessionStudent?.source ?? matrixStatus?.source : matrixStatus?.source;

        return (
          <td
            key={weekNumber}
            className={`px-3 py-3 text-center align-middle ${
              weekNumber === selectedWeekNumber ? 'bg-sky-50/60' : ''
            }`}
          >
            {isEditableWeek ? (
              <StatusButtons
                value={status ?? ''}
                onChange={nextStatus => onStatusChange(student.studentId, nextStatus)}
              />
            ) : (
              <StatusBadge status={status || undefined} source={source} />
            )}
          </td>
        );
      })}
    </tr>
  );
}

function StatusButtons({
  value,
  onChange,
}: {
  value: EditableStatus;
  onChange: (status: AttendanceStatus) => void;
}) {
  return (
    <div className="flex min-w-[220px] flex-wrap justify-center gap-1">
      {(Object.keys(STATUS_LABELS) as AttendanceStatus[]).map(status => (
        <Button
          key={status}
          type="button"
          size="sm"
          variant="outline"
          onClick={() => onChange(status)}
          className={value === status ? `h-7 px-2 text-xs ${STATUS_STYLES[status]}` : 'h-7 px-2 text-xs'}
        >
          {STATUS_LABELS[status]}
        </Button>
      ))}
    </div>
  );
}

function StatusBadge({
  status,
  source,
}: {
  status?: AttendanceStatus;
  source?: AttendanceEntrySource | null;
}) {
  if (!status) {
    return <span className="text-muted-foreground">-</span>;
  }

  return (
    <div className="flex min-h-8 items-center justify-center gap-1">
      <Badge variant="outline" className={STATUS_STYLES[status]}>
        {STATUS_LABELS[status]}
      </Badge>
      {source === 'Qr' && <Badge variant="outline">QR</Badge>}
    </div>
  );
}

function QrPanel({
  session,
  qrUrl,
  durationHours,
  setDurationHours,
  durationMinutes,
  setDurationMinutes,
  isQrActive,
  remainingMs,
  copied,
  onOpenQr,
  onDurationKeyDown,
  onCloseQr,
  onCopy,
  onDownloadPdf,
  isOpening,
  isClosing,
}: {
  session: AttendanceSession;
  qrUrl: string;
  durationHours: string;
  setDurationHours: (value: string) => void;
  durationMinutes: string;
  setDurationMinutes: (value: string) => void;
  isQrActive: boolean;
  remainingMs: number;
  copied: boolean;
  onOpenQr: () => void;
  onDurationKeyDown: (event: KeyboardEvent<HTMLInputElement>) => void;
  onCloseQr: () => void;
  onCopy: () => void;
  onDownloadPdf: () => void;
  isOpening: boolean;
  isClosing: boolean;
}) {
  return (
    <Card>
      <CardContent className="grid gap-5 p-4 lg:grid-cols-[320px_1fr]">
        <div className="flex min-h-[320px] items-center justify-center rounded-lg border bg-white p-4">
          {session.token ? (
            <QrSvg value={qrUrl} />
          ) : (
            <QrCode className="h-24 w-24 text-muted-foreground/40" />
          )}
        </div>

        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant={isQrActive ? 'success' : 'secondary'} className="gap-1.5">
              {isQrActive ? <CheckCircle2 className="h-3.5 w-3.5" /> : <XCircle className="h-3.5 w-3.5" />}
              {isQrActive ? 'Aktif' : 'Kapalı'}
            </Badge>
            <Badge variant="outline">{session.weekNumber}. Hafta</Badge>
            <Badge variant="outline">Şube {session.sectionNumber}</Badge>
            {session.qrExpiresAt && (
              <Badge variant="outline" className="gap-1.5">
                <Timer className="h-3.5 w-3.5" />
                {isQrActive ? formatRemaining(remainingMs) : 'Süre doldu'}
              </Badge>
            )}
            <Badge variant="outline" className="gap-1.5">
              <Users className="h-3.5 w-3.5" />
              {session.presentCount} var
            </Badge>
          </div>

          <div className="grid gap-3 lg:grid-cols-[300px_1fr]">
            <div className="space-y-2">
              <Label>Aktif kalma süresi</Label>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <span className="text-xs text-muted-foreground">Saat</span>
                  <Input
                    type="number"
                    min={0}
                    max={24}
                    value={durationHours}
                    onChange={event => setDurationHours(event.target.value)}
                    onKeyDown={onDurationKeyDown}
                    aria-label="QR aktif kalma saati"
                  />
                </div>
                <div className="space-y-1">
                  <span className="text-xs text-muted-foreground">Dakika</span>
                  <Input
                    type="number"
                    min={0}
                    max={59}
                    value={durationMinutes}
                    onChange={event => setDurationMinutes(event.target.value)}
                    onKeyDown={onDurationKeyDown}
                    aria-label="QR aktif kalma dakikası"
                  />
                </div>
              </div>
            </div>
            <div className="space-y-2">
              <Label>Form bağlantısı</Label>
              <div className="flex gap-2">
                <Input value={qrUrl} readOnly className="font-mono text-xs" />
                <Button type="button" variant="outline" size="icon" onClick={onCopy} disabled={!qrUrl}>
                  <Copy className="h-4 w-4" />
                </Button>
              </div>
              {copied && <p className="text-xs text-emerald-700">Bağlantı kopyalandı.</p>}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button type="button" onClick={onOpenQr} disabled={isOpening} className="gap-2">
              <QrCode className="h-4 w-4" />
              {isQrActive ? 'Yeni QR Oluştur' : session.qrExpiresAt ? 'Yeni QR Oluştur ve Başlat' : 'QR Oluştur ve Başlat'}
            </Button>
            <Button type="button" variant="outline" onClick={onCloseQr} disabled={!session.isQrOpen || isClosing}>
              QR Kapat
            </Button>
            <Button type="button" variant="outline" onClick={onDownloadPdf} disabled={!qrUrl} className="gap-2">
              <Download className="h-4 w-4" />
              PDF İndir
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function QrSvg({ value }: { value: string }) {
  const matrix = useMemo(() => {
    try {
      return createQrCodeMatrix(value);
    } catch {
      return null;
    }
  }, [value]);
  const size = getQrCodeSize();
  const quietZone = 4;

  if (!matrix) {
    return <div className="text-center text-sm text-slate-600">QR bağlantısı çok uzun.</div>;
  }

  return (
    <svg
      viewBox={`0 0 ${size + quietZone * 2} ${size + quietZone * 2}`}
      className="h-full max-h-[280px] w-full max-w-[280px]"
      role="img"
      aria-label="Yoklama QR kodu"
    >
      <rect width={size + quietZone * 2} height={size + quietZone * 2} fill="#ffffff" />
      <g transform={`translate(${quietZone} ${quietZone})`}>
        {matrix.map((row, y) =>
          row.map((isDark, x) =>
            isDark ? <rect key={`${x}-${y}`} x={x} y={y} width={1} height={1} fill="#111827" /> : null
          )
        )}
      </g>
    </svg>
  );
}

function getDefaultWeekDateInput(week?: AttendanceMatrixWeek | null) {
  const value = week?.sessionDate ?? week?.suggestedSessionDate;
  return value ? formatDateInput(new Date(value)) : formatDateInput(new Date());
}

function formatDateInput(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function formatDisplayDate(value: string) {
  return new Date(value).toLocaleDateString('tr-TR');
}

function formatRemaining(ms: number) {
  const seconds = Math.ceil(ms / 1000);
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}

function normalizeQrDuration(hoursValue: string, minutesValue: string) {
  const hours = clampNumber(Number(hoursValue), 0, 24);
  const minutes = clampNumber(Number(minutesValue), 0, 59);
  return Math.max(1, Math.min(1440, hours * 60 + minutes));
}

function splitQrDuration(totalMinutes: number) {
  return {
    hours: Math.floor(totalMinutes / 60),
    minutes: totalMinutes % 60,
  };
}

function clampNumber(value: number, min: number, max: number) {
  if (!Number.isFinite(value)) return min;
  return Math.max(min, Math.min(max, Math.trunc(value)));
}
