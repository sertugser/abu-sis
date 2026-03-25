import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { useStudentInfo } from '../hooks/useStudentInfo';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { formatDate } from '../lib/utils';
import { STUDENT_STATUS_LABELS } from '../lib/constants';

export default function DashboardPage() {
  const { data: student, isLoading: studentLoading } = useStudentInfo();
  const { data: term } = useActiveTerm();

  if (studentLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {[1,2,3].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {[1,2,3].map(i => <Skeleton key={i} className="h-28 rounded-xl" />)}
        </div>
      </div>
    );
  }

  if (!student) {
    return (
      <div className="text-center py-16 text-muted-foreground">
        Öğrenci bilgisi bulunamadı.
      </div>
    );
  }

  const currentSemester = student.completedSemesters + 1;

  return (
    <div className="space-y-8">
      {/* Welcome */}
      <div>
        <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
          Hoş Geldiniz, <span className="text-primary">{student.firstName}</span>
        </h1>
      </div>

      {/* Row 1 — daily essentials */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 stagger-children">
        
        <StatCard label="GANO" value={student.cgpa.toFixed(2)} sub={`DANO: ${student.sgpa.toFixed(2)} · ECTS Limiti: ${student.ectsLimit}`} accentColor="border-l-blue-600" />
        
        <StatCard label="Sınıf / Dönem" value={`${student.classYear}. Sınıf`} sub={`${currentSemester}. Dönem`} accentColor="border-l-amber-500" />
        
        <Card className="border-l-4 border-l-indigo-500 hover:-translate-y-0.5 hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Danışman</p>
              {student.advisorName ? (
                <>
                  <p className="text-base font-bold text-foreground font-heading">{student.advisorTitle} {student.advisorName}</p>
                  <p className="text-xs text-muted-foreground">Akademik Danışman</p>
                </>
              ) : (
                <p className="text-sm text-muted-foreground mt-1">Atanmamış</p>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Row 2 — reference info */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 stagger-children">
        <Card className="border-l-4 border-l-emerald-500 hover:-translate-y-0.5 hover:shadow-md transition-all">
          <CardContent className="p-5">
            <div className="space-y-2">
              <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Durum</p>
              <Badge variant={student.status === 'Active' ? 'success' : 'secondary'}>
                {STUDENT_STATUS_LABELS[student.status] || student.status}
              </Badge>
              {student.isScholarshipStudent && (
                <p className="text-xs font-medium text-accent">Burslu Öğrenci</p>
              )}
            </div>
          </CardContent>
        </Card>

        <StatCard label="Öğrenci No" value={student.studentNumber} accentColor="border-l-[#003366]" mono />

        <StatCard label="Program / Fakülte" value={student.programName} sub={`${student.departmentName} — ${student.facultyName}`} accentColor="border-l-violet-400" valueSize="text-base" />
      </div>

      {/* Active term */}
      {term && (
        <Card>
          <CardContent className="p-5">
            <h3 className="font-heading text-sm font-semibold text-foreground mb-4">Aktif Dönem Bilgisi</h3>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              <div>
                <span className="text-muted-foreground">Dönem</span>
                <p className="font-medium mt-0.5">{term.name}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Kayıt Tarihleri</span>
                <p className="font-medium mt-0.5">{formatDate(term.registrationStart)} — {formatDate(term.registrationEnd)}</p>
              </div>
              <div>
                <span className="text-muted-foreground">Ek/Bırak Tarihleri</span>
                <p className="font-medium mt-0.5">{formatDate(term.addDropStart)} — {formatDate(term.addDropEnd)}</p>
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
    <Card className={`border-l-4 ${accentColor} hover:-translate-y-0.5 hover:shadow-md transition-all`}>
      <CardContent className="p-5">
        <div className="space-y-1">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">{label}</p>
          <p className={`${valueSize} font-bold text-foreground ${mono ? 'font-mono' : 'font-heading'}`}>{value}</p>
          {sub && <p className="text-xs text-muted-foreground">{sub}</p>}
        </div>
      </CardContent>
    </Card>
  );
}
