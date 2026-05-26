import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, FileBarChart2, FileSearch, Loader2, Search } from 'lucide-react';
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
          (r.description?.toLowerCase().includes(q) ?? false) ||
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

  return (
    <div className="flex min-h-[calc(100vh-7rem)] flex-col p-6">
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border bg-card shadow-sm">
        {/* Başlık şeridi: bağlam + arama */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">
          <span className="text-sm font-semibold text-foreground">Erişilebilir Raporlar</span>
          <div className="relative w-full sm:w-80">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 rounded-lg pl-9"
              placeholder="Rapor veya kategori ara..."
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
            />
          </div>
        </div>

        {/* Gövde */}
        <div className="min-h-0 flex-1 overflow-auto bg-muted/20 p-5">
          {/* Durumlar */}
      {error ? (
        <StateBox tone="error" icon={FileBarChart2} title="Raporlar yüklenemedi" subtitle="Lütfen sayfayı yenileyin." />
      ) : isLoading ? (
        <div className="flex items-center justify-center gap-2 py-20 text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Yükleniyor...
        </div>
      ) : !data || data.length === 0 ? (
        <StateBox
          icon={FileBarChart2}
          title="Henüz hiçbir rapora erişiminiz yok"
          subtitle="Yetkilendirme için sistem yöneticinize başvurun."
        />
      ) : grouped.length === 0 ? (
        <StateBox icon={FileSearch} title="Eşleşen rapor yok" subtitle={`"${filter}" için sonuç bulunamadı.`} />
      ) : (
        <div className="space-y-8">
          {grouped.map(([category, items]) => (
            <section key={category}>
              <div className="mb-3 flex items-center gap-2.5">
                <span className="h-4 w-1 rounded-full bg-primary" />
                <h2 className="text-sm font-semibold uppercase tracking-wide text-foreground/80">{category}</h2>
                <Badge variant="secondary" className="text-[10px]">{items.length}</Badge>
              </div>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {items.map((r) => (
                  <button
                    key={r.id}
                    type="button"
                    onClick={() => setActiveReport(r)}
                    className="group flex flex-col rounded-xl border bg-card p-4 text-left transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <div className="mb-3 flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground">
                      <FileBarChart2 className="h-4 w-4" />
                    </div>
                    <div className="font-medium leading-snug">{r.name}</div>
                    {r.description && (
                      <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{r.description}</p>
                    )}
                    <div className="mt-3 flex items-center text-xs font-medium text-muted-foreground transition-colors group-hover:text-primary">
                      Aç
                      <ArrowRight className="ml-1 h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
                    </div>
                  </button>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}
        </div>
      </div>

      <ReportRunPanel report={activeReport} onClose={() => setActiveReport(null)} />
    </div>
  );
}

function StateBox({
  icon: Icon,
  title,
  subtitle,
  tone = 'default',
}: {
  icon: typeof FileBarChart2;
  title: string;
  subtitle?: string;
  tone?: 'default' | 'error';
}) {
  return (
    <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed py-20 text-center">
      <div className={`mb-3 flex h-12 w-12 items-center justify-center rounded-xl ${tone === 'error' ? 'bg-destructive/10 text-destructive' : 'bg-muted text-muted-foreground'}`}>
        <Icon className="h-6 w-6" />
      </div>
      <p className={`text-sm font-medium ${tone === 'error' ? 'text-destructive' : 'text-foreground'}`}>{title}</p>
      {subtitle && <p className="mt-1 text-xs text-muted-foreground">{subtitle}</p>}
    </div>
  );
}
