import { Building2, TrendingUp, AlertCircle, Layers } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

interface ClassroomSummaryCardsProps {
  totalClassrooms: number;
  occupancyRate: number;
  conflictsCount: number;
  assignedCount: number;
  isLoading?: boolean;
}

export default function ClassroomSummaryCards({
  totalClassrooms, occupancyRate, conflictsCount, assignedCount, isLoading,
}: ClassroomSummaryCardsProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {[1, 2, 3, 4].map(i => <Skeleton key={i} className="h-20 rounded-xl" />)}
      </div>
    );
  }

  const cards = [
    {
      icon: Building2,
      iconBg: 'bg-blue-50',
      iconColor: 'text-blue-600',
      value: totalClassrooms,
      label: 'Toplam Derslik',
    },
    {
      icon: TrendingUp,
      iconBg: cn(occupancyRate > 75 ? 'bg-red-50' : occupancyRate > 50 ? 'bg-amber-50' : 'bg-emerald-50'),
      iconColor: cn(occupancyRate > 75 ? 'text-red-600' : occupancyRate > 50 ? 'text-amber-600' : 'text-emerald-600'),
      value: `%${occupancyRate}`,
      label: 'Doluluk Oranı',
      tooltip: 'Tüm derslik-saat slotları üzerinden ortalama doluluk',
    },
    {
      icon: AlertCircle,
      iconBg: cn(conflictsCount > 0 ? 'bg-red-50' : 'bg-green-50'),
      iconColor: cn(conflictsCount > 0 ? 'text-red-600' : 'text-green-600'),
      value: conflictsCount,
      label: 'Çakışma',
    },
    {
      icon: Layers,
      iconBg: 'bg-violet-50',
      iconColor: 'text-violet-600',
      value: <>{assignedCount}<span className="text-sm font-normal text-muted-foreground">/{totalClassrooms}</span></>,
      label: 'Programı Olan',
    },
  ];

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
      {cards.map(card => {
        const content = (
          <div className="bg-white border rounded-xl p-4 flex items-center gap-3">
            <div className={cn('p-2 rounded-lg', card.iconBg)}>
              <card.icon className={cn('h-4 w-4', card.iconColor)} />
            </div>
            <div>
              <p className="text-xl font-bold">{card.value}</p>
              <p className="text-[11px] text-muted-foreground">{card.label}</p>
            </div>
          </div>
        );

        if (card.tooltip) {
          return (
            <TooltipProvider key={card.label} delayDuration={100}>
              <Tooltip>
                <TooltipTrigger asChild>{content}</TooltipTrigger>
                <TooltipContent side="bottom" className="text-xs">{card.tooltip}</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          );
        }
        return <div key={card.label}>{content}</div>;
      })}
    </div>
  );
}
