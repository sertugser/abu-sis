import { useMemo, useState, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus, Trash2, Search, ChevronDown, ChevronRight, BookOpen, History, Info, AlertTriangle, RotateCcw } from 'lucide-react';
import {
  useAdvisorStudentRegistrations,
  useAdvisorStudentCatalog,
  useAdvisorAddCourse,
  useAdvisorDropCourse,
} from '../../hooks/useAdvisor';
import { getErrorMessage } from '../../lib/errorMessage';
import { CourseInfoSheet } from '../my-courses/CourseInfoSheet';
import type { StudentCourseDto } from '../../types/course';
import type { StudentRegistrationItem } from '../../api/advisor.api';

const TAKE_TYPE_LABELS: Record<string, string> = {
  Zorunlu: 'Zorunlu',
  Secmeli: 'Seçmeli',
  Alttan: 'Alttan',
  Yukseltme: 'Yükseltme',
};

// Bu dönem kaydı sayılmayan (listede gösterilmeyen) statüler.
const INACTIVE_STATUSES = new Set(['Dropped', 'Withdrawn', 'Rejected', 'Draft']);

const REG_STATUS_LABELS: Record<string, string> = {
  PendingAdvisorApproval: 'Onay Bekliyor',
  Active: 'Aktif',
  Draft: 'Taslak',
};

const REG_STATUS_VARIANTS: Record<string, 'default' | 'secondary' | 'destructive' | 'outline' | 'success' | 'warning'> = {
  PendingAdvisorApproval: 'warning',
  Active: 'success',
  Draft: 'secondary',
};

// Müfredat dışı dersler için sözde "sınıf" anahtarı.
const OUT_OF_CURRICULUM = 0;

/** Önerilen yarıyıldan sınıf: 1-2 → 1, 3-4 → 2 ... 0/yok → müfredat dışı (0). */
function classYearOf(suggestedSemester: number): number {
  return suggestedSemester > 0 ? Math.floor((suggestedSemester - 1) / 2) + 1 : OUT_OF_CURRICULUM;
}

/** Yarıyıl yarısı: tek yarıyıl = Güz, çift = Bahar. */
function semesterHalfOf(suggestedSemester: number): 'Guz' | 'Bahar' | null {
  if (suggestedSemester <= 0) return null;
  return suggestedSemester % 2 === 1 ? 'Guz' : 'Bahar';
}

/** "T+U" gösterimi: teori + (uygulama/lab). */
function theoryPlusApplied(reg: StudentRegistrationItem): string {
  const applied = reg.applicationHours ?? reg.labHours ?? 0;
  return `${reg.theoryHours}+${applied}`;
}

type HalfFilter = 'Tumu' | 'Guz' | 'Bahar';

interface Props {
  studentId: number;
  /** Öğrencinin güncel sınıfı (varsayılan aktif sekme için). */
  studentClass?: number;
  /** Proliz sırası: aday grid ile seçilen dersler arasına yerleşen kontrol alanı (Kontrol Et / Onay durumu). */
  controlSlot?: ReactNode;
}

/**
 * Danışmanın öğrenci adına ders ekle/çıkar editörü — Proliz "Danışman Onay" ekranı sırası:
 *   1) ÜSTTE: Ders Ekle — müfredat SINIF sekmeleri (1./2./3./4. Sınıf Dersleri + Müfredat/Bölüm
 *      Dışı Dersler) + Güz/Bahar/Tümü RADYO filtresi + aday ders GRID'i ([+] ile şube).
 *   2) Tekrar Edilmesi Gereken Dersler paneli.
 *   3) controlSlot (Kontrol Et ve Onayla / onay durumu) — ortada.
 *   4) ALTTA: "Seçilen Dersler" tam kolonlu tablo + satır aksiyonları (Diğer Bilgiler / Sil).
 * İş/faz kuralları backend'de uygulanır.
 */
export default function AdvisorCourseEditor({ studentId, studentClass, controlSlot }: Props) {
  const { data: registrations = [], isLoading: regsLoading } = useAdvisorStudentRegistrations(studentId);
  const { data: catalog = [], isLoading: catalogLoading } = useAdvisorStudentCatalog(studentId);
  const addMutation = useAdvisorAddCourse();
  const dropMutation = useAdvisorDropCourse();

  const [search, setSearch] = useState('');
  const [expandedCourseId, setExpandedCourseId] = useState<number | null>(null);
  const [showPast, setShowPast] = useState(false);
  const [onlyAlttan, setOnlyAlttan] = useState(false);
  const [half, setHalf] = useState<HalfFilter>('Tumu');
  const [classTab, setClassTab] = useState<number>(studentClass && studentClass > 0 ? studentClass : 1);
  const [error, setError] = useState('');
  const [detailCourseId, setDetailCourseId] = useState<number | null>(null);

  // Bu dönem fiilen kayıtlı dersler (Dropped/Rejected/Draft hariç), onay bekleyenler önce.
  const activeRegs = useMemo(() => {
    const order = (s: string) => (s === 'PendingAdvisorApproval' ? 0 : s === 'Active' ? 1 : 2);
    return registrations
      .filter(r => !INACTIVE_STATUSES.has(r.status))
      .slice()
      .sort((a, b) => order(a.status) - order(b.status));
  }, [registrations]);

  const registeredSectionIds = useMemo(
    () => new Set(registrations.map(r => r.courseSectionId)),
    [registrations],
  );
  const registeredCourseCodes = useMemo(
    () => new Set(activeRegs.map(r => r.courseCode)),
    [activeRegs],
  );

  const retakeCourses = useMemo(
    () => catalog.filter(c => c.takeStatus === 'Alttan'),
    [catalog],
  );

  const addableCatalog = useMemo(
    () => catalog.filter(c => {
      if (registeredCourseCodes.has(c.code)) return false;
      if (!showPast && c.takeStatus === 'OncedenAlindi') return false;
      return true;
    }),
    [catalog, showPast, registeredCourseCodes],
  );

  const classesWithCourses = useMemo(() => {
    const set = new Set<number>();
    for (const c of addableCatalog) set.add(classYearOf(c.suggestedSemester));
    return set;
  }, [addableCatalog]);

  const visibleCourses = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr');
    return addableCatalog
      .filter(c => classYearOf(c.suggestedSemester) === classTab)
      .filter(c => {
        if (half === 'Tumu') return true;
        const h = semesterHalfOf(c.suggestedSemester);
        return half === 'Guz' ? h === 'Guz' : h === 'Bahar';
      })
      .filter(c => !onlyAlttan || c.takeStatus === 'Alttan')
      .filter(c => !q || c.code.toLocaleLowerCase('tr').includes(q) || c.name.toLocaleLowerCase('tr').includes(q))
      .sort((a, b) => a.suggestedSemester - b.suggestedSemester || a.code.localeCompare(b.code, 'tr'));
  }, [addableCatalog, classTab, half, onlyAlttan, search]);

  const busy = addMutation.isPending || dropMutation.isPending;

  async function handleAdd(courseSectionId: number) {
    setError('');
    try {
      await addMutation.mutateAsync({ studentId, courseSectionId });
    } catch (err) {
      setError(getErrorMessage(err, 'Ders eklenemedi.'));
    }
  }

  async function handleDrop(registrationId: number) {
    setError('');
    try {
      await dropMutation.mutateAsync({ studentId, registrationId });
    } catch (err) {
      setError(getErrorMessage(err, 'Ders çıkarılamadı.'));
    }
  }

  const totalEcts = activeRegs.reduce((sum, r) => sum + r.ects, 0);

  const classTabs: { key: number; label: string }[] = [
    { key: 1, label: '1. Sınıf Dersleri' },
    { key: 2, label: '2. Sınıf Dersleri' },
    { key: 3, label: '3. Sınıf Dersleri' },
    { key: 4, label: '4. Sınıf Dersleri' },
    { key: OUT_OF_CURRICULUM, label: 'Müfredat/Bölüm Dışı Dersler' },
  ];

  const halfOptions: { key: HalfFilter; label: string }[] = [
    { key: 'Guz', label: 'Güz Yarıyılı Dersleri' },
    { key: 'Bahar', label: 'Bahar Yarıyılı Dersleri' },
    { key: 'Tumu', label: 'Tümü' },
  ];

  return (
    <div className="space-y-4">
      {error && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      {/* ── 1) DERS EKLE (üstte) — sınıf sekmeleri + yarıyıl radyo + aday grid ── */}
      <div className="space-y-2">
        {/* Sınıf sekmeleri (sol) + Güz/Bahar/Tümü radyo (sağ) */}
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border">
          <div className="flex flex-wrap items-center gap-1">
            {classTabs.map(t => {
              const hasCourses = classesWithCourses.has(t.key);
              const active = classTab === t.key;
              return (
                <button
                  key={t.key}
                  type="button"
                  onClick={() => setClassTab(t.key)}
                  className={`relative -mb-px rounded-t-md px-3 py-1.5 text-xs font-medium transition-colors ${
                    active
                      ? 'border border-b-transparent border-border bg-background text-primary'
                      : 'text-foreground hover:bg-muted/40'
                  }`}
                  title={hasCourses ? undefined : 'Bu sınıfta eklenebilecek ders yok'}
                >
                  {t.label}
                  {hasCourses && <span className="ml-1 inline-block h-1.5 w-1.5 rounded-full bg-primary/60 align-middle" />}
                </button>
              );
            })}
          </div>
          <div className="flex items-center gap-3 pb-1 text-xs">
            {halfOptions.map(o => (
              <label key={o.key} className="inline-flex cursor-pointer items-center gap-1 text-muted-foreground">
                <input
                  type="radio"
                  name="yariyil"
                  checked={half === o.key}
                  onChange={() => setHalf(o.key)}
                  className="h-3.5 w-3.5 accent-primary"
                />
                {o.label}
              </label>
            ))}
          </div>
        </div>

        {/* Yardımcı filtreler: Alttan / Geçmiş + arama */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-1.5">
            <button
              type="button"
              onClick={() => setOnlyAlttan(v => !v)}
              className={`inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[11px] transition-colors ${
                onlyAlttan ? 'border-amber-500/50 bg-amber-500/10 text-amber-700 dark:text-amber-400' : 'border-border text-muted-foreground hover:bg-muted/40'
              }`}
              title="Sadece alttan (FF/NA/U) dersleri göster"
            >
              <AlertTriangle className="h-3.5 w-3.5" /> Alttan Dersleri Göster
            </button>
            <button
              type="button"
              onClick={() => setShowPast(v => !v)}
              className={`inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[11px] transition-colors ${
                showPast ? 'border-primary/40 bg-primary/5 text-primary' : 'border-border text-muted-foreground hover:bg-muted/40'
              }`}
              title="Önceden alınmış (geçmiş) dersleri göster/gizle"
            >
              <History className="h-3.5 w-3.5" /> Geçmiş dersler
            </button>
          </div>
          <div className="relative w-full sm:w-56">
            <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Kod veya ad ara..."
              className="h-8 pl-8 text-xs"
            />
          </div>
        </div>

        {/* Aday ders GRID'i */}
        {catalogLoading ? (
          <div className="space-y-2">{[1, 2, 3].map(i => <Skeleton key={i} className="h-9 rounded" />)}</div>
        ) : visibleCourses.length === 0 ? (
          <p className="rounded-lg border border-dashed border-border px-3 py-4 text-sm text-muted-foreground">
            Bu sekmede eklenebilecek ders bulunamadı.
          </p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full min-w-[680px] border-collapse text-xs">
              <thead>
                <tr className="border-b border-border bg-muted/40 text-left text-[11px] uppercase tracking-wide text-muted-foreground">
                  <th className="w-8 px-2 py-2"></th>
                  <th className="px-2 py-2 font-semibold">Ders Kodu</th>
                  <th className="px-2 py-2 font-semibold">Ders Adı</th>
                  <th className="px-2 py-2 text-center font-semibold">Z/S</th>
                  <th className="px-2 py-2 text-center font-semibold">KRD</th>
                  <th className="px-2 py-2 text-center font-semibold">AKTS</th>
                  <th className="px-2 py-2 text-center font-semibold">Snf</th>
                </tr>
              </thead>
              <tbody>
                {visibleCourses.map(course => (
                  <CatalogCourseRows
                    key={course.courseId}
                    course={course}
                    expanded={expandedCourseId === course.courseId}
                    onToggle={() => setExpandedCourseId(id => (id === course.courseId ? null : course.courseId))}
                    registeredSectionIds={registeredSectionIds}
                    busy={busy}
                    onAdd={handleAdd}
                    onShowDetail={() => setDetailCourseId(course.courseId)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── 2) Tekrar edilmesi gereken (alttan) dersler ── */}
      {retakeCourses.length > 0 && (
        <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2">
          <p className="mb-1 flex items-center gap-1.5 text-sm font-semibold text-amber-700 dark:text-amber-400">
            <RotateCcw className="h-4 w-4" /> Tekrar Edilmesi Gereken Dersler ({retakeCourses.length})
          </p>
          <div className="flex flex-wrap gap-1.5">
            {retakeCourses.map(c => {
              const selected = registeredCourseCodes.has(c.code);
              return (
                <span
                  key={c.courseId}
                  className={`inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] ${
                    selected
                      ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400'
                      : 'border-amber-500/50 bg-background text-amber-700 dark:text-amber-400'
                  }`}
                  title={selected ? 'Plana eklenmiş' : 'Henüz plana eklenmemiş'}
                >
                  <span className="font-mono font-semibold">{c.code}</span>
                  {c.previousGrade && <span className="opacity-70">({c.previousGrade})</span>}
                  {selected ? '✓' : '!'}
                </span>
              );
            })}
          </div>
        </div>
      )}

      {/* ── 3) Kontrol alanı (Proliz: aday grid ile seçilen dersler arası) ── */}
      {controlSlot}

      {/* ── 4) SEÇİLEN DERSLER (altta) — tam kolonlu tablo ── */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Seçilen Dersler ({activeRegs.length})
          </p>
          {totalEcts > 0 && (
            <span className="text-xs text-muted-foreground">Toplam <strong className="text-foreground">{totalEcts}</strong> AKTS</span>
          )}
        </div>

        {regsLoading ? (
          <div className="space-y-2">{[1, 2, 3].map(i => <Skeleton key={i} className="h-10 rounded-lg" />)}</div>
        ) : activeRegs.length === 0 ? (
          <p className="rounded-lg border border-dashed border-border px-3 py-6 text-center text-sm text-muted-foreground">
            Bu dönem seçili ders yok.
          </p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full min-w-[820px] border-collapse text-xs">
              <thead>
                <tr className="border-b border-border bg-muted/40 text-left text-[11px] uppercase tracking-wide text-muted-foreground">
                  <th className="px-2 py-2 font-semibold">Şb</th>
                  <th className="px-2 py-2 font-semibold">Ders Kodu</th>
                  <th className="px-2 py-2 font-semibold">Ders Adı</th>
                  <th className="px-2 py-2 text-center font-semibold">Z/S</th>
                  <th className="px-2 py-2 text-center font-semibold">T+U</th>
                  <th className="px-2 py-2 text-center font-semibold">Krd</th>
                  <th className="px-2 py-2 text-center font-semibold">AKTS</th>
                  <th className="px-2 py-2 text-center font-semibold">Snf</th>
                  <th className="px-2 py-2 font-semibold">Alış</th>
                  <th className="px-2 py-2 font-semibold">Açma Nedeni</th>
                  <th className="px-2 py-2 font-semibold">Grup Kod</th>
                  <th className="px-2 py-2 font-semibold">Durum</th>
                  <th className="px-2 py-2 text-right font-semibold">İşlem</th>
                </tr>
              </thead>
              <tbody>
                {activeRegs.map(reg => (
                  <tr key={reg.registrationId} className="border-b border-border last:border-0 hover:bg-muted/20">
                    <td className="px-2 py-2 text-center font-mono">{reg.sectionNumber}</td>
                    <td className="px-2 py-2 font-mono font-semibold text-primary">{reg.courseCode}</td>
                    <td className="px-2 py-2">
                      <span className="text-foreground">{reg.courseName}</span>
                      {reg.isRepeated && <Badge variant="outline" className="ml-1 px-1 py-0 text-[10px]">Tekrar</Badge>}
                    </td>
                    <td className="px-2 py-2 text-center">
                      <span className={reg.isCompulsory ? 'font-semibold text-foreground' : 'text-muted-foreground'}>
                        {reg.isCompulsory ? 'Z' : 'S'}
                      </span>
                    </td>
                    <td className="px-2 py-2 text-center font-mono text-muted-foreground">{theoryPlusApplied(reg)}</td>
                    <td className="px-2 py-2 text-center font-mono">{reg.nationalCredit}</td>
                    <td className="px-2 py-2 text-center font-mono font-semibold">{reg.ects}</td>
                    <td className="px-2 py-2 text-center">{reg.suggestedSemester > 0 ? classYearOf(reg.suggestedSemester) : '—'}</td>
                    <td className="px-2 py-2">{TAKE_TYPE_LABELS[reg.takeType] ?? reg.takeType}</td>
                    <td className="px-2 py-2 text-muted-foreground">{reg.openingReasonName ?? '—'}</td>
                    <td className="px-2 py-2 font-mono text-muted-foreground">{reg.electiveGroupCode ?? reg.electivePoolSlotName ?? '—'}</td>
                    <td className="px-2 py-2">
                      <Badge variant={REG_STATUS_VARIANTS[reg.status] ?? 'secondary'} className="px-1 py-0 text-[10px]">
                        {REG_STATUS_LABELS[reg.status] ?? reg.status}
                      </Badge>
                    </td>
                    <td className="px-2 py-2">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 gap-1 px-2 text-muted-foreground hover:text-foreground"
                          title="Diğer bilgiler"
                          onClick={() => setDetailCourseId(reg.courseId)}
                        >
                          <Info className="h-3.5 w-3.5" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 gap-1 px-2 text-destructive hover:bg-destructive/10"
                          disabled={busy}
                          title="Dersi çıkar"
                          onClick={() => handleDrop(reg.registrationId)}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* "Diğer Bilgiler" — ortak ders detay dialog'u (Alınan Dersler ile aynı bileşen) */}
      <CourseInfoSheet courseId={detailCourseId} onOpenChange={(open) => { if (!open) setDetailCourseId(null); }} />
    </div>
  );
}

interface CatalogCourseRowsProps {
  course: StudentCourseDto;
  expanded: boolean;
  onToggle: () => void;
  registeredSectionIds: Set<number>;
  busy: boolean;
  onAdd: (courseSectionId: number) => void;
  onShowDetail: () => void;
}

/** Aday ders satırı (grid) + açılınca şube alt-satırı. */
function CatalogCourseRows({ course, expanded, onToggle, registeredSectionIds, busy, onAdd, onShowDetail }: CatalogCourseRowsProps) {
  const classYear = course.suggestedSemester > 0 ? classYearOf(course.suggestedSemester) : null;
  return (
    <>
      <tr className="border-b border-border last:border-0 hover:bg-muted/20">
        <td className="px-2 py-1.5 text-center">
          <button type="button" onClick={onToggle} title={expanded ? 'Kapat' : 'Şubeler'} className="text-muted-foreground hover:text-foreground">
            {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </button>
        </td>
        <td className="px-2 py-1.5">
          <button type="button" onClick={onToggle} className="font-mono font-semibold text-primary hover:underline">{course.code}</button>
        </td>
        <td className="px-2 py-1.5">
          <div className="flex items-center gap-1.5">
            <span className="text-foreground">{course.name}</span>
            {course.takeStatus === 'Alttan' && <Badge variant="destructive" className="px-1 py-0 text-[10px]">Alttan</Badge>}
            {!course.prerequisitesMet && <Badge variant="destructive" className="px-1 py-0 text-[10px]">Önkoşul eksik</Badge>}
            <button type="button" onClick={onShowDetail} title="Diğer bilgiler" className="text-muted-foreground hover:text-foreground">
              <Info className="h-3.5 w-3.5" />
            </button>
          </div>
        </td>
        <td className="px-2 py-1.5 text-center text-muted-foreground">{course.isCompulsory ? 'Z' : 'S'}</td>
        <td className="px-2 py-1.5 text-center font-mono">{course.nationalCredit}</td>
        <td className="px-2 py-1.5 text-center font-mono font-semibold">{course.ects}</td>
        <td className="px-2 py-1.5 text-center">{classYear ?? '—'}</td>
      </tr>
      {expanded && (
        <tr>
          <td colSpan={7} className="bg-muted/20 px-3 py-2">
            <div className="space-y-1.5">
              {course.sections.length === 0 ? (
                <p className="text-xs text-muted-foreground">Açık şube yok.</p>
              ) : (
                course.sections.map(section => {
                  const already = registeredSectionIds.has(section.sectionId);
                  const full = section.quotaVisible && section.availableSeats <= 0;
                  const disabled = busy || already || full || !section.isActive;
                  return (
                    <div key={section.sectionId} className="flex items-center justify-between gap-2 rounded-md bg-background px-2.5 py-1.5">
                      <div className="min-w-0 text-xs">
                        <div className="flex items-center gap-1.5">
                          <BookOpen className="h-3.5 w-3.5 text-muted-foreground" />
                          <span className="truncate text-foreground">{section.instructorName || 'Öğretim üyesi atanmadı'}</span>
                        </div>
                        <p className="mt-0.5 text-muted-foreground">
                          {section.schedule || 'Program belirlenmedi'}
                          {section.classroomName ? ` • ${section.classroomName}` : ''}
                          {section.quotaVisible ? ` • ${section.availableSeats} boş` : ''}
                        </p>
                      </div>
                      <Button
                        size="sm"
                        className="h-7 gap-1 px-2"
                        disabled={disabled}
                        title={already ? 'Zaten kayıtlı' : full ? 'Kontenjan dolu' : !section.isActive ? 'Şube pasif' : 'Dersi ekle'}
                        onClick={() => onAdd(section.sectionId)}
                      >
                        <Plus className="h-3.5 w-3.5" />
                        {already ? 'Kayıtlı' : 'Ekle'}
                      </Button>
                    </div>
                  );
                })
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
