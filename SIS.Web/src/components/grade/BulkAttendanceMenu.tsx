// SIS.Web/src/components/grade/BulkAttendanceMenu.tsx
// "Girme Durum" toplu işaretleme menüsü (bileşen-farkında).
// Önce bileşen seçilir (birden fazla varsa alt menü), sonra aksiyon:
// notu girilmeyen veya tüm öğrencileri Girdi/Girmedi/Mazeretli işaretle.

import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuSubContent,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { Users, ChevronDown } from 'lucide-react';
import type { ExamAttendance, GradeComponent, StudentGradeRow } from '../../types/grade';

export interface BulkAttendanceItem {
  studentId: number;
  attendance: ExamAttendance;
}

interface BulkAttendanceMenuProps {
  students: StudentGradeRow[];
  components: GradeComponent[];
  disabled?: boolean;
  onBulkSet: (componentId: number, items: BulkAttendanceItem[]) => void;
}

/** Bir bileşen için notu girilmemiş öğrencileri döndürür. */
function ungradedStudents(students: StudentGradeRow[], componentId: number): StudentGradeRow[] {
  return students.filter((s) => s.scores[componentId] == null);
}

/** Tek bir bileşen için aksiyon listesi (alt menü ya da doğrudan menü içeriği). */
function ComponentActions({
  students,
  componentId,
  onBulkSet,
}: {
  students: StudentGradeRow[];
  componentId: number;
  onBulkSet: (componentId: number, items: BulkAttendanceItem[]) => void;
}) {
  const setForUngraded = (attendance: ExamAttendance) => {
    const targets = ungradedStudents(students, componentId);
    if (targets.length === 0) return;
    onBulkSet(componentId, targets.map((s) => ({ studentId: s.studentId, attendance })));
  };

  const setForAll = (attendance: ExamAttendance) => {
    if (students.length === 0) return;
    onBulkSet(componentId, students.map((s) => ({ studentId: s.studentId, attendance })));
  };

  return (
    <>
      <DropdownMenuLabel>Notu Girilmeyenleri İşaretle</DropdownMenuLabel>
      <DropdownMenuItem onSelect={() => setForUngraded('Present')}>Girdi olarak işaretle</DropdownMenuItem>
      <DropdownMenuItem onSelect={() => setForUngraded('Absent')}>Girmedi olarak işaretle</DropdownMenuItem>
      <DropdownMenuItem onSelect={() => setForUngraded('Excused')}>Mazeretli olarak işaretle</DropdownMenuItem>
      <DropdownMenuSeparator />
      <DropdownMenuLabel>Tümünü İşaretle</DropdownMenuLabel>
      <DropdownMenuItem onSelect={() => setForAll('Present')}>Tümünü Girdi</DropdownMenuItem>
      <DropdownMenuItem onSelect={() => setForAll('Absent')}>Tümünü Girmedi</DropdownMenuItem>
    </>
  );
}

export function BulkAttendanceMenu({
  students,
  components,
  disabled = false,
  onBulkSet,
}: BulkAttendanceMenuProps) {
  const singleComponent = components.length === 1 ? components[0] : null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || components.length === 0}
          className="gap-1.5 text-sm"
        >
          <Users className="h-4 w-4" />
          Girme Durum
          <ChevronDown className="h-3.5 w-3.5 opacity-60" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        {singleComponent ? (
          <ComponentActions students={students} componentId={singleComponent.id} onBulkSet={onBulkSet} />
        ) : (
          components.map((comp) => (
            <DropdownMenuSub key={comp.id}>
              <DropdownMenuSubTrigger>
                {comp.name} <span className="ml-1 font-mono text-xs text-muted-foreground">%{comp.weight}</span>
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent className="w-60">
                <ComponentActions students={students} componentId={comp.id} onBulkSet={onBulkSet} />
              </DropdownMenuSubContent>
            </DropdownMenuSub>
          ))
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
