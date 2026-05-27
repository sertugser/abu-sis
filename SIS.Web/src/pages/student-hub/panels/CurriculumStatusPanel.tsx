import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { useStudentCardCurriculumStatus } from '../../../hooks/useStudentCard';
import { SemesterTable } from '../../../components/curriculum/SemesterTable';

export default function CurriculumStatusPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  // Hook called unconditionally; disabled when student id is unavailable.
  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentCardCurriculumStatus(studentId);

  if (!context) return null;

  if (isLoading) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Yükleniyor...
      </div>
    );
  }

  if (!data || data.semesters.length === 0) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Müfredat bilgisi bulunamadı.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-border bg-white px-5 py-4 text-sm">
        <p>
          <span className="text-muted-foreground">Genel Kredi: </span>
          <span className="font-bold text-foreground">{data.totalCredits}</span>
          <span className="text-muted-foreground ml-3">AKTS: </span>
          <span className="font-bold text-foreground">{data.totalEcts}</span>
          <span className="text-muted-foreground ml-3">Genel Ort: </span>
          <span className={`font-bold ${data.cgpa < 2.0 ? 'text-destructive' : 'text-primary'}`}>{data.cgpa.toFixed(2)}</span>
        </p>
        <p className="mt-1.5">
          <span className="text-muted-foreground">Başarılı Olunan Kredi: </span>
          <span className="font-bold text-success">{data.passedCredits}</span>
          <span className="text-muted-foreground ml-3">AKTS: </span>
          <span className="font-bold text-success">{data.passedEcts}</span>
        </p>
      </div>

      {data.semesters.map(semester => (
        <SemesterTable key={semester.semesterNumber} semester={semester} />
      ))}
    </div>
  );
}
