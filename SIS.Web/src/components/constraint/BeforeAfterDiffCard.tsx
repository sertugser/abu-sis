import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ArrowDown, ArrowUp, CheckCircle2, Minus, XCircle } from 'lucide-react';
import type { SimulationResult } from '../../types/simulation';

interface BeforeAfterDiffCardProps {
  result: SimulationResult;
}

function DeltaIndicator({ before, after, label, invert }: {
  before: number;
  after: number;
  label: string;
  invert?: boolean;
}) {
  const delta = after - before;
  const improved = invert ? delta > 0 : delta < 0;
  const worsened = invert ? delta < 0 : delta > 0;

  return (
    <div className="flex items-center justify-between py-2">
      <span className="text-sm text-muted-foreground">{label}</span>
      <div className="flex items-center gap-3">
        <span className="text-sm font-mono tabular-nums">{before}</span>
        <span className="text-muted-foreground">→</span>
        <span className={`text-sm font-mono tabular-nums font-semibold ${
          improved ? 'text-emerald-600' : worsened ? 'text-red-600' : ''
        }`}>
          {typeof after === 'number' && after % 1 !== 0 ? after.toFixed(1) : after}
        </span>
        {delta !== 0 && (
          <span className={`flex items-center gap-0.5 text-xs font-medium ${
            improved ? 'text-emerald-600' : 'text-red-600'
          }`}>
            {delta > 0 ? <ArrowUp className="h-3 w-3" /> : <ArrowDown className="h-3 w-3" />}
            {Math.abs(delta) % 1 !== 0 ? Math.abs(delta).toFixed(1) : Math.abs(delta)}
          </span>
        )}
        {delta === 0 && (
          <span className="flex items-center gap-0.5 text-xs text-muted-foreground">
            <Minus className="h-3 w-3" />
          </span>
        )}
      </div>
    </div>
  );
}

export default function BeforeAfterDiffCard({ result }: BeforeAfterDiffCardProps) {
  const { before, after, diff } = result;

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Simulasyon Sonucu</CardTitle>
          <Badge variant={diff.netImprovement ? 'default' : 'destructive'} className="text-xs">
            {diff.netImprovement ? 'Iyilesme' : 'Kotulesme'}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Metrics comparison */}
        <div className="divide-y">
          <DeltaIndicator
            before={before.hardViolations}
            after={after.hardViolations}
            label="Zorunlu Ihlaller"
          />
          <DeltaIndicator
            before={before.softPenalty}
            after={after.softPenalty}
            label="Ceza Puani"
          />
        </div>

        {/* Feasibility badge */}
        <div className="flex items-center gap-2 pt-1">
          <span className="text-sm text-muted-foreground">Uygulanabilirlik:</span>
          <Badge variant={result.feasible ? 'outline' : 'destructive'} className="text-xs">
            {result.feasible ? 'Uygulanabilir' : 'Zorunlu ihlal mevcut'}
          </Badge>
        </div>

        {/* Resolved conflicts */}
        {diff.resolvedConflicts.length > 0 && (
          <div className="space-y-1.5">
            <p className="text-xs font-medium text-emerald-600">Cozulen Catismalar</p>
            <ul className="space-y-1">
              {diff.resolvedConflicts.map((c, i) => (
                <li key={i} className="flex items-start gap-1.5 text-xs text-muted-foreground">
                  <CheckCircle2 className="h-3.5 w-3.5 mt-0.5 shrink-0 text-emerald-500" />
                  {c}
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* New conflicts */}
        {diff.newConflicts.length > 0 && (
          <div className="space-y-1.5">
            <p className="text-xs font-medium text-red-600">Yeni Catismalar</p>
            <ul className="space-y-1">
              {diff.newConflicts.map((c, i) => (
                <li key={i} className="flex items-start gap-1.5 text-xs text-muted-foreground">
                  <XCircle className="h-3.5 w-3.5 mt-0.5 shrink-0 text-red-500" />
                  {c}
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Penalty delta summary */}
        {diff.penaltyDelta !== 0 && (
          <p className="text-xs text-muted-foreground pt-1 border-t">
            Ceza degisimi: <span className={`font-medium ${diff.penaltyDelta < 0 ? 'text-emerald-600' : 'text-red-600'}`}>
              {diff.penaltyDelta > 0 ? '+' : ''}{diff.penaltyDelta.toFixed(1)}
            </span>
          </p>
        )}
      </CardContent>
    </Card>
  );
}
