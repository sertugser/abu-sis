import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { cgpaColor } from '../utils';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground font-medium uppercase tracking-wide">{label}</p>
      <p className="mt-0.5">{children}</p>
    </div>
  );
}

export default function OverviewPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  if (!context) return null;
  const { student } = context;
  const transcriptClosed = student.transcriptAccessible === false;
  return (
    <div className="grid grid-cols-2 gap-x-6 gap-y-4 max-w-2xl">
      <Field label="Öğrenci No"><span className="font-mono font-semibold">{student.studentNumber}</span></Field>
      <Field label="E-posta"><span className="text-muted-foreground">{student.email}</span></Field>
      <Field label="Ad Soyad">{student.firstName} {student.lastName}</Field>
      <Field label="Program">{student.programName}<span className="block text-xs text-muted-foreground">{student.departmentName}</span></Field>
      <Field label="GNO">{transcriptClosed ? <span className="text-muted-foreground">Kapalı</span> : <span className={`font-semibold ${cgpaColor(student.cgpa)}`}>{student.cgpa?.toFixed(2) ?? '—'} / 4.00</span>}</Field>
      <Field label="Tamamlanan Dönem">{student.completedSemesters ?? '—'}</Field>
      <Field label="Danışman">{student.advisorName ?? '—'}</Field>
      <Field label="Burs">{student.isScholarshipStudent ? 'Burslu' : '—'}</Field>
    </div>
  );
}
