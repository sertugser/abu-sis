import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Activity, AlertTriangle, ShieldAlert, BarChart3 } from 'lucide-react';
import { useAuditLogStats } from '../../hooks/useAuditLog';

const STAT_CARDS = [
  { key: 'todayTotal' as const, label: 'Bugün Toplam', icon: Activity, color: 'text-blue-600', bg: 'bg-blue-50' },
  { key: 'todayFailedLogins' as const, label: 'Başarısız Giriş', icon: AlertTriangle, color: 'text-amber-600', bg: 'bg-amber-50' },
  { key: 'todayErrors' as const, label: 'Sistem Hatası', icon: ShieldAlert, color: 'text-red-600', bg: 'bg-red-50' },
  { key: 'last7DaysTotal' as const, label: 'Son 7 Gün', icon: BarChart3, color: 'text-violet-600', bg: 'bg-violet-50' },
] as const;

export function AuditStatCards() {
  const { data, isLoading } = useAuditLogStats();

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
      {STAT_CARDS.map(({ key, label, icon: Icon, color, bg }) => (
        <Card key={key} className="overflow-hidden">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${bg}`}>
                <Icon className={`h-4.5 w-4.5 ${color}`} />
              </div>
              <div className="min-w-0">
                <p className="text-xs text-muted-foreground truncate">{label}</p>
                {isLoading ? (
                  <Skeleton className="h-6 w-12 mt-0.5" />
                ) : (
                  <p className="text-lg font-bold text-foreground">
                    {(data?.[key] ?? 0).toLocaleString('tr-TR')}
                  </p>
                )}
              </div>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
