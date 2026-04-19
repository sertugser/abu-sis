import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

const DAY_SHORT: Record<number, string> = {
  0: 'Pzt', 1: 'Sal', 2: 'Çar', 3: 'Per', 4: 'Cum',
};

interface PeakSlot {
  day: number;
  hour: number;
  startTime: string;
  occupancy: number;
}

interface ClassroomSummaryCardsProps {
  totalClassrooms: number;
  occupancyRate: number;
  assignedCount: number;
  emptySlots: number;
  peakSlot: PeakSlot | null;
  isLoading?: boolean;
}

export default function ClassroomSummaryCards({
  totalClassrooms, occupancyRate, assignedCount, emptySlots, peakSlot, isLoading,
}: ClassroomSummaryCardsProps) {
  if (isLoading) {
    return <Skeleton className="h-6 w-full rounded-lg" />;
  }

  const peakPct = peakSlot ? Math.round(peakSlot.occupancy * 100) : 0;

  const stats = [
    {
      value: `${assignedCount}/${totalClassrooms}`,
      label: 'programı olan',
      tooltip: 'Ders programı atanmış derslik sayısı / toplam derslik',
      color: 'text-violet-600',
    },
    {
      value: `%${occupancyRate}`,
      label: 'doluluk',
      tooltip: 'Tüm derslik-saat slotları üzerinden ortalama doluluk oranı',
      color: cn(occupancyRate > 75 ? 'text-red-600' : occupancyRate > 50 ? 'text-amber-600' : 'text-emerald-600'),
    },
    {
      value: peakSlot ? `${DAY_SHORT[peakSlot.day]} ${peakSlot.startTime}` : '—',
      label: peakSlot ? `pik %${peakPct}` : 'pik',
      tooltip: 'Hafta boyunca en yoğun gün-saat dilimi',
      color: cn(peakPct > 75 ? 'text-red-600' : peakPct > 50 ? 'text-amber-600' : 'text-emerald-600'),
    },
    {
      value: String(emptySlots),
      label: 'boş slot',
      tooltip: 'Haftalık toplam kullanılmayan derslik-saat slotu sayısı',
      color: 'text-blue-600',
    },
  ];

  return (
    <div className="flex items-center gap-4 flex-wrap">
      {stats.map((stat, i) => (
        <TooltipProvider key={stat.label} delayDuration={100}>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="flex items-center gap-1.5 cursor-default">
                <span className={cn('text-sm font-bold tabular-nums', stat.color)}>{stat.value}</span>
                <span className="text-xs text-muted-foreground">{stat.label}</span>
                {i < stats.length - 1 && <span className="text-muted-foreground/30 ml-2.5">|</span>}
              </div>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">{stat.tooltip}</TooltipContent>
          </Tooltip>
        </TooltipProvider>
      ))}
    </div>
  );
}
