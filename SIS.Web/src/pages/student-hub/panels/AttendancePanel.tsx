import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { useStudentCardAttendance } from '../../../hooks/useStudentCard';

export default function AttendancePanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  // Hook called unconditionally; disabled when student id is unavailable.
  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentCardAttendance(studentId);

  if (!context) return null;

  if (isLoading) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Yükleniyor...
      </div>
    );
  }

  if (!data || data.courses.length === 0) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Devamsızlık verisi bulunamadı.
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <h3 className="text-sm font-semibold text-foreground">{data.termName}</h3>
      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Kodu</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Adı</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Teorik</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Lab</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {data.courses.map((course) => (
              <tr key={course.registrationId} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3 font-mono text-xs">{course.courseCode}</td>
                <td className="px-4 py-3 font-medium text-foreground">{course.courseName}</td>
                <td className="px-4 py-3 text-muted-foreground">
                  {course.theoryAbsenceHours ?? '—'} / {course.totalTheoryHours}
                </td>
                <td className="px-4 py-3 text-muted-foreground">
                  {course.labAbsenceHours ?? '—'} / {course.totalLabHours}
                </td>
                <td className="px-4 py-3 text-muted-foreground">{course.attendanceStatusLabel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
