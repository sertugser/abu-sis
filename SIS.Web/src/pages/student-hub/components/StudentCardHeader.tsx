import { Badge } from '@/components/ui/badge';
import { STUDENT_STATUS_LABELS, STUDENT_STATUS_VARIANT } from '../../../lib/constants';
import type { AdminStudentDetail } from '../../../hooks/useAdminStudents';

export default function StudentCardHeader({ student }: { student: AdminStudentDetail }) {
  return (
    <div className="flex items-center gap-4 border-b border-border bg-white px-6 py-4">
      <div aria-hidden="true" className="h-14 w-14 rounded-full bg-muted flex items-center justify-center text-lg font-semibold text-muted-foreground">
        {student.firstName?.[0]}{student.lastName?.[0]}
      </div>
      <div>
        <p className="font-mono text-xs text-muted-foreground">{student.studentNumber}</p>
        <p className="text-lg font-semibold">{student.firstName} {student.lastName}</p>
        <p className="text-xs text-muted-foreground">{student.programName}</p>
      </div>
      <div className="ml-auto">
        <Badge variant={STUDENT_STATUS_VARIANT[student.status] ?? 'secondary'}>{STUDENT_STATUS_LABELS[student.status] ?? student.status}</Badge>
      </div>
    </div>
  );
}
