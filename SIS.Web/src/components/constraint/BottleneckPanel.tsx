import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Progress } from '@/components/ui/progress';
import type { BottleneckDto } from '@/types/constraintAnalytics';

interface Props {
  bottleneck: BottleneckDto | undefined;
  isLoading: boolean;
}

export default function BottleneckPanel({ bottleneck, isLoading }: Props) {
  if (isLoading) {
    return (
      <Card>
        <CardContent className="pt-4">
          <div className="h-48 bg-muted animate-pulse rounded" />
        </CardContent>
      </Card>
    );
  }

  if (!bottleneck) return null;

  const hasData =
    bottleneck.topConstraints.length > 0 ||
    bottleneck.topResources.length > 0 ||
    bottleneck.topTimeSlots.length > 0;

  if (!hasData) return null;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">Darbogazlar</CardTitle>
      </CardHeader>
      <CardContent>
        <Tabs defaultValue="constraints" className="w-full">
          <TabsList className="h-8 w-full">
            <TabsTrigger value="constraints" className="text-xs flex-1">Kisitlamalar</TabsTrigger>
            <TabsTrigger value="resources" className="text-xs flex-1">Kaynaklar</TabsTrigger>
            <TabsTrigger value="slots" className="text-xs flex-1">Zaman Slotlari</TabsTrigger>
          </TabsList>

          <TabsContent value="constraints" className="mt-2 space-y-2">
            {bottleneck.topConstraints.length === 0 && (
              <div className="text-xs text-muted-foreground py-4 text-center">Veri yok</div>
            )}
            {bottleneck.topConstraints.map(c => (
              <div key={c.code} className="space-y-1">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-medium truncate max-w-[140px]" title={c.name}>{c.code}</span>
                  <span className="text-muted-foreground">{c.violationCount} ihlal ({c.percentage}%)</span>
                </div>
                <Progress value={c.percentage} className="h-1.5" />
              </div>
            ))}
          </TabsContent>

          <TabsContent value="resources" className="mt-2 space-y-2">
            {bottleneck.topResources.length === 0 && (
              <div className="text-xs text-muted-foreground py-4 text-center">Veri yok</div>
            )}
            {bottleneck.topResources.map((r, i) => (
              <div key={i} className="flex items-center justify-between text-xs py-1">
                <div>
                  <span className="inline-block rounded bg-muted px-1.5 py-0.5 text-[10px] font-medium mr-1.5">
                    {r.type === 'Instructor' ? 'Ogr.Uyesi' : 'Derslik'}
                  </span>
                  <span className="truncate max-w-[120px]" title={r.name}>{r.name}</span>
                </div>
                <span className="text-muted-foreground font-medium">{r.conflictCount}</span>
              </div>
            ))}
          </TabsContent>

          <TabsContent value="slots" className="mt-2 space-y-2">
            {bottleneck.topTimeSlots.length === 0 && (
              <div className="text-xs text-muted-foreground py-4 text-center">Veri yok</div>
            )}
            {bottleneck.topTimeSlots.map((s, i) => (
              <div key={i} className="flex items-center justify-between text-xs py-1">
                <span className="font-medium">{s.day} {s.slot}</span>
                <span className="text-muted-foreground">{s.totalViolations} ihlal</span>
              </div>
            ))}
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}
