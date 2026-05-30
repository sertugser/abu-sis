import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { dayNumberToTurkish } from '@/lib/scheduleUtils';
import {
  blockHeight,
  collectBoundaries,
  computeLanes,
  computeMinuteRange,
  minutesToLabel,
  minutesToOffset,
  rangeHeight,
  timeToMinutes,
  timeToOffset,
  type MinuteRange,
} from '@/lib/timetableGrid';
import { colorForCourse, LAB_STRIPE_CLASS } from '@/lib/timetableColors';

// Hem öğrenci hem öğretmen tarafının kullandığı nötr blok/section modeli.
// (StudentTimetableSection ve InstructorSection bu şekle yapısal olarak uyar.)
export interface WeekTimetableBlock {
  /** .NET DayOfWeek: 0=Paz, 1=Pzt … 6=Cmt */
  day: number;
  startTime: string; // "HH:mm" veya "HH:mm:ss"
  endTime: string;
  blockType: 'Theory' | 'Lab';
  classroomName: string | null;
}

export interface WeekTimetableSection {
  sectionId: number;
  sectionNumber: string;
  courseCode: string;
  courseName: string;
  /** Meta satırında derslikten sonra gösterilir. Gizlemek için boş string geç. */
  instructorDisplay: string;
  blocks: WeekTimetableBlock[];
}

export interface WeekTimetableSlot {
  start: string; // "HH:mm"
  end: string;
}

interface WeekTimetableGridProps {
  sections: WeekTimetableSection[];
  /** Term'in tanımlı ders saati pencereleri — ızgaranın satır yapısı (saat çizgileri + görünür
   *  aralık) bunlardan üretilir. Boşsa derslerden türetilir. Hardcoded saat yok. */
  slots?: WeekTimetableSlot[];
  /** Sistem ayarından gelen öğle arası penceresi ("HH:mm"). Verilirse bant olarak çizilir. */
  lunchBreakStart?: string;
  lunchBreakEnd?: string;
}

interface LunchBand {
  top: number;
  height: number;
  midTop: number;
  label: string;
}

interface GridBlock extends WeekTimetableBlock {
  section: WeekTimetableSection;
}

// Izgaranın üst/altına eklenen nefes payı (px). İlk/son saat etiketi kırpılmasın.
const PAD = 12;
// İki saat etiketi arası en az bu kadar px varsa ikincisi de yazılır (üst üste binmesin).
const MIN_LABEL_PX = 14;
// Sütun sırası: Pzt–Cuma her zaman; Cmt (6) ve Paz (0) yalnızca o güne ders varsa.
const DAY_ORDER = [1, 2, 3, 4, 5, 6, 0];

function classroomLabel(block: WeekTimetableBlock): string {
  return block.classroomName ?? 'Çevrimiçi';
}

function hhmm(time: string): string {
  return time.length >= 5 ? time.slice(0, 5) : time;
}

export function WeekTimetableGrid({
  sections,
  slots,
  lunchBreakStart,
  lunchBreakEnd,
}: WeekTimetableGridProps) {
  // Her ders saati AYRI blok olarak gösterilir (birleştirme yok).
  const all: GridBlock[] = [];
  for (const section of sections) {
    for (const block of section.blocks) {
      all.push({ ...block, section });
    }
  }

  const isEmpty = all.length === 0;

  // Görünür aralık: term'in TANIMLI ders saatlerinden (slots) + derslerden + öğle arasından
  // türetilir. Hardcoded saat yok. Slot tanımı yoksa derslerin aralığına düşer.
  const range = ((): MinuteRange => {
    const mins: number[] = [];
    const maxs: number[] = [];
    for (const s of slots ?? []) {
      mins.push(timeToMinutes(s.start));
      maxs.push(timeToMinutes(s.end));
    }
    for (const b of all) {
      mins.push(timeToMinutes(b.startTime));
      maxs.push(timeToMinutes(b.endTime));
    }
    if (lunchBreakStart && lunchBreakEnd) {
      mins.push(timeToMinutes(lunchBreakStart));
      maxs.push(timeToMinutes(lunchBreakEnd));
    }
    if (mins.length === 0) return computeMinuteRange(all);
    return { startMin: Math.min(...mins), endMin: Math.max(...maxs) };
  })();

  // Yatay çizgiler: tanımlı slot kenarları + ders kenarları (blok hizalaması için).
  // İkisinin birleşimi → boş saatlerde de tanımlı slot çizgileri görünür, hardcoded yok.
  const boundaries = ((): number[] => {
    const set = new Set<number>(collectBoundaries(all));
    for (const s of slots ?? []) {
      set.add(timeToMinutes(s.start));
      set.add(timeToMinutes(s.end));
    }
    set.add(range.startMin);
    set.add(range.endMin);
    return [...set].sort((a, b) => a - b);
  })();

  const innerHeight = rangeHeight(range);
  const totalHeight = innerHeight + PAD * 2;

  // Görünür günler: Pzt–Cuma sabit; Cmt/Paz yalnızca o güne ders varsa eklenir.
  const presentDays = new Set(all.map((b) => b.day));
  const visibleDays = DAY_ORDER.filter((d) => (d >= 1 && d <= 5) || presentDays.has(d));
  const dayToCol = new Map(visibleDays.map((d, i) => [d, i] as const));
  const gridStyle = {
    gridTemplateColumns: `64px repeat(${visibleDays.length}, minmax(0, 1fr))`,
  };

  // Öğle arası bandı — yalnızca görünür aralıkla kesişiyorsa çiz (aralığa kırp).
  const lunch = ((): LunchBand | null => {
    if (isEmpty || !lunchBreakStart || !lunchBreakEnd) return null;
    const ls = timeToMinutes(lunchBreakStart);
    const le = timeToMinutes(lunchBreakEnd);
    const start = Math.max(ls, range.startMin);
    const end = Math.min(le, range.endMin);
    if (end <= start) return null;
    const top = PAD + minutesToOffset(start, range);
    const height = minutesToOffset(end, range) - minutesToOffset(start, range);
    return {
      top,
      height,
      midTop: top + height / 2,
      label: `${minutesToLabel(ls)}–${minutesToLabel(le)}`,
    };
  })();

  // Saat etiketleri: çizgiler her sınırda, ama etiketler üst üste binmesin diye seyreltilir.
  const labeledBoundaries = ((): { minutes: number; offset: number; showLabel: boolean }[] => {
    const result: { minutes: number; offset: number; showLabel: boolean }[] = [];
    let lastLabelOffset = Number.NEGATIVE_INFINITY;
    for (const minutes of boundaries) {
      const offset = minutesToOffset(minutes, range);
      const showLabel = offset - lastLabelOffset >= MIN_LABEL_PX;
      if (showLabel) lastLabelOffset = offset;
      result.push({ minutes, offset, showLabel });
    }
    return result;
  })();

  // Görünür günlere göre blokları dağıt.
  const byColumn: GridBlock[][] = visibleDays.map(() => []);
  for (const block of all) {
    const col = dayToCol.get(block.day);
    if (col === undefined) continue;
    byColumn[col].push(block);
  }

  return (
    <div className="flex flex-1 flex-col">
      {isEmpty && (
        <p className="border-b border-border bg-muted/20 px-4 py-2 text-xs text-muted-foreground">
          Bu dönem için kayıtlı ders bulunmuyor.
        </p>
      )}

      {/* Başlık + gövde tek scroll kapsayıcısında → kolonlar daima hizalı */}
      <div className="flex-1 overflow-auto">
        {/* Gün başlıkları (sticky) */}
        <div
          className="sticky top-0 z-20 grid border-b border-border bg-muted/40 backdrop-blur"
          style={gridStyle}
        >
          <div className="px-2 py-2 text-center text-[11px] font-medium text-muted-foreground">
            Saat
          </div>
          {visibleDays.map((day) => (
            <div
              key={day}
              className="border-l border-border px-2 py-2 text-center text-sm font-semibold text-foreground"
            >
              {dayNumberToTurkish(day)}
            </div>
          ))}
        </div>

        {/* Izgara gövdesi */}
        <div className="relative grid" style={gridStyle}>
          {/* Saat kolonu — etiketler gerçek ders saatlerinden */}
          <div className="relative" style={{ height: totalHeight }}>
            {labeledBoundaries.map(
              ({ minutes, offset, showLabel }) =>
                showLabel && (
                  <div
                    key={minutes}
                    className="absolute right-2 -translate-y-1/2 text-[11px] font-medium tabular-nums text-muted-foreground"
                    style={{ top: PAD + offset }}
                  >
                    {minutesToLabel(minutes)}
                  </div>
                ),
            )}
          </div>

          {/* Gün kolonları */}
          {byColumn.map((blocks, col) => {
            const positioned = computeLanes(blocks);
            return (
              <div
                key={visibleDays[col]}
                className="relative border-l border-border"
                style={{ height: totalHeight }}
              >
                {/* Öğle arası bandı — bloklardan ÖNCE (arkada kalsın) */}
                {lunch && (
                  <div
                    className="absolute inset-x-0 border-y border-amber-200/70 bg-amber-100/50"
                    style={{ top: lunch.top, height: lunch.height }}
                    aria-hidden
                  />
                )}

                {/* Yatay çizgiler — her gerçek ders saati sınırında (kutu kenarlarıyla birebir) */}
                {boundaries.map((minutes) => (
                  <div
                    key={minutes}
                    className="absolute inset-x-0 border-t border-border/40"
                    style={{ top: PAD + minutesToOffset(minutes, range) }}
                    aria-hidden
                  />
                ))}

                {/* Ders blokları */}
                {positioned.map((pos) => {
                  const gb = pos.item;
                  const color = colorForCourse(gb.section.courseCode);
                  const isLab = gb.blockType === 'Lab';
                  const isConflict = pos.laneCount > 1; // aynı saatte başka ders var
                  const top = PAD + timeToOffset(gb.startTime, range);
                  const h = blockHeight(gb.startTime, gb.endTime);
                  const widthPct = 100 / pos.laneCount;
                  const leftPct = pos.lane * widthPct;
                  const timeRange = `${hhmm(gb.startTime)}–${hhmm(gb.endTime)}`;
                  const meta = gb.section.instructorDisplay
                    ? `${classroomLabel(gb)} · ${gb.section.instructorDisplay}`
                    : classroomLabel(gb);

                  return (
                    <div
                      key={`${gb.section.sectionId}-${gb.day}-${gb.startTime}`}
                      data-testid={isLab ? 'lab-block' : 'theory-block'}
                      className={cn(
                        'absolute overflow-hidden rounded-md border px-1.5 py-1 leading-tight text-slate-900 shadow-sm',
                        color.base,
                        isConflict && 'ring-2 ring-red-500',
                      )}
                      style={{
                        top,
                        height: h,
                        left: `calc(${leftPct}% + 2px)`,
                        width: `calc(${widthPct}% - 4px)`,
                      }}
                      title={`${timeRange} · ${gb.section.courseCode} (${gb.section.sectionNumber}) ${gb.section.courseName}\n${meta}${isConflict ? '\n⚠ Bu saatte başka ders ile çakışıyor' : ''}`}
                    >
                      {isLab && (
                        <span
                          className={cn(
                            'pointer-events-none absolute inset-0 rounded-md',
                            LAB_STRIPE_CLASS,
                          )}
                          aria-hidden
                        />
                      )}
                      <div className="relative flex min-w-0 flex-col gap-0.5">
                        {/* 1. satır: ders kodu (sol) + saat (sağ) yan yana */}
                        <div className="flex items-baseline justify-between gap-1">
                          <span className="truncate text-[13px] font-bold">
                            {gb.section.courseCode}
                            <span className="font-semibold text-slate-600">
                              ·{gb.section.sectionNumber}
                            </span>
                          </span>
                          <span className="flex shrink-0 items-center gap-1">
                            {isConflict && (
                              <Badge
                                variant="destructive"
                                className="h-4 px-1 text-[9px] font-semibold"
                              >
                                Çakışma
                              </Badge>
                            )}
                            {isLab && (
                              <Badge
                                variant="secondary"
                                className="h-4 px-1 text-[9px] font-semibold"
                              >
                                Lab
                              </Badge>
                            )}
                            <span className="text-[12px] font-semibold tabular-nums">
                              {timeRange}
                            </span>
                          </span>
                        </div>
                        {/* 2. satır: ders adı */}
                        <span className="truncate text-[12px]" title={gb.section.courseName}>
                          {gb.section.courseName}
                        </span>
                        {/* 3. satır: derslik · öğretim elemanı (yan yana) */}
                        {h > 56 && (
                          <span className="truncate text-[11px] text-slate-600" title={meta}>
                            {meta}
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            );
          })}

          {/* Öğle arası etiketi — bandın tam ortasında, gün sütunları boyunca yatay ortalı */}
          {lunch && (
            <div
              className="pointer-events-none absolute right-0 z-10 flex -translate-y-1/2 justify-center"
              style={{ top: lunch.midTop, left: 64 }}
            >
              <span className="text-[13px] font-semibold uppercase tracking-[0.2em] text-amber-700/90">
                Öğle Arası
              </span>
            </div>
          )}
        </div>
      </div>

      {/* Açıklama şeridi */}
      <div className="flex flex-none flex-wrap items-center gap-x-4 gap-y-1 border-t border-border bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
        <span className="flex items-center gap-2">
          <span
            className={cn('inline-block h-3 w-4 rounded-sm border border-border', LAB_STRIPE_CLASS)}
            aria-hidden
          />
          Uygulama (lab) saatleri çizgili gösterilir
        </span>
        {lunch && (
          <span className="flex items-center gap-2">
            <span
              className="inline-block h-3 w-4 rounded-sm border border-amber-200/70 bg-amber-100/50"
              aria-hidden
            />
            Öğle arası: {lunch.label}
          </span>
        )}
      </div>
    </div>
  );
}
