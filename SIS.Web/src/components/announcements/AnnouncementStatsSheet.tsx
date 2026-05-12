import { useMemo } from 'react';
import { Dialog, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  Check,
  Clock,
  Eye,
  FileText,
  Flame,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { useAnnouncementStats } from '../../hooks/useAnnouncements';
import { formatDate } from '../../lib/utils';
import type { AnnouncementReader } from '../../types/announcementStats';

interface AnnouncementStatsSheetProps {
  announcementId: number | null;
  announcementTitle: string;
  onClose: () => void;
}

export default function AnnouncementStatsSheet({
  announcementId,
  announcementTitle,
  onClose,
}: AnnouncementStatsSheetProps) {
  const open = announcementId != null;
  const { data, isLoading, isError } = useAnnouncementStats(announcementId, open);

  // Onay oranı: okuyan kişiler arasında ne kadarı onayladı (RequiresAck yoksa anlamsız)
  const ackRate = useMemo(() => {
    if (!data || data.readCount === 0) return 0;
    return Math.round((data.acknowledgedCount / data.readCount) * 100);
  }, [data]);

  // İlk okuma ile son okuma arasındaki süre
  const readingSpan = useMemo(() => {
    if (!data?.firstReadAt || !data?.lastReadAt) return null;
    const first = new Date(data.firstReadAt).getTime();
    const last  = new Date(data.lastReadAt).getTime();
    const diffMs = last - first;
    if (diffMs <= 0) return 'Tek seferde';
    const minutes = Math.floor(diffMs / 60_000);
    if (minutes < 60) return `${minutes} dk`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} sa ${minutes % 60} dk`;
    const days = Math.floor(hours / 24);
    return `${days} gün ${hours % 24} sa`;
  }, [data]);

  const chartData = useMemo(() => {
    if (!data) return [];
    return data.dailyReads.map((d) => ({
      day: new Date(d.day).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short' }),
      count: d.count,
    }));
  }, [data]);

  const hasAnyReads     = (data?.readCount ?? 0) > 0;
  const hasTrendActivity = chartData.some((d) => d.count > 0);

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <ResizableDialogContent
        defaultWidth={880}
        defaultHeight={680}
        minWidth={680}
        minHeight={500}
        className="flex flex-col p-0 gap-0"
      >
        {/* Header */}
        <DialogHeader className="px-6 pt-5 pb-4 shrink-0 border-b border-border/60 bg-gradient-to-r from-card via-card to-accent/20">
          <div className="flex items-start gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary-light shadow-md shrink-0">
              <Eye className="h-5 w-5 text-white" />
            </div>
            <div className="min-w-0 flex-1">
              <DialogTitle className="text-lg leading-tight truncate">
                {announcementTitle}
              </DialogTitle>
              <p className="text-xs text-muted-foreground mt-0.5">
                Okundu istatistikleri
              </p>
            </div>
          </div>
        </DialogHeader>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-5">
          {isLoading && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-4 gap-3">
                {[1, 2, 3, 4].map((i) => (
                  <Skeleton key={i} className="h-24 rounded-xl" />
                ))}
              </div>
              <Skeleton className="h-48 rounded-xl" />
              <Skeleton className="h-32 rounded-xl" />
            </div>
          )}

          {isError && (
            <p className="text-sm text-destructive">İstatistikler yüklenemedi.</p>
          )}

          {data && (
            <>
              {/* ── 4'lü KPI kartları ──────────────────────────── */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                <KpiCard
                  icon={<Users className="h-4 w-4" />}
                  label="Okuyan"
                  value={data.readCount}
                  accent="primary"
                />
                <AckRateCard
                  acknowledged={data.acknowledgedCount}
                  read={data.readCount}
                  rate={ackRate}
                />
                <KpiCard
                  icon={<Flame className="h-4 w-4" />}
                  label="Son 24 Saat"
                  value={data.readsInLast24h}
                  accent="amber"
                  suffix="okuma"
                />
                <KpiCard
                  icon={<FileText className="h-4 w-4" />}
                  label="Ekli Dosya"
                  value={data.attachmentCount}
                  accent="slate"
                />
              </div>

              {/* ── 14 günlük okuma trendi ──────────────────────── */}
              <Section title="Son 14 Gün" subtitle="Günlük okuma sayısı">
                {hasTrendActivity ? (
                  <div className="h-44 -mx-2">
                    <ResponsiveContainer width="100%" height="100%">
                      <AreaChart
                        data={chartData}
                        margin={{ top: 8, right: 16, left: 8, bottom: 0 }}
                      >
                        <defs>
                          <linearGradient id="readGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%"  stopColor="oklch(0.55 0.12 250)" stopOpacity={0.35} />
                            <stop offset="95%" stopColor="oklch(0.55 0.12 250)" stopOpacity={0}    />
                          </linearGradient>
                        </defs>
                        <CartesianGrid
                          strokeDasharray="3 3"
                          stroke="oklch(0.92 0.005 250)"
                          vertical={false}
                        />
                        <XAxis
                          dataKey="day"
                          tick={{ fontSize: 10, fill: 'oklch(0.55 0.02 260)' }}
                          axisLine={false}
                          tickLine={false}
                        />
                        <YAxis
                          allowDecimals={false}
                          tick={{ fontSize: 10, fill: 'oklch(0.55 0.02 260)' }}
                          axisLine={false}
                          tickLine={false}
                          width={28}
                        />
                        <Tooltip
                          contentStyle={{
                            fontSize: 12,
                            borderRadius: 8,
                            border: '1px solid oklch(0.92 0.005 250)',
                          }}
                          labelStyle={{ fontWeight: 600 }}
                        />
                        <Area
                          type="monotone"
                          dataKey="count"
                          name="Okuma"
                          stroke="oklch(0.45 0.12 250)"
                          strokeWidth={2}
                          fill="url(#readGradient)"
                        />
                      </AreaChart>
                    </ResponsiveContainer>
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground text-center py-6">
                    Son 14 günde okuma kaydı yok.
                  </p>
                )}
              </Section>

              {/* ── Zaman çizgisi ──────────────────────────────── */}
              {hasAnyReads && (
                <Section title="Zaman Çizgisi" subtitle="Okuma yayılımı">
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    <TimelineItem
                      icon={<Clock className="h-4 w-4 text-emerald-600" />}
                      label="İlk Okuma"
                      value={data.firstReadAt ? formatDate(data.firstReadAt) : '—'}
                    />
                    <TimelineItem
                      icon={<Clock className="h-4 w-4 text-blue-600" />}
                      label="Son Okuma"
                      value={data.lastReadAt ? formatDate(data.lastReadAt) : '—'}
                    />
                    <TimelineItem
                      icon={<Clock className="h-4 w-4 text-amber-600" />}
                      label="Yayılım Süresi"
                      value={readingSpan ?? '—'}
                    />
                  </div>
                </Section>
              )}

              {/* ── Son okuyucular ─────────────────────────────── */}
              <Section
                title="Okuyucular"
                subtitle={
                  data.recentReaders.length > 0
                    ? `En yeni ${data.recentReaders.length} kişi`
                    : 'Henüz okuyan yok'
                }
              >
                {data.recentReaders.length === 0 ? (
                  <p className="text-sm text-muted-foreground text-center py-6">
                    Henüz okuyan yok.
                  </p>
                ) : (
                  <ul className="divide-y divide-border/60">
                    {data.recentReaders.map((r) => (
                      <ReaderRow key={r.userId} reader={r} />
                    ))}
                  </ul>
                )}
              </Section>
            </>
          )}
        </div>
      </ResizableDialogContent>
    </Dialog>
  );
}

// ─── Yardımcı bileşenler ──────────────────────────────────────────

type Accent = 'primary' | 'emerald' | 'amber' | 'slate';

const ACCENT_STYLES: Record<Accent, { bg: string; iconBg: string; iconFg: string }> = {
  primary: { bg: 'from-primary/8 to-primary/3',         iconBg: 'bg-primary/15',        iconFg: 'text-primary'       },
  emerald: { bg: 'from-emerald-500/10 to-emerald-500/3', iconBg: 'bg-emerald-500/15',  iconFg: 'text-emerald-600'  },
  amber:   { bg: 'from-amber-500/12 to-amber-500/3',    iconBg: 'bg-amber-500/15',    iconFg: 'text-amber-600'    },
  slate:   { bg: 'from-slate-500/10 to-slate-500/3',    iconBg: 'bg-slate-500/15',    iconFg: 'text-slate-600'    },
};

interface KpiCardProps {
  icon: React.ReactNode;
  label: string;
  value: number;
  suffix?: string;
  accent: Accent;
}

function KpiCard({ icon, label, value, suffix, accent }: KpiCardProps) {
  const s = ACCENT_STYLES[accent];
  return (
    <div className={`relative overflow-hidden rounded-xl border border-border/70 bg-gradient-to-br ${s.bg} px-4 py-3.5`}>
      <div className="flex items-center justify-between mb-1.5">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
          {label}
        </span>
        <span className={`flex h-7 w-7 items-center justify-center rounded-lg ${s.iconBg} ${s.iconFg}`}>
          {icon}
        </span>
      </div>
      <div className="flex items-baseline gap-1.5">
        <span className="font-heading text-2xl font-bold tabular-nums leading-none">
          {value}
        </span>
        {suffix && <span className="text-xs text-muted-foreground">{suffix}</span>}
      </div>
    </div>
  );
}

interface AckRateCardProps {
  acknowledged: number;
  read: number;
  rate: number;
}

function AckRateCard({ acknowledged, read, rate }: AckRateCardProps) {
  return (
    <div className="relative overflow-hidden rounded-xl border border-border/70 bg-gradient-to-br from-emerald-500/10 to-emerald-500/3 px-4 py-3.5">
      <div className="flex items-center justify-between mb-1.5">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
          Onay Oranı
        </span>
        <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-500/15 text-emerald-600">
          <ShieldCheck className="h-4 w-4" />
        </span>
      </div>
      <div className="flex items-baseline gap-1.5">
        <span className="font-heading text-2xl font-bold tabular-nums leading-none text-emerald-700">
          %{rate}
        </span>
        <span className="text-xs text-muted-foreground">
          {acknowledged}/{read}
        </span>
      </div>
      <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-emerald-500/15">
        <div
          className="h-full bg-emerald-500 transition-all"
          style={{ width: `${rate}%` }}
        />
      </div>
    </div>
  );
}

interface SectionProps {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
}

function Section({ title, subtitle, children }: SectionProps) {
  return (
    <section className="rounded-xl border border-border/70 bg-card/60 p-4">
      <div className="flex items-center justify-between mb-3">
        <h4 className="text-sm font-semibold">{title}</h4>
        {subtitle && (
          <span className="text-[11px] text-muted-foreground">{subtitle}</span>
        )}
      </div>
      {children}
    </section>
  );
}

interface TimelineItemProps {
  icon: React.ReactNode;
  label: string;
  value: string;
}

function TimelineItem({ icon, label, value }: TimelineItemProps) {
  return (
    <div className="flex items-center gap-2.5 rounded-lg bg-muted/40 px-3 py-2.5">
      <div className="shrink-0">{icon}</div>
      <div className="min-w-0">
        <p className="text-[11px] text-muted-foreground uppercase tracking-wide">{label}</p>
        <p className="text-sm font-semibold truncate">{value}</p>
      </div>
    </div>
  );
}

// ─── Renkli baş harf avatar ───────────────────────────────────────

const AVATAR_COLORS = [
  'bg-primary/15 text-primary',
  'bg-emerald-500/15 text-emerald-700',
  'bg-amber-500/15 text-amber-700',
  'bg-pink-500/15 text-pink-700',
  'bg-blue-500/15 text-blue-700',
  'bg-violet-500/15 text-violet-700',
  'bg-cyan-500/15 text-cyan-700',
  'bg-rose-500/15 text-rose-700',
];

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function avatarColorFor(userId: number): string {
  return AVATAR_COLORS[userId % AVATAR_COLORS.length];
}

function ReaderRow({ reader }: { reader: AnnouncementReader }) {
  return (
    <li className="flex items-center gap-3 py-2.5">
      <div className={`flex h-9 w-9 items-center justify-center rounded-full text-xs font-bold shrink-0 ${avatarColorFor(reader.userId)}`}>
        {getInitials(reader.displayName)}
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium truncate">{reader.displayName}</p>
        {reader.email && (
          <p className="text-[11px] text-muted-foreground truncate">{reader.email}</p>
        )}
      </div>
      <div className="text-right shrink-0">
        <p className="text-[11px] text-muted-foreground tabular-nums">
          {formatDate(reader.readAt)}
        </p>
        {reader.acknowledgedAt && (
          <Badge variant="secondary" className="mt-0.5 text-[9px] gap-0.5 px-1.5 bg-emerald-500/12 text-emerald-700 border-0">
            <Check className="h-2.5 w-2.5" />
            Onaylandı
          </Badge>
        )}
      </div>
    </li>
  );
}
