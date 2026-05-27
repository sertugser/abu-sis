import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus, Trash2, Search, ChevronDown, ChevronRight, BookOpen, History } from 'lucide-react';
import {
  useAdvisorStudentRegistrations,
  useAdvisorStudentCatalog,
  useAdvisorAddCourse,
  useAdvisorDropCourse,
} from '../../hooks/useAdvisor';
import { getErrorMessage } from '../../lib/errorMessage';
import type { StudentCourseDto } from '../../types/course';

const TAKE_STATUS_LABELS: Record<string, string> = {
  IlkDefa: 'İlk Defa',
  Alttan: 'Alttan',
  OncedenAlindi: 'Önceden Alındı',
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

/**
 * Danışmanın öğrenci adına ders ekle/çıkar editörü (iki sütun: kayıtlı dersler + aday katalog).
 * Tam ekran detayda inline gömülür; faz/iş kuralları backend'de uygulanır.
 */
export default function AdvisorCourseEditor({ studentId }: { studentId: number }) {
  const { data: registrations = [], isLoading: regsLoading } = useAdvisorStudentRegistrations(studentId);
  const { data: catalog = [], isLoading: catalogLoading } = useAdvisorStudentCatalog(studentId);
  const addMutation = useAdvisorAddCourse();
  const dropMutation = useAdvisorDropCourse();

  const [search, setSearch] = useState('');
  const [expandedCourseId, setExpandedCourseId] = useState<number | null>(null);
  const [showPast, setShowPast] = useState(false);
  const [error, setError] = useState('');

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

  // Zaten bu dönem kayıtlı olunan dersler (kod bazlı) — eklenebilirler listesinden çıkarılır.
  const registeredCourseCodes = useMemo(
    () => new Set(activeRegs.map(r => r.courseCode)),
    [activeRegs],
  );

  const filteredCatalog = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr');
    return catalog.filter(c => {
      if (registeredCourseCodes.has(c.code)) return false;             // zaten kayıtlı
      if (!showPast && c.takeStatus === 'OncedenAlindi') return false; // geçmişte alınan (varsayılan gizli)
      if (!q) return true;
      return c.code.toLocaleLowerCase('tr').includes(q) || c.name.toLocaleLowerCase('tr').includes(q);
    });
  }, [catalog, search, showPast, registeredCourseCodes]);

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

  return (
    <div className="space-y-3">
      {error && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
        {/* Bu dönem dersleri — onaya gönderilen / aktif, çıkar */}
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Bu Dönem Dersleri ({activeRegs.length})
          </p>
          {regsLoading ? (
            <div className="space-y-2">{[1, 2, 3].map(i => <Skeleton key={i} className="h-14 rounded-lg" />)}</div>
          ) : activeRegs.length === 0 ? (
            <p className="rounded-lg border border-dashed border-border px-3 py-4 text-sm text-muted-foreground">
              Bu dönem kayıtlı ders yok.
            </p>
          ) : (
            <div className="space-y-2">
              {activeRegs.map(reg => (
                <div key={reg.registrationId} className="flex items-start justify-between gap-2 rounded-lg border border-border bg-card px-3 py-2">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="font-mono text-sm font-semibold text-primary">{reg.courseCode}</span>
                      <span className="truncate text-sm text-foreground">{reg.courseName}</span>
                      <Badge variant={REG_STATUS_VARIANTS[reg.status] ?? 'secondary'} className="px-1 py-0 text-[10px]">
                        {REG_STATUS_LABELS[reg.status] ?? reg.status}
                      </Badge>
                      {reg.isRepeated && <Badge variant="outline" className="px-1 py-0 text-[10px]">Tekrar</Badge>}
                    </div>
                    <p className="mt-0.5 text-xs text-muted-foreground">
                      {reg.ects} AKTS{reg.schedule ? ` • ${reg.schedule}` : ''}{reg.instructorName ? ` • ${reg.instructorName}` : ''}
                    </p>
                  </div>
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
              ))}
            </div>
          )}
        </div>

        {/* Aday dersler — ekle */}
        <div className="space-y-2">
          <div className="flex items-center justify-between gap-2">
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Ders Ekle</p>
            <button
              type="button"
              onClick={() => setShowPast(v => !v)}
              className={`inline-flex items-center gap-1 rounded-md border px-2 py-1 text-[11px] transition-colors ${
                showPast ? 'border-primary/40 bg-primary/5 text-primary' : 'border-border text-muted-foreground hover:bg-muted/40'
              }`}
              title="Önceden alınmış (geçmiş) dersleri göster/gizle"
            >
              <History className="h-3.5 w-3.5" />
              Geçmiş dersler
            </button>
          </div>
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Kod veya ad ara..."
              className="pl-8"
            />
          </div>

          {catalogLoading ? (
            <div className="space-y-2">{[1, 2, 3].map(i => <Skeleton key={i} className="h-12 rounded-lg" />)}</div>
          ) : filteredCatalog.length === 0 ? (
            <p className="rounded-lg border border-dashed border-border px-3 py-4 text-sm text-muted-foreground">
              Eklenebilecek ders bulunamadı.
            </p>
          ) : (
            <div className="space-y-1.5">
              {filteredCatalog.map(course => (
                <CatalogCourseRow
                  key={course.courseId}
                  course={course}
                  expanded={expandedCourseId === course.courseId}
                  onToggle={() => setExpandedCourseId(id => (id === course.courseId ? null : course.courseId))}
                  registeredSectionIds={registeredSectionIds}
                  busy={busy}
                  onAdd={handleAdd}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

interface CatalogCourseRowProps {
  course: StudentCourseDto;
  expanded: boolean;
  onToggle: () => void;
  registeredSectionIds: Set<number>;
  busy: boolean;
  onAdd: (courseSectionId: number) => void;
}

function CatalogCourseRow({ course, expanded, onToggle, registeredSectionIds, busy, onAdd }: CatalogCourseRowProps) {
  return (
    <div className="rounded-lg border border-border bg-card">
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left hover:bg-muted/40"
      >
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="font-mono text-sm font-semibold text-primary">{course.code}</span>
            <span className="truncate text-sm text-foreground">{course.name}</span>
          </div>
          <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
            <span>{course.ects} AKTS</span>
            <Badge variant="outline" className="px-1 py-0 text-[10px]">{TAKE_STATUS_LABELS[course.takeStatus] ?? course.takeStatus}</Badge>
            {!course.prerequisitesMet && <Badge variant="destructive" className="px-1 py-0 text-[10px]">Önkoşul eksik</Badge>}
          </div>
        </div>
        {expanded ? <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" /> : <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />}
      </button>

      {expanded && (
        <div className="space-y-1.5 border-t border-border px-3 py-2">
          {course.sections.length === 0 ? (
            <p className="text-xs text-muted-foreground">Açık şube yok.</p>
          ) : (
            course.sections.map(section => {
              const already = registeredSectionIds.has(section.sectionId);
              const full = section.quotaVisible && section.availableSeats <= 0;
              const disabled = busy || already || full || !section.isActive;
              return (
                <div key={section.sectionId} className="flex items-center justify-between gap-2 rounded-md bg-muted/40 px-2.5 py-1.5">
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
      )}
    </div>
  );
}
