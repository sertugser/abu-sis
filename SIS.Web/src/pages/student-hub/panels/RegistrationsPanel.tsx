import { useOutletContext } from 'react-router-dom';
import { type StudentCardOutletContext } from '../StudentCardPage';
import { useStudentCardRegistrations } from '../../../hooks/useStudentCard';
import { Badge } from '@/components/ui/badge';

export default function RegistrationsPanel() {
  const context = useOutletContext<StudentCardOutletContext | null>();
  // Hook called unconditionally; disabled when student id is unavailable.
  const studentId = context?.student.id ?? null;
  const { data, isLoading } = useStudentCardRegistrations(studentId);

  if (!context) return null;

  return (
    <div className="bg-white border border-border rounded-xl overflow-hidden">
      <table className="w-full text-sm">
        <thead className="bg-muted/40 border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Kodu</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Ders Adı</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">AKTS</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Öğretim Üyesi</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Program/Saat</th>
            <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Durum</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading ? (
            <tr>
              <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Yükleniyor...</td>
            </tr>
          ) : !data || data.length === 0 ? (
            <tr>
              <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">Kayıt bulunamadı.</td>
            </tr>
          ) : (
            data.map((reg) => (
              <tr key={reg.registrationId} className="hover:bg-muted/20 transition-colors">
                <td className="px-4 py-3 font-mono text-xs">{reg.courseCode}</td>
                <td className="px-4 py-3 font-medium text-foreground">{reg.courseName}</td>
                <td className="px-4 py-3 text-muted-foreground">{reg.ects}</td>
                <td className="px-4 py-3 text-muted-foreground">{reg.instructorName ?? '—'}</td>
                <td className="px-4 py-3 text-muted-foreground text-xs">{reg.schedule ?? '—'}</td>
                <td className="px-4 py-3">
                  <Badge variant="secondary">{reg.status}</Badge>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
