import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { cgpaColor } from '../utils';

function Cell({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="bg-white px-4 py-3.5">
      <p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      <div className="mt-1 break-words text-sm">{children}</div>
    </div>
  );
}

export default function OverviewPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  if (!context) return null;
  const { student } = context;
  const transcriptClosed = student.transcriptAccessible === false;
  return (
    <div className="overflow-hidden rounded-xl border border-border">
      <div className="grid grid-cols-1 gap-px bg-border sm:grid-cols-2 lg:grid-cols-4">
        <Cell label="Öğrenci No"><span className="font-mono font-semibold">{student.studentNumber}</span></Cell>
        <Cell label="Ad Soyad">{student.firstName} {student.lastName}</Cell>
        <Cell label="GNO">
          {transcriptClosed ? (
            <span className="text-muted-foreground">Kapalı</span>
          ) : (
            <span className={`font-semibold ${cgpaColor(student.cgpa)}`}>
              {student.cgpa?.toFixed(2) ?? '—'} <span className="font-normal text-muted-foreground">/ 4.00</span>
            </span>
          )}
        </Cell>
        <Cell label="Tamamlanan Dönem">{student.completedSemesters ?? '—'}</Cell>
        <Cell label="E-posta"><span className="text-muted-foreground">{student.email}</span></Cell>
        <Cell label="Program">
          {student.programName}
          <span className="block text-xs text-muted-foreground">{student.departmentName}</span>
        </Cell>
        <Cell label="Danışman">{student.advisorName ?? '—'}</Cell>
        <Cell label="Burs">{student.isScholarshipStudent ? 'Burslu' : '—'}</Cell>
      </div>
    </div>
  );
}
