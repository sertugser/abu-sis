import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ChevronDown, ChevronRight } from 'lucide-react';
import type { ConstraintScoreItemDto } from '@/types/constraint';

interface Props {
  constraints: ConstraintScoreItemDto[] | undefined;
}

export default function ConflictDrillDown({ constraints }: Props) {
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  if (!constraints || constraints.length === 0) return null;

  const active = constraints.filter(c => c.violationCount > 0 || c.weightedPenalty > 0);
  if (active.length === 0) return null;

  const toggle = (code: string) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  };

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium">Kisitlama Detaylari</CardTitle>
      </CardHeader>
      <CardContent className="space-y-1">
        {active.map(c => {
          const isOpen = expanded.has(c.code);
          return (
            <div key={c.code} className="border rounded-md">
              <button
                className="flex items-center w-full px-3 py-2 text-left text-xs hover:bg-muted/50 transition-colors"
                onClick={() => toggle(c.code)}
              >
                {isOpen ? (
                  <ChevronDown className="h-3.5 w-3.5 mr-2 shrink-0" />
                ) : (
                  <ChevronRight className="h-3.5 w-3.5 mr-2 shrink-0" />
                )}
                <span className="font-medium mr-2">{c.code}</span>
                <span className="text-muted-foreground truncate flex-1">{c.name}</span>
                <span className="ml-2 shrink-0">
                  {c.category === 'Hard' ? (
                    <span className="text-destructive font-medium">{c.violationCount} ihlal</span>
                  ) : (
                    <span className="text-amber-600 font-medium">{c.weightedPenalty.toFixed(1)} ceza</span>
                  )}
                </span>
              </button>
              {isOpen && (
                <div className="px-3 pb-2 pt-1 border-t space-y-1">
                  <div className="grid grid-cols-3 gap-2 text-[10px] text-muted-foreground">
                    <div>Agirlik: <span className="font-medium text-foreground">{c.weight}</span></div>
                    <div>Ham Ceza: <span className="font-medium text-foreground">{c.rawPenalty.toFixed(1)}</span></div>
                    <div>Etkilenen: <span className="font-medium text-foreground">{c.affectedSectionCount} seksyon</span></div>
                  </div>
                  {c.sampleViolations.length > 0 && (
                    <div className="mt-1 space-y-0.5">
                      <div className="text-[10px] font-medium text-muted-foreground">Ornek Ihlaller:</div>
                      {c.sampleViolations.map((v, i) => (
                        <div key={i} className="text-[10px] pl-2 text-muted-foreground leading-tight">
                          {v}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
