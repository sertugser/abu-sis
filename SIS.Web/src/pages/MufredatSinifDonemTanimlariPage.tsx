import { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Copy, Edit2, Scissors, Users } from 'lucide-react';
import {
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
} from '../hooks/useAdminAcademicStructure';
import { useFacultyLookup } from '../hooks/useAdminLookup';
import { useCurriculumList } from '../hooks/useCurriculumManagement';
import { useCurriculumTermRules } from '../hooks/useCurriculumTermRules';
import {
  ALL_CLASS_YEARS,
  ALL_RULE_TYPES,
  ALL_TERM_HALVES,
  CLASS_YEAR_LABELS,
  RULE_TYPE_LABELS,
  TERM_HALF_LABELS,
  GPA_RULE_TYPES,
} from '../types/curriculumTermRule';
import type {
  CurriculumTermRuleType,
} from '../types/curriculumTermRule';
import { CurriculumTermRulesDialog } from '../components/curriculum-management/CurriculumTermRulesDialog';
import PromotionRulesPanel from './curriculum-management/PromotionRulesPanel';
import { getFromYearOptionsFromEcts } from './curriculum-management/promotionRulesUtils';

export default function MufredatSinifDonemTanimlariPage() {
  // ── Cascade state ──
  const [facultyId, setFacultyId] = useState<number | undefined>();
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [programId, setProgramId] = useState<number | undefined>();
  const [curriculumId, setCurriculumId] = useState<number | undefined>();

  // ── Cascade queries ──
  const { data: faculties = [], isLoading: loadingFaculties } = useFacultyLookup();
  const { data: departments = [] } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs = [] } = useAdminProgramsByStructure(departmentId);
  const { data: curricula = [] } = useCurriculumList({ programId });

  // ── Data queries (only when curriculum selected) ──
  const { data: termRules = [] } = useCurriculumTermRules(curriculumId ?? null);

  const [showTermRulesDialog, setShowTermRulesDialog] = useState(false);

  // ── Cascade handlers ──
  function handleFacultyChange(val: string) {
    setFacultyId(Number(val));
    setDepartmentId(undefined);
    setProgramId(undefined);
    setCurriculumId(undefined);
  }
  function handleDepartmentChange(val: string) {
    setDepartmentId(Number(val));
    setProgramId(undefined);
    setCurriculumId(undefined);
  }
  function handleProgramChange(val: string) {
    setProgramId(Number(val));
    setCurriculumId(undefined);
  }

  // ── Derived: from year options (program'ın AKTS yüküne göre normal süre) ──
  // YÖK standardı: 60 AKTS = 1 yıl (lisans 240 → 4 yıl, tıp 360 → 6 yıl, önlisans 120 → 2 yıl)
  // maxStudyDurationSemesters AZAMİ süredir (lisans 14 dönem = 7 yıl), atlatma için yanlış kaynak.
  const fromYearOptions = useMemo<number[]>(() => {
    const program = programs.find(p => p.id === programId);
    return getFromYearOptionsFromEcts(program?.totalRequiredEcts);
  }, [programs, programId]);

  const selectedCurriculum = curricula.find(c => c.id === curriculumId);
  const curriculumLabel = selectedCurriculum
    ? `(${selectedCurriculum.version}) ${selectedCurriculum.name ?? selectedCurriculum.programName}`
    : '';

  // Cascade row helper — tutarlı label-value yatay düzeni
  const cascadeRow = (
    label: string,
    selectNode: React.ReactNode,
  ) => (
    <div className="grid grid-cols-[100px_1fr] items-center gap-3">
      <Label className="text-sm font-medium text-muted-foreground">{label}</Label>
      {selectNode}
    </div>
  );

  return (
    <div className="space-y-4">
      {/* ── Üst row: Cascade selector (sol, 2/5) + Sınıf Atlatma Kuralları (sağ, 3/5) — Proliz layout
          Responsive: lg ve üstü → yan yana 2/5 + 3/5; md → yan yana eşit; sm/mobile → alt alta tam genişlik ── */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        {/* Sol: Müfredat cascade selector */}
        <Card className="lg:col-span-2">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Müfredat Seçimi</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {cascadeRow('Fakülte',
              <Select
                value={facultyId?.toString() ?? ''}
                onValueChange={handleFacultyChange}
                disabled={loadingFaculties}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Fakülte seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {faculties.map(f => (
                    <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
            {cascadeRow('Bölüm',
              <Select
                value={departmentId?.toString() ?? ''}
                onValueChange={handleDepartmentChange}
                disabled={!facultyId}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Bölüm seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {departments.map(d => (
                    <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
            {cascadeRow('Program',
              <Select
                value={programId?.toString() ?? ''}
                onValueChange={handleProgramChange}
                disabled={!departmentId}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Program seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {programs.map(p => (
                    <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
            {cascadeRow('Müfredat',
              <Select
                value={curriculumId?.toString() ?? ''}
                onValueChange={(val) => setCurriculumId(Number(val))}
                disabled={!programId}
              >
                <SelectTrigger className="h-9 text-sm">
                  <SelectValue placeholder="Müfredat seçin..." />
                </SelectTrigger>
                <SelectContent>
                  {curricula.map(c => (
                    <SelectItem key={c.id} value={c.id.toString()}>
                      ({c.version}) {c.name ?? c.programName}
                      {c.isActive && ' • Aktif'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>,
            )}
          </CardContent>
        </Card>

        {/* Sağ: Sınıf Atlatma Kuralları flat tablo (3/5) — gömülebilir panel */}
        <div className="lg:col-span-3">
          <PromotionRulesPanel
            curriculumId={curriculumId}
            programId={programId}
            fromYearOptions={fromYearOptions}
          />
        </div>
      </div>

      {!curriculumId ? null : (
        <>
          {/* ── Müfredatın Dönem Tanımları matrix view ── */}
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between gap-2">
                <CardTitle className="text-base">Müfredatın Dönem Tanımları</CardTitle>
                <Button
                  size="sm" variant="outline"
                  onClick={() => setShowTermRulesDialog(true)}
                >
                  <Edit2 className="h-4 w-4 mr-1" /> Kural Düzenleyici
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto rounded border">
                <table className="w-full text-xs">
                  <thead className="bg-muted/50 border-b">
                    <tr className="text-left">
                      <th className="px-2 py-2 font-medium whitespace-nowrap sticky left-0 bg-muted/50 z-10">Sınıf</th>
                      <th className="px-2 py-2 font-medium whitespace-nowrap">Dönem</th>
                      {ALL_RULE_TYPES.map(rt => (
                        <th key={rt} className="px-2 py-2 font-medium text-right whitespace-nowrap">
                          {RULE_TYPE_LABELS[rt]}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {ALL_CLASS_YEARS.flatMap(cy =>
                      ALL_TERM_HALVES.map(th => {
                        const rulesForCell = termRules.filter(r => r.classYear === cy && r.termHalf === th);
                        return (
                          <tr
                            key={`${cy}-${th}`}
                            className="border-b hover:bg-muted/30"
                          >
                            <td className="px-2 py-1.5 font-medium whitespace-nowrap sticky left-0 bg-background z-10">
                              {CLASS_YEAR_LABELS[cy]}
                            </td>
                            <td className="px-2 py-1.5 whitespace-nowrap">{TERM_HALF_LABELS[th]}</td>
                            {ALL_RULE_TYPES.map(rt => {
                              const rule = rulesForCell.find(r => r.ruleType === rt);
                              const isGpa = GPA_RULE_TYPES.includes(rt as CurriculumTermRuleType);
                              return (
                                <td key={rt} className="px-2 py-1.5 text-right">
                                  {rule
                                    ? (isGpa ? Number(rule.value).toFixed(2) : rule.value)
                                    : <span className="text-muted-foreground italic">—</span>}
                                </td>
                              );
                            })}
                          </tr>
                        );
                      }),
                    )}
                  </tbody>
                </table>
              </div>
              <p className="text-xs text-muted-foreground italic mt-3">
                Hücreleri düzenlemek için sağ üstteki <span className="font-medium">"Kural Düzenleyici"</span> butonunu kullanın.
              </p>
            </CardContent>
            <CardFooter className="border-t pt-4 flex flex-wrap items-center gap-2">
              <Button
                size="sm" variant="outline" disabled
                title="Yakında — Enstitü öğrencileri için ayrı kriterler tanımlanır"
              >
                <Scissors className="h-4 w-4 mr-1" /> Enstitü İlişik Kesme Kriterleri
              </Button>
              <Button
                size="sm" variant="outline" disabled
                title="Yakında — Bu müfredatın tüm tanımlarını başka müfredata kopyala"
              >
                <Copy className="h-4 w-4 mr-1" /> Müfredat Kopyalama İşlemleri
              </Button>
              <Button
                size="sm" variant="outline" disabled
                title="Yakında — Sınıf+dönem grup kopyalama"
              >
                <Users className="h-4 w-4 mr-1" /> Grup Kopyalama
              </Button>
            </CardFooter>
          </Card>
        </>
      )}


      {/* ── Curriculum Term Rules Edit Dialog (mevcut, reuse) ── */}
      {curriculumId && (
        <CurriculumTermRulesDialog
          curriculumId={curriculumId}
          curriculumLabel={curriculumLabel}
          open={showTermRulesDialog}
          onClose={() => setShowTermRulesDialog(false)}
        />
      )}
    </div>
  );
}
