import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { PreviousTermCourseDto, InstructorOptionDto } from '../../../types/courseOpening';
import type { CourseOverride } from '../types';
import { semesterToClassLevel } from '../types';

interface RowEditSheetProps {
  course: PreviousTermCourseDto | null;
  override: CourseOverride | null;
  instructors: InstructorOptionDto[];
  maxSections: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdate: <K extends keyof CourseOverride>(field: K, value: CourseOverride[K]) => void;
}

/**
 * Ortada açılan modal (dialog). Kullanıcı bir dersi düzenleyince burası açılır.
 * Sheet (yan drawer) yerine modal tercih edildi.
 */
export function RowEditSheet({ course, override, instructors, maxSections, open, onOpenChange, onUpdate }: RowEditSheetProps) {
  if (!course || !override) return null;

  const occupancyRatio = course.totalCapacity > 0 ? Math.round((course.totalEnrolled / course.totalCapacity) * 100) : 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="font-mono">{course.courseCode}</DialogTitle>
          <DialogDescription>{course.courseName}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Read-only bilgi paneli */}
          <div className="grid grid-cols-3 gap-2 p-3 bg-muted/30 rounded-md text-xs">
            <InfoCell label="Türü" value={course.isCompulsory ? 'Zorunlu' : 'Seçmeli'} />
            <InfoCell label="Sınıf" value={semesterToClassLevel(course.suggestedSemester) || '—'} />
            <InfoCell label="AKTS" value={course.ects} />
            <InfoCell label="Kredi" value={course.credits} />
            <InfoCell
              label="Geçen Dönem Doluluk"
              value={`${course.totalEnrolled}/${course.totalCapacity} (%${occupancyRatio})`}
            />
            <InfoCell
              label="Tahmin Edilen Talep"
              value={course.predictedDemand != null ? course.predictedDemand : '—'}
            />
          </div>

          {/* Aksiyon alanları */}
          <div className="space-y-3">
            <h4 className="text-sm font-semibold text-muted-foreground border-b pb-1">Aktarım Ayarları</h4>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Şube Sayısı</Label>
                <Input
                  type="number"
                  min={1}
                  max={maxSections}
                  value={override.sectionCount}
                  onChange={(e) =>
                    onUpdate(
                      'sectionCount',
                      Math.max(1, Math.min(maxSections, Number(e.target.value) || 1)),
                    )
                  }
                  className="h-9"
                />
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Şube Başı Kontenjan</Label>
                <Input
                  type="number"
                  min={1}
                  value={override.capacityPerSection}
                  onChange={(e) => onUpdate('capacityPerSection', Math.max(1, Number(e.target.value) || 1))}
                  className="h-9"
                />
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Öğretim Üyesi</Label>
                <Select
                  value={override.instructorId != null ? String(override.instructorId) : 'none'}
                  onValueChange={(v) => onUpdate('instructorId', v === 'none' ? null : Number(v))}
                >
                  <SelectTrigger className="h-9">
                    <SelectValue placeholder="Hoca" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">— Atanmamış —</SelectItem>
                    {instructors.map((inst) => (
                      <SelectItem key={inst.instructorId} value={String(inst.instructorId)}>
                        {inst.fullName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          {/* İleri ayarlar */}
          <div className="space-y-3">
            <h4 className="text-sm font-semibold text-muted-foreground border-b pb-1">İleri Ayarlar</h4>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">İç Kontenjan</Label>
                <Input
                  type="number"
                  min={0}
                  placeholder={course.sourceInternalQuota?.toString() ?? '—'}
                  value={override.internalQuota ?? ''}
                  onChange={(e) =>
                    onUpdate(
                      'internalQuota',
                      e.target.value === '' ? null : Math.max(0, Number(e.target.value)),
                    )
                  }
                  className="h-9"
                />
                <p className="text-[10px] text-muted-foreground">Bölüm içi öğrenciler</p>
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Dış Kontenjan</Label>
                <Input
                  type="number"
                  min={0}
                  placeholder={course.sourceExternalQuota?.toString() ?? '—'}
                  value={override.externalQuota ?? ''}
                  onChange={(e) =>
                    onUpdate(
                      'externalQuota',
                      e.target.value === '' ? null : Math.max(0, Number(e.target.value)),
                    )
                  }
                  className="h-9"
                />
                <p className="text-[10px] text-muted-foreground">Diğer bölümler</p>
              </div>
            </div>

            {course.hasCrossLists && (
              <div className="flex items-start gap-2 p-3 bg-blue-50 dark:bg-blue-950/20 rounded-md border border-blue-200 dark:border-blue-900">
                <input
                  type="checkbox"
                  id="crosslist-edit"
                  checked={override.includeCrossLists}
                  onChange={(e) => onUpdate('includeCrossLists', e.target.checked)}
                  className="mt-0.5 h-4 w-4 cursor-pointer"
                />
                <div className="space-y-0.5">
                  <Label htmlFor="crosslist-edit" className="text-xs cursor-pointer font-medium">
                    Cross-list dersleri de aç
                  </Label>
                  <p className="text-[10px] text-muted-foreground">
                    Bu derse bağlı diğer ders kodları için paralel şube oluşturulur (aynı hoca, aynı kontenjan).
                  </p>
                </div>
              </div>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function InfoCell({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="space-y-0.5">
      <div className="text-[10px] text-muted-foreground uppercase tracking-wide">{label}</div>
      <div className="text-sm font-medium">{value}</div>
    </div>
  );
}
