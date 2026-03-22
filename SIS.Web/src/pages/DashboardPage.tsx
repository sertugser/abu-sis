import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { formatDate, getEctsLimit } from '../lib/utils';
import { STUDENT_STATUS_LABELS } from '../lib/constants';

export default function DashboardPage() {
  const { data: student, isLoading: studentLoading } = useStudentInfo();
  const { data: term } = useActiveTerm();

  if (studentLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {[1, 2, 3].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
        </div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {[1, 2, 3].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
        </div>
      </div>
    );
  }

  if (!student) {
    return (
      <div className="py-16 text-center text-muted-foreground">
        Öğrenci bilgisi bulunamadı.
      </div>
    );
  }

  const ectsLimit = getEctsLimit(student.cgpa);
  const currentSemester = student.completedSemesters + 1;
  const classYear = Math.min(Math.floor(student.completedSemesters / 2) + 1, 4);

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
          Hoş Geldiniz, <span className="text-primary">{student.firstName}</span>
        </h1>
        <p className="mt-1 text-muted-foreground">
          {student.programName} — {classYear}. Sınıf, {currentSemester}. Dönem
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 stagger-children">
        <StatCard label="Öğrenci No" value={student.studentNumber} accentColor="border-l-teal-500" mono />
        <StatCard label="GANO" value={student.cgpa.toFixed(2)} sub={`ECTS Limiti: ${ectsLimit}`} accentColor="border-l-blue-500" />
        <StatCard label="Sınıf / Dönem" value={`${classYear}. Sınıf`} sub={`${currentSemester}. Dönem`} accentColor="border-l-cyan-500" />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 stagger-children">
        <StatCard label="Program" value={student.programName} sub={student.departmentName} accentColor="border-l-indigo-700" valueSize="text-base" />
        <StatCard label="Fakülte" value={student.facultyName} accentColor="border-l-[#003366]" valueSize="text-base" />
        <Card className="border-l-4 border-l-success hover:-translate-y-0.5 hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="space-y-2">
              <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">Durum</p>
              <Badge variant={student.status === 'Active' ? 'success' : 'secondary'}>
                {STUDENT_STATUS_LABELS[student.status] || student.status}
              </Badge>
              {student.isScholarshipStudent && (
                <p className="text-xs font-medium text-accent">Burslu Öğrenci</p>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      {term && (
        <Card>
          <CardContent className="p-5">
            <h3 className="mb-4 font-heading text-sm font-semibold text-foreground">Aktif Dönem Bilgisi</h3>
            <div className="grid grid-cols-1 gap-4 text-sm md:grid-cols-3">
              <div>
                <span className="text-muted-foreground">Dönem</span>
                <p className="mt-0.5 font-medium">{term.name}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Kayıt Tarihleri</span>
                <p className="mt-0.5 font-medium">{formatDate(term.registrationStart)} — {formatDate(term.registrationEnd)}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Ek/Bırak Tarihleri</span>
                <p className="mt-0.5 font-medium">{formatDate(term.addDropStart)} — {formatDate(term.addDropEnd)}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

interface StatCardProps {
  label: string;
  value: string;
  sub?: string;
  accentColor: string;
  mono?: boolean;
  valueSize?: string;
}

function StatCard({ label, value, sub, accentColor, mono, valueSize = 'text-xl' }: StatCardProps) {
  return (
    <Card className={`border-l-4 ${accentColor} transition-all hover:-translate-y-0.5 hover:shadow-md`}>
      <CardContent className="p-5">
        <div className="space-y-1">
          <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{label}</p>
          <p className={`${valueSize} font-bold text-foreground ${mono ? 'font-mono' : 'font-heading'}`}>{value}</p>
          {sub && <p className="text-xs text-muted-foreground">{sub}</p>}
        </div>
      </CardContent>
    </Card>
  );
}
