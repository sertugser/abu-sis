import { useMemo } from 'react';
import {
  AlertTriangle,
  Users,
  UserCog,
  GraduationCap,
  BookOpen,
  Scale,
  Loader2,
  Trash2,
  Edit2,
} from 'lucide-react';
import {
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Tooltip,
  Legend,
} from 'recharts';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useDepartmentOverview, type DepartmentDto } from '../../hooks/useAdminAcademicStructure';
import { DEGREE_LEVELS, EDUCATION_TYPES, formatDateTime } from './shared-constants';
import { KpiCard, SectionCard, AlertStat } from './panel-primitives';

interface DepartmentDetailPanelProps {
  department: DepartmentDto;
  onEdit: () => void;
  onDelete: () => void;
}

const DEGREE_COLORS = ['#2563eb', '#7c3aed', '#0891b2', '#d97706', '#059669', '#db2777'];
const TAB_CONTENT = 'space-y-4 min-h-[320px]';

function degreeLabel(value: string): string {
  return DEGREE_LEVELS.find(d => d.value === value)?.label ?? value;
}
function educationLabel(value: string): string {
  return EDUCATION_TYPES.find(e => e.value === value)?.label ?? value;
}

function InfoItem({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm text-foreground">{value}</dd>
    </div>
  );
}

export function DepartmentDetailPanel({ department, onEdit, onDelete }: DepartmentDetailPanelProps) {
  const { data: overview, isLoading, isError } = useDepartmentOverview(department.id);

  const degreeData = useMemo(
    () => (overview ? overview.programsByDegree.map(d => ({ name: degreeLabel(d.degreeLevel), value: d.count })) : []),
    [overview],
  );

  const ratio = overview && overview.instructorCount > 0
    ? (overview.studentCount / overview.instructorCount).toFixed(1)
    : null;

  const largestProgram = useMemo(() => {
    if (!overview || overview.programs.length === 0) return null;
    const top = overview.programs.reduce((m, p) => (p.studentCount > m.studentCount ? p : m), overview.programs[0]);
    return {
      name: top.name,
      students: top.studentCount,
      pct: overview.studentCount > 0 ? Math.round((top.studentCount / overview.studentCount) * 100) : 0,
    };
  }, [overview]);

  const noInstructorWithStudents = overview ? overview.studentCount > 0 && overview.instructorCount === 0 : false;

  return (
    <div className="mx-auto flex h-full w-full max-w-6xl flex-col">
      <DialogHeader className="pr-24 border-b border-slate-200 pb-4">
        <div className="flex flex-wrap items-center gap-3">
          <DialogTitle className="text-2xl">{department.name}</DialogTitle>
          <Badge variant="outline" className="font-mono text-xs tabular-nums uppercase tracking-wide">
            {department.code}
          </Badge>
          {overview?.isServiceUnit && (
            <Badge className="bg-violet-50 text-violet-700 border border-violet-200 hover:bg-violet-50 text-xs font-normal">
              Koordinatörlük
            </Badge>
          )}
          <span className="text-sm text-muted-foreground">· {overview?.facultyName ?? department.facultyName}</span>
        </div>
      </DialogHeader>

      {isLoading && (
        <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Bölüm verileri yükleniyor...
        </div>
      )}

      {isError && (
        <div className="my-6 rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
          Bölüm istatistikleri yüklenemedi. Lütfen tekrar deneyin.
        </div>
      )}

      {overview && (
        <Tabs defaultValue="overview" className="pt-3">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="overview">Genel Bakış</TabsTrigger>
            <TabsTrigger value="programs">Programlar ({overview.programCount})</TabsTrigger>
            <TabsTrigger value="staff">Kadro ({overview.instructorCount})</TabsTrigger>
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
                <div className="text-xs text-muted-foreground">En büyük program</div>
                {largestProgram ? (
                  <>
                    <div className="mt-1 truncate text-base font-semibold text-foreground" title={largestProgram.name}>
                      {largestProgram.name}
                    </div>
                    <div className="text-xs text-muted-foreground tabular-nums">
                      {overview.studentCount > 0
                        ? `Öğrencilerin %${largestProgram.pct}'i (${largestProgram.students}) bu programda`
                        : 'Henüz öğrenci yok'}
                    </div>
                  </>
                ) : (
                  <div className="mt-1 text-sm text-muted-foreground">
                    {overview.isServiceUnit ? 'Koordinatörlük — program taşımaz' : 'Henüz program yok'}
                  </div>
                )}
              </div>
            </div>

            {!overview.isServiceUnit && (
              <SectionCard title="Hazırlık & Uyarılar" hint="Bölümün eksik / aksiyon gerektiren noktaları">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  <AlertStat label="Müfredatsız program" count={overview.programsWithoutCurriculum} total={overview.programCount} critical />
                  <AlertStat label="Öğrencisi olup öğretim üyesi yok" count={noInstructorWithStudents ? 1 : 0} critical />
                  <AlertStat label="Dış sistem kodu eksik program" count={overview.programsWithoutExternalCode} total={overview.programCount} />
                  <AlertStat label="Program tanımlı değil" count={overview.programCount === 0 ? 1 : 0} />
                </div>
              </SectionCard>
            )}

            {degreeData.length > 1 && (
              <SectionCard title="Program derece dağılımı">
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
              </SectionCard>
            )}
          </TabsContent>

          {/* ── Programlar ── */}
          <TabsContent value="programs" className={TAB_CONTENT}>
            <SectionCard title={`Programlar (${overview.programCount})`}>
              {overview.programs.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">
                  {overview.isServiceUnit
                    ? 'Koordinatörlük birimlerinde program bulunmaz.'
                    : 'Bu bölüme henüz program eklenmemiş.'}
                </p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 text-xs text-muted-foreground">
                        <th className="px-3 py-2 text-left font-medium">Program</th>
                        <th className="px-3 py-2 text-left font-medium w-24">Kod</th>
                        <th className="px-3 py-2 text-left font-medium w-28">Derece</th>
                        <th className="px-3 py-2 text-right font-medium w-24">Öğrenci</th>
                        <th className="px-3 py-2 text-right font-medium w-20">AKTS</th>
                        <th className="px-3 py-2 text-right font-medium w-24">Müfredat</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {overview.programs.map(p => (
                        <tr key={p.id} className="hover:bg-slate-50/60">
                          <td className="px-3 py-2">
                            <div className="flex items-center gap-2">
                              <span className="font-medium text-foreground">{p.name}</span>
                              {p.curriculumCount === 0 && (
                                <span title="Müfredat tanımlı değil">
                                  <AlertTriangle className="h-3.5 w-3.5 text-amber-500" />
                                </span>
                              )}
                            </div>
                            <span className="text-[11px] text-muted-foreground">{educationLabel(p.educationType)}</span>
                          </td>
                          <td className="px-3 py-2">
                            <Badge variant="outline" className="font-mono text-[11px] uppercase">{p.code}</Badge>
                          </td>
                          <td className="px-3 py-2 text-muted-foreground">{degreeLabel(p.degreeLevel)}</td>
                          <td className="px-3 py-2 text-right tabular-nums">{p.studentCount}</td>
                          <td className="px-3 py-2 text-right tabular-nums text-muted-foreground">{p.totalRequiredEcts}</td>
                          <td className="px-3 py-2 text-right tabular-nums">{p.curriculumCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </SectionCard>
          </TabsContent>

          {/* ── Kadro (öğretim üyeleri) ── */}
          <TabsContent value="staff" className={TAB_CONTENT}>
            <SectionCard title={`Öğretim Üyeleri (${overview.instructorCount})`}>
              {overview.instructors.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">Bu bölüme öğretim üyesi atanmamış.</p>
              ) : (
                <ul className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  {overview.instructors.map(i => (
                    <li key={i.id} className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white px-3 py-2">
                      <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-blue-50 text-blue-600">
                        <UserCog className="h-4 w-4" />
                      </span>
                      <div className="min-w-0">
                        <div className="truncate text-sm font-medium text-foreground">{i.name}</div>
                        {i.title && <div className="truncate text-xs text-muted-foreground">{i.title}</div>}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </SectionCard>
          </TabsContent>

          {/* ── Bilgiler ── */}
          <TabsContent value="info" className={TAB_CONTENT}>
            <SectionCard title="Genel Bilgiler">
              <dl className="grid grid-cols-2 gap-x-6 gap-y-4 sm:grid-cols-3">
                <InfoItem label="Bölüm Adı" value={<span className="font-medium">{department.name}</span>} />
                <InfoItem label="Kod" value={<span className="font-mono uppercase tracking-wide">{department.code}</span>} />
                <InfoItem label="Fakülte" value={overview.facultyName} />
                <InfoItem label="Birim Türü" value={overview.isServiceUnit ? 'Koordinatörlük' : 'Akademik bölüm'} />
                <InfoItem label="ID" value={<span className="font-mono tabular-nums text-muted-foreground">#{department.id}</span>} />
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
