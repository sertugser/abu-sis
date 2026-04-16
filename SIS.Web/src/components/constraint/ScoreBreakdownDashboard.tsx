import { useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell,
} from 'recharts';
import type { ScoreBreakdownDto } from '@/types/constraint';

interface Props {
  breakdown: ScoreBreakdownDto | undefined;
  isLoading: boolean;
}

export default function ScoreBreakdownDashboard({ breakdown, isLoading }: Props) {
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
      <div className="grid grid-cols-3 gap-4">
        <Card>
          <CardContent className="pt-4">
            <div className="text-sm text-muted-foreground">Hard Ihlaller</div>
            <div className="text-2xl font-bold text-destructive">
              {breakdown.totalHardViolations}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4">
            <div className="text-sm text-muted-foreground">Soft Penalty</div>
            <div className="text-2xl font-bold text-amber-600">
              {breakdown.totalSoftPenalty.toFixed(1)}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4">
            <div className="text-sm text-muted-foreground">Durum</div>
            <Badge variant={breakdown.feasible ? 'default' : 'destructive'} className="mt-1">
              {breakdown.feasible ? 'Uygulanabilir' : 'Ihlal Var'}
            </Badge>
          </CardContent>
        </Card>
      </div>

      {chartData.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium">Kisitlama Katkisi</CardTitle>
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
