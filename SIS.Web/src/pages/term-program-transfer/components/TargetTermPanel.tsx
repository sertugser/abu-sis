import { useMemo, useState } from 'react';
import { Inbox, Eye, Pencil, Plus, Users, Trash2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useOpenedCourses } from '../../../hooks/useCourseOpening';
import { useAdminProgramsByStructure } from '../../../hooks/useAdminAcademicStructure';
import { semesterToClassLevel, CLASS_LEVELS } from '../types';
import type { OpenedCourseSectionDto } from '../../../types/courseOpening';

interface DepartmentOption {
  departmentId: number;
  departmentName: string;
}

interface TermOption {
  id: number;
  name: string;
}

interface TargetTermPanelProps {
  departments: DepartmentOption[];
  /** Bölüm — sol panel ile ÇİFT YÖNLÜ sync (kullanıcı kararı: ortak state). */
  departmentId: number | null;
  onDepartmentChange: (id: number) => void;
  /** Program — sol panel ile ÇİFT YÖNLÜ sync. */
  programId: number | null;
  onProgramChange: (id: number | null) => void;
  /** Hedef dönem — bağımsız (sağ panel kendi içinde tutuyor). */
  defaultTermId: number | null;
  availableTerms: TermOption[];
  onDetail: (section: OpenedCourseSectionDto) => void;
  onEdit: (section: OpenedCourseSectionDto) => void;
  onAddSection: (section: OpenedCourseSectionDto) => void;
  onDelete: (section: OpenedCourseSectionDto) => void;
}

export function TargetTermPanel({
  departments,
  departmentId,
  onDepartmentChange,
  programId,
  onProgramChange,
  defaultTermId,
  availableTerms,
  onDetail,
  onEdit,
  onAddSection,
  onDelete,
}: TargetTermPanelProps) {
  // Bölüm/Program: parent kontrolünde (çift yönlü sync sol panel ile)
  // Dönem/Sınıf/Arama: bu panele özel
  const [termId, setTermId] = useState<number | null>(defaultTermId);
  const [classLevel, setClassLevel] = useState<number | null>(null);
  const [searchCode, setSearchCode] = useState('');
  const [searchName, setSearchName] = useState('');
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  // defaultTermId değişince termId'yi senkronla — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const [prevDefaultTermId, setPrevDefaultTermId] = useState(defaultTermId);
  if (defaultTermId !== prevDefaultTermId) {
    setPrevDefaultTermId(defaultTermId);
    if (defaultTermId !== null) setTermId(defaultTermId);
  }

  const { data, isLoading } = useOpenedCourses(departmentId, termId);
  const programsQuery = useAdminProgramsByStructure(departmentId ?? undefined);
  const programs = useMemo(() => (programsQuery.data ?? []).map((p) => ({ id: p.id, name: p.name })), [programsQuery.data]);

  const sections = useMemo(() => data?.sections ?? [], [data]);

  // Section-bazlı liste (her satır 1 şube)
  const filtered = useMemo(() => {
    let result = sections;
    if (classLevel != null) {
      const levelMin = (classLevel - 1) * 2 + 1;
      const levelMax = levelMin + 1;
      result = result.filter((s) => s.suggestedSemester >= levelMin && s.suggestedSemester <= levelMax);
    }
    if (searchCode.trim()) {
      const q = searchCode.trim().toLowerCase();
      result = result.filter((s) => s.courseCode.toLowerCase().includes(q));
    }
    if (searchName.trim()) {
      const q = searchName.trim().toLowerCase();
      result = result.filter((s) => s.courseName.toLowerCase().includes(q));
    }
    return result.sort((a, b) => a.courseCode.localeCompare(b.courseCode) || a.sectionNumber - b.sectionNumber);
  }, [sections, classLevel, searchCode, searchName]);

  const selectedSection = useMemo(
    () => filtered.find((s) => s.sectionId === selectedSectionId) ?? null,
    [filtered, selectedSectionId],
  );

  return (
    <Card className="overflow-hidden">
      {/* Başlık + Filtreler */}
      <div className="border-b bg-muted/30">
        <div className="px-4 py-2.5 flex items-center gap-2 border-b border-border/40">
          <h3 className="text-base font-semibold">Hedef Dönemde Açılmış Dersler</h3>
          {filtered.length > 0 && (
            <span className="text-xs text-muted-foreground ml-auto">{filtered.length} şube</span>
          )}
        </div>

        <div className="px-4 py-3 space-y-2">
          <FilterRow label="Dönem">
            <Select value={String(termId ?? '')} onValueChange={(v) => setTermId(Number(v))}>
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                {availableTerms.map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Bölüm">
            <Select
              value={departmentId == null ? '' : String(departmentId)}
              onValueChange={(v) => onDepartmentChange(Number(v))}
              disabled={departments.length === 0}
            >
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Bölüm seçin" />
              </SelectTrigger>
              <SelectContent>
                {departments.map((d) => (
                  <SelectItem key={d.departmentId} value={String(d.departmentId)}>
                    {d.departmentName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Program">
            <Select value={programId == null ? 'all' : String(programId)} onValueChange={(v) => onProgramChange(v === 'all' ? null : Number(v))}>
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Tüm programlar" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm programlar</SelectItem>
                {programs.map((p) => (
                  <SelectItem key={p.id} value={String(p.id)}>
                    {p.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Sınıf">
            <Select value={classLevel == null ? 'all' : String(classLevel)} onValueChange={(v) => setClassLevel(v === 'all' ? null : Number(v))}>
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Tümü" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {CLASS_LEVELS.map((lvl) => (
                  <SelectItem key={lvl.value} value={String(lvl.value)}>
                    {lvl.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Ders Kod">
            <Input value={searchCode} onChange={(e) => setSearchCode(e.target.value)} className="h-9 text-sm" placeholder="Örn. CENG101" />
          </FilterRow>

          <FilterRow label="Ders Ad">
            <Input value={searchName} onChange={(e) => setSearchName(e.target.value)} className="h-9 text-sm" placeholder="Ders adında ara..." />
          </FilterRow>
        </div>

        {/* Aksiyon barı — bir satır seçili iken görünür */}
        {selectedSection && (
          <div className="px-4 py-2 border-t border-border/40 bg-blue-50/50 dark:bg-blue-950/20 flex items-center gap-2 flex-wrap">
            <span className="text-xs font-medium text-muted-foreground mr-1">
              <span className="font-mono text-foreground">{selectedSection.courseCode}</span> Şube{' '}
              <span className="text-foreground">{selectedSection.sectionNumber}</span>:
            </span>
            <Button variant="outline" size="sm" className="h-7 text-xs gap-1" onClick={() => onDetail(selectedSection)}>
              <Eye className="h-3 w-3" />
              İncele
            </Button>
            <Button variant="outline" size="sm" className="h-7 text-xs gap-1" onClick={() => onEdit(selectedSection)}>
              <Pencil className="h-3 w-3" />
              Düzenle
            </Button>
            <Button variant="outline" size="sm" className="h-7 text-xs gap-1" onClick={() => onAddSection(selectedSection)}>
              <Plus className="h-3 w-3" />
              + Şube Ekle
            </Button>
            <Button variant="outline" size="sm" className="h-7 text-xs gap-1" disabled title="Yakında">
              <Users className="h-3 w-3" />
              Erişim
            </Button>
            <Button variant="outline" size="sm" className="h-7 text-xs gap-1 text-destructive hover:bg-destructive/10" onClick={() => onDelete(selectedSection)}>
              <Trash2 className="h-3 w-3" />
              Sil
            </Button>
            <Button variant="ghost" size="sm" className="h-7 text-xs ml-auto" onClick={() => setSelectedSectionId(null)}>
              Seçimi Bırak
            </Button>
          </div>
        )}
      </div>

      <CardContent className="p-0">
        {isLoading ? (
          <div className="p-3 space-y-2">
            <Skeleton className="h-7 w-full" />
            <Skeleton className="h-7 w-full" />
            <Skeleton className="h-7 w-full" />
          </div>
        ) : !departmentId || !termId ? (
          <div className="py-12 text-center text-sm text-muted-foreground">Bölüm ve hedef dönem seçin.</div>
        ) : filtered.length === 0 ? (
          <div className="py-12 text-center">
            <Inbox className="h-9 w-9 text-muted-foreground/30 mx-auto mb-2" />
            <p className="text-sm text-muted-foreground">
              {sections.length === 0 ? 'Bu dönemde henüz ders açılmamış.' : 'Filtrelerle eşleşen ders yok.'}
            </p>
          </div>
        ) : (
          <div className="overflow-auto" style={{ maxHeight: '60vh' }}>
            <table className="w-full text-sm">
              <thead className="bg-muted/50 sticky top-0 z-10">
                <tr>
                  <th className="px-3 py-2 text-left font-semibold">Kod</th>
                  <th className="px-3 py-2 text-left font-semibold">Ad</th>
                  <th className="px-2 py-2 text-center font-semibold w-10" title="Şube">Şb</th>
                  <th className="px-2 py-2 text-center font-semibold w-10" title="Sınıf">Sn</th>
                  <th className="px-2 py-2 text-center font-semibold w-12">Krd</th>
                  <th className="px-2 py-2 text-center font-semibold w-14">AKTS</th>
                  <th className="px-2 py-2 text-center font-semibold w-20">Doluluk</th>
                  <th className="px-3 py-2 text-left font-semibold w-40">Hoca</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((s) => {
                  const ratio = s.capacity > 0 ? Math.round((s.enrolledCount / s.capacity) * 100) : 0;
                  const ratioColor = ratio >= 90 ? 'text-red-600' : ratio >= 70 ? 'text-amber-600' : 'text-emerald-600';
                  const isSelected = selectedSectionId === s.sectionId;
                  return (
                    <tr
                      key={s.sectionId}
                      className={`border-t cursor-pointer transition-colors ${
                        isSelected ? 'bg-blue-100 dark:bg-blue-950/40' : 'hover:bg-muted/30'
                      }`}
                      onClick={() => setSelectedSectionId(isSelected ? null : s.sectionId)}
                    >
                      <td className="px-3 py-2 font-mono font-medium">{s.courseCode}</td>
                      <td className="px-3 py-2 truncate max-w-[260px]" title={s.courseName}>{s.courseName}</td>
                      <td className="px-2 py-2 text-center">{s.sectionNumber}</td>
                      <td className="px-2 py-2 text-center text-muted-foreground">{semesterToClassLevel(s.suggestedSemester) || '—'}</td>
                      <td className="px-2 py-2 text-center">{s.credits}</td>
                      <td className="px-2 py-2 text-center">{s.ects}</td>
                      <td className={`px-2 py-2 text-center text-xs ${ratioColor}`}>
                        <div>{s.enrolledCount}/{s.capacity}</div>
                        <div className="text-[10px]">%{ratio}</div>
                      </td>
                      <td className="px-3 py-2 text-xs text-muted-foreground truncate max-w-[160px]" title={s.instructorName ?? '—'}>
                        {s.instructorName ?? '— Atanmamış —'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function FilterRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[88px_1fr] items-center gap-3">
      <Label className="text-xs text-muted-foreground font-medium">{label}</Label>
      {children}
    </div>
  );
}
