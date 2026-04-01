import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription,
} from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  AlertCircle, ArrowRight, ChevronLeft, ChevronRight, Eye,
  Filter, RotateCcw, Search,
} from 'lucide-react';
import { useAuditLogs } from '../hooks/useAuditLog';
import type { AuditLogDto } from '../api/auditLog.api';

/* ────────────────────────────── Mappings ────────────────────────────── */

const ENTITY_TYPE_LABELS: Record<string, string> = {
  AcademicTerm: 'Akademik Dönem',
  CourseSection: 'Ders Seksiyonu',
  CourseRegistration: 'Ders Kaydı',
  Student: 'Öğrenci',
  SystemConfig: 'Sistem Ayarı',
  FacultyScheduleApproval: 'Fakülte Program Onayı',
  PromotionRule: 'Yükselme Kuralı',
  RegistrationPlan: 'Kayıt Planı',
  CourseAccessPolicy: 'Ders Erişim Politikası',
  ClassroomTermAssignment: 'Derslik Dönem Ataması',
  Course: 'Ders',
  Curriculum: 'Müfredat',
  StudentGrade: 'Öğrenci Notu',
  GradeComponent: 'Not Bileşeni',
  GradingCriteria: 'Değerlendirme Kriteri',
  Authentication: 'Kimlik Doğrulama',
  System: 'Sistem',
};

const ACTION_CONFIG: Record<string, { label: string; color: string; bg: string; icon: string }> = {
  Created:      { label: 'Oluşturuldu',      color: 'text-emerald-700', bg: 'bg-emerald-50 border-emerald-200', icon: '＋' },
  Modified:     { label: 'Güncellendi',       color: 'text-blue-700',    bg: 'bg-blue-50 border-blue-200',      icon: '✎' },
  Deleted:      { label: 'Silindi',           color: 'text-red-700',     bg: 'bg-red-50 border-red-200',        icon: '✕' },
  LoginFailed:  { label: 'Başarısız Giriş',   color: 'text-amber-700',   bg: 'bg-amber-50 border-amber-200',    icon: '⚠' },
  DataAccess:   { label: 'Veri Erişimi',      color: 'text-violet-700',  bg: 'bg-violet-50 border-violet-200',  icon: '◉' },
  SystemError:  { label: 'Sistem Hatası',     color: 'text-red-700',     bg: 'bg-red-50 border-red-200',        icon: '✕' },
  Unauthorized: { label: 'Yetkisiz Erişim',   color: 'text-orange-700',  bg: 'bg-orange-50 border-orange-200',  icon: '⛔' },
  Forbidden:    { label: 'Yasaklı Erişim',    color: 'text-orange-700',  bg: 'bg-orange-50 border-orange-200',  icon: '⛔' },
};

const ENTITY_TYPE_OPTIONS = [
  { value: 'all', label: 'Tüm Varlıklar' },
  { value: 'StudentGrade', label: 'Öğrenci Notu' },
  { value: 'CourseRegistration', label: 'Ders Kaydı' },
  { value: 'CourseSection', label: 'Ders Seksiyonu' },
  { value: 'Student', label: 'Öğrenci' },
  { value: 'Course', label: 'Ders' },
  { value: 'Curriculum', label: 'Müfredat' },
  { value: 'AcademicTerm', label: 'Akademik Dönem' },
  { value: 'SystemConfig', label: 'Sistem Ayarı' },
  { value: 'ClassroomTermAssignment', label: 'Derslik Dönem Ataması' },
  { value: 'Authentication', label: 'Kimlik Doğrulama' },
  { value: 'System', label: 'Sistem' },
];

/* ────────────────────────────── Helpers ─────────────────────────────── */

function formatRelativeTime(timestamp: string): string {
  const now = Date.now();
  const t = new Date(timestamp).getTime();
  const diffSec = Math.floor((now - t) / 1000);

  if (diffSec < 60) return 'Az önce';
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)} dk önce`;
  if (diffSec < 86400) return `${Math.floor(diffSec / 3600)} saat önce`;
  if (diffSec < 604800) return `${Math.floor(diffSec / 86400)} gün önce`;

  return new Date(timestamp).toLocaleDateString('tr-TR', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

function formatFullDate(timestamp: string): string {
  return new Date(timestamp).toLocaleString('tr-TR', {
    day: '2-digit', month: 'long', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

function formatJson(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); }
  catch { return value; }
}

function getUserInitials(name: string | undefined): string {
  if (!name) return '#';
  const parts = name.trim().split(' ');
  return parts.length >= 2
    ? (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    : name.substring(0, 2).toUpperCase();
}

function buildSummary(log: AuditLogDto): string {
  const user = log.userName ?? `Kullanıcı #${log.userId}`;
  const entity = ENTITY_TYPE_LABELS[log.entityType] ?? log.entityType;
  const actionCfg = ACTION_CONFIG[log.action];
  const action = actionCfg?.label ?? log.action;

  switch (log.action) {
    case 'Created':
      return `${user}, yeni bir ${entity} kaydı oluşturdu`;
    case 'Modified':
      return `${user}, bir ${entity} kaydını güncelledi`;
    case 'Deleted':
      return `${user}, bir ${entity} kaydını sildi`;
    case 'LoginFailed':
      return `${log.entityId ?? 'Bilinmeyen'} adresiyle başarısız giriş denemesi`;
    case 'DataAccess':
      return `${user}, ${entity} verilerine erişim sağladı`;
    case 'SystemError':
      return `Sistem hatası oluştu`;
    case 'Unauthorized':
    case 'Forbidden':
      return `${user ? user + ' — ' : ''}yetkisiz erişim denemesi`;
    default:
      return `${user} — ${action} (${entity})`;
  }
}

/* ────────────────────────────── Page ────────────────────────────────── */

export default function AuditLogPage() {
  const [entityType, setEntityType] = useState('');
  const [userId, setUserId] = useState('');
  const [action, setAction] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);
  const [showFilters, setShowFilters] = useState(false);
  const pageSize = 30;

  const { data, isLoading, isError } = useAuditLogs({
    entityType: entityType || undefined,
    userId: userId || undefined,
    action: action || undefined,
    from: from || undefined,
    to: to || undefined,
    page,
    pageSize,
  });

  const totalPages = data ? Math.ceil(data.totalCount / pageSize) : 0;
  const hasActiveFilter = !!(entityType || userId || action || from || to);

  function clearFilters() {
    setEntityType('');
    setUserId('');
    setAction('');
    setFrom('');
    setTo('');
    setPage(1);
  }

  return (
    <div className="space-y-5 animate-fade-in">

      {/* ── Toolbar ── */}
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          {data && !isLoading && (
            <>
              <span className="font-semibold text-foreground">{(data.totalCount).toLocaleString('tr-TR')}</span> kayıt
              {hasActiveFilter && <span className="ml-1">(filtrelenmiş)</span>}
            </>
          )}
        </p>
        <div className="flex items-center gap-2">
          {hasActiveFilter && (
            <Button variant="ghost" size="sm" onClick={clearFilters} className="text-muted-foreground">
              <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
              Filtreleri Temizle
            </Button>
          )}
          <Button
            variant={showFilters ? 'default' : 'outline'}
            size="sm"
            onClick={() => setShowFilters(f => !f)}
          >
            <Filter className="h-3.5 w-3.5 mr-1.5" />
            Filtreler
            {hasActiveFilter && (
              <span className="ml-1.5 flex h-4 w-4 items-center justify-center rounded-full bg-white/20 text-[10px] font-bold">
                {[entityType, userId, action, from, to].filter(Boolean).length}
              </span>
            )}
          </Button>
        </div>
      </div>

      {/* ── Filter Bar ── */}
      {showFilters && (
        <Card className="animate-fade-in border-dashed">
          <CardContent className="pt-4 pb-4">
            <div className="flex flex-wrap items-end gap-3">
              <FilterField label="Varlık Tipi">
                <Select value={entityType || 'all'} onValueChange={v => { setEntityType(v === 'all' ? '' : v); setPage(1); }}>
                  <SelectTrigger className="h-9 w-48">
                    <SelectValue placeholder="Tüm Varlıklar" />
                  </SelectTrigger>
                  <SelectContent>
                    {ENTITY_TYPE_OPTIONS.map(o => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FilterField>

              <FilterField label="İşlem Türü">
                <Select value={action || 'all'} onValueChange={v => { setAction(v === 'all' ? '' : v); setPage(1); }}>
                  <SelectTrigger className="h-9 w-48">
                    <SelectValue placeholder="Tüm İşlemler" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tüm İşlemler</SelectItem>
                    {Object.entries(ACTION_CONFIG).map(([key, cfg]) => (
                      <SelectItem key={key} value={key}>
                        <span className="flex items-center gap-2">
                          <span className={`text-xs ${cfg.color}`}>{cfg.icon}</span>
                          {cfg.label}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FilterField>

              <FilterField label="Kullanıcı ID">
                <Input
                  className="h-9 w-32"
                  placeholder="örn: 1"
                  value={userId}
                  onChange={e => { setUserId(e.target.value); setPage(1); }}
                />
              </FilterField>

              <FilterField label="Başlangıç">
                <Input
                  type="date"
                  className="h-9 w-40"
                  value={from}
                  onChange={e => { setFrom(e.target.value); setPage(1); }}
                />
              </FilterField>

              <FilterField label="Bitiş">
                <Input
                  type="date"
                  className="h-9 w-40"
                  value={to}
                  onChange={e => { setTo(e.target.value); setPage(1); }}
                />
              </FilterField>

              <Button size="sm" className="h-9" onClick={() => setPage(1)}>
                <Search className="h-3.5 w-3.5 mr-1.5" />
                Ara
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* ── Results ── */}
      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="h-16 rounded-lg" />
          ))}
        </div>
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Denetim kayıtları yüklenemedi. Lütfen tekrar deneyin.</AlertDescription>
        </Alert>
      ) : (
        <>
          {/* Table */}
          <Card className="overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40 hover:bg-muted/40">
                  <TableHead className="w-[180px] font-semibold">Zaman</TableHead>
                  <TableHead className="w-[140px] font-semibold">İşlem</TableHead>
                  <TableHead className="font-semibold">Açıklama</TableHead>
                  <TableHead className="w-[180px] font-semibold">Kullanıcı</TableHead>
                  <TableHead className="w-[60px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(data?.items ?? []).length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center py-16">
                      <div className="flex flex-col items-center gap-2 text-muted-foreground">
                        <Search className="h-8 w-8 opacity-30" />
                        <p className="text-sm font-medium">Kayıt bulunamadı</p>
                        <p className="text-xs">Filtre kriterlerinizi değiştirmeyi deneyin</p>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  (data?.items ?? []).map(log => (
                    <AuditLogRow
                      key={log.id}
                      log={log}
                      onViewDetail={() => setSelectedLog(log)}
                    />
                  ))
                )}
              </TableBody>
            </Table>
          </Card>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-1">
              <Button
                variant="outline" size="icon"
                className="h-8 w-8"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>

              {generatePageNumbers(page, totalPages).map((p, i) =>
                p === '...' ? (
                  <span key={`ellipsis-${i}`} className="px-1 text-xs text-muted-foreground">...</span>
                ) : (
                  <Button
                    key={p}
                    variant={p === page ? 'default' : 'outline'}
                    size="icon"
                    className="h-8 w-8 text-xs"
                    onClick={() => setPage(p as number)}
                  >
                    {p}
                  </Button>
                )
              )}

              <Button
                variant="outline" size="icon"
                className="h-8 w-8"
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </>
      )}

      {/* ── Detail Sheet ── */}
      <Sheet open={!!selectedLog} onOpenChange={open => { if (!open) setSelectedLog(null); }}>
        <SheetContent side="right" className="w-full sm:max-w-lg overflow-y-auto">
          {selectedLog && <LogDetailPanel log={selectedLog} />}
        </SheetContent>
      </Sheet>
    </div>
  );
}

/* ────────────────────────────── Sub-components ──────────────────────── */

function FilterField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label className="text-xs font-medium text-muted-foreground uppercase tracking-wider">{label}</label>
      {children}
    </div>
  );
}

function AuditLogRow({ log, onViewDetail }: { log: AuditLogDto; onViewDetail: () => void }) {
  const actionCfg = ACTION_CONFIG[log.action] ?? { label: log.action, color: 'text-gray-700', bg: 'bg-gray-50 border-gray-200', icon: '•' };
  const summary = buildSummary(log);
  const entityLabel = ENTITY_TYPE_LABELS[log.entityType] ?? log.entityType;
  const userName = log.userName ?? `Kullanıcı #${log.userId}`;
  const initials = getUserInitials(log.userName);

  return (
    <TableRow className="group hover:bg-muted/30 transition-colors">
      {/* Zaman */}
      <TableCell className="py-3">
        <div className="flex flex-col">
          <span className="text-sm text-foreground">{formatRelativeTime(log.timestamp)}</span>
          <span className="text-[11px] text-muted-foreground">{formatFullDate(log.timestamp)}</span>
        </div>
      </TableCell>

      {/* İşlem Badge */}
      <TableCell className="py-3">
        <span className={`inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-xs font-medium ${actionCfg.bg} ${actionCfg.color}`}>
          <span className="text-[10px] leading-none">{actionCfg.icon}</span>
          {actionCfg.label}
        </span>
      </TableCell>

      {/* Açıklama */}
      <TableCell className="py-3">
        <p className="text-sm text-foreground leading-snug">{summary}</p>
        <div className="flex items-center gap-2 mt-0.5">
          <span className="text-[11px] text-muted-foreground">{entityLabel}</span>
          {log.entityId && (
            <>
              <span className="text-muted-foreground/40">·</span>
              <span className="text-[11px] font-mono text-muted-foreground">#{log.entityId}</span>
            </>
          )}
        </div>
      </TableCell>

      {/* Kullanıcı */}
      <TableCell className="py-3">
        <div className="flex items-center gap-2">
          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary text-[10px] font-bold">
            {initials}
          </div>
          <span className="text-sm text-foreground truncate max-w-[120px]">{userName}</span>
        </div>
      </TableCell>

      {/* Detay */}
      <TableCell className="py-3">
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 opacity-0 group-hover:opacity-100 transition-opacity"
          onClick={onViewDetail}
        >
          <Eye className="h-3.5 w-3.5" />
        </Button>
      </TableCell>
    </TableRow>
  );
}

function LogDetailPanel({ log }: { log: AuditLogDto }) {
  const actionCfg = ACTION_CONFIG[log.action] ?? { label: log.action, color: 'text-gray-700', bg: 'bg-gray-50 border-gray-200', icon: '•' };
  const entityLabel = ENTITY_TYPE_LABELS[log.entityType] ?? log.entityType;
  const userName = log.userName ?? `Kullanıcı #${log.userId}`;

  // Parse JSON values into key-value pairs for display
  let oldObj: Record<string, unknown> | null = null;
  let newObj: Record<string, unknown> | null = null;
  try { if (log.oldValue) oldObj = JSON.parse(log.oldValue); } catch { /* skip */ }
  try { if (log.newValue) newObj = JSON.parse(log.newValue); } catch { /* skip */ }

  // For Modified: find changed keys
  const allKeys = oldObj && newObj
    ? [...new Set([...Object.keys(oldObj), ...Object.keys(newObj)])]
    : [];
  const changedKeys = allKeys.filter(k => JSON.stringify(oldObj![k]) !== JSON.stringify(newObj![k]));
  const unchangedKeys = allKeys.filter(k => JSON.stringify(oldObj![k]) === JSON.stringify(newObj![k]));

  // For Created/Deleted: show all fields
  const singleObj = newObj ?? oldObj;
  const singleKeys = singleObj ? Object.keys(singleObj) : [];

  return (
    <div className="space-y-5">
      {/* Header */}
      <SheetHeader className="space-y-3">
        <div className="flex items-center gap-2.5">
          <span className={`inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-xs font-semibold ${actionCfg.bg} ${actionCfg.color}`}>
            {actionCfg.icon} {actionCfg.label}
          </span>
        </div>
        <SheetTitle className="text-xl">{entityLabel}</SheetTitle>
        <SheetDescription className="text-sm">{buildSummary(log)}</SheetDescription>
      </SheetHeader>

      {/* ── Genel Bilgiler ── */}
      <div className="rounded-lg border bg-muted/20 p-4">
        <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-3">Genel Bilgiler</h3>
        <div className="grid grid-cols-2 gap-y-3 gap-x-4">
          <div>
            <p className="text-[11px] text-muted-foreground">Log ID</p>
            <p className="text-sm font-mono font-medium">{log.id}</p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Kayıt ID (Entity)</p>
            <p className="text-sm font-mono font-medium">{log.entityId ?? '—'}</p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Varlık Tipi</p>
            <p className="text-sm font-medium">{entityLabel} <span className="text-muted-foreground font-mono text-xs">({log.entityType})</span></p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">İşlem</p>
            <p className="text-sm font-medium">{actionCfg.label} <span className="text-muted-foreground font-mono text-xs">({log.action})</span></p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Tarih / Saat</p>
            <p className="text-sm font-medium">{formatFullDate(log.timestamp)}</p>
          </div>
          <div>
            <p className="text-[11px] text-muted-foreground">Kullanıcı</p>
            <p className="text-sm font-medium">{userName} {log.userId && <span className="text-muted-foreground font-mono text-xs">(ID: {log.userId})</span>}</p>
          </div>
          <div className="col-span-2">
            <p className="text-[11px] text-muted-foreground">IP Adresi</p>
            <p className="text-sm font-mono font-medium">{log.ipAddress ?? '—'}</p>
          </div>
        </div>
      </div>

      {/* ── Modified: Diff View ── */}
      {log.action === 'Modified' && oldObj && newObj && changedKeys.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            Değişen Alanlar <span className="text-foreground">({changedKeys.length})</span>
          </h3>
          <div className="space-y-2">
            {changedKeys.map(key => (
              <div key={key} className="rounded-lg border p-3 space-y-2">
                <p className="text-xs font-bold font-mono text-foreground">{key}</p>
                <div className="flex items-start gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="rounded bg-red-50 border border-red-200 px-2.5 py-1.5">
                      <p className="text-[10px] text-red-400 uppercase tracking-wider mb-0.5">Eski Değer</p>
                      <code className="text-xs text-red-800 break-all font-mono">{oldObj![key] !== undefined ? String(oldObj![key]) : '—'}</code>
                    </div>
                  </div>
                  <ArrowRight className="h-3.5 w-3.5 text-muted-foreground mt-4 shrink-0" />
                  <div className="flex-1 min-w-0">
                    <div className="rounded bg-emerald-50 border border-emerald-200 px-2.5 py-1.5">
                      <p className="text-[10px] text-emerald-400 uppercase tracking-wider mb-0.5">Yeni Değer</p>
                      <code className="text-xs text-emerald-800 break-all font-mono">{newObj![key] !== undefined ? String(newObj![key]) : '—'}</code>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Unchanged fields - collapsed */}
          {unchangedKeys.length > 0 && (
            <details className="group">
              <summary className="text-xs text-muted-foreground cursor-pointer hover:text-foreground transition-colors">
                Değişmeyen alanlar ({unchangedKeys.length})
              </summary>
              <div className="mt-2 rounded-lg border bg-muted/20 p-3">
                <div className="grid grid-cols-1 gap-1.5">
                  {unchangedKeys.map(key => (
                    <div key={key} className="flex items-baseline gap-2 text-xs">
                      <span className="font-mono font-medium text-muted-foreground w-32 shrink-0 truncate">{key}</span>
                      <span className="text-foreground truncate">{String(newObj![key] ?? '—')}</span>
                    </div>
                  ))}
                </div>
              </div>
            </details>
          )}
        </div>
      )}

      {/* ── Created / Deleted: Full Data ── */}
      {(log.action === 'Created' || log.action === 'Deleted') && singleObj && singleKeys.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
            {log.action === 'Created' ? 'Oluşturulan Veri' : 'Silinen Veri'}
            <span className="text-foreground ml-1">({singleKeys.length} alan)</span>
          </h3>
          <div className="rounded-lg border overflow-hidden">
            <table className="w-full text-xs">
              <thead>
                <tr className="bg-muted/40">
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Alan</th>
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Değer</th>
                </tr>
              </thead>
              <tbody>
                {singleKeys.map((key, i) => (
                  <tr key={key} className={i % 2 === 0 ? 'bg-background' : 'bg-muted/10'}>
                    <td className="px-3 py-1.5 font-mono font-medium text-foreground whitespace-nowrap">{key}</td>
                    <td className="px-3 py-1.5 text-muted-foreground break-all">{String(singleObj![key] ?? '—')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Other actions with newValue (LoginFailed, DataAccess, SystemError etc.) ── */}
      {!['Created', 'Modified', 'Deleted'].includes(log.action) && newObj && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Detay Verisi</h3>
          <div className="rounded-lg border overflow-hidden">
            <table className="w-full text-xs">
              <thead>
                <tr className="bg-muted/40">
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Alan</th>
                  <th className="text-left px-3 py-2 font-semibold text-muted-foreground">Değer</th>
                </tr>
              </thead>
              <tbody>
                {Object.entries(newObj).map(([key, val], i) => (
                  <tr key={key} className={i % 2 === 0 ? 'bg-background' : 'bg-muted/10'}>
                    <td className="px-3 py-1.5 font-mono font-medium text-foreground whitespace-nowrap">{key}</td>
                    <td className="px-3 py-1.5 text-muted-foreground break-all">
                      {typeof val === 'string' && val.length > 200
                        ? <pre className="whitespace-pre-wrap font-mono">{val}</pre>
                        : String(val ?? '—')
                      }
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Raw JSON (fallback for non-parsed data) ── */}
      {(log.oldValue || log.newValue) && !oldObj && !newObj && (
        <div className="space-y-3">
          <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Ham Veri</h3>
          {log.oldValue && (
            <div>
              <p className="text-[11px] text-muted-foreground mb-1">Eski Değer</p>
              <pre className="text-xs bg-muted/40 border rounded-lg p-3 overflow-auto max-h-60 whitespace-pre-wrap font-mono">
                {formatJson(log.oldValue)}
              </pre>
            </div>
          )}
          {log.newValue && (
            <div>
              <p className="text-[11px] text-muted-foreground mb-1">Yeni Değer</p>
              <pre className="text-xs bg-muted/40 border rounded-lg p-3 overflow-auto max-h-60 whitespace-pre-wrap font-mono">
                {formatJson(log.newValue)}
              </pre>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/* ────────────────────────────── Pagination Helper ───────────────────── */

function generatePageNumbers(current: number, total: number): (number | '...')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

  const pages: (number | '...')[] = [1];

  if (current > 3) pages.push('...');

  const start = Math.max(2, current - 1);
  const end = Math.min(total - 1, current + 1);

  for (let i = start; i <= end; i++) pages.push(i);

  if (current < total - 2) pages.push('...');

  pages.push(total);
  return pages;
}
