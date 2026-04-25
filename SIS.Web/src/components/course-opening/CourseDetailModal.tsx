import React, { useState, useEffect } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Users, BookOpen, TrendingUp, Layers, AlertTriangle, BarChart3, ArrowRight, Target } from 'lucide-react';
import type { CurriculumCourseForOpeningDto, InstructorOptionDto } from '../../types/courseOpening';
import type { CourseDemandForecastDto } from '../../types/forecast';

interface CourseDetailModalProps {
  open: boolean;
  onClose: () => void;
  course: CurriculumCourseForOpeningDto;
  forecast: CourseDemandForecastDto | undefined;
  instructors: InstructorOptionDto[];
  onOpenCourse: (request: {
    courseId: number;
    sectionCount: number;
    capacityPerSection: number;
    instructorId?: number;
  }) => void;
  isOpening: boolean;
}

export function CourseDetailModal({
  open,
  onClose,
  course,
  forecast,
  instructors,
  onOpenCourse,
  isOpening,
}: CourseDetailModalProps) {
  const [sectionCount, setSectionCount] = useState(forecast?.recommendedSectionCount ?? 1);
  const [capacity, setCapacity] = useState(
    forecast?.sectionDistribution?.effectiveCapacityPerSection ?? 40,
  );
  const [instructorId, setInstructorId] = useState<number | undefined>(undefined);

  React.useEffect(() => {
    setSectionCount(forecast?.recommendedSectionCount ?? 1);
    setCapacity(forecast?.sectionDistribution?.effectiveCapacityPerSection ?? 40);
    setInstructorId(undefined);
  }, [course.courseId, forecast]);

  const bd = forecast?.demandBreakdown;
  const sd = forecast?.sectionDistribution;
  const conf = forecast?.confidence;

  const confRange = conf ? conf.upperBound - conf.lowerBound : 0;
  const confLowerPct = conf && confRange > 0
    ? ((conf.lowerBound - conf.lowerBound) / confRange) * 100
    : 0;
  const confUpperPct = 100;
  void confLowerPct;
  void confUpperPct;

  // Max historical enrollment for chart scaling
  const maxHistorical = forecast
    ? Math.max(
        ...(forecast.historicalEnrollments.map((h) => h.actualEnrollment) ?? [0]),
        forecast.totalPredictedDemand,
        1,
      )
    : 1;

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose(); }}>
      <ResizableDialogContent
        defaultWidth={720}
        defaultHeight={680}
        minWidth={560}
        minHeight={480}
        className="flex flex-col"
      >
        <DialogTitle className="sr-only">
          {course.courseCode} — {course.courseName}
        </DialogTitle>

        {/* ── Header ── */}
        <div className="shrink-0 pb-3 border-b">
          <div className="flex items-start gap-3 pr-16">
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="font-mono font-bold text-[#003366] text-base">
                  {course.courseCode}
                </span>
                <span className="font-semibold text-sm truncate">{course.courseName}</span>
                <Badge
                  variant={course.isCompulsory ? 'default' : 'outline'}
                  className="text-[10px] shrink-0"
                >
                  {course.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                </Badge>
                {course.alreadyOpened ? (
                  <Badge variant="success" className="text-[10px] shrink-0">
                    Açıldı ({course.existingSectionCount} şube)
                  </Badge>
                ) : (
                  <Badge variant="outline" className="text-[10px] shrink-0 text-amber-600 border-amber-300">
                    Açılmadı
                  </Badge>
                )}
              </div>
              <div className="flex items-center gap-3 mt-1">
                <span className="text-xs text-muted-foreground">{course.ects} AKTS</span>
                <span className="text-xs text-muted-foreground">
                  {course.theoryHours}+{course.labHours} saat
                </span>
                {course.suggestedSemester > 0 && (
                  <span className="text-xs text-muted-foreground">
                    {Math.ceil(course.suggestedSemester / 2)}. yıl{' '}
                    {course.suggestedSemester % 2 === 1 ? 'Güz' : 'Bahar'}
                  </span>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* ── Scrollable body ── */}
        <div className="flex-1 overflow-y-auto py-4 space-y-5 min-h-0">
          {/* 1. Summary cards */}
          {forecast ? (
            <div className="grid grid-cols-4 gap-2">
              <StatCard
                icon={<Users className="h-4 w-4" />}
                label="Toplam Talep"
                value={forecast.totalPredictedDemand}
                color="blue"
              />
              <StatCard
                icon={<BookOpen className="h-4 w-4" />}
                label="İlk Kez Alanlar"
                value={forecast.predictedEligibleCount}
                color="emerald"
              />
              <StatCard
                icon={<TrendingUp className="h-4 w-4" />}
                label="Tekrar Alanlar"
                value={forecast.predictedRetakeCount}
                sub={bd ? `FF:${bd.retakeFF} NA:${bd.retakeNA} U:${bd.retakeU}` : undefined}
                color="red"
              />
              <StatCard
                icon={<Layers className="h-4 w-4" />}
                label="Önerilen Şube"
                value={forecast.recommendedSectionCount}
                sub={
                  sd
                    ? `${sd.effectiveCapacityPerSection} kişi/şube`
                    : undefined
                }
                color="violet"
              />
            </div>
          ) : (
            <div className="text-sm text-muted-foreground text-center py-4">
              Bu ders için tahmin verisi bulunamadı.
            </div>
          )}

          {/* 2. Demand breakdown */}
          {bd && (
            <div>
              <div className="flex items-center gap-1.5 mb-2">
                <BarChart3 className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                  Talep Dağılımı
                </span>
              </div>
              <div className="grid grid-cols-4 gap-2">
                <MiniStat label="Zamanında Alan" value={bd.regularOnSchedule} />
                <MiniStat label="Gecikmeli Alan" value={bd.behindSchedule} />
                <MiniStat label="Üst Dönemden" value={bd.upperCourse} />
                <MiniStat label="AKTS Kısıtlı" value={bd.ectsLimited} />
              </div>
            </div>
          )}

          {/* Show Rate */}
          {forecast && forecast.showRate > 0 && forecast.showRate < 1 && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 text-xs text-blue-800 flex items-center gap-2">
              <Target className="h-4 w-4 shrink-0" />
              <span>
                Geçmiş verilere göre uygun öğrencilerin <strong>%{Math.round(forecast.showRate * 100)}</strong>'i kayıt oluyor.
                Tahmin bu orana göre düzeltilmiştir.
              </span>
            </div>
          )}

          {/* 3. Confidence range */}
          {conf && (
            <div>
              <div className="flex items-center gap-1.5 mb-2">
                <ArrowRight className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                  Güven Aralığı
                </span>
                <span className="text-[10px] text-muted-foreground ml-auto">
                  Yöntem: {conf.method}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-mono w-10 text-right">{conf.lowerBound}</span>
                <div className="flex-1 relative h-4 bg-gray-100 rounded-full overflow-hidden">
                  {forecast && (
                    <>
                      {/* Confidence band */}
                      <div
                        className="absolute top-0 bottom-0 bg-blue-200 rounded-full"
                        style={{
                          left: `${(conf.lowerBound / maxHistorical) * 100}%`,
                          right: `${100 - (conf.upperBound / maxHistorical) * 100}%`,
                        }}
                      />
                      {/* Total demand marker */}
                      <div
                        className="absolute top-0 bottom-0 w-0.5 bg-blue-600"
                        style={{
                          left: `${(forecast.totalPredictedDemand / maxHistorical) * 100}%`,
                        }}
                      />
                    </>
                  )}
                </div>
                <span className="text-xs font-mono w-10">{conf.upperBound}</span>
                <span className="text-xs text-muted-foreground">±{conf.margin}</span>
              </div>
            </div>
          )}

          {/* 4. Historical enrollments chart */}
          {forecast && forecast.historicalEnrollments.length > 0 && (
            <div>
              <div className="flex items-center gap-1.5 mb-2">
                <BarChart3 className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                  Geçmiş Kayıtlar
                </span>
              </div>
              <div className="flex items-end gap-1.5 h-24">
                {forecast.historicalEnrollments.map((h) => (
                  <div key={h.termId} className="flex flex-col items-center gap-0.5 flex-1 min-w-0">
                    <span className="text-[9px] text-muted-foreground font-mono">
                      {h.actualEnrollment}
                    </span>
                    <div
                      className="w-full bg-blue-400 rounded-t"
                      style={{
                        height: `${Math.round((h.actualEnrollment / maxHistorical) * 80)}px`,
                        minHeight: 2,
                      }}
                    />
                    <span
                      className="text-[9px] text-muted-foreground truncate w-full text-center"
                      title={h.termName}
                    >
                      {h.termName.slice(0, 6)}
                    </span>
                  </div>
                ))}
                {/* Current forecast bar — dashed style */}
                <div className="flex flex-col items-center gap-0.5 flex-1 min-w-0">
                  <span className="text-[9px] text-violet-600 font-mono font-bold">
                    {forecast.totalPredictedDemand}
                  </span>
                  <div
                    className="w-full rounded-t border-2 border-dashed border-violet-500 bg-violet-100"
                    style={{
                      height: `${Math.round((forecast.totalPredictedDemand / maxHistorical) * 80)}px`,
                      minHeight: 2,
                    }}
                  />
                  <span className="text-[9px] text-violet-600 truncate w-full text-center">
                    Tahmin
                  </span>
                </div>
              </div>
            </div>
          )}

          {/* 5. Section distribution */}
          {sd && (
            <div>
              <div className="flex items-center gap-1.5 mb-2">
                <Layers className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                  Şube Dağılımı Önerisi
                </span>
              </div>
              <div className="grid grid-cols-3 gap-2 mb-2">
                <MiniStat label="Şube Sayısı" value={sd.recommendedSections} />
                <MiniStat
                  label="Öğrenci/Şube"
                  value={`${Math.round(sd.studentsPerSection?.reduce((a, b) => a + b, 0) / (sd.studentsPerSection?.length || 1))} ort.`}
                />
                <MiniStat
                  label="Kullanım Oranı"
                  value={`%${Math.round(sd.utilizationRate * 100)}`}
                />
              </div>
              {sd.studentsPerSection && sd.studentsPerSection.length > 0 && (
                <div className="flex flex-wrap gap-1">
                  {sd.studentsPerSection.map((count, i) => (
                    <span
                      key={i}
                      className="text-[10px] px-2 py-0.5 rounded-full bg-violet-100 text-violet-700 font-mono"
                    >
                      Şube {i + 1}: {count}
                    </span>
                  ))}
                </div>
              )}
              {sd.warnings && sd.warnings.length > 0 && (
                <div className="mt-2 space-y-1">
                  {sd.warnings.map((w, i) => (
                    <div
                      key={i}
                      className="flex items-start gap-1.5 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded px-2 py-1"
                    >
                      <AlertTriangle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                      <span>{w}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* 6. Cross-department demand */}
          {forecast?.crossDepartmentDemand && forecast.crossDepartmentDemand.length > 0 && (
            <div>
              <div className="flex items-center gap-1.5 mb-2">
                <Users className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                  Bölümler Arası Talep
                </span>
              </div>
              <div className="space-y-1">
                {forecast.crossDepartmentDemand.map((cd, i) => (
                  <div
                    key={i}
                    className="flex items-center justify-between text-xs px-2 py-1 rounded bg-sky-50"
                  >
                    <span className="text-sky-800 font-medium">{cd.departmentName}</span>
                    <span className="font-mono text-sky-700">{cd.estimatedStudentCount} öğrenci</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* 7. Notes / warnings */}
          {forecast?.notes && forecast.notes.length > 0 && (
            <div className="space-y-1">
              {forecast.notes.map((note, i) => (
                <div
                  key={i}
                  className="flex items-start gap-1.5 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded px-2 py-1.5"
                >
                  <AlertTriangle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                  <span>{note}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* ── Footer: course opening form ── */}
        {!course.alreadyOpened && (
          <div className="shrink-0 border-t pt-3 space-y-3">
            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Şube Sayısı</Label>
                <Input
                  type="number"
                  min={1}
                  max={20}
                  value={sectionCount}
                  onChange={(e) => setSectionCount(Math.max(1, parseInt(e.target.value) || 1))}
                  className="h-8 text-sm"
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Kontenjan (şube başı)</Label>
                <Input
                  type="number"
                  min={1}
                  max={999}
                  value={capacity}
                  onChange={(e) => setCapacity(Math.max(1, parseInt(e.target.value) || 1))}
                  className="h-8 text-sm"
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs">Öğretim Elemanı</Label>
                <Select
                  value={instructorId?.toString() ?? 'none'}
                  onValueChange={(v) =>
                    setInstructorId(v === 'none' ? undefined : parseInt(v))
                  }
                >
                  <SelectTrigger className="h-8 text-sm">
                    <SelectValue placeholder="Seçiniz..." />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Atanmadı</SelectItem>
                    {instructors.map((ins) => (
                      <SelectItem key={ins.instructorId} value={ins.instructorId.toString()}>
                        {ins.fullName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex justify-end">
              <Button
                size="sm"
                disabled={isOpening}
                onClick={() =>
                  onOpenCourse({
                    courseId: course.courseId,
                    sectionCount,
                    capacityPerSection: capacity,
                    instructorId,
                  })
                }
              >
                {isOpening ? 'Açılıyor...' : 'Bu Dersi Aç'}
              </Button>
            </div>
          </div>
        )}
      </ResizableDialogContent>
    </Dialog>
  );
}

// ── Inline helper components ──

interface StatCardProps {
  icon: React.ReactNode;
  label: string;
  value: number | string;
  sub?: string;
  color: 'blue' | 'emerald' | 'red' | 'violet';
}

const colorMap: Record<StatCardProps['color'], { bg: string; text: string; iconBg: string }> = {
  blue: { bg: 'bg-blue-50', text: 'text-blue-700', iconBg: 'bg-blue-100' },
  emerald: { bg: 'bg-emerald-50', text: 'text-emerald-700', iconBg: 'bg-emerald-100' },
  red: { bg: 'bg-red-50', text: 'text-red-700', iconBg: 'bg-red-100' },
  violet: { bg: 'bg-violet-50', text: 'text-violet-700', iconBg: 'bg-violet-100' },
};

function StatCard({ icon, label, value, sub, color }: StatCardProps) {
  const c = colorMap[color];
  return (
    <div className={`rounded-lg p-3 ${c.bg}`}>
      <div className={`inline-flex p-1.5 rounded-md mb-1.5 ${c.iconBg} ${c.text}`}>{icon}</div>
      <div className={`text-xl font-bold ${c.text}`}>{value}</div>
      <div className="text-[11px] text-muted-foreground">{label}</div>
      {sub && <div className={`text-[10px] font-mono mt-0.5 ${c.text}`}>{sub}</div>}
    </div>
  );
}

interface MiniStatProps {
  label: string;
  value: number | string;
}

function MiniStat({ label, value }: MiniStatProps) {
  return (
    <div className="rounded-md border px-3 py-2 text-center">
      <div className="text-sm font-semibold">{value}</div>
      <div className="text-[10px] text-muted-foreground">{label}</div>
    </div>
  );
}
