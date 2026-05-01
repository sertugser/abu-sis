import { cn } from '@/lib/utils';
import { Boxes, ChevronRight } from 'lucide-react';
import { getSubtypeColorByCode } from '@/lib/electiveSubtypeUtils';
import type { PoolSlotForOpeningDto } from '../../types/courseOpening';

interface Props {
  slot: PoolSlotForOpeningDto;
  onOpen: (slot: PoolSlotForOpeningDto) => void;
}

/**
 * Müfredattaki bir havuz slot'unu (TE-1 / NDE-2 vs.) tek bir tablo satırı olarak render eder.
 * Tıklanınca PoolCandidatesDialog açılır — ders açma akışı modal içinde olur.
 * Subtype'a göre renkli badge — TE/NDE/OC için 3 default, custom için gri fallback.
 */
export function PoolSlotRow({ slot, onOpen }: Props) {
  const classYear =
    slot.suggestedSemesterStart > 0 ? Math.ceil(slot.suggestedSemesterStart / 2) : null;
  const subtypeColor = getSubtypeColorByCode(slot.subtypeCode);
  const allOpened = slot.candidateCount > 0 && slot.openedCandidateCount === slot.candidateCount;

  return (
    <div
      className={cn(
        'grid grid-cols-[28px_100px_1fr_32px_48px_52px_40px_80px_32px] items-center gap-2 px-3 py-2 border-b border-gray-100 text-sm cursor-pointer transition-colors',
        'bg-violet-50/40 hover:bg-violet-100/60',
      )}
      onClick={() => onOpen(slot)}
      title="Havuza tıkla — aday dersleri görüntüle ve aç"
    >
      {/* Checkbox kolonu boş — havuz satırı seçilemez */}
      <div className="flex items-center justify-center">
        <Boxes className="h-4 w-4 text-violet-600" />
      </div>

      {/* Slot adı — kod kolonunda */}
      <span className="font-mono text-violet-800 font-semibold truncate" title={slot.slotName}>
        {slot.slotName || '—'}
      </span>

      {/* Havuz adı + subtype badge */}
      <div className="flex items-center gap-2 min-w-0">
        <span className="text-gray-800 truncate" title={slot.electiveGroupName}>
          {slot.electiveGroupName}
        </span>
        {slot.subtypeCode && (
          <span
            className={cn(
              'px-1.5 py-0 rounded border text-[10px] font-semibold whitespace-nowrap shrink-0',
              subtypeColor,
            )}
            title={slot.subtypeName}
          >
            {slot.subtypeCode}
          </span>
        )}
      </div>

      {/* Z/S — havuzlar her zaman seçmeli */}
      <span className="text-center font-semibold text-emerald-600">S</span>

      {/* KRD — havuz seviyesinde tutulmaz */}
      <span className="text-center text-gray-400">—</span>

      {/* AKTS override (varsa) */}
      <span className="text-center tabular-nums text-gray-700 font-medium">
        {slot.ectsPerCourse ?? '—'}
      </span>

      {/* Sınıf */}
      <span className="text-center tabular-nums text-gray-600">{classYear ?? '—'}</span>

      {/* Aday/Açılmış sayım */}
      <span
        className={cn(
          'text-center text-xs tabular-nums',
          allOpened ? 'text-emerald-700 font-medium' : 'text-violet-700',
        )}
        title={`${slot.openedCandidateCount}/${slot.candidateCount} aday açılmış`}
      >
        {slot.openedCandidateCount}/{slot.candidateCount}
      </span>

      {/* Modal aç ikonu */}
      <div className="flex justify-center">
        <ChevronRight className="h-4 w-4 text-violet-500" />
      </div>
    </div>
  );
}
