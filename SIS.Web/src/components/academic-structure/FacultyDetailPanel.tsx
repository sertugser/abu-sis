import { useMemo } from 'react';
import {
  AlertTriangle,
  GraduationCap,
  BookOpen,
  Users,
  UserCog,
  Scale,
  PieChart as PieIcon,
  Loader2,
  Trash2,
  Edit2,
} from 'lucide-react';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  PieChart,
  Pie,
  Cell,
  Legend,
} from 'recharts';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useFacultyOverview, type FacultyDto } from '../../hooks/useAdminAcademicStructure';
import { DEGREE_LEVELS, formatDateTime } from './shared-constants';
import { KpiCard, SectionCard, AlertStat } from './panel-primitives';

interface FacultyDetailPanelProps {
  faculty: FacultyDto;
  onEdit: () => void;
  onDelete: () => void;
}

const DEGREE_COLORS = ['#2563eb', '#7c3aed', '#0891b2', '#d97706', '#059669', '#db2777'];
const TAB_CONTENT = 'space-y-4 min-h-[320px]';

const RATIO_HIGH = 30;
const RATIO_MID = 20;

function degreeLabel(value: string): string {
  return DEGREE_LEVELS.find(d => d.value === value)?.label ?? value;
}
function ratioColor(r: number): string {
  if (r > RATIO_HIGH) return '#ef4444';
  if (r >= RATIO_MID) return '#f59e0b';
  return '#10b981';
}

function InfoItem({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm text-foreground">{value}</dd>
    </div>
  );
}

export function FacultyDetailPanel({ faculty, onEdit, onDelete }: FacultyDetailPanelProps) {
  const { data: overview, isLoading, isError } = useFacultyOverview(faculty.id);

  const degreeData = useMemo(
    () => (overview ? overview.programsByDegree.map(d => ({ name: degreeLabel(d.degreeLevel), value: d.count })) : []),
    [overview],
  );

  const workload = useMemo(() => {
    if (!overview) return [];
    return overview.departments
      .filter(d => d.instructorCount > 0)
      .map(d => ({ name: d.code || d.name, ratio: Number((d.studentCount / d.instructorCount).toFixed(1)) }))
      .sort((a, b) => b.ratio - a.ratio)
      .slice(0, 8);
  }, [overview]);

  const ratio = overview && overview.instructorCount > 0
    ? (overview.studentCount / overview.instructorCount).toFixed(1)
    : null;

  const concentration = useMemo(() => {
    if (!overview || overview.studentCount === 0) return null;
    const top = overview.departments.reduce(
      (m, d) => (d.studentCount > m.studentCount ? d : m),
      overview.departments[0],
    );
    return { name: top.name, pct: Math.round((top.studentCount / overview.studentCount) * 100), students: top.studentCount };
  }, [overview]);

  const noProgramDepts = overview ? overview.departments.filter(d => d.programCount === 0 && !d.isServiceUnit).length : 0;
  const noInstructorDepts = overview ? overview.departments.filter(d => d.instructorCount === 0 && !d.isServiceUnit).length : 0;
  const overloadedWithoutInstructor = overview
    ? overview.departments.filter(d => d.instructorCount === 0 && d.studentCount > 0).length
    : 0;

  const isEmpty = (overview?.departmentCount ?? faculty.departmentCount) === 0;

  return (
    <div className="mx-auto flex h-full w-full max-w-6xl flex-col">
      <DialogHeader className="pr-24 border-b border-slate-200 pb-4">
        <div className="flex flex-wrap items-center gap-3">
          <DialogTitle className="text-2xl">{faculty.name}</DialogTitle>
          <Badge variant="outline" className="font-mono text-xs tabular-nums uppercase tracking-wide">
            {faculty.code}
          </Badge>
          {isEmpty && (
            <span className="inline-flex items-center gap-1 text-xs text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
              <AlertTriangle className="h-3 w-3" />
              Bölüm tanımlı değil
            </span>
          )}
        </div>
      </DialogHeader>

      {isLoading && (
        <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Fakülte verileri yükleniyor...
        </div>
      )}

      {isError && (
        <div className="my-6 rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          Fakülte istatistikleri yüklenemedi. Lütfen tekrar deneyin.
        </div>
      )}

      {overview && (
        <Tabs defaultValue="overview" className="pt-3">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="overview">Genel Bakış</TabsTrigger>
            <TabsTrigger value="departments">Bölümler ({overview.departmentCount})</TabsTrigger>
            <TabsTrigger value="info">Bilgiler</TabsTrigger>
          </TabsList>

          {/* ── Genel Bakış ── */}
          <TabsContent value="overview" className={TAB_CONTENT}>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <KpiCard icon={Users} label="Öğrenci" value={overview.studentCount} tone="emerald" />
              <KpiCard icon={UserCog} label="Öğretim Üyesi" value={overview.instructorCount} tone="amber" />
              <KpiCard icon={GraduationCap} label="Program" value={overview.programCount} tone="indigo" />
              <KpiCard icon={BookOpen} label="Müfredat" value={overview.curriculumCount} tone="violet" />
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="rounded-xl border border-slate-200 bg-gradient-to-br from-slate-50 to-white px-4 py-3">
                <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Scale className="h-3.5 w-3.5" /> Öğrenci / Öğretim Üyesi oranı
                </div>
                <div className="mt-1 text-2xl font-semibold tabular-nums">{ratio ? `${ratio} : 1` : '—'}</div>
                <div className="text-xs text-muted-foreground">
                  {ratio ? `Öğretim üyesi başına ~${Math.round(Number(ratio))} öğrenci` : 'Öğretim üyesi atanmamış'}
                </div>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <PieIcon className="h-3.5 w-3.5" /> Öğrenci yoğunlaşması
                </div>
                {concentration ? (
                  <>
                    <div className="mt-1 truncate text-base font-semibold text-foreground" title={concentration.name}>
                      {concentration.name}
                    </div>
                    <div className="text-xs text-muted-foreground tabular-nums">
                      Toplam öğrencinin %{concentration.pct}'i ({concentration.students}) bu bölümde
                    </div>
                  </>
                ) : (
                  <div className="mt-1 text-sm text-muted-foreground">Henüz öğrenci yok</div>
                )}
              </div>
            </div>

            <SectionCard title="Hazırlık & Uyarılar" hint="Akademik yapının eksik / aksiyon gerektiren noktaları">
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <AlertStat label="Müfredatsız program" count={overview.programsWithoutCurriculum} total={overview.programCount} critical />
                <AlertStat label="Öğrencisi olup hocası olmayan bölüm" count={overloadedWithoutInstructor} critical />
                <AlertStat label="Programsız bölüm" count={noProgramDepts} />
                <AlertStat label="Öğretim üyesi atanmamış bölüm" count={noInstructorDepts} />
                <AlertStat label="Dış sistem kodu eksik program" count={overview.programsWithoutExternalCode} total={overview.programCount} />
              </div>
            </SectionCard>

            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              <SectionCard title="Program derece dağılımı">
                {degreeData.length > 0 ? (
                  <ResponsiveContainer width="100%" height={220}>
                    <PieChart>
                      <Pie data={degreeData} dataKey="value" nameKey="name" innerRadius={50} outerRadius={80} paddingAngle={2}>
                        {degreeData.map((_, i) => (
                          <Cell key={i} fill={DEGREE_COLORS[i % DEGREE_COLORS.length]} />
                        ))}
                      </Pie>
                      <Tooltip formatter={(value, name) => [`${value} program`, name]} />
                      <Legend wrapperStyle={{ fontSize: 12 }} />
                    </PieChart>
                  </ResponsiveContainer>
                ) : (
                  <p className="py-8 text-center text-sm text-muted-foreground">Henüz program tanımlı değil.</p>
                )}
              </SectionCard>

              <SectionCard title="Bölüm iş yükü" hint="Öğrenci / öğretim üyesi oranı — kırmızı = yüksek yük">
                {workload.length > 0 ? (
                  <ResponsiveContainer width="100%" height={Math.max(180, workload.length * 28)}>
                    <BarChart data={workload} layout="vertical" margin={{ left: 8, right: 24, top: 4, bottom: 4 }}>
                      <CartesianGrid horizontal={false} stroke="#f1f5f9" />
                      <XAxis type="number" tick={{ fontSize: 11 }} stroke="#94a3b8" />
                      <YAxis type="category" dataKey="name" width={64} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                      <Tooltip cursor={{ fill: '#f8fafc' }} formatter={(value) => [`${value} : 1`, 'Öğrenci/Hoca']} labelFormatter={() => ''} />
                      <Bar dataKey="ratio" radius={[0, 4, 4, 0]} maxBarSize={20}>
                        {workload.map((d, i) => (
                          <Cell key={i} fill={ratioColor(d.ratio)} />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                ) : (
                  <p className="py-8 text-center text-sm text-muted-foreground">Öğretim üyesi atanmış bölüm yok.</p>
                )}
              </SectionCard>
            </div>
          </TabsContent>

          {/* ── Bölümler ── */}
          <TabsContent value="departments" className={TAB_CONTENT}>
            <SectionCard title={`Bağlı Bölümler (${overview.departmentCount})`}>
              {overview.departments.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">Bu fakülteye henüz bölüm eklenmemiş.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 text-xs text-muted-foreground">
                        <th className="px-3 py-2 text-left font-medium">Bölüm</th>
                        <th className="px-3 py-2 text-left font-medium w-20">Kod</th>
                        <th className="px-3 py-2 text-right font-medium w-24">Öğrenci</th>
                        <th className="px-3 py-2 text-right font-medium w-28">Öğr. Üyesi</th>
                        <th className="px-3 py-2 text-right font-medium w-24">Program</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {overview.departments.map(d => {
                        const gap = d.programCount === 0 && !d.isServiceUnit;
                        return (
                          <tr key={d.id} className="hover:bg-slate-50/60">
                            <td className="px-3 py-2">
                              <div className="flex items-center gap-2">
                                <span className="font-medium text-foreground">{d.name}</span>
                                {d.isServiceUnit && (
                                  <Badge className="bg-violet-50 text-violet-700 border border-violet-200 hover:bg-violet-50 text-[10px] font-normal">
                                    Koordinatörlük
                                  </Badge>
                                )}
                                {gap && (
                                  <span title="Programsız bölüm">
                                    <AlertTriangle className="h-3.5 w-3.5 text-amber-500" />
                                  </span>
                                )}
                              </div>
                            </td>
                            <td className="px-3 py-2">
                              <Badge variant="outline" className="font-mono text-[11px] uppercase">{d.code}</Badge>
                            </td>
                            <td className="px-3 py-2 text-right tabular-nums">{d.studentCount}</td>
                            <td className="px-3 py-2 text-right tabular-nums">{d.instructorCount}</td>
                            <td className="px-3 py-2 text-right tabular-nums">{d.programCount}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </SectionCard>
          </TabsContent>

          {/* ── Bilgiler ── */}
          <TabsContent value="info" className={TAB_CONTENT}>
            <SectionCard title="Genel Bilgiler">
              <dl className="grid grid-cols-2 gap-x-6 gap-y-4 sm:grid-cols-3">
                <InfoItem label="Fakülte Adı" value={<span className="font-medium">{faculty.name}</span>} />
                <InfoItem label="Kod" value={<span className="font-mono uppercase tracking-wide">{faculty.code}</span>} />
                <InfoItem label="ID" value={<span className="font-mono tabular-nums text-muted-foreground">#{faculty.id}</span>} />
                <InfoItem label="Oluşturulma" value={<span className="text-muted-foreground">{formatDateTime(overview.createdAt)}</span>} />
                <InfoItem label="Son Güncelleme" value={<span className="text-muted-foreground">{formatDateTime(overview.updatedAt)}</span>} />
              </dl>
            </SectionCard>
          </TabsContent>
        </Tabs>
      )}

      <DialogFooter className="mt-auto pt-4 sm:justify-between gap-2 border-t border-slate-200">
        <Button variant="outline" className="text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={onDelete}>
          <Trash2 className="h-4 w-4 mr-1.5" /> Sil
        </Button>
        <Button onClick={onEdit}>
          <Edit2 className="h-4 w-4 mr-1.5" /> Düzenle
        </Button>
      </DialogFooter>
    </div>
  );
}
