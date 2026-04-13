import { Badge } from '@/components/ui/badge';
import { CheckCircle2 } from 'lucide-react';
import type { AutoSchedulePreviewItem } from '../../../types/facultyPlanning';

interface SuccessDetailProps {
  item: AutoSchedulePreviewItem;
  isChecked: boolean;
  onToggleCheck: () => void;
}

export function SuccessDetail({ item, isChecked, onToggleCheck }: SuccessDetailProps) {
  return (
    <div className="p-4 h-full overflow-y-auto">
      <div className="grid grid-cols-2 gap-2">
        {/* R1C1: Ders bilgisi */}
        <div className="bg-muted/50 rounded-lg p-3">
          <div className="text-base font-bold text-foreground">{item.courseCode}-{String(item.sectionNumber).padStart(2, '0')}</div>
          <div className="text-xs text-muted-foreground">{item.courseName}</div>
          <div className="text-[10px] text-muted-foreground/70 mt-1">{item.departmentCode}</div>
        </div>

        {/* R1C2: Durum + Fit Score */}
        <div className="bg-muted/50 rounded-lg p-3 flex justify-between items-center">
          <div>
            <Badge variant="success" className="text-[10px] gap-1">
              <CheckCircle2 className="h-3 w-3" />
              Başarılı
            </Badge>
          </div>
          <div className="text-right">
            <div className="text-[10px] text-muted-foreground">Fit Score</div>
            <div className="text-lg font-bold text-emerald-600">{item.fitScore}</div>
          </div>
        </div>

        {/* R2C1: Derslik */}
        <div className="bg-muted/50 rounded-lg p-3">
          <div className="text-[10px] text-muted-foreground uppercase tracking-wider">Derslik</div>
          <div className="text-sm font-semibold text-foreground mt-1">{item.classroomName}</div>
        </div>

        {/* R2C2: Program */}
        <div className="bg-muted/50 rounded-lg p-3">
          <div className="text-[10px] text-muted-foreground uppercase tracking-wider">Program</div>
          <div className="text-sm font-semibold text-foreground mt-1">{item.scheduleSummary}</div>
        </div>

        {/* R3C1: Öğretim Üyesi */}
        <div className="bg-muted/50 rounded-lg p-3">
          <div className="text-[10px] text-muted-foreground uppercase tracking-wider">Öğretim Üyesi</div>
          <div className="text-sm font-semibold text-foreground mt-1">{item.instructorName ?? 'Atanmamış'}</div>
        </div>

        {/* R3C2: Slot detayı */}
        <div className="bg-muted/50 rounded-lg p-3">
          <div className="text-[10px] text-muted-foreground uppercase tracking-wider">Slot Sayısı</div>
          <div className="text-sm font-semibold text-foreground mt-1">{item.scheduleSlots.length} blok</div>
        </div>

        {/* R4: Checkbox — full width */}
        <div className="col-span-2">
          <button
            type="button"
            onClick={onToggleCheck}
            className={`w-full flex items-center gap-2 px-4 py-3 rounded-lg border transition-colors ${
              isChecked
                ? 'bg-indigo-500/10 border-indigo-500/30'
                : 'bg-muted/50 border-border/50'
            }`}
          >
            <input
              type="checkbox"
              checked={isChecked}
              readOnly
              className="rounded accent-indigo-600"
            />
            <span className="text-sm text-foreground">Bu atamayı uygula</span>
          </button>
        </div>
      </div>
    </div>
  );
}
