import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { useSnapshotComparison, useSnapshots } from '@/hooks/useConstraintAnalytics';
import type { SnapshotDto } from '@/types/constraintAnalytics';

interface Props {
  termId: number | null;
}

function formatSnapshotLabel(s: SnapshotDto): string {
  return `#${s.id} - ${new Date(s.snapshotAt).toLocaleString('tr-TR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  })}`;
}

export default function SnapshotCompare({ termId }: Props) {
  const [id1, setId1] = useState<number | null>(null);
  const [id2, setId2] = useState<number | null>(null);

  const { data: snapshots } = useSnapshots(termId);
  const { data: comparison, isLoading } = useSnapshotComparison(id1, id2);

  if (!snapshots || snapshots.length < 2) return null;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">Snapshot Karsilastirma</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="flex items-center gap-3 mb-3">
          <select
            className="h-8 rounded border bg-background px-2 text-xs flex-1"
            value={id1 ?? ''}
            onChange={e => setId1(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Onceki...</option>
            {snapshots.map(s => (
              <option key={s.id} value={s.id}>{formatSnapshotLabel(s)}</option>
            ))}
          </select>
          <span className="text-xs text-muted-foreground">vs</span>
          <select
            className="h-8 rounded border bg-background px-2 text-xs flex-1"
            value={id2 ?? ''}
            onChange={e => setId2(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Sonraki...</option>
            {snapshots.map(s => (
              <option key={s.id} value={s.id}>{formatSnapshotLabel(s)}</option>
            ))}
          </select>
        </div>

        {isLoading && (
          <div className="h-24 bg-muted animate-pulse rounded" />
        )}

        {comparison && (
          <>
            <div className="flex items-center gap-2 mb-3 text-xs">
              <Badge variant="default">{comparison.improvedCount} iyilesti</Badge>
              <Badge variant="destructive">{comparison.worsenedCount} kotuslesti</Badge>
              <Badge variant="secondary">{comparison.unchangedCount} degismedi</Badge>
            </div>

            <div className="max-h-64 overflow-y-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="text-xs">Kisitlama</TableHead>
                    <TableHead className="text-xs text-right">Onceki</TableHead>
                    <TableHead className="text-xs text-right">Sonraki</TableHead>
                    <TableHead className="text-xs text-right">Fark</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {comparison.items.map(item => (
                    <TableRow key={item.constraintCode} className="text-xs">
                      <TableCell className="font-medium">{item.constraintCode}</TableCell>
                      <TableCell className="text-right">{item.violationsBefore}</TableCell>
                      <TableCell className="text-right">{item.violationsAfter}</TableCell>
                      <TableCell className="text-right">
                        <span
                          className={
                            item.change === 'improved'
                              ? 'text-green-600 dark:text-green-400'
                              : item.change === 'worsened'
                                ? 'text-red-600 dark:text-red-400'
                                : 'text-muted-foreground'
                          }
                        >
                          {item.violationDelta > 0 ? '+' : ''}{item.violationDelta}
                        </span>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
