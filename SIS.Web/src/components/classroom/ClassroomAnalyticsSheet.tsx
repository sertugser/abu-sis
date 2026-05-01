import { useMemo } from 'react';
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription,
} from '@/components/ui/sheet';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Building2, Users, Layers, TrendingUp } from 'lucide-react';
import type { ClassroomDto } from '../../api/classroom.api';
import {
  getClassroomTypeLabel,
  getClassroomTypeBarClass,
} from '../../lib/classroomTypeUtils';

const CAPACITY_BUCKETS = [
  { label: '0–30 kişi',   min: 0,   max: 30 },
  { label: '31–60 kişi',  min: 31,  max: 60 },
  { label: '61–90 kişi',  min: 61,  max: 90 },
  { label: '91+ kişi',    min: 91,  max: Infinity },
] as const;

interface ClassroomAnalyticsSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  classrooms: ClassroomDto[];
}

export default function ClassroomAnalyticsSheet({
  open, onOpenChange, classrooms,
}: ClassroomAnalyticsSheetProps) {
  const stats = useMemo(() => {
    const total = classrooms.length;
    const totalCapacity = classrooms.reduce((s, c) => s + c.capacity, 0);
    const avgCapacity = total > 0 ? Math.round(totalCapacity / total) : 0;
    const minCap = total > 0 ? Math.min(...classrooms.map(c => c.capacity)) : 0;
    const maxCap = total > 0 ? Math.max(...classrooms.map(c => c.capacity)) : 0;

    const byType = classrooms.reduce((acc, c) => {
      acc[c.type] = (acc[c.type] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const byBuilding = classrooms.reduce((acc, c) => {
      const key = c.buildingName?.trim() || 'Belirtilmemiş';
      acc[key] = (acc[key] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const buckets = CAPACITY_BUCKETS.map(b => ({
      ...b,
      count: classrooms.filter(c => c.capacity >= b.min && c.capacity <= b.max).length,
    }));

    return { total, totalCapacity, avgCapacity, minCap, maxCap, byType, byBuilding, buckets };
  }, [classrooms]);

  const buildingsSorted = Object.entries(stats.byBuilding).sort((a, b) => b[1] - a[1]);
  const typesSorted = Object.entries(stats.byType).sort((a, b) => b[1] - a[1]);
  const maxBucket = Math.max(...stats.buckets.map(b => b.count), 1);

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-md overflow-hidden flex flex-col">
        <SheetHeader>
          <SheetTitle>Derslik Analizi</SheetTitle>
          <SheetDescription>
            Toplam, dağılım ve kapasite analizleri.
          </SheetDescription>
        </SheetHeader>

        <ScrollArea className="flex-1 -mx-6 px-6 mt-4">
          <div className="space-y-6 pb-6">

            {/* Metric grid */}
            <div className="grid grid-cols-2 gap-3">
              <MetricCard
                icon={<Building2 className="h-4 w-4 text-blue-600" />}
                bg="bg-blue-50"
                value={stats.total}
                label="Toplam Derslik"
              />
              <MetricCard
                icon={<Users className="h-4 w-4 text-emerald-600" />}
                bg="bg-emerald-50"
                value={stats.totalCapacity}
                label="Toplam Kapasite"
              />
              <MetricCard
                icon={<TrendingUp className="h-4 w-4 text-amber-600" />}
                bg="bg-amber-50"
                value={stats.avgCapacity}
                label="Ortalama Kapasite"
              />
              <MetricCard
                icon={<Layers className="h-4 w-4 text-violet-600" />}
                bg="bg-violet-50"
                value={
                  <>
                    {stats.minCap}
                    <span className="text-sm font-normal text-muted-foreground"> – {stats.maxCap}</span>
                  </>
                }
                label="Kapasite Aralığı"
              />
            </div>

            {/* Type distribution */}
            {typesSorted.length > 0 && (
              <section>
                <h3 className="text-sm font-semibold mb-3">Tip Dağılımı</h3>
                <div className="space-y-2">
                  {typesSorted.map(([type, count]) => {
                    const pct = stats.total > 0 ? (count / stats.total) * 100 : 0;
                    return (
                      <div key={type} className="space-y-1">
                        <div className="flex justify-between text-xs">
                          <span className="text-gray-700">{getClassroomTypeLabel(type)}</span>
                          <span className="font-mono text-muted-foreground">
                            {count} <span className="text-[10px]">({pct.toFixed(0)}%)</span>
                          </span>
                        </div>
                        <div className="h-1.5 rounded-full bg-gray-100 overflow-hidden">
                          <div
                            className={`h-full rounded-full ${getClassroomTypeBarClass(type)}`}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </section>
            )}

            {/* Capacity histogram */}
            {stats.total > 0 && (
              <section>
                <h3 className="text-sm font-semibold mb-3">Kapasite Dağılımı</h3>
                <div className="space-y-2">
                  {stats.buckets.map(b => {
                    const pct = (b.count / maxBucket) * 100;
                    return (
                      <div key={b.label} className="flex items-center gap-3">
                        <span className="text-xs text-gray-600 w-20 shrink-0">{b.label}</span>
                        <div className="flex-1 h-5 rounded bg-gray-100 overflow-hidden relative">
                          <div
                            className="h-full bg-[#003366]/80 rounded transition-all"
                            style={{ width: `${pct}%` }}
                          />
                          <span className="absolute inset-0 flex items-center px-2 text-[11px] font-mono">
                            {b.count}
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </section>
            )}

            {/* Building distribution */}
            {buildingsSorted.length > 0 && (
              <section>
                <h3 className="text-sm font-semibold mb-3">Bina Dağılımı</h3>
                <div className="space-y-1.5">
                  {buildingsSorted.map(([name, count]) => (
                    <div
                      key={name}
                      className="flex items-center justify-between text-sm py-1.5 px-3 bg-gray-50 rounded-md"
                    >
                      <span className="text-gray-700 truncate">{name}</span>
                      <span className="font-mono text-xs font-semibold text-gray-900 shrink-0 ml-2">
                        {count} derslik
                      </span>
                    </div>
                  ))}
                </div>
              </section>
            )}

            {stats.total === 0 && (
              <div className="text-center py-12 text-sm text-muted-foreground">
                Henüz derslik tanımlanmamış.
              </div>
            )}
          </div>
        </ScrollArea>
      </SheetContent>
    </Sheet>
  );
}

interface MetricCardProps {
  icon: React.ReactNode;
  bg: string;
  value: React.ReactNode;
  label: string;
}

function MetricCard({ icon, bg, value, label }: MetricCardProps) {
  return (
    <div className="bg-white border rounded-xl p-3.5 flex items-center gap-3">
      <div className={`${bg} p-2 rounded-lg shrink-0`}>{icon}</div>
      <div className="min-w-0">
        <p className="text-lg font-bold leading-tight">{value}</p>
        <p className="text-[11px] text-muted-foreground">{label}</p>
      </div>
    </div>
  );
}
