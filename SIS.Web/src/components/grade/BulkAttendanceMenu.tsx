// SIS.Web/src/components/grade/BulkAttendanceMenu.tsx
// "Girme Durum" toplu işaretleme menüsü.
// Notu girilmeyen öğrencileri veya tüm öğrencileri toplu olarak Girdi/Girmedi/Mazeretli işaretler.

import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { Users, ChevronDown } from 'lucide-react';
import type { ExamAttendance, StudentGradeRow } from '../../types/grade';

export interface BulkAttendanceItem {
  studentId: number;
  attendance: ExamAttendance;
}

interface BulkAttendanceMenuProps {
  students: StudentGradeRow[];
  componentId: number;
  disabled?: boolean;
  onBulkSet: (items: BulkAttendanceItem[]) => void;
}

/** Aktif bileşen için notu girilmemiş öğrencileri döndürür. */
function ungradedStudents(students: StudentGradeRow[], componentId: number): StudentGradeRow[] {
  return students.filter((s) => s.scores[componentId] == null);
}

export function BulkAttendanceMenu({
  students,
  componentId,
  disabled = false,
  onBulkSet,
}: BulkAttendanceMenuProps) {
  const setForUngraded = (attendance: ExamAttendance) => {
    const targets = ungradedStudents(students, componentId);
    if (targets.length === 0) return;
    onBulkSet(targets.map((s) => ({ studentId: s.studentId, attendance })));
  };

  const setForAll = (attendance: ExamAttendance) => {
    if (students.length === 0) return;
    onBulkSet(students.map((s) => ({ studentId: s.studentId, attendance })));
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" disabled={disabled} className="gap-1.5 text-sm">
          <Users className="h-4 w-4" />
          Girme Durum
          <ChevronDown className="h-3.5 w-3.5 opacity-60" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel>Notu Girilmeyenleri İşaretle</DropdownMenuLabel>
        <DropdownMenuItem onSelect={() => setForUngraded('Present')}>Girdi olarak işaretle</DropdownMenuItem>
        <DropdownMenuItem onSelect={() => setForUngraded('Absent')}>Girmedi olarak işaretle</DropdownMenuItem>
        <DropdownMenuItem onSelect={() => setForUngraded('Excused')}>Mazeretli olarak işaretle</DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Tümünü İşaretle</DropdownMenuLabel>
        <DropdownMenuItem onSelect={() => setForAll('Present')}>Tümünü Girdi</DropdownMenuItem>
        <DropdownMenuItem onSelect={() => setForAll('Absent')}>Tümünü Girmedi</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
