import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { X } from 'lucide-react';
import type { FacultyPlanningSection } from '../../types/facultyPlanning';

const STATUS_VARIANT: Record<string, 'default' | 'success' | 'destructive' | 'warning' | 'outline'> = {
  Unscheduled: 'outline',
  Scheduled: 'success',
  Conflicted: 'destructive',
  Partial: 'warning',
};

const STATUS_LABEL: Record<string, string> = {
  Unscheduled: 'Zamanlanmamis',
  Scheduled: 'Zamanlanmis',
  Conflicted: 'Cakismali',
  Partial: 'Kismi',
};

interface SectionDetailPanelProps {
  section: FacultyPlanningSection | null;
  onClose: () => void;
  onUnassign: (sectionId: number) => void;
  isUnassigning: boolean;
}

export function SectionDetailPanel({ section, onClose, onUnassign, isUnassigning }: SectionDetailPanelProps) {
  if (!section) {
    return (
      <div className="w-60 flex-shrink-0 border rounded-lg bg-muted/30 flex items-center justify-center">
        <p className="text-xs text-muted-foreground text-center px-4">
          Detay gormek icin takvimde bir ders secin
        </p>
      </div>
    );
  }

  const s = section;
  return (
    <div className="w-60 flex-shrink-0 border rounded-lg bg-muted/30 flex flex-col">
      <div className="p-3 border-b flex items-center justify-between">
        <h3 className="text-sm font-semibold">Ders Detayi</h3>
        <Button variant="ghost" size="icon" className="h-6 w-6" onClick={onClose}>
          <X className="h-3 w-3" />
        </Button>
      </div>
      <ScrollArea className="flex-1 p-3">
        <div className="space-y-3">
          <div>
            <span className="font-mono text-sm font-bold text-primary">{s.courseCode}</span>
            <p className="text-sm">{s.courseName}</p>
            <p className="text-xs text-muted-foreground">Sube {s.sectionIndex}</p>
          </div>

          <div className="space-y-1.5 text-xs">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Durum</span>
              <Badge variant={STATUS_VARIANT[s.status] ?? 'outline'} className="text-[10px]">
                {STATUS_LABEL[s.status] ?? s.status}
              </Badge>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Bolum</span>
              <span>{s.departmentCode}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Kapasite</span>
              <span>{s.capacity}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Yuklenme</span>
              <span>{s.theoryHours}T + {s.labHours}L</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Derslik</span>
              <span>{s.classroomName ?? '—'}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Hoca</span>
              <span className="text-right max-w-[120px] truncate">{s.instructorName ?? '—'}</span>
            </div>
            {s.scheduleSummary && (
              <div>
                <span className="text-muted-foreground">Program</span>
                <p className="mt-0.5 font-mono">{s.scheduleSummary}</p>
              </div>
            )}
          </div>

          {s.status !== 'Unscheduled' && (
            <Button
              variant="destructive"
              size="sm"
              className="w-full"
              disabled={isUnassigning}
              onClick={() => onUnassign(s.sectionId)}
            >
              <X className="h-3 w-3 mr-1" /> Atamayi Kaldir
            </Button>
          )}
        </div>
      </ScrollArea>
    </div>
  );
}
