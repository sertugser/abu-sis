import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { useStudentCardTermGpa } from '../../../hooks/useStudentCard';

export default function TermGpaPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  // Hook called unconditionally; disabled when student id is unavailable.
  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentCardTermGpa(studentId);

  if (!context) return null;

  return (
    <div className="bg-white border border-border rounded-xl overflow-hidden">
      <table className="w-full text-sm">
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Dönem</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Sayısı</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Kredi</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">AKTS</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">DNO</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">GNO</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr>
              <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td>
            </tr>
          ) : !data || data.length === 0 ? (
            <tr>
              <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Dönem ortalaması bulunamadı.</td>
            </tr>
          ) : (
            data.map((term) => (
              <tr key={term.termId} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3 font-medium text-foreground">{term.termName}</td>
                <td className="px-4 py-3 text-muted-foreground">{term.courseCount}</td>
                <td className="px-4 py-3 text-muted-foreground">{term.totalCredits}</td>
                <td className="px-4 py-3 text-muted-foreground">{term.totalEcts}</td>
                <td className="px-4 py-3 font-semibold">{term.sgpa.toFixed(2)}</td>
                <td className="px-4 py-3 font-semibold">{term.cgpa.toFixed(2)}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
