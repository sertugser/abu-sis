import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { useStudentCardCourseHistory } from '../../../hooks/useStudentCard';
import type { CourseHistoryTermDto } from '../../../types/registration';
import { Badge } from '@/components/ui/badge';

function TermTable({ term }: { term: CourseHistoryTermDto }) {
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-semibold text-foreground">{term.termName}</h3>
      <div className="bg-white border border-border rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Kodu</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Adı</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">AKTS</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kredi</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Not</th>
              <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {term.courses.map((course) => (
              <tr key={course.registrationId} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3 font-mono text-xs">{course.courseCode}</td>
                <td className="px-4 py-3 font-medium text-foreground">{course.courseName}</td>
                <td className="px-4 py-3 text-muted-foreground">{course.ects}</td>
                <td className="px-4 py-3 text-muted-foreground">{course.nationalCredit}</td>
                <td className="px-4 py-3 font-semibold">{course.grade ?? '—'}</td>
                <td className="px-4 py-3">
                  <Badge variant="secondary">{course.status}</Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default function GradesPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  // Hook called unconditionally; disabled when student id is unavailable.
  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentCardCourseHistory(studentId);

  if (!context) return null;

  if (isLoading) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Yükleniyor...
      </div>
    );
  }

  if (!data || data.length === 0) {
    return (
      <div className="bg-white border border-border rounded-xl px-4 py-8 text-center text-muted-foreground">
        Not kaydı bulunamadı.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {data.map((term) => (
        <TermTable key={term.termId} term={term} />
      ))}
    </div>
  );
}
