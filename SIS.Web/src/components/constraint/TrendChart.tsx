import { useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  ComposedChart, Bar, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid,
} from 'recharts';
import type { TrendDataDto } from '@/types/constraintAnalytics';

interface Props {
  trend: TrendDataDto | undefined;
  isLoading: boolean;
}

const SNAPSHOT_TYPE_LABELS: Record<string, string> = {
  Manual: 'Manuel',
  Automatic: 'Otomatik',
  ProfileApply: 'Profil Uygulama',
  SimulationApply: 'Simulasyon',
};

const TREND_LABELS: Record<string, { label: string; variant: 'default' | 'destructive' | 'secondary' }> = {
  improving: { label: 'Iyilesiyor', variant: 'default' },
  worsening: { label: 'Kotulesme', variant: 'destructive' },
  stable: { label: 'Stabil', variant: 'secondary' },
  insufficient_data: { label: 'Yetersiz Veri', variant: 'secondary' },
};

export default function TrendChart({ trend, isLoading }: Props) {
  const chartData = useMemo(() => {
    if (!trend) return [];
    return trend.dataPoints.map((dp, i) => ({
      index: i,
      label: new Date(dp.snapshotAt).toLocaleString('tr-TR', {
        day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
      }),
      hardViolations: dp.hardViolations,
      softPenalty: dp.softPenalty,
      type: SNAPSHOT_TYPE_LABELS[dp.snapshotType] ?? dp.snapshotType,
    }));
  }, [trend]);

  if (isLoading) {
    return (
      <Card>
        <CardContent className="pt-4">
          <div className="h-48 bg-muted animate-pulse rounded" />
        </CardContent>
      </Card>
    );
  }

  if (!trend || trend.dataPoints.length === 0) return null;

  const trendInfo = TREND_LABELS[trend.improvement.trend] ?? TREND_LABELS.insufficient_data;

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-medium">Trend</CardTitle>
          <div className="flex items-center gap-2 text-xs">
            <Badge variant={trendInfo.variant}>{trendInfo.label}</Badge>
            {trend.improvement.hardReduction !== 'N/A' && (
              <span className="text-muted-foreground">
                Hard: {trend.improvement.hardReduction}
              </span>
            )}
            {trend.improvement.penaltyReduction !== 'N/A' && (
              <span className="text-muted-foreground">
                Penalty: {trend.improvement.penaltyReduction}
              </span>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <ResponsiveContainer width="100%" height={220}>
          <ComposedChart data={chartData} margin={{ left: 0, right: 8, top: 4, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
            <XAxis dataKey="label" tick={{ fontSize: 10 }} />
            <YAxis yAxisId="left" tick={{ fontSize: 11 }} label={{ value: 'Hard', angle: -90, position: 'insideLeft', fontSize: 11 }} />
            <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 11 }} label={{ value: 'Soft', angle: 90, position: 'insideRight', fontSize: 11 }} />
            <Tooltip
              content={({ active, payload }) => {
                if (!active || !payload?.length) return null;
                const item = payload[0].payload as (typeof chartData)[0];
                return (
                  <div className="rounded-md border bg-popover px-3 py-2 text-xs shadow-md">
                    <div className="font-medium">{item.label}</div>
                    <div className="text-muted-foreground">{item.type}</div>
                    <div className="mt-1">Hard: <span className="font-medium text-destructive">{item.hardViolations}</span></div>
                    <div>Soft: <span className="font-medium text-amber-600">{item.softPenalty.toFixed(1)}</span></div>
                  </div>
                );
              }}
            />
            <Bar yAxisId="left" dataKey="hardViolations" fill="#ef4444" opacity={0.7} radius={[2, 2, 0, 0]} />
            <Line yAxisId="right" dataKey="softPenalty" stroke="#f59e0b" strokeWidth={2} dot={{ r: 3 }} />
          </ComposedChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}
