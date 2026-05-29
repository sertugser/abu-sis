import { useEffect, useMemo, useState } from 'react';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ProlizWeekLayout } from '@/components/scheduling/ProlizWeekLayout';
import type { ProlizRow } from '@/components/scheduling/ProlizDayTable';
import { useStudentTimetable } from '@/hooks/useStudentTimetable';
import { useStudentTimetableTerms } from '@/hooks/useStudentTimetableTerms';
import { downloadStudentTimetablePdf } from '@/api/studentTimetable.api';
import type { ScheduleBlock } from '@/types/studentTimetable';

function formatClassroomLabel(block: ScheduleBlock): string {
  if (!block.classroomName) return 'Çevrimiçi';
  return block.classroomName;
}

export default function StudentTimetablePage() {
  const {
    data: terms,
    isLoading: termsLoading,
    isError: termsError,
  } = useStudentTimetableTerms();
  const [selectedTermId, setSelectedTermId] = useState<number | undefined>(undefined);

  useEffect(() => {
    if (selectedTermId === undefined && terms && terms.length > 0) {
      const defaultTerm = terms.find((t) => t.isDefault) ?? terms[0];
      setSelectedTermId(defaultTerm.termId);
    }
  }, [terms, selectedTermId]);

  const { data, isLoading, isError } = useStudentTimetable(selectedTermId);

  const rowsByDay = useMemo(() => {
    if (!data) return {} as Record<number, ProlizRow[]>;
    const grouped: Record<number, ProlizRow[]> = {};
    for (const section of data.sections) {
      for (const block of section.blocks) {
        (grouped[block.day] ??= []).push({
          sectionId: section.sectionId,
          sectionNumber: section.sectionNumber,
          courseCode: section.courseCode,
          courseName: section.courseName,
          classroomLabel: formatClassroomLabel(block),
          instructorDisplay: section.instructorDisplay,
          startTime: block.startTime.slice(0, 5),
          endTime: block.endTime.slice(0, 5),
          isLab: block.blockType === 'Lab',
        });
      }
    }
    for (const day of Object.keys(grouped)) {
      grouped[Number(day)].sort((a, b) => a.startTime.localeCompare(b.startTime));
    }
    return grouped;
  }, [data]);

  const handlePrint = async () => {
    if (!selectedTermId) return;
    try {
      const blob = await downloadStudentTimetablePdf(selectedTermId);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      alert('PDF oluşturulamadı. Lütfen tekrar deneyin.');
    }
  };

  if (termsLoading) {
    return <Skeleton className="h-[600px] rounded-xl" />;
  }

  if (termsError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Dönem listesi yüklenemedi</AlertTitle>
        <AlertDescription>Sayfayı yenileyip tekrar deneyin.</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-2 border-b pb-3">
        <h1 className="text-xl font-semibold">Öğrenci Ders Programı</h1>
        {data?.studentName && (
          <p className="text-sm text-muted-foreground">{data.studentName}</p>
        )}
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <label className="text-sm">Dönem</label>
            <Select
              value={selectedTermId?.toString()}
              onValueChange={(v) => setSelectedTermId(Number(v))}
            >
              <SelectTrigger className="w-64">
                <SelectValue placeholder="Dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                {terms?.map((t) => (
                  <SelectItem key={t.termId} value={t.termId.toString()}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button onClick={handlePrint} disabled={!selectedTermId} variant="outline">
            <Printer className="mr-2 h-4 w-4" /> Yazdır
          </Button>
        </div>
      </div>

      {isLoading ? (
        <Skeleton className="h-[400px] rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertTitle>Program yüklenemedi</AlertTitle>
          <AlertDescription>Dönemi değiştirip tekrar deneyin.</AlertDescription>
        </Alert>
      ) : (
        <ProlizWeekLayout rowsByDay={rowsByDay} />
      )}
    </div>
  );
}
