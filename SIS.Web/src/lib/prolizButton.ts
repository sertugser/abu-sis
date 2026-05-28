import { cn } from './utils';

/**
 * Klasik kurumsal buton stili (Proliz benzeri): açık gri gradient + ince koyu border +
 * hafif inset highlight + soldan küçük ikon. Modern flat shadcn yerine.
 */
export const prolizButtonClass = cn(
  'inline-flex items-center gap-1.5 h-7 px-2.5 text-[11px] font-medium text-slate-700',
  'bg-gradient-to-b from-slate-50 to-slate-200',
  'border border-slate-400/70',
  'rounded shadow-[inset_0_1px_0_rgba(255,255,255,0.6)]',
  'hover:from-slate-100 hover:to-slate-300 hover:border-slate-500/80',
  'active:from-slate-200 active:to-slate-300 active:shadow-[inset_0_2px_3px_rgba(0,0,0,0.08)]',
  'disabled:opacity-50 disabled:cursor-not-allowed disabled:from-slate-100 disabled:to-slate-100',
  'transition-all duration-100',
);

/** Vurgulu/onaylama versiyonu (örn. Kaydı Kesinleştir) — Proliz mavi/yeşil gradient */
export const prolizPrimaryButtonClass = cn(
  'inline-flex items-center gap-1.5 h-7 px-3 text-[11px] font-semibold text-white',
  'bg-gradient-to-b from-blue-500 to-blue-700',
  'border border-blue-800/70',
  'rounded shadow-[inset_0_1px_0_rgba(255,255,255,0.3)]',
  'hover:from-blue-600 hover:to-blue-800',
  'active:from-blue-700 active:to-blue-800',
  'disabled:opacity-50 disabled:cursor-not-allowed',
  'transition-all duration-100',
);

/** Tehlike/uyarı versiyonu (örn. Çıkar) — kırmızı tonlu */
export const prolizDangerButtonClass = cn(
  'inline-flex items-center justify-center h-6 w-6 text-rose-600',
  'bg-gradient-to-b from-rose-50 to-rose-100',
  'border border-rose-300',
  'rounded',
  'hover:from-rose-100 hover:to-rose-200',
  'disabled:opacity-50 disabled:cursor-not-allowed',
  'transition-all duration-100',
);
