// SIS.Web/src/components/faculty-planning/ScheduleResultModal.tsx
import { useMemo } from 'react';
import { Dialog, DialogContent } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  CheckCircle2, XCircle, AlertTriangle,
  Clock, Building2, Calendar,
} from 'lucide-react';
import type { AutoSchedulePreview } from '../../types/facultyPlanning';
import { cn } from '@/lib/utils';

const FAILURE_LABELS: Record<string, string> = {
  CapacityIncrease: 'Kapasite yetersiz',
  ClassroomAdd: 'Uygun derslik yok',
  TimeChange: 'Zaman çakışması',
  InstructorReassign: 'Hoca çakışması',
  ConstraintRelax: 'Kısıt ihlali',
};

interface ScheduleResultModalProps {
  open: boolean;
  onClose: () => void;
  previewData: AutoSchedulePreview;
  onApplyAll: () => void;
  onViewCalendar: () => void;
  applyPending: boolean;
}

export function ScheduleResultModal({
  open, onClose, previewData, onApplyAll, onViewCalendar, applyPending,
}: ScheduleResultModalProps) {
  const { assignments, failures, stats } = previewData;
  const total = assignments.length + failures.length;
  const pct = total > 0 ? Math.round((assignments.length / total) * 100) : 0;

  // Başarılı dersleri departmentCode'a göre grupla
  const successByDept = useMemo(() => {
    const map = new Map<string, number>();
    for (const a of assignments) {
      map.set(a.departmentCode, (map.get(a.departmentCode) ?? 0) + 1);
    }
    return Array.from(map.entries()).sort((a, b) => b[1] - a[1]);
  }, [assignments]);

  // Kullanılan derslikler
  const usedClassrooms = useMemo(() => {
    const set = new Set<string>();
    for (const a of assignments) {
      if (a.classroomName) set.add(a.classroomName);
    }
    return set.size;
  }, [assignments]);

  // Başarısız dersleri sebebe göre grupla
  const failuresByReason = useMemo(() => {
    const map = new Map<string, typeof failures>();
    for (const f of failures) {
      const type = f.suggestions?.[0]?.suggestionType ?? 'Unknown';
      const arr = map.get(type) ?? [];
      arr.push(f);
      map.set(type, arr);
    }
    return Array.from(map.entries());
  }, [failures]);

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose(); }}>
      <DialogContent className="max-w-2xl p-0 gap-0">

        {/* ── Header — Başarı oranı ── */}
        <div className="px-6 pt-6 pb-4">
          <div className="flex items-center gap-3 mb-4">
            <div className={cn(
              'w-10 h-10 rounded-full flex items-center justify-center',
              pct >= 80 ? 'bg-emerald-100' : pct >= 50 ? 'bg-amber-100' : 'bg-red-100',
            )}>
              {pct >= 80
                ? <CheckCircle2 className="h-5 w-5 text-emerald-600" />
                : pct >= 50
                  ? <AlertTriangle className="h-5 w-5 text-amber-600" />
                  : <XCircle className="h-5 w-5 text-red-600" />
              }
            </div>
            <div>
              <h2 className="text-lg font-semibold text-slate-800">Programlama Tamamlandı</h2>
              <p className="text-sm text-slate-500">
                {assignments.length} / {total} ders başarıyla yerleştirildi
              </p>
            </div>
          </div>

          {/* Progress bar */}
          <div className="relative h-3 bg-slate-100 rounded-full overflow-hidden mb-1">
            <div
              className={cn(
                'h-full rounded-full transition-all duration-700',
                pct >= 80 ? 'bg-emerald-500' : pct >= 50 ? 'bg-amber-500' : 'bg-red-500',
              )}
              style={{ width: `${pct}%` }}
            />
          </div>
          <div className="flex items-center justify-between">
            <span className={cn(
              'text-2xl font-bold tabular-nums',
              pct >= 80 ? 'text-emerald-600' : pct >= 50 ? 'text-amber-600' : 'text-red-600',
            )}>
              %{pct}
            </span>
            <div className="flex items-center gap-3 text-xs text-slate-500">
              {stats && (
                <>
                  <span className="flex items-center gap-1">
                    <Clock className="h-3 w-3" />
                    {stats.elapsedSeconds.toFixed(1)}s
                  </span>
                  <span className="flex items-center gap-1">
                    <Building2 className="h-3 w-3" />
                    {usedClassrooms} derslik
                  </span>
                  <span className="flex items-center gap-1">
                    <Calendar className="h-3 w-3" />
                    {stats.saIterations.toLocaleString()} iterasyon
                  </span>
                </>
              )}
            </div>
          </div>
        </div>

        {/* ── İçerik — İki sütun ── */}
        <div className="border-t border-slate-100">
          <ScrollArea className="max-h-[320px]">
            <div className="grid grid-cols-2 divide-x divide-slate-100">

              {/* Sol: Başarılı */}
              <div className="p-4">
                <div className="flex items-center gap-2 mb-3">
                  <CheckCircle2 className="h-4 w-4 text-emerald-500" />
                  <span className="text-sm font-semibold text-slate-700">Yerleştirilen</span>
                  <Badge className="text-[10px] bg-emerald-50 text-emerald-700 border border-emerald-200 hover:bg-emerald-50">
                    {assignments.length}
                  </Badge>
                </div>
                <div className="space-y-2">
                  {successByDept.map(([dept, count]) => (
                    <div key={dept} className="flex items-center justify-between">
                      <span className="text-xs text-slate-600">{dept}</span>
                      <div className="flex items-center gap-2">
                        <div className="h-1.5 w-16 bg-slate-100 rounded-full overflow-hidden">
                          <div
                            className="h-full bg-emerald-400 rounded-full"
                            style={{ width: `${(count / assignments.length) * 100}%` }}
                          />
                        </div>
                        <span className="text-xs font-medium text-slate-700 tabular-nums w-6 text-right">{count}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Sağ: Başarısız */}
              <div className="p-4">
                <div className="flex items-center gap-2 mb-3">
                  <XCircle className="h-4 w-4 text-red-400" />
                  <span className="text-sm font-semibold text-slate-700">Yerleşemeyen</span>
                  {failures.length > 0 && (
                    <Badge className="text-[10px] bg-red-50 text-red-600 border border-red-200 hover:bg-red-50">
                      {failures.length}
                    </Badge>
                  )}
                </div>
                {failures.length === 0 ? (
                  <p className="text-xs text-slate-400 italic">Tüm dersler başarıyla yerleştirildi.</p>
                ) : (
                  <div className="space-y-3">
                    {failuresByReason.map(([type, items]) => (
                      <div key={type}>
                        <div className="flex items-center gap-1.5 mb-1">
                          <AlertTriangle className="h-3 w-3 text-amber-500" />
                          <span className="text-[11px] font-medium text-slate-600">
                            {FAILURE_LABELS[type] ?? type}
                          </span>
                          <span className="text-[10px] text-slate-400">({items.length})</span>
                        </div>
                        <div className="flex flex-wrap gap-1 ml-4">
                          {items.map(f => (
                            <span
                              key={f.sectionId}
                              className="px-1.5 py-0.5 rounded text-[10px] bg-red-50 text-red-600 border border-red-200"
                            >
                              {f.courseCode}
                            </span>
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </ScrollArea>
        </div>

        {/* ── Footer — Aksiyonlar ── */}
        <div className="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-100">
          <Button variant="outline" onClick={onViewCalendar}>
            Takvimde Gör
          </Button>
          <Button
            onClick={onApplyAll}
            disabled={applyPending || assignments.length === 0}
            className="bg-[#003366] hover:bg-[#002244]"
          >
            {applyPending ? 'Uygulanıyor...' : `Tümünü Uygula (${assignments.length})`}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
