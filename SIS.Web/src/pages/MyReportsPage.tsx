import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileBarChart, Loader2, Search } from 'lucide-react';
import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { myReportsApi, type AccessibleReportDto } from '@/api/myReports.api';
import { ReportRunPanel } from '@/components/reports/ReportRunPanel';

export default function MyReportsPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['my-reports'],
    queryFn: () => myReportsApi.list(),
    staleTime: 60_000,
  });

  const [filter, setFilter] = useState('');
  const [activeReport, setActiveReport] = useState<AccessibleReportDto | null>(null);

  const grouped = useMemo(() => {
    if (!data) return [];
    const q = filter.trim().toLowerCase();
    const filtered = q
      ? data.filter((r) =>
          r.name.toLowerCase().includes(q) ||
          r.code.toLowerCase().includes(q) ||
          r.categoryName.toLowerCase().includes(q),
        )
      : data;

    const map = new Map<string, AccessibleReportDto[]>();
    for (const r of filtered) {
      const list = map.get(r.categoryName) ?? [];
      list.push(r);
      map.set(r.categoryName, list);
    }
    return Array.from(map.entries()).sort(([a], [b]) => a.localeCompare(b, 'tr'));
  }, [data, filter]);

  const totalCount = data?.length ?? 0;
  const filteredCount = grouped.reduce((sum, [, items]) => sum + items.length, 0);

  return (
    <div className="container mx-auto p-6 space-y-6">
      <div>
        <h1 className="font-heading text-3xl font-bold tracking-tight flex items-center gap-2">
          <FileBarChart className="h-7 w-7" />
          Raporlarım
        </h1>
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4">
          <div>
            <CardTitle>Erişilebilir Raporlar</CardTitle>
            <CardDescription>
              Toplam <strong>{totalCount}</strong> rapor
              {filter && totalCount !== filteredCount ? <> · filtrelenmiş: <strong>{filteredCount}</strong></> : null}
            </CardDescription>
          </div>
          <div className="relative w-72">
            <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-8"
              placeholder="Rapor / kategori ara..."
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
            />
          </div>
        </CardHeader>
        <CardContent>
          {error && (
            <div className="text-center text-destructive py-8 text-sm">Raporlar yüklenemedi.</div>
          )}
          {isLoading ? (
            <div className="flex items-center gap-2 py-12 justify-center text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" />
              Yükleniyor...
            </div>
          ) : !data || data.length === 0 ? (
            <div className="py-16 text-center">
              <FileBarChart className="mx-auto h-10 w-10 text-muted-foreground/40 mb-3" />
              <p className="text-sm text-muted-foreground">
                Henüz hiçbir rapora erişiminiz yok.
              </p>
              <p className="text-xs text-muted-foreground mt-1">
                Yetkilendirme için sistem yöneticinize başvurun.
              </p>
            </div>
          ) : grouped.length === 0 ? (
            <div className="py-12 text-center text-muted-foreground text-sm">Eşleşen rapor yok.</div>
          ) : (
            <div className="space-y-6">
              {grouped.map(([category, items]) => (
                <div key={category}>
                  <div className="flex items-center gap-2 mb-2">
                    <h3 className="font-semibold text-sm text-foreground">{category}</h3>
                    <Badge variant="secondary" className="text-[10px]">{items.length}</Badge>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
                    {items.map((r) => (
                      <button
                        key={r.id}
                        type="button"
                        className="text-left rounded-md border bg-card hover:bg-muted/40 transition-colors px-3 py-3"
                        onClick={() => setActiveReport(r)}
                      >
                        <div className="font-medium text-sm">{r.name}</div>
                        <div className="text-xs text-muted-foreground font-mono mt-1">{r.code}</div>
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <ReportRunPanel report={activeReport} onClose={() => setActiveReport(null)} />
    </div>
  );
}
