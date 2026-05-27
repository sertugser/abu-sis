import { useParams, useNavigate, Outlet } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import { useAdminStudent, type AdminStudentDetail } from '../../hooks/useAdminStudents';
import CardSidebar from './components/CardSidebar';
import StudentCardHeader from './components/StudentCardHeader';

export type StudentCardOutletContext = { student: AdminStudentDetail };

export default function StudentCardPage() {
  const { studentId } = useParams();
  const navigate = useNavigate();
  const id = studentId ? Number(studentId) : null;
  const { data: student, isLoading, isError } = useAdminStudent(id);

  if (isLoading) return <div className="p-8 text-muted-foreground">Yükleniyor...</div>;
  if (isError || !student) return <div className="p-8 text-destructive">Öğrenci bulunamadı.</div>;

  return (
    <div className="overflow-hidden rounded-xl border border-border bg-white shadow-sm">
      {/* Back link grounded as the card's top strip (not floating above it) */}
      <div className="border-b border-border px-4 py-2.5">
        <button
          type="button"
          onClick={() => navigate('/ogrenci-rehberi')}
          className="inline-flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <ChevronLeft className="h-4 w-4" /> Listeye Dön
        </button>
      </div>
      <div className="flex min-h-[70vh]">
        <CardSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <StudentCardHeader student={student} />
          <div className="flex-1 p-6"><Outlet context={{ student } satisfies StudentCardOutletContext} /></div>
        </div>
      </div>
    </div>
  );
}
