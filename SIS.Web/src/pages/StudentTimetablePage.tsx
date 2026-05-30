import { useEffect, useMemo, useState } from 'react';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
      window.open(url, '_blank', 'noopener,noreferrer');
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
    <Card className="flex min-h-[calc(100vh-8rem)] flex-col overflow-hidden">
      <CardContent className="flex flex-1 flex-col p-0">
        <header className="flex items-center justify-between gap-3 border-b border-border bg-muted/20 px-4 py-2">
          <div className="flex items-center gap-2">
            <label className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
              Dönem
            </label>
            <Select
              value={selectedTermId?.toString()}
              onValueChange={(v) => setSelectedTermId(Number(v))}
            >
              <SelectTrigger className="h-8 w-52 text-xs">
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
          <Button
            onClick={handlePrint}
            disabled={!selectedTermId}
            variant="ghost"
            size="sm"
            className="h-8 text-xs"
          >
            <Printer className="mr-1.5 h-3.5 w-3.5" />
            Yazdır
          </Button>
        </header>

        {isLoading ? (
          <div className="flex-1 p-6">
            <Skeleton className="h-full min-h-[400px] rounded-xl" />
          </div>
        ) : isError ? (
          <div className="p-6">
            <Alert variant="destructive">
              <AlertTitle>Program yüklenemedi</AlertTitle>
              <AlertDescription>Dönemi değiştirip tekrar deneyin.</AlertDescription>
            </Alert>
          </div>
        ) : (
          <div className="flex flex-1 flex-col">
            <ProlizWeekLayout rowsByDay={rowsByDay} />
          </div>
        )}
      </CardContent>
    </Card>
  );
}
