import { cn } from '@/lib/utils';
import { CheckSquare, Square } from 'lucide-react';
import type { OpenedCourseSectionDto } from '../../types/courseOpening';

interface Props {
  courseCode: string;
  courseName: string;
  courseId: number;
  sections: OpenedCourseSectionDto[];
  selected?: boolean;
  onSelect?: (courseId: number) => void;
  onOpenManagement?: (courseId: number) => void;
}

/**
 * Açılan dersin tablo satırı — Proliz tarzı.
 * - Tek tıkla = sarı highlight (selected)
 * - Çift tıkla = ders yönetim dialog'u açılır (CourseManagementDialog)
 *
 * Şube ekleme/düzenleme/kapatma vb. tüm aksiyonlar dialog içine taşındı.
 * Bu satır sadece özet bilgi gösterir.
 */
export function OpenedCourseRow({
  courseCode,
  courseName,
  courseId,
  sections,
  selected = false,
  onSelect,
  onOpenManagement,
}: Props) {
  const totCap = sections.reduce((s, sec) => s + sec.capacity, 0);
  // Z/S, Krd, AKTS bilgileri ders bazında uniform → ilk şubeden alınır
  const firstSection = sections[0];

  return (
    <div
      className={cn(
        'grid grid-cols-[28px_100px_1fr_32px_48px_52px_72px] items-center gap-2 px-2 py-2 text-xs cursor-pointer transition-colors border-b border-gray-100 last:border-b-0',
        selected ? 'bg-amber-50 hover:bg-amber-100/80' : 'hover:bg-gray-50',
      )}
      onClick={() => onSelect?.(courseId)}
      onDoubleClick={(e) => {
        e.preventDefault();
        onOpenManagement?.(courseId);
      }}
      title="Çift tıkla: ders yönetim ekranı"
    >
      {/* Seçim göstergesi (sol panel checkbox sütunuyla simetri) */}
      <div className="text-center">
        {selected ? (
          <CheckSquare className="h-3.5 w-3.5 text-primary mx-auto" />
        ) : (
          <Square className="h-3.5 w-3.5 text-muted-foreground/50 mx-auto" />
        )}
      </div>

      <span className="font-mono text-slate-800 font-medium truncate" title={courseCode}>
        {courseCode}
      </span>

      <span className="text-gray-800 truncate" title={courseName}>
        {courseName}
      </span>

      {/* Z/S — bölüm müfredatından */}
      <span
        className={cn(
          'text-center font-semibold',
          firstSection?.isCompulsory ? 'text-red-600' : 'text-emerald-600',
        )}
        title={firstSection?.isCompulsory ? 'Zorunlu' : 'Seçmeli'}
      >
        {firstSection?.isCompulsory ? 'Z' : 'S'}
      </span>

      {/* Krd */}
      <span className="text-center tabular-nums text-gray-700">{firstSection?.credits ?? '—'}</span>

      {/* AKTS */}
      <span className="text-center tabular-nums text-gray-700">{firstSection?.ects ?? '—'}</span>

      {/* Kontenjan */}
      <span className="text-center tabular-nums text-gray-700">{totCap}</span>
    </div>
  );
}
