import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import type { OpenedCourseSectionDto } from '../../../types/courseOpening';
import { semesterToClassLevel } from '../types';

interface SectionDetailDialogProps {
  section: OpenedCourseSectionDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Read-only şube detay görüntüleme. */
export function SectionDetailDialog({ section, open, onOpenChange }: SectionDetailDialogProps) {
  if (!section) return null;
  const ratio = section.capacity > 0 ? Math.round((section.enrolledCount / section.capacity) * 100) : 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="font-mono">
            {section.courseCode} — Şube {section.sectionNumber}
          </DialogTitle>
          <DialogDescription>{section.courseName}</DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-3 gap-3 mt-2">
          <Cell label="Sınıf" value={semesterToClassLevel(section.suggestedSemester) || '—'} />
          <Cell label="Kredi" value={section.credits} />
          <Cell label="AKTS" value={section.ects} />
          <Cell label="Teori Saat" value={section.theoryHours} />
          <Cell label="Lab Saat" value={section.labHours} />
          <Cell label="Durum" value={section.isActive ? 'Aktif' : 'Pasif'} />
          <Cell label="Kontenjan" value={section.capacity} />
          <Cell label="İç Kota" value={section.internalQuota ?? '—'} />
          <Cell label="Dış Kota" value={section.externalQuota ?? '—'} />
          <Cell label="Kayıtlı" value={section.enrolledCount} />
          <Cell label="Beklemede" value={section.draftOrPendingCount} />
          <Cell label="Doluluk" value={`%${ratio}`} />
          <Cell label="Cross-list Grup" value={section.crossListGroupId ?? '—'} />
          <Cell label="Hoca" value={section.instructorName ?? '— Atanmamış —'} fullWidth />
        </div>
      </DialogContent>
    </Dialog>
  );
}

function Cell({ label, value, fullWidth }: { label: string; value: string | number; fullWidth?: boolean }) {
  return (
    <div className={`p-3 bg-muted/30 rounded-md ${fullWidth ? 'col-span-3' : ''}`}>
      <div className="text-[10px] text-muted-foreground uppercase tracking-wide">{label}</div>
      <div className="text-sm font-medium mt-0.5">{value}</div>
    </div>
  );
}
