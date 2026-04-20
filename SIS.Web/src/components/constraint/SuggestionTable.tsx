import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import type { SuggestionItem } from '../../types/simulation';

const DAY_NAMES: Record<number, string> = {
  0: 'Pazar',
  1: 'Pazartesi',
  2: 'Sali',
  3: 'Carsamba',
  4: 'Persembe',
  5: 'Cuma',
  6: 'Cumartesi',
};

interface SuggestionTableProps {
  suggestions: SuggestionItem[];
  isLoading: boolean;
  onSelect?: (item: SuggestionItem) => void;
}

export default function SuggestionTable({ suggestions, isLoading, onSelect }: SuggestionTableProps) {
  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    );
  }

  if (suggestions.length === 0) {
    return (
      <p className="text-sm text-muted-foreground text-center py-6">
        Uygun oneri bulunamadi.
      </p>
    );
  }

  return (
    <div className="border rounded-md overflow-auto max-h-[400px]">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-12">#</TableHead>
            <TableHead>Gun</TableHead>
            <TableHead>Saat</TableHead>
            <TableHead>Derslik</TableHead>
            <TableHead className="text-right">Zorunlu</TableHead>
            <TableHead className="text-right">Ceza</TableHead>
            <TableHead className="text-right">Iyilesme</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {suggestions.map((item) => (
            <TableRow
              key={`${item.rank}-${item.dayOfWeek}-${item.timeSlot}-${item.classroomId}`}
              className={`cursor-pointer hover:bg-muted/50 ${item.rank === 1 ? 'bg-emerald-50/50 dark:bg-emerald-950/20' : ''}`}
              onClick={() => onSelect?.(item)}
            >
              <TableCell className="font-medium">
                {item.rank === 1 ? (
                  <Badge variant="default" className="text-[10px] px-1.5">1</Badge>
                ) : (
                  <span className="text-muted-foreground text-xs">{item.rank}</span>
                )}
              </TableCell>
              <TableCell className="text-sm">{DAY_NAMES[item.dayOfWeek] ?? item.dayOfWeek}</TableCell>
              <TableCell className="text-sm font-mono">{item.timeSlot}</TableCell>
              <TableCell className="text-sm">{item.classroomName ?? '-'}</TableCell>
              <TableCell className="text-right">
                {item.hardViolations === 0 ? (
                  <Badge variant="outline" className="text-[10px] text-emerald-600">0</Badge>
                ) : (
                  <Badge variant="destructive" className="text-[10px]">{item.hardViolations}</Badge>
                )}
              </TableCell>
              <TableCell className="text-right text-sm font-mono tabular-nums">
                {item.softPenalty.toFixed(1)}
              </TableCell>
              <TableCell className="text-right">
                <span className={`text-sm font-mono tabular-nums ${
                  item.improvement > 0 ? 'text-emerald-600' : item.improvement < 0 ? 'text-red-600' : 'text-muted-foreground'
                }`}>
                  {item.improvement > 0 ? '+' : ''}{item.improvement.toFixed(1)}
                </span>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
