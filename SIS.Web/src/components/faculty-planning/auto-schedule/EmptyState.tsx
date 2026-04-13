import { Zap } from 'lucide-react';

interface EmptyStateProps {
  unscheduledCount: number;
  availableClassroomCount: number | undefined;
}

export function EmptyState({ unscheduledCount, availableClassroomCount }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center h-full p-10">
      <div className="w-14 h-14 bg-indigo-500/10 rounded-2xl flex items-center justify-center mb-4">
        <Zap className="h-7 w-7 text-indigo-500" />
      </div>
      <h3 className="text-base font-semibold text-foreground mb-1">Henüz çalıştırma yok</h3>
      <p className="text-sm text-muted-foreground text-center max-w-[300px] leading-relaxed">
        Soldaki {unscheduledCount} şube için otomatik derslik ve saat ataması yapmak için{' '}
        <strong className="text-indigo-400">"Otomatik Çalıştır"</strong> butonuna basın.
      </p>
      <div className="mt-5 grid grid-cols-2 gap-2 w-full max-w-[280px]">
        <div className="bg-muted/50 rounded-lg p-3 text-center">
          <div className="text-sm font-bold text-emerald-600">{availableClassroomCount ?? '—'}</div>
          <div className="text-[10px] text-muted-foreground">Müsait derslik</div>
        </div>
        <div className="bg-muted/50 rounded-lg p-3 text-center">
          <div className="text-sm font-bold text-amber-600">~3 sn</div>
          <div className="text-[10px] text-muted-foreground">Tahmini süre</div>
        </div>
      </div>
    </div>
  );
}
