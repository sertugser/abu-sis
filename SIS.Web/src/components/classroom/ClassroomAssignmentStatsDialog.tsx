import { useMemo } from 'react';
import {
  Dialog, DialogFooter, DialogHeader, DialogTitle, DialogDescription,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  AlertCircle, BarChart3, Building2, CheckCircle2, Layers, MapPin, Users,
} from 'lucide-react';
import type { ClassroomDto } from '../../api/classroom.api';
import type { ClassroomTermAssignmentDto } from '../../api/classroomAssignment.api';
import {
  CLASSROOM_TYPE_CODES,
  getClassroomTypeLabel,
  getClassroomTypeBadgeClass,
  getClassroomTypeBarClass,
} from '../../lib/classroomTypeUtils';

// ── Props ──────────────────────────────────────────────────────────────────

interface ClassroomAssignmentStatsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  termName?: string;
  classrooms: ClassroomDto[];
  assignments: ClassroomTermAssignmentDto[];
  facultyCount: number; // toplam fakülte sayısı (kapsam için)
}

// ── Faculty palette (Stats dialog için — Page'le ortak olabilir, şimdilik local) ──

const FACULTY_BAR_COLORS = [
  'bg-blue-500', 'bg-emerald-500', 'bg-amber-500', 'bg-violet-500',
  'bg-rose-500', 'bg-cyan-500', 'bg-teal-500', 'bg-fuchsia-500',
];

// ── Component ──────────────────────────────────────────────────────────────

export default function ClassroomAssignmentStatsDialog({
  open, onOpenChange, termName, classrooms, assignments, facultyCount,
}: ClassroomAssignmentStatsDialogProps) {
  const stats = useMemo(() => {
    const total = classrooms.length;
    const assigned = assignments.length;
    const unassigned = total - assigned;
    const coveragePct = total > 0 ? Math.round((assigned / total) * 100) : 0;

    // Toplam kapasite (atanmış / atanmamış / genel)
    const assignedClassroomIds = new Set(assignments.map(a => a.classroomId));
    const totalCapacity = classrooms.reduce((s, c) => s + (c.capacity || 0), 0);
    const assignedCapacity = classrooms
      .filter(c => assignedClassroomIds.has(c.id))
      .reduce((s, c) => s + (c.capacity || 0), 0);
    const unassignedCapacity = totalCapacity - assignedCapacity;

    // Tip dağılımı (toplam vs atanmış)
    const typeCounts: Record<string, { total: number; assigned: number }> = {};
    for (const c of classrooms) {
      if (!typeCounts[c.type]) typeCounts[c.type] = { total: 0, assigned: 0 };
      typeCounts[c.type].total++;
      if (assignedClassroomIds.has(c.id)) typeCounts[c.type].assigned++;
    }

    // Fakülte bazlı breakdown
    const facultyMap = new Map<number, {
      facultyId: number;
      facultyName: string;
      classroomCount: number;
      totalCapacity: number;
      byType: Record<string, number>;
    }>();
    for (const a of assignments) {
      const cr = classrooms.find(c => c.id === a.classroomId);
      if (!cr) continue;
      let f = facultyMap.get(a.facultyId);
      if (!f) {
        f = {
          facultyId: a.facultyId,
          facultyName: a.facultyName,
          classroomCount: 0,
          totalCapacity: 0,
          byType: {},
        };
        facultyMap.set(a.facultyId, f);
      }
      f.classroomCount++;
      f.totalCapacity += cr.capacity || 0;
      f.byType[cr.type] = (f.byType[cr.type] || 0) + 1;
    }
    const facultyBreakdown = Array.from(facultyMap.values())
      .sort((a, b) => b.classroomCount - a.classroomCount);

    // Bina bazlı kullanım
    const buildingMap = new Map<string, { total: number; assigned: number; capacity: number }>();
    for (const c of classrooms) {
      const key = c.buildingName?.trim() || 'Belirtilmemiş';
      let b = buildingMap.get(key);
      if (!b) {
        b = { total: 0, assigned: 0, capacity: 0 };
        buildingMap.set(key, b);
      }
      b.total++;
      b.capacity += c.capacity || 0;
      if (assignedClassroomIds.has(c.id)) b.assigned++;
    }
    const buildingBreakdown = Array.from(buildingMap.entries())
      .map(([name, v]) => ({ name, ...v }))
      .sort((a, b) => b.total - a.total);

    // Atanmamış derslikler — tipe göre breakdown
    const unassignedByType: Record<string, number> = {};
    for (const c of classrooms) {
      if (!assignedClassroomIds.has(c.id)) {
        unassignedByType[c.type] = (unassignedByType[c.type] || 0) + 1;
      }
    }

    return {
      total, assigned, unassigned, coveragePct,
      totalCapacity, assignedCapacity, unassignedCapacity,
      typeCounts,
      facultyBreakdown,
      buildingBreakdown,
      unassignedByType,
      activeFacultyCount: facultyMap.size,
    };
  }, [classrooms, assignments]);

  // Tip kodlarını DB-driven sırala (sayfadaki gibi)
  const sortedTypeCodes = useMemo(() => {
    const known = (CLASSROOM_TYPE_CODES as readonly string[]).filter(t => stats.typeCounts[t]?.total > 0);
    const knownSet = new Set<string>(CLASSROOM_TYPE_CODES);
    const extras = Object.keys(stats.typeCounts)
      .filter(t => !knownSet.has(t))
      .sort((a, b) => getClassroomTypeLabel(a).localeCompare(getClassroomTypeLabel(b), 'tr'));
    return [...known, ...extras];
  }, [stats.typeCounts]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent
        defaultWidth={1024}
        defaultHeight={760}
        minWidth={720}
        minHeight={500}
        className="flex flex-col p-0 gap-0 overflow-hidden"
      >
        <DialogHeader className="shrink-0 border-b border-slate-100 bg-gradient-to-r from-violet-50/70 via-violet-50/30 to-transparent px-6 py-4 pr-28">
          <DialogTitle className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-violet-100 text-violet-700">
              <BarChart3 className="h-4 w-4" />
            </span>
            Atama İstatistikleri
            {termName && (
              <span className="ml-2 text-sm font-normal text-slate-500">— {termName}</span>
            )}
          </DialogTitle>
          <DialogDescription>
            Derslik dağılımı, fakülte ve bina kullanımı, tip bazlı analiz.
            <span className="ml-2 text-[10px] text-slate-400">(Pencereyi sürüklemek için çift tık · kenarlardan boyutlandır)</span>
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto px-6 py-5">
          <div className="space-y-6">

            {/* ── Genel özet kartları ───────────────────────────────────── */}
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <SummaryCard
                icon={<Building2 className="h-4 w-4" />}
                label="Toplam Derslik"
                value={stats.total}
                hint={`${stats.totalCapacity.toLocaleString('tr-TR')} kişi kapasite`}
                color="blue"
              />
              <SummaryCard
                icon={<CheckCircle2 className="h-4 w-4" />}
                label="Atanmış"
                value={stats.assigned}
                hint={`%${stats.coveragePct} kapsam · ${stats.assignedCapacity.toLocaleString('tr-TR')} kişi`}
                color="emerald"
              />
              <SummaryCard
                icon={<AlertCircle className="h-4 w-4" />}
                label="Atanmamış"
                value={stats.unassigned}
                hint={stats.unassigned > 0
                  ? `${stats.unassignedCapacity.toLocaleString('tr-TR')} kişi kullanımda değil`
                  : 'hepsi atandı'}
                color={stats.unassigned > 0 ? 'rose' : 'slate'}
              />
              <SummaryCard
                icon={<Users className="h-4 w-4" />}
                label="Aktif Fakülte"
                value={stats.activeFacultyCount}
                hint={`/ ${facultyCount} fakülte`}
                color="violet"
              />
            </div>

            {/* ── Tip bazlı dağılım ─────────────────────────────────────── */}
            {sortedTypeCodes.length > 0 && (
              <section>
                <SectionHeader
                  icon={<Layers className="h-4 w-4 text-violet-600" />}
                  title="Tip Bazlı Dağılım"
                  subtitle="Her derslik tipi için atanmış / toplam"
                />
                <div className="space-y-2.5 rounded-xl border border-slate-200 bg-white p-4">
                  {sortedTypeCodes.map(t => {
                    const data = stats.typeCounts[t];
                    if (!data) return null;
                    const assignedPct = data.total > 0 ? (data.assigned / data.total) * 100 : 0;
                    return (
                      <div key={t} className="space-y-1">
                        <div className="flex items-center justify-between text-xs">
                          <span className="inline-flex items-center gap-2 font-medium text-slate-700">
                            <span className={`h-2 w-2 rounded-full ${getClassroomTypeBarClass(t)}`} />
                            {getClassroomTypeLabel(t)}
                          </span>
                          <span className="font-mono tabular-nums text-slate-600">
                            {data.assigned} / {data.total}
                            <span className="ml-1.5 text-[11px] text-slate-400">
                              (%{Math.round(assignedPct)})
                            </span>
                          </span>
                        </div>
                        <div className="h-1.5 overflow-hidden rounded-full bg-slate-100">
                          <div
                            className={`h-full ${getClassroomTypeBarClass(t)} transition-all duration-500`}
                            style={{ width: `${assignedPct}%` }}
                          />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </section>
            )}

            {/* ── Fakülte bazlı breakdown ───────────────────────────────── */}
            {stats.facultyBreakdown.length > 0 && (
              <section>
                <SectionHeader
                  icon={<Users className="h-4 w-4 text-blue-600" />}
                  title="Fakülte Bazlı Kullanım"
                  subtitle="Her fakültenin derslik sayısı ve tip dağılımı"
                />
                <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
                  <table className="w-full text-sm">
                    <thead className="border-b border-slate-200 bg-slate-50/40">
                      <tr>
                        <th className="px-4 py-2.5 text-left text-xs font-semibold text-slate-600">Fakülte</th>
                        <th className="px-3 py-2.5 text-center text-xs font-semibold text-slate-600">Derslik</th>
                        <th className="px-3 py-2.5 text-center text-xs font-semibold text-slate-600">Kapasite</th>
                        <th className="px-4 py-2.5 text-left text-xs font-semibold text-slate-600">Tip Dağılımı</th>
                        <th className="px-4 py-2.5 text-right text-xs font-semibold text-slate-600">Pay</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {stats.facultyBreakdown.map((f, idx) => {
                        const sharePct = stats.assigned > 0
                          ? Math.round((f.classroomCount / stats.assigned) * 100)
                          : 0;
                        const barColor = FACULTY_BAR_COLORS[idx % FACULTY_BAR_COLORS.length];
                        return (
                          <tr key={f.facultyId} className="transition-colors hover:bg-slate-50/50">
                            <td className="px-4 py-2.5">
                              <div className="flex items-center gap-2">
                                <span className={`h-2.5 w-2.5 rounded-full ${barColor}`} />
                                <span className="font-medium text-slate-900">{f.facultyName}</span>
                              </div>
                            </td>
                            <td className="px-3 py-2.5 text-center font-mono text-sm tabular-nums">{f.classroomCount}</td>
                            <td className="px-3 py-2.5 text-center font-mono text-xs tabular-nums text-slate-600">
                              {f.totalCapacity.toLocaleString('tr-TR')}
                            </td>
                            <td className="px-4 py-2.5">
                              <div className="flex flex-wrap gap-1">
                                {Object.entries(f.byType)
                                  .sort((a, b) => b[1] - a[1])
                                  .map(([t, cnt]) => (
                                    <span
                                      key={t}
                                      className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] ${getClassroomTypeBadgeClass(t)}`}
                                    >
                                      <span className={`h-1.5 w-1.5 rounded-full ${getClassroomTypeBarClass(t)}`} />
                                      {getClassroomTypeLabel(t)} · {cnt}
                                    </span>
                                  ))}
                              </div>
                            </td>
                            <td className="px-4 py-2.5">
                              <div className="flex items-center justify-end gap-2">
                                <div className="h-1.5 w-16 overflow-hidden rounded-full bg-slate-100">
                                  <div
                                    className={`h-full ${barColor}`}
                                    style={{ width: `${sharePct}%` }}
                                  />
                                </div>
                                <span className="w-9 text-right font-mono text-xs tabular-nums text-slate-600">
                                  %{sharePct}
                                </span>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </section>
            )}

            {/* ── Bina kullanımı ────────────────────────────────────────── */}
            {stats.buildingBreakdown.length > 0 && (
              <section>
                <SectionHeader
                  icon={<MapPin className="h-4 w-4 text-amber-600" />}
                  title="Bina Bazlı Kullanım"
                  subtitle="Her binanın atanmış / toplam derslik sayısı"
                />
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {stats.buildingBreakdown.map(b => {
                    const pct = b.total > 0 ? Math.round((b.assigned / b.total) * 100) : 0;
                    const isFullyAssigned = b.assigned === b.total;
                    return (
                      <div
                        key={b.name}
                        className={`rounded-lg border p-3 transition-colors ${
                          isFullyAssigned
                            ? 'border-emerald-200/60 bg-gradient-to-br from-emerald-50/40 to-white'
                            : 'border-slate-200 bg-white'
                        }`}
                      >
                        <div className="mb-2 flex items-start justify-between gap-2">
                          <div className="min-w-0 flex-1">
                            <div className="truncate text-sm font-semibold text-slate-900">{b.name}</div>
                            <div className="text-[11px] text-slate-500">
                              {b.capacity.toLocaleString('tr-TR')} kişi kapasite
                            </div>
                          </div>
                          {isFullyAssigned && (
                            <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600" />
                          )}
                        </div>
                        <div className="flex items-center gap-2">
                          <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-100">
                            <div
                              className={`h-full transition-all duration-500 ${
                                isFullyAssigned
                                  ? 'bg-gradient-to-r from-emerald-400 to-emerald-600'
                                  : 'bg-gradient-to-r from-blue-400 to-blue-600'
                              }`}
                              style={{ width: `${pct}%` }}
                            />
                          </div>
                          <span className="font-mono text-xs tabular-nums text-slate-600">
                            {b.assigned}/{b.total}
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </section>
            )}

            {/* ── Atanmamış derslik analizi ──────────────────────────────── */}
            {stats.unassigned > 0 && Object.keys(stats.unassignedByType).length > 0 && (
              <section>
                <SectionHeader
                  icon={<AlertCircle className="h-4 w-4 text-rose-600" />}
                  title="Atanmamış Derslikler"
                  subtitle="Atama bekleyen dersliklerin tip bazlı dağılımı"
                />
                <div className="rounded-xl border border-rose-200/60 bg-gradient-to-br from-rose-50/30 to-white p-4">
                  <div className="flex flex-wrap gap-2">
                    {sortedTypeCodes
                      .filter(t => (stats.unassignedByType[t] || 0) > 0)
                      .map(t => (
                        <Badge
                          key={t}
                          variant="outline"
                          className={`gap-1.5 ${getClassroomTypeBadgeClass(t)}`}
                        >
                          <span className={`h-1.5 w-1.5 rounded-full ${getClassroomTypeBarClass(t)}`} />
                          {getClassroomTypeLabel(t)}
                          <span className="font-mono font-semibold">
                            {stats.unassignedByType[t]}
                          </span>
                        </Badge>
                      ))}
                  </div>
                </div>
              </section>
            )}

            {/* ── Empty state ──────────────────────────────────────────── */}
            {stats.total === 0 && (
              <div className="rounded-xl border border-slate-200 bg-white py-12 text-center">
                <Building2 className="mx-auto mb-3 h-10 w-10 text-slate-300" />
                <p className="text-sm text-slate-600">Sistemde henüz derslik tanımlı değil.</p>
              </div>
            )}
          </div>
        </div>

        <DialogFooter className="shrink-0 border-t border-slate-100 bg-slate-50/40 px-6 py-3">
          <Button variant="outline" onClick={() => onOpenChange(false)}>Kapat</Button>
        </DialogFooter>
      </ResizableDialogContent>
    </Dialog>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────

interface SummaryCardProps {
  icon: React.ReactNode;
  label: string;
  value: number | string;
  hint?: string;
  color: 'blue' | 'emerald' | 'rose' | 'violet' | 'slate';
}

function SummaryCard({ icon, label, value, hint, color }: SummaryCardProps) {
  const palette = {
    blue:    { ring: 'border-blue-200/60',    bg: 'from-blue-50 via-blue-50/60 to-white',       iconBg: 'bg-blue-100',    iconColor: 'text-blue-700' },
    emerald: { ring: 'border-emerald-200/60', bg: 'from-emerald-50 via-emerald-50/60 to-white', iconBg: 'bg-emerald-100', iconColor: 'text-emerald-700' },
    rose:    { ring: 'border-rose-200/60',    bg: 'from-rose-50 via-rose-50/60 to-white',       iconBg: 'bg-rose-100',    iconColor: 'text-rose-700' },
    violet:  { ring: 'border-violet-200/60',  bg: 'from-violet-50 via-violet-50/60 to-white',   iconBg: 'bg-violet-100',  iconColor: 'text-violet-700' },
    slate:   { ring: 'border-slate-200/60',   bg: 'from-slate-50 to-white',                     iconBg: 'bg-slate-100',   iconColor: 'text-slate-600' },
  }[color];

  return (
    <div className={`relative overflow-hidden rounded-xl border ${palette.ring} bg-gradient-to-br ${palette.bg} px-4 py-3.5 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-600">{label}</div>
          <div className="mt-1.5 text-2xl font-bold tabular-nums text-slate-900">{value}</div>
          {hint && <div className="mt-0.5 truncate text-[11px] text-slate-500">{hint}</div>}
        </div>
        <div className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${palette.iconBg} ${palette.iconColor} shadow-sm`}>
          {icon}
        </div>
      </div>
      <div className="pointer-events-none absolute -right-6 -top-6 h-20 w-20 rounded-full bg-white/30 blur-2xl" />
    </div>
  );
}

interface SectionHeaderProps {
  icon: React.ReactNode;
  title: string;
  subtitle?: string;
}

function SectionHeader({ icon, title, subtitle }: SectionHeaderProps) {
  return (
    <div className="mb-3 flex items-baseline gap-2">
      <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-slate-900">
        {icon}
        {title}
      </span>
      {subtitle && <span className="text-[11px] text-slate-500">— {subtitle}</span>}
    </div>
  );
}
