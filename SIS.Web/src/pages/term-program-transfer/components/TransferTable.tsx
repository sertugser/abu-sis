import { CheckSquare, Square, Search, Settings2, Link2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { PreviousTermCourseDto, InstructorOptionDto } from '../../../types/courseOpening';
import type { CourseOverride } from '../types';

interface TransferTableProps {
  courses: PreviousTermCourseDto[];
  selectedIds: Set<number>;
  onToggle: (course: PreviousTermCourseDto) => void;
  getOverride: (course: PreviousTermCourseDto) => CourseOverride;
  onUpdateOverride: <K extends keyof CourseOverride>(
    courseId: number,
    field: K,
    value: CourseOverride[K],
  ) => void;
  instructors: InstructorOptionDto[];
  maxSections: number;
}

function formatOccupancy(enrolled: number, capacity: number): { ratio: number; color: string } {
  if (capacity === 0) return { ratio: 0, color: 'text-muted-foreground' };
  const ratio = Math.round((enrolled / capacity) * 100);
  const color = ratio >= 90 ? 'text-red-600' : ratio >= 70 ? 'text-amber-600' : 'text-emerald-600';
  return { ratio, color };
}

export function TransferTable({
  courses,
  selectedIds,
  onToggle,
  getOverride,
  onUpdateOverride,
  instructors,
  maxSections,
}: TransferTableProps) {
  return (
    <Card>
      <CardContent className="p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[1100px]">
            <thead className="bg-muted/50">
              <tr>
                <th className="p-3 w-10" />
                <th className="p-3 text-left font-medium text-xs">Ders Kodu</th>
                <th className="p-3 text-left font-medium text-xs">Ders Adı</th>
                <th className="p-3 text-center font-medium text-xs">AKTS</th>
                <th className="p-3 text-center font-medium text-xs w-24">Doluluk</th>
                <th className="p-3 text-center font-medium text-xs w-20">Tahmin</th>
                <th className="p-3 text-center font-medium text-xs w-20">Şube</th>
                <th className="p-3 text-center font-medium text-xs w-24">Kontenjan</th>
                <th className="p-3 text-left font-medium text-xs w-44">Hoca</th>
                <th className="p-3 text-center font-medium text-xs w-28">Ek Ayarlar</th>
                <th className="p-3 text-center font-medium text-xs">Durum</th>
              </tr>
            </thead>
            <tbody>
              {courses.map((c) => {
                const sel = selectedIds.has(c.courseId);
                const ov = getOverride(c);
                const occ = formatOccupancy(c.totalEnrolled, c.totalCapacity);
                return (
                  <tr
                    key={c.courseId}
                    className={`border-t transition-colors ${c.alreadyOpened ? 'opacity-50' : 'hover:bg-muted/30'} ${sel ? 'bg-blue-50 dark:bg-blue-950/20' : ''}`}
                  >
                    <td className="p-3 text-center cursor-pointer" onClick={() => onToggle(c)}>
                      {sel ? (
                        <CheckSquare className="h-4 w-4 text-primary mx-auto" />
                      ) : (
                        <Square className="h-4 w-4 text-muted-foreground mx-auto" />
                      )}
                    </td>
                    <td className="p-3 font-mono font-medium cursor-pointer" onClick={() => onToggle(c)}>
                      <div className="flex items-center gap-1.5">
                        {c.courseCode}
                        {c.hasCrossLists && (
                          <Link2 className="h-3 w-3 text-blue-500" aria-label="Cross-list bağlı ders" />
                        )}
                      </div>
                    </td>
                    <td className="p-3 cursor-pointer" onClick={() => onToggle(c)}>
                      {c.courseName}
                    </td>
                    <td className="p-3 text-center cursor-pointer" onClick={() => onToggle(c)}>
                      {c.ects}
                    </td>
                    <td className="p-3 text-center cursor-pointer" onClick={() => onToggle(c)}>
                      <div className={`text-xs font-medium ${occ.color}`}>
                        {c.totalEnrolled}/{c.totalCapacity}
                      </div>
                      <div className="text-[10px] text-muted-foreground">%{occ.ratio}</div>
                    </td>
                    <td className="p-3 text-center cursor-pointer" onClick={() => onToggle(c)}>
                      {c.predictedDemand != null ? (
                        <span className="text-xs font-medium">{c.predictedDemand}</span>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="p-3 text-center">
                      {c.alreadyOpened ? (
                        c.sectionCount
                      ) : (
                        <Input
                          type="number"
                          min={1}
                          max={maxSections}
                          value={ov.sectionCount}
                          onChange={(e) =>
                            onUpdateOverride(
                              c.courseId,
                              'sectionCount',
                              Math.max(1, Math.min(maxSections, Number(e.target.value) || 1)),
                            )
                          }
                          className="h-7 w-16 text-center text-xs mx-auto"
                        />
                      )}
                    </td>
                    <td className="p-3 text-center">
                      {c.alreadyOpened ? (
                        c.totalCapacity
                      ) : (
                        <Input
                          type="number"
                          min={1}
                          value={ov.capacityPerSection}
                          onChange={(e) =>
                            onUpdateOverride(c.courseId, 'capacityPerSection', Math.max(1, Number(e.target.value) || 1))
                          }
                          className="h-7 w-20 text-center text-xs mx-auto"
                        />
                      )}
                    </td>
                    <td className="p-3">
                      {c.alreadyOpened ? (
                        <span className="text-xs text-muted-foreground">{c.instructorName ?? '—'}</span>
                      ) : (
                        <Select
                          value={ov.instructorId != null ? String(ov.instructorId) : 'none'}
                          onValueChange={(v) =>
                            onUpdateOverride(c.courseId, 'instructorId', v === 'none' ? null : Number(v))
                          }
                        >
                          <SelectTrigger className="h-7 w-40 text-xs">
                            <SelectValue placeholder="Hoca seçin" />
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
                      )}
                    </td>
                    <td className="p-3 text-center">
                      {c.alreadyOpened ? (
                        <span className="text-xs text-muted-foreground">—</span>
                      ) : (
                        <Popover>
                          <PopoverTrigger asChild>
                            <Button variant="ghost" size="sm" className="h-7 px-2 text-xs gap-1.5">
                              <Settings2 className="h-3.5 w-3.5" />
                              İleri
                            </Button>
                          </PopoverTrigger>
                          <PopoverContent className="w-72 space-y-3" align="end">
                            <div className="space-y-1">
                              <Label className="text-xs">İç Kontenjan (Bölüm içi)</Label>
                              <Input
                                type="number"
                                min={0}
                                placeholder={c.sourceInternalQuota?.toString() ?? '—'}
                                value={ov.internalQuota ?? ''}
                                onChange={(e) =>
                                  onUpdateOverride(
                                    c.courseId,
                                    'internalQuota',
                                    e.target.value === '' ? null : Math.max(0, Number(e.target.value)),
                                  )
                                }
                                className="h-8 text-xs"
                              />
                            </div>
                            <div className="space-y-1">
                              <Label className="text-xs">Dış Kontenjan (Diğer bölümler)</Label>
                              <Input
                                type="number"
                                min={0}
                                placeholder={c.sourceExternalQuota?.toString() ?? '—'}
                                value={ov.externalQuota ?? ''}
                                onChange={(e) =>
                                  onUpdateOverride(
                                    c.courseId,
                                    'externalQuota',
                                    e.target.value === '' ? null : Math.max(0, Number(e.target.value)),
                                  )
                                }
                                className="h-8 text-xs"
                              />
                            </div>
                            {c.hasCrossLists && (
                              <div className="flex items-start gap-2 pt-1 border-t">
                                <input
                                  type="checkbox"
                                  id={`crosslist-${c.courseId}`}
                                  checked={ov.includeCrossLists}
                                  onChange={(e) =>
                                    onUpdateOverride(c.courseId, 'includeCrossLists', e.target.checked)
                                  }
                                  className="mt-0.5 h-4 w-4 cursor-pointer"
                                />
                                <div className="space-y-0.5">
                                  <Label htmlFor={`crosslist-${c.courseId}`} className="text-xs cursor-pointer">
                                    Cross-list dersleri de aç
                                  </Label>
                                  <p className="text-[10px] text-muted-foreground">
                                    Bağlı ders kodları için paralel şube oluşturulur
                                  </p>
                                </div>
                              </div>
                            )}
                            {c.sourceAccessibleDepartmentIds && c.sourceAccessibleDepartmentIds.length > 0 && (
                              <div className="text-[10px] text-muted-foreground border-t pt-2">
                                Kaynak dönem erişim politikası: {c.sourceAccessibleDepartmentIds.length} bölüm
                              </div>
                            )}
                          </PopoverContent>
                        </Popover>
                      )}
                    </td>
                    <td className="p-3 text-center cursor-pointer" onClick={() => onToggle(c)}>
                      {c.alreadyOpened ? (
                        <Badge variant="secondary" className="text-xs">
                          Zaten Açık
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="text-green-600 border-green-400 text-xs">
                          Aktarılabilir
                        </Badge>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {courses.length === 0 && (
          <div className="py-12 text-center">
            <Search className="h-8 w-8 text-muted-foreground/30 mx-auto mb-3" />
            <p className="text-muted-foreground text-sm">Aramanızla eşleşen ders bulunamadı.</p>
            <p className="text-xs text-muted-foreground/60 mt-1">Filtrelerinizi değiştirmeyi deneyin.</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
