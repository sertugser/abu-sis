import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { ROUTES } from '@/lib/constants';
import { AlertTriangle, Calendar, RefreshCcw } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { getSchedulingErrorMessage } from '../api/instructorScheduling.api';
import WeeklyTimetableGrid, { type TimetableEntry } from '../components/scheduling/WeeklyTimetableGrid';
import { useInstructorSections } from '../hooks/useInstructorSections';
import { parseScheduleJson } from '../lib/scheduleUtils';

export default function InstructorSchedulingPage() {
  const navigate = useNavigate();
  const {
    data: sectionsData,
    isLoading,
    isError,
    error,
    refetch,
  } = useInstructorSections();

  const sections = sectionsData?.sections ?? [];

  const errorMessage = isError
    ? getSchedulingErrorMessage(error, 'Ders programı bilgileri yüklenemedi.')
    : null;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-[500px] rounded-xl" />
      </div>
    );
  }

  if (errorMessage) {
    return (
      <div className="space-y-6">
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Program ekranı yüklenemedi</AlertTitle>
          <AlertDescription className="space-y-4">
            <p>{errorMessage}</p>
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
              <h2 className="font-heading text-xl font-semibold text-foreground">
                Atanmış ders bulunamadı
              </h2>
              <p className="max-w-xl text-sm text-muted-foreground">
                Bu dönem size atanmış ders bulunamadı. Ders programınız burada görüntülenecektir.
              </p>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const entries = buildTimetableEntries(sections);

  return (
    <div className="space-y-6">
      <WeeklyTimetableGrid
        entries={entries}
        title="Ders Programım"
      />
    </div>
  );
}

function buildTimetableEntries(
  sections: Array<{
    sectionId: number;
    courseCode: string;
    courseName: string;
    classroomName: string;
    scheduleJson?: string;
  }>,
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
      hasConflict: false,
    });
  }

  return entries;
}
