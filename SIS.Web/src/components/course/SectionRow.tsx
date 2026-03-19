import { useState } from 'react';
import CapacityBar from './CapacityBar';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { PlusCircle, Check, Ban, Info, MapPin, Clock, Users, BookOpen } from 'lucide-react';
import { useSectionDetail } from '../../hooks/useSectionDetail';
import type { SectionDto } from '../../types/course';

interface SectionRowProps {
  section: SectionDto;
  onAddToPlan: (sectionId: number) => void;
  isAdding: boolean;
  isInPlan: boolean;
  isCourseRegistered: boolean;
  prerequisitesMet: boolean;
}

export default function SectionRow({ section, onAddToPlan, isAdding, isInPlan, isCourseRegistered, prerequisitesMet }: SectionRowProps) {
  const isFull = section.availableSeats <= 0;
  const [detailOpen, setDetailOpen] = useState(false);
  const { data: detail, isLoading: detailLoading } = useSectionDetail(detailOpen ? section.sectionId : null);

  return (
    <>
      <div className="flex items-center justify-between py-3 px-4 bg-muted/40 rounded-lg transition-colors hover:bg-muted/60">
        <div className="flex-1 grid grid-cols-1 sm:grid-cols-4 gap-3 items-center">
          <div>
            <div className="text-sm font-medium text-foreground">{section.instructorName}</div>
            {section.classroomName && (
              <div className="text-xs text-muted-foreground">{section.classroomName}</div>
            )}
          </div>
          <div className="text-sm text-muted-foreground">{section.schedule || 'Program belirtilmemiş'}</div>
          <CapacityBar enrolled={section.enrolledCount} capacity={section.capacity} />
          <div className="flex items-center justify-end gap-2">
            <Button
              size="icon"
              variant="ghost"
              className="h-8 w-8 text-muted-foreground hover:text-primary"
              onClick={() => setDetailOpen(true)}
              title="Detay"
            >
              <Info className="h-4 w-4" />
            </Button>
            {isInPlan ? (
              <Badge variant="success" className="gap-1">
                <Check className="h-3 w-3" />
                Planda
              </Badge>
            ) : isCourseRegistered ? (
              <Badge variant="secondary" className="gap-1">
                Başka şube seçili
              </Badge>
            ) : !prerequisitesMet ? (
              <Badge variant="warning" className="gap-1">
                <Ban className="h-3 w-3" />
                Onkosul eksik
              </Badge>
            ) : isFull ? (
              <Badge variant="destructive" className="gap-1">
                <Ban className="h-3 w-3" />
                Dolu
              </Badge>
            ) : (
              <Button size="sm" onClick={() => onAddToPlan(section.sectionId)} disabled={isAdding} className="gap-1.5">
                <PlusCircle className="h-3.5 w-3.5" />
                {isAdding ? '...' : 'Plana Ekle'}
              </Button>
            )}
          </div>
        </div>
      </div>

      {/* Section Detail Dialog */}
      <Dialog open={detailOpen} onOpenChange={setDetailOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Şube Detayı</DialogTitle>
            <DialogDescription>
              {detail ? `${detail.course.code} — ${detail.course.name}` : 'Yükleniyor...'}
            </DialogDescription>
          </DialogHeader>

          {detailLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-5 w-48" />
              <Skeleton className="h-5 w-36" />
              <Skeleton className="h-5 w-52" />
              <Skeleton className="h-5 w-40" />
            </div>
          ) : detail ? (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <DetailItem icon={BookOpen} label="Ders Kodu" value={detail.course.code} />
                <DetailItem icon={BookOpen} label="ECTS" value={`${detail.course.ects}`} />
                <DetailItem icon={Users} label="Öğretim Üyesi" value={detail.section.instructorName || '—'} />
                <DetailItem icon={MapPin} label="Sınıf" value={detail.section.classroomName || 'Belirtilmemiş'} />
                <DetailItem icon={Clock} label="Program" value={detail.section.schedule || 'Belirtilmemiş'} />
                <DetailItem icon={Users} label="Kontenjan" value={`${detail.section.enrolledCount} / ${detail.section.capacity}`} />
              </div>
              <div className="flex items-center gap-2 pt-2 border-t border-border">
                <span className="text-sm text-muted-foreground">Boş Yer:</span>
                <Badge variant={detail.section.availableSeats > 0 ? 'success' : 'destructive'}>
                  {detail.section.availableSeats}
                </Badge>
                <span className="text-sm text-muted-foreground ml-auto">Durum:</span>
                <Badge variant={detail.section.isActive ? 'success' : 'secondary'}>
                  {detail.section.isActive ? 'Aktif' : 'Pasif'}
                </Badge>
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}

function DetailItem({ icon: Icon, label, value }: { icon: React.ComponentType<{ className?: string }>; label: string; value: string }) {
  return (
    <div className="flex items-start gap-2">
      <Icon className="h-4 w-4 text-muted-foreground mt-0.5 shrink-0" />
      <div>
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="text-sm font-medium text-foreground">{value}</p>
      </div>
    </div>
  );
}
