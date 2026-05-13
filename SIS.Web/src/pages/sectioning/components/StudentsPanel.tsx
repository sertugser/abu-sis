import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { STATUS_LABELS } from '@/lib/constants';
import type { DepartmentSectionClassList } from '@/types/academicOperations';
import { EmptyMessage } from './EmptyMessage';

interface StudentsPanelProps {
  section: DepartmentSectionClassList | null;
  selectedStudentIds: Set<number>;
  onToggleStudent: (studentId: number) => void;
  onToggleAll: (checked: boolean) => void;
}

export function StudentsPanel({ section, selectedStudentIds, onToggleStudent, onToggleAll }: StudentsPanelProps) {
  const allChecked = section
    ? section.students.length > 0 && section.students.every(s => selectedStudentIds.has(s.studentId))
    : false;

  return (
    <Card className="overflow-hidden">
      <div className="flex items-center justify-between border-b bg-gray-50 px-4 py-2">
        <span className="text-sm font-semibold text-gray-700">
          Seçilen Şubenin Öğrencileri
          {section && (
            <span className="ml-2 font-normal text-gray-500">
              — Şube {section.sectionNumber} ({section.students.length})
            </span>
          )}
        </span>
        {selectedStudentIds.size > 0 && (
          <Badge variant="secondary">{selectedStudentIds.size} seçili</Badge>
        )}
      </div>
      <div className="max-h-[440px] min-h-[380px] overflow-y-auto">
        {!section ? (
          <EmptyMessage text="Şube seçilmedi" emphasis />
        ) : section.students.length === 0 ? (
          <EmptyMessage text="Bu şubede henüz kayıtlı öğrenci yok" />
        ) : (
          <Table>
            <TableHeader className="sticky top-0 z-10 bg-gray-100">
              <TableRow>
                <TableHead className="h-9 w-10 text-center">
                  <input
                    type="checkbox"
                    checked={allChecked}
                    onChange={e => onToggleAll(e.target.checked)}
                    className="h-4 w-4 cursor-pointer"
                  />
                </TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Öğrenci No</TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Adı</TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Soyadı</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Durum</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Not</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {section.students.map(student => {
                const isChecked = selectedStudentIds.has(student.studentId);
                const [firstName, ...rest] = student.studentName.split(' ');
                const lastName = rest.join(' ');
                return (
                  <TableRow key={student.studentId} className={cn('hover:bg-blue-50', isChecked && 'bg-blue-50')}>
                    <TableCell className="text-center">
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => onToggleStudent(student.studentId)}
                        className="h-4 w-4 cursor-pointer"
                      />
                    </TableCell>
                    <TableCell className="font-mono text-sm text-blue-700">{student.studentNumber}</TableCell>
                    <TableCell className="text-sm text-gray-800">{firstName}</TableCell>
                    <TableCell className="text-sm font-medium text-gray-800">{lastName}</TableCell>
                    <TableCell className="text-center text-xs text-gray-600">
                      {STATUS_LABELS[student.status] ?? student.status}
                    </TableCell>
                    <TableCell className="text-center font-mono text-sm">
                      {student.grade ? (
                        <span
                          className={cn(
                            student.grade === 'FF' || student.grade === 'NA'
                              ? 'font-bold text-red-700'
                              : 'text-gray-700',
                          )}
                        >
                          {student.grade}
                        </span>
                      ) : (
                        <span className="text-gray-300">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </div>
    </Card>
  );
}
