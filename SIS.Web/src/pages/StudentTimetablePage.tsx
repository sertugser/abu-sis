import { useEffect, useMemo, useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { TimetableView, type TimetableTermOption } from '@/components/scheduling/TimetableView';
import { useStudentTimetable } from '@/hooks/useStudentTimetable';
import { useStudentTimetableTerms } from '@/hooks/useStudentTimetableTerms';
import { downloadStudentTimetablePdf } from '@/api/studentTimetable.api';

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

  const termOptions: TimetableTermOption[] = useMemo(
    () => (terms ?? []).map((t) => ({ termId: t.termId, label: t.label })),
    [terms],
  );

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
    <TimetableView
      sections={data?.sections ?? []}
      slots={data?.slots}
      lunchBreakStart={data?.lunchBreakStart}
      lunchBreakEnd={data?.lunchBreakEnd}
      terms={termOptions}
      selectedTermId={selectedTermId}
      onTermChange={setSelectedTermId}
      onPrint={handlePrint}
      isLoading={isLoading}
      isError={isError}
    />
  );
}
