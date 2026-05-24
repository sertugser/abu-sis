import { useParams, useNavigate, Outlet } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
    <div className="space-y-4">
      <Button variant="ghost" size="sm" onClick={() => navigate('/ogrenci-rehberi')}>
        <ChevronLeft className="h-4 w-4 mr-1" /> Listeye Dön
      </Button>
      <div className="flex border border-border rounded-xl overflow-hidden bg-white min-h-[60vh]">
        <CardSidebar />
        <div className="flex-1 flex flex-col">
          <StudentCardHeader student={student} />
          <div className="flex-1 p-6"><Outlet context={{ student } satisfies StudentCardOutletContext} /></div>
        </div>
      </div>
    </div>
  );
}
