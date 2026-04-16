import { useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell,
} from 'recharts';
import { Camera } from 'lucide-react';
import TrendChart from './TrendChart';
import ViolationHeatmap from './ViolationHeatmap';
import BottleneckPanel from './BottleneckPanel';
import type { ScoreBreakdownDto } from '@/types/constraint';
import type { TrendDataDto, HeatmapDto, BottleneckDto } from '@/types/constraintAnalytics';

interface Props {
  breakdown: ScoreBreakdownDto | undefined;
  isLoading: boolean;
  trendData?: TrendDataDto;
  heatmapData?: HeatmapDto;
  bottleneckData?: BottleneckDto;
  onTakeSnapshot?: () => void;
  snapshotPending?: boolean;
}

export default function ScoreBreakdownDashboard({
  breakdown, isLoading, trendData, heatmapData, bottleneckData,
  onTakeSnapshot, snapshotPending,
}: Props) {
  const chartData = useMemo(() => {
    if (!breakdown) return [];
    return breakdown.constraints
      .filter(c => c.violationCount > 0 || c.weightedPenalty > 0)
      .slice(0, 10)
      .map(c => ({
        name: c.code,
        fullName: c.name,
        value: c.category === 'Hard' ? c.violationCount : c.weightedPenalty,
        percentage: c.percentage,
        category: c.category,
      }));
  }, [breakdown]);

  if (isLoading) {
    return (
      <div className="grid grid-cols-3 gap-4 mb-6">
        {[1, 2, 3].map(i => (
          <Card key={i}>
            <CardContent className="pt-4">
              <div className="h-12 bg-muted animate-pulse rounded" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (!breakdown) return null;

  return (
    <div className="space-y-4 mb-6">
      {/* Trend + Snapshot */}
      {trendData && trendData.dataPoints.length > 1 && (
        <div className="flex items-start gap-4">
          <div className="flex-1">
            <TrendChart data={trendData} />
          </div>
          {onTakeSnapshot && (
            <Button variant="outline" size="sm" onClick={onTakeSnapshot} disabled={snapshotPending} className="mt-1 shrink-0">
              <Camera className="h-4 w-4 mr-1" />
              {snapshotPending ? 'Alınıyor...' : 'Snapshot Al'}
            </Button>
          )}
        </div>
      )}

      {/* Heatmap + Bottleneck side by side */}
      {(heatmapData || bottleneckData) && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {heatmapData && <ViolationHeatmap data={heatmapData} />}
          {bottleneckData && <BottleneckPanel data={bottleneckData} />}
        </div>
      )}

      {/* Score Breakdown Chart */}
      {chartData.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium">Kısıtlama Katkısı</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={Math.max(200, chartData.length * 36)}>
              <BarChart data={chartData} layout="vertical" margin={{ left: 60, right: 20 }}>
                <XAxis type="number" />
                <YAxis type="category" dataKey="name" width={50} tick={{ fontSize: 12 }} />
                <Tooltip
                  formatter={(value: number) => [value.toFixed(1), '']}
                  labelFormatter={(label: string) => {
                    const item = chartData.find(d => d.name === label);
                    return item ? `${item.fullName} (%${item.percentage})` : label;
                  }}
                />
                <Bar dataKey="value" radius={[0, 4, 4, 0]}>
                  {chartData.map((entry, i) => (
                    <Cell key={i} fill={entry.category === 'Hard' ? '#ef4444' : '#f59e0b'} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
