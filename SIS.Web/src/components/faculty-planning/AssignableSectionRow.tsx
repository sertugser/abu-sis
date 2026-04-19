import { useState } from 'react';
import { ChevronDown, ChevronUp, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import type { AvailableClassroom } from '../../types/facultyPlanning';

interface SectionInfo {
  sectionId: number;
  courseCode: string;
  courseName: string;
  sectionIndex: number;
  instructorName: string | null;
  capacity: number;
  theoryHours: number;
  labHours: number;
}

interface AssignableSectionRowProps {
  section: SectionInfo;
  available: boolean;
  reason?: string;
  classrooms: AvailableClassroom[];
  classroomsLoading: boolean;
  onAssign: (sectionId: number, classroomId: number) => void;
  isAssigning: boolean;
}

export function AssignableSectionRow({
  section,
  available,
  reason,
  classrooms,
  classroomsLoading,
  onAssign,
  isAssigning,
}: AssignableSectionRowProps) {
  const [expanded, setExpanded] = useState(false);
  const [selectedClassroomId, setSelectedClassroomId] = useState<string>('');

  const handleConfirm = () => {
    const id = parseInt(selectedClassroomId, 10);
    if (!isNaN(id)) {
      onAssign(section.sectionId, id);
    }
  };

  if (!available) {
    return (
      <div className="flex items-center gap-3 px-3 py-2.5 opacity-50 cursor-not-allowed">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-mono font-semibold text-sm text-slate-600">
              {section.courseCode}
            </span>
            <Badge variant="outline" className="text-[10px]">
              Ş.{section.sectionIndex}
            </Badge>
          </div>
          <p className="text-xs text-slate-400 truncate">{section.courseName}</p>
        </div>
        {reason && (
          <span className="text-[11px] text-red-500 shrink-0 max-w-[140px] text-right leading-tight">
            {reason}
          </span>
        )}
      </div>
    );
  }

  return (
    <div className={cn(
      'border-b border-slate-100 last:border-0 transition-colors',
      expanded ? 'bg-blue-50/40' : 'hover:bg-slate-50/60',
    )}>
      {/* Clickable header */}
      <button
        type="button"
        className="w-full flex items-center gap-3 px-3 py-2.5 text-left"
        onClick={() => setExpanded(prev => !prev)}
      >
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-mono font-semibold text-sm text-slate-800">
              {section.courseCode}
            </span>
            <Badge variant="outline" className="text-[10px]">
              Ş.{section.sectionIndex}
            </Badge>
            <Badge variant="outline" className="text-[10px] text-slate-400">
              {section.theoryHours}T+{section.labHours}L
            </Badge>
          </div>
          <p className="text-xs text-slate-500 truncate">{section.courseName}</p>
          <div className="flex items-center gap-3 mt-0.5 text-[11px] text-slate-400">
            {section.instructorName && (
              <span className="truncate">{section.instructorName}</span>
            )}
            <span className="flex items-center gap-1 shrink-0">
              <Users className="h-3 w-3" />
              {section.capacity}
            </span>
          </div>
        </div>
        {expanded
          ? <ChevronUp className="h-4 w-4 text-slate-400 shrink-0" />
          : <ChevronDown className="h-4 w-4 text-slate-400 shrink-0" />
        }
      </button>

      {/* Expanded — classroom selector */}
      {expanded && (
        <div className="px-3 pb-3 space-y-2">
          <Select
            value={selectedClassroomId}
            onValueChange={setSelectedClassroomId}
            disabled={classroomsLoading}
          >
            <SelectTrigger className="h-8 text-sm">
              <SelectValue
                placeholder={classroomsLoading ? 'Derslikler yükleniyor…' : 'Derslik seç'}
              />
            </SelectTrigger>
            <SelectContent>
              {classrooms.length === 0 && !classroomsLoading && (
                <div className="px-3 py-2 text-sm text-slate-400">Uygun derslik yok</div>
              )}
              {classrooms.map(cr => (
                <SelectItem key={cr.classroomId} value={String(cr.classroomId)}>
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{cr.name}</span>
                    <span className="text-xs text-slate-400">
                      {cr.type} · {cr.capacity} kişi
                    </span>
                  </div>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            size="sm"
            className="w-full h-8 text-[12px] bg-[#003366] hover:bg-[#002244]"
            disabled={!selectedClassroomId || isAssigning}
            onClick={handleConfirm}
          >
            {isAssigning ? 'Atanıyor…' : 'Onayla'}
          </Button>
        </div>
      )}
    </div>
  );
}
