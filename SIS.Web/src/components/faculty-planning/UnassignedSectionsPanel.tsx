import { useMemo, useState } from 'react';
import { useDraggable, useDroppable } from '@dnd-kit/core';
import { BookOpen, ChevronDown, ChevronUp, FlaskConical, GripVertical, Inbox, Search, Trash2, Users } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Input } from '@/components/ui/input';
import type { FacultyPlanningSection, ScheduleBlockType } from '../../types/facultyPlanning';

export const UNASSIGNED_POOL_DROP_ID = 'unassigned-pool';
export const POOL_DRAG_ID_PREFIX = 'pool-section-';

/** Pool kartı için varsayılan slot süresi (dk) — Türkiye'de standart 50; gerçek değer parent'tan geçer. */
const DEFAULT_SLOT_MINUTES = 50;

interface UnassignedSectionsPanelProps {
  /** Tüm fakülte section'ları — panel kendisi 'Unscheduled' filtresi uygular. */
  sections: FacultyPlanningSection[];
  /** Mevcut sürükleme aktif mi — havuza drop hint için. */
  isDragging: boolean;
  /** Sürüklenen kart pool'da drop edilebilir mi (yalnız grid'den sürükleme + section atanmış olmalı). */
  canAcceptDrop: boolean;
  /** Gerçek slot süresi (dk) — pool drag için durationMinutes hesabı. Yoksa 50 dk varsayılır. */
  slotMinutes?: number;
}

/**
 * Sol panel — atanmamış section'ların listesi + grid kartlarının "kaldır" hedefi.
 *
 * - 'Unscheduled' statüsündeki section'lar burada listelenir.
 * - Search ile hızlı filtre (ders kodu / ad / hoca).
 * - Grid kartını bu panele bırakırsanız → unassign tetiklenir (handler parent'ta).
 *
 * Faz 4 sürümü: panel kartları sürüklenmez (yalnız gösterim + drop target).
 * Pool'dan grid'e atama için boş hücreye tıklayıp SlotAssignDialog kullanılır.
 */
export function UnassignedSectionsPanel({
  sections, isDragging, canAcceptDrop, slotMinutes = DEFAULT_SLOT_MINUTES,
}: UnassignedSectionsPanelProps) {
  const [search, setSearch] = useState('');
  const [deptFilter, setDeptFilter] = useState('');
  const [yearFilter, setYearFilter] = useState('');
  const [collapsed, setCollapsed] = useState(false);
  // KRİTİK: disabled drag state'ine bağlanmaz — drag start'ta dnd-kit ilk ölçümünü yaparken
  // React state batched update ile gecikme yaratıyor; havuz "kayıtsız" kalıp drop hedefi olarak
  // bulunamıyordu. canAcceptDrop yalnız görsel feedback için; drop akışı handleDragEnd'de
  // pinli/atanmamış kontrolü ile yönetilir.
  const { setNodeRef, isOver } = useDroppable({
    id: UNASSIGNED_POOL_DROP_ID,
  });

  // Atanmamış havuz — tamamen unscheduled VEYA Partial (kısmen atanmış) section'ları içerir.
  // Partial section'ların yalnız atanmamış bölümü (Teori veya Lab) drag edilebilir; kart UI
  // her bölüm için ayrı drag handle gösterir.
  const unscheduled = useMemo(
    () => sections.filter(s => {
      if (s.status === 'Conflicted' || s.status === 'Scheduled') return false;
      // Bölümlerden en az biri atanmamışsa havuzda göster
      const theoryAssigned = s.schedules.some(b => b.blockType === 'Theory');
      const labAssigned = s.schedules.some(b => b.blockType === 'Lab');
      const theoryPending = s.theoryHours > 0 && !theoryAssigned;
      const labPending = s.labHours > 0 && !labAssigned;
      return theoryPending || labPending;
    }),
    [sections],
  );

  // Dropdown opsiyonları havuzun gerçek içeriğinden türetilir — hiç olmayan bölüm/sınıf gösterilmez.
  const deptOptions = useMemo(() => {
    const set = new Set<string>();
    for (const s of unscheduled) if (s.departmentCode) set.add(s.departmentCode);
    return Array.from(set).sort();
  }, [unscheduled]);

  const yearOptions = useMemo(() => {
    const set = new Set<number>();
    for (const s of unscheduled) {
      const y = s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : 0;
      if (y > 0) set.add(y);
    }
    return Array.from(set).sort((a, b) => a - b);
  }, [unscheduled]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    const yearNum = yearFilter ? Number(yearFilter) : null;
    return unscheduled.filter(s => {
      if (deptFilter && s.departmentCode !== deptFilter) return false;
      if (yearNum != null) {
        const y = s.suggestedSemester > 0 ? Math.ceil(s.suggestedSemester / 2) : 0;
        if (y !== yearNum) return false;
      }
      if (q) {
        const hit = s.courseCode.toLowerCase().includes(q)
          || s.courseName.toLowerCase().includes(q)
          || (s.instructorName?.toLowerCase().includes(q) ?? false)
          || s.departmentCode.toLowerCase().includes(q);
        if (!hit) return false;
      }
      return true;
    });
  }, [unscheduled, search, deptFilter, yearFilter]);

  const activeFilterCount = [search, deptFilter, yearFilter].filter(Boolean).length;
  const clearFilters = () => { setSearch(''); setDeptFilter(''); setYearFilter(''); };

  const showAcceptHint = isDragging && canAcceptDrop;

  return (
    <aside
      ref={setNodeRef}
      className={cn(
        'shrink-0 bg-white border-r border-slate-200 flex flex-col transition-colors',
        collapsed ? 'w-12' : 'w-72',
        isOver && canAcceptDrop && 'bg-red-50/60 ring-2 ring-red-300 ring-inset',
        showAcceptHint && !isOver && 'bg-amber-50/40',
      )}
    >
      {/* Header */}
      <div className="px-3 py-2.5 border-b border-slate-100 flex items-center gap-2">
        <button
          type="button"
          onClick={() => setCollapsed(prev => !prev)}
          className="p-1 rounded hover:bg-slate-100 text-slate-500"
          aria-label={collapsed ? 'Aç' : 'Daralt'}
        >
          {collapsed ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronUp className="h-3.5 w-3.5" />}
        </button>
        {!collapsed && (
          <>
            <Inbox className="h-3.5 w-3.5 text-slate-400" />
            <h2 className="text-xs font-semibold uppercase tracking-wider text-slate-600 flex-1">
              Atanmamış
            </h2>
            <span className="text-[10px] tabular-nums bg-slate-100 text-slate-600 rounded px-1.5 py-0.5 font-medium">
              {filtered.length}{activeFilterCount > 0 ? `/${unscheduled.length}` : ''}
            </span>
          </>
        )}
      </div>

      {collapsed ? (
        <div className="flex-1 flex items-start justify-center pt-3">
          <span className="text-[10px] font-bold tabular-nums text-slate-500 rotate-180" style={{ writingMode: 'vertical-rl' }}>
            {unscheduled.length} adet
          </span>
        </div>
      ) : (
        <>
          {/* Filtreler — uzun havuz listesinde kullanıcı kaybolmasın diye bölüm + sınıf daralt */}
          <div className="px-3 py-2 border-b border-slate-100 space-y-1.5">
            <div className="relative">
              <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3 w-3 text-slate-400" />
              <Input
                placeholder="Ders, hoca, bölüm..."
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="pl-7 h-7 text-xs border-slate-200"
              />
            </div>
            <div className="flex items-center gap-1.5">
              <select
                className="h-7 flex-1 min-w-0 rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-700"
                value={deptFilter}
                onChange={e => setDeptFilter(e.target.value)}
                aria-label="Bölüm filtresi"
              >
                <option value="">Tüm Bölümler</option>
                {deptOptions.map(code => (
                  <option key={code} value={code}>{code}</option>
                ))}
              </select>
              <select
                className="h-7 flex-1 min-w-0 rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-700"
                value={yearFilter}
                onChange={e => setYearFilter(e.target.value)}
                aria-label="Sınıf filtresi"
              >
                <option value="">Tüm Sınıflar</option>
                {yearOptions.map(y => (
                  <option key={y} value={y}>{y}. sınıf</option>
                ))}
              </select>
            </div>
            {activeFilterCount > 0 && (
              <button
                type="button"
                onClick={clearFilters}
                className="text-[10px] text-slate-500 hover:text-red-600 underline underline-offset-2"
              >
                Filtreyi temizle ({activeFilterCount})
              </button>
            )}
          </div>

          {/* Drop hint */}
          {showAcceptHint && (
            <div className="px-3 py-2 bg-red-50/80 border-b border-red-200">
              <p className="text-[11px] text-red-700 flex items-center gap-1.5 font-medium">
                <Trash2 className="h-3 w-3" />
                Kartı buraya bırakarak atamayı kaldır
              </p>
            </div>
          )}

          {/* List */}
          <div className="flex-1 overflow-y-auto p-2 space-y-1">
            {filtered.length === 0 ? (
              <div className="py-6 text-center">
                <Inbox className="h-6 w-6 text-slate-300 mx-auto mb-1.5" />
                <p className="text-[11px] text-slate-400">
                  {activeFilterCount > 0 ? 'Eşleşen şube yok' : 'Tüm şubeler atanmış'}
                </p>
              </div>
            ) : (
              filtered.map(s => (
                <UnassignedSectionCard key={s.sectionId} section={s} slotMinutes={slotMinutes} />
              ))
            )}
          </div>
        </>
      )}
    </aside>
  );
}

/**
 * Atanmamış section kartı — Teori ve Lab bölümleri için AYRI drag handle.
 *
 * Sebebi: 3T+2L gibi karma derslerde "5 ardışık slot" bulmak çoğu durumda imkansız. Önceden tek
 * drag tüm 5 slot'u tek block olarak yerleştirmeye çalışıyor, neredeyse her hücre invalid çıkıyordu.
 * Yeni davranış: kullanıcı Teori bloğunu bir yere (3 slot), Lab bloğunu başka bir yere (2 slot)
 * ayrı ayrı bırakır. Section "Partial" status'a düşer, geri kalan bölüm havuzda görünür kalır.
 *
 * Pure-Theory veya pure-Lab dersler tek handle ile (eski davranış) gösterilir.
 */
function UnassignedSectionCard({ section, slotMinutes }: { section: FacultyPlanningSection; slotMinutes: number }) {
  const totalHours = section.theoryHours + section.labHours;
  const theoryAssigned = section.schedules.some(b => b.blockType === 'Theory');
  const labAssigned = section.schedules.some(b => b.blockType === 'Lab');
  const theoryPending = section.theoryHours > 0 && !theoryAssigned;
  const labPending = section.labHours > 0 && !labAssigned;
  const isPartial = theoryAssigned !== labAssigned && (section.theoryHours > 0 && section.labHours > 0);

  return (
    <div
      className={cn(
        'relative rounded-md border border-slate-200 bg-white hover:border-slate-300 hover:shadow-sm transition-all px-2.5 py-2',
        section.isPinned && 'opacity-60',
      )}
    >
      <div className="flex items-baseline gap-1.5 min-w-0">
        <span className="font-mono font-bold text-xs text-slate-800 shrink-0">
          {section.courseCode}
        </span>
        <span className="text-[10px] text-slate-400 shrink-0">·{section.sectionIndex}</span>
        {isPartial && (
          <span className="text-[9px] font-semibold uppercase tracking-wider text-amber-700 bg-amber-100 px-1 py-px rounded shrink-0">
            Kısmi
          </span>
        )}
        <span className="text-[10px] text-slate-400 ml-auto bg-slate-50 px-1 py-0.5 rounded shrink-0">
          {section.departmentCode}
        </span>
      </div>
      <p className="text-[11px] text-slate-600 truncate mt-0.5">
        {section.courseName}
      </p>
      <div className="flex items-center gap-2 mt-1 text-[10px] text-slate-400">
        <span className="flex items-center gap-0.5">
          <Users className="h-2.5 w-2.5" />
          {section.capacity}
        </span>
        <span>{section.theoryHours}T+{section.labHours}L</span>
        {totalHours > 0 && <span className="text-slate-300">· {totalHours}sa</span>}
        {section.instructorName && (
          <span className="ml-auto truncate max-w-[100px]">{section.instructorName.split(' ').pop()}</span>
        )}
      </div>

      {/* Bölüm bazlı drag handle'lar — yalnız atanmamış bölümler için görünür. */}
      <div className="mt-1.5 flex gap-1.5">
        {theoryPending && (
          <PoolDragHandle
            section={section}
            blockType="Theory"
            hours={section.theoryHours}
            slotMinutes={slotMinutes}
            icon={<BookOpen className="h-3 w-3" />}
            label="Teori"
            colorClass="bg-blue-50 hover:bg-blue-100 border-blue-200 text-blue-800"
          />
        )}
        {labPending && (
          <PoolDragHandle
            section={section}
            blockType="Lab"
            hours={section.labHours}
            slotMinutes={slotMinutes}
            icon={<FlaskConical className="h-3 w-3" />}
            label="Lab"
            colorClass="bg-purple-50 hover:bg-purple-100 border-purple-200 text-purple-800"
          />
        )}
        {theoryAssigned && (
          <span className="text-[10px] text-emerald-700 bg-emerald-50 border border-emerald-200 px-1.5 py-0.5 rounded">
            ✓ Teori atandı
          </span>
        )}
        {labAssigned && (
          <span className="text-[10px] text-emerald-700 bg-emerald-50 border border-emerald-200 px-1.5 py-0.5 rounded">
            ✓ Lab atandı
          </span>
        )}
      </div>
    </div>
  );
}

/** Tek bölüm için (Teori veya Lab) sürüklenebilir buton. DragData.blockType ile backend'e doğru block türü gider. */
function PoolDragHandle({
  section, blockType, hours, slotMinutes, icon, label, colorClass,
}: {
  section: FacultyPlanningSection;
  blockType: ScheduleBlockType;
  hours: number;
  slotMinutes: number;
  icon: React.ReactNode;
  label: string;
  colorClass: string;
}) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `${POOL_DRAG_ID_PREFIX}${section.sectionId}-${blockType}`,
    disabled: section.isPinned,
    data: {
      kind: 'pool',
      sectionId: section.sectionId,
      classroomId: null,
      blockType,
      // Bu bölüm için saat sayısı × slot süresi — kısmi atama için kritik
      durationMinutes: hours * slotMinutes,
    },
  });

  const dragTransformStyle = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`, zIndex: 50 }
    : undefined;

  return (
    <button
      ref={setNodeRef}
      style={dragTransformStyle}
      {...attributes}
      {...listeners}
      type="button"
      className={cn(
        'flex items-center gap-1 text-[10px] font-medium border rounded px-1.5 py-0.5 transition-all',
        colorClass,
        section.isPinned ? 'cursor-not-allowed opacity-50' : 'cursor-grab active:cursor-grabbing',
        isDragging && 'opacity-40 ring-2 ring-[#003366]',
      )}
      aria-label={`${section.courseCode} ${label} bloğunu sürükle (${hours} saat)`}
    >
      <GripVertical className="h-2.5 w-2.5 text-slate-400" aria-hidden="true" />
      {icon}
      <span>{label}</span>
      <span className="text-slate-500">({hours}sa)</span>
    </button>
  );
}
