import { useEffect, useMemo, useState } from 'react';
import { TimetableView, type TimetableTermOption } from '@/components/scheduling/TimetableView';
import type { WeekTimetableSection } from '@/components/scheduling/WeekTimetableGrid';
import { downloadInstructorTimetablePdf } from '../api/instructor.api';
import { useInstructorSections, useInstructorTerms } from '../hooks/useInstructorSections';

export default function InstructorSchedulingPage() {
  const { data: terms } = useInstructorTerms();
  const [selectedTermId, setSelectedTermId] = useState<number | undefined>(undefined);

  useEffect(() => {
    if (selectedTermId === undefined && terms && terms.length > 0) {
      const defaultTerm = terms.find((t) => t.isActive) ?? terms[0];
      setSelectedTermId(defaultTerm.id);
    }
  }, [terms, selectedTermId]);

  const { data, isLoading, isError } = useInstructorSections(selectedTermId);

  const termOptions: TimetableTermOption[] = useMemo(
    () => (terms ?? []).map((t) => ({ termId: t.id, label: t.name })),
    [terms],
  );

  const sections: WeekTimetableSection[] = useMemo(
    () =>
      (data?.sections ?? []).map((s) => ({
        sectionId: s.sectionId,
        sectionNumber: String(s.sectionNumber),
        courseCode: s.courseCode,
        courseName: s.courseName,
        // Kendi programı — meta satırında hoca adı gereksiz; sadece derslik gösterilir.
        instructorDisplay: '',
        blocks: s.schedules,
      })),
    [data],
  );

  const handlePrint = async () => {
    if (!selectedTermId) return;
    try {
      const blob = await downloadInstructorTimetablePdf(selectedTermId);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      alert('PDF oluşturulamadı. Lütfen tekrar deneyin.');
    }
  };

  return (
    <TimetableView
      sections={sections}
      slots={data?.slots}
      lunchBreakStart={data?.lunchBreakStart}
      lunchBreakEnd={data?.lunchBreakEnd}
      terms={termOptions}
      selectedTermId={selectedTermId}
      onTermChange={setSelectedTermId}
      onPrint={handlePrint}
      isLoading={isLoading}
      isError={isError}
      errorTitle="Ders programı yüklenemedi"
      errorDescription="Dönemi değiştirip tekrar deneyin."
    />
  );
}
