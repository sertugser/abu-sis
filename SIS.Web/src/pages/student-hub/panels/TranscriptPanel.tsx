import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { useStudentCardTranscript } from '../../../hooks/useStudentCard';
import { TranscriptReportDocument } from '../../../components/transcript/TranscriptReportDocument';

export default function TranscriptPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  // Hook called unconditionally; disabled when student id is unavailable.
  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentCardTranscript(studentId);

  if (!context) return null;

  if (isLoading) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Yükleniyor...
      </div>
    );
  }

  if (!data) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Transkript bilgisi bulunamadı.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <div className="mx-auto w-[860px] max-w-none rounded-xl border border-slate-300 bg-white shadow-sm">
        <TranscriptReportDocument transcript={data} />
      </div>
    </div>
  );
}
