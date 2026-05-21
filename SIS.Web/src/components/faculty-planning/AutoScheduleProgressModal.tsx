import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Loader2, CheckCircle2, AlertCircle, AlertTriangle, XCircle, X,
  Database, Sparkles, Trophy, Clock, Building2, BookOpen, Users,
} from 'lucide-react';
import { useSignalR } from '@/hooks/useSignalR';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { cn } from '@/lib/utils';
import type { AutoSchedulePreview } from '../../types/facultyPlanning';
import type { HubConnection } from '@microsoft/signalr';

/**
 * Failure suggestionType → Türkçe etiket. Backend `SuggestionType` enum'unun string'i; UI çevirisi.
 * Backend bir tür yollamazsa veya bilinmeyen tür gelirse ham string fallback olarak gösterilir.
 */
const FAILURE_LABELS: Record<string, string> = {
  CapacityIncrease: 'Kapasite yetersiz',
  ClassroomAdd: 'Uygun derslik yok',
  TimeChange: 'Zaman çakışması',
  InstructorReassign: 'Hoca çakışması',
  ConstraintRelax: 'Kısıt ihlali',
};

const SEARCH_STRATEGY_LABELS: Record<string, string> = {
  auto: 'Otomatik',
  fixed: 'Sabit',
  portfolio: 'Portföy',
};

export type ProgressStage = 'idle' | 'started' | 'v2-building-request' | 'v2-solving' | 'complete' | 'failed';

interface ScheduleProgressEvent {
  stage: ProgressStage;
  percent: number;
  current: number;
  total: number;
  detail?: string | null;
}

interface AutoScheduleProgressModalProps {
  open: boolean;
  /**
   * Aktif olarak bir algoritma çalışıyor mu. Modal mod ayrımı:
   *   - isPending=true  → progress UI (stage indicator + animated bar)
   *   - isPending=false → previewData varsa ResultPanel; yoksa modal görünmez
   * Bu prop olmadan, geçmiş run'lı bir oturumda "Otomatik Programla"ya basınca
   * modal direkt eski sonucu gösterir — yeni algoritmanın progress'i gizlenirdi.
   */
  isPending?: boolean;
  /** Loading state'inde "İptal Et" butonu. Algoritma çalışırken backend Hub.Cancel() tetiklenir. */
  onCancel?: () => void;
  /**
   * Algoritma tamamlandıktan sonra modal "sonuç paneli" moduna geçer (eski ScheduleResultModal birleştirildi).
   * Bu prop set'liyse ve <c>isPending=false</c> ise "Yerleştirilen / Yerleşemeyen" özeti + aksiyon butonları gösterilir.
   */
  previewData?: AutoSchedulePreview | null;
  /** Sonuç panelinde "Tümünü Uygula" butonu. previewData mevcutsa gerekli. */
  onApplyAll?: () => void;
  /** Apply mutation pending — buton disable + "Uygulanıyor..." metni. */
  applyPending?: boolean;
  /** Sağ üstteki X butonu — sonuç panelini kapatır (loading'de X gösterilmez, iptal akışı ayrı). */
  onDismiss?: () => void;
}

interface StageMeta {
  label: string;
  icon: typeof Database;
  bg: string;
  text: string;
  ring: string;
}

// v2-ortools 3 aşama: backend AutoScheduleEngine sırasıyla yayınlar.
//   1) v2-building-request — section/derslik/zaman dilimleri yükleniyor (0-10%)
//   2) v2-solving           — kısıtlar değerlendiriliyor, program aranıyor (10-90%)
//   3) complete             — sonuç hazır (100%)
const STAGE_ORDER: ProgressStage[] = ['v2-building-request', 'v2-solving', 'complete'];

const STAGE_META: Record<ProgressStage, StageMeta> = {
  idle:                  { label: 'Hazırlanıyor',   icon: Loader2,     bg: 'bg-slate-100',   text: 'text-slate-400', ring: 'ring-slate-200' },
  started:               { label: 'Başlatılıyor',   icon: Loader2,     bg: 'bg-slate-100',   text: 'text-slate-400', ring: 'ring-slate-200' },
  'v2-building-request': { label: 'Veri Hazırlığı', icon: Database,    bg: 'bg-blue-500',    text: 'text-white',     ring: 'ring-blue-300' },
  'v2-solving':          { label: 'Program Çözümü', icon: Sparkles,    bg: 'bg-violet-500',  text: 'text-white',     ring: 'ring-violet-300' },
  complete:              { label: 'Tamamlandı',     icon: Trophy,      bg: 'bg-emerald-500', text: 'text-white',     ring: 'ring-emerald-300' },
  failed:                { label: 'Hata',           icon: AlertCircle, bg: 'bg-red-500',     text: 'text-white',     ring: 'ring-red-300' },
};

// localStorage history — son 5 run'ın elapsed süresi (saniye)
const RUN_HISTORY_KEY = 'sis_auto_schedule_run_history';
const HISTORY_LIMIT = 5;

function loadRunHistory(): number[] {
  try {
    const raw = localStorage.getItem(RUN_HISTORY_KEY);
    if (!raw) return [];
    const arr = JSON.parse(raw);
    return Array.isArray(arr) ? arr.filter((n) => typeof n === 'number' && n > 0) : [];
  } catch {
    return [];
  }
}

function saveRunDuration(durationSec: number) {
  if (durationSec <= 0) return;
  const history = loadRunHistory();
  history.push(durationSec);
  while (history.length > HISTORY_LIMIT) history.shift();
  try { localStorage.setItem(RUN_HISTORY_KEY, JSON.stringify(history)); } catch { /* quota fall-through */ }
}

/** Son run'ların ortalaması; history yoksa null — "tahmin yok" anlamı taşır.
 *  Sabit fallback (örn. 45s) projeden projeye anlamsız olur, kaldırıldı. */
function averageRunDuration(): number | null {
  const h = loadRunHistory();
  if (h.length === 0) return null;
  return h.reduce((a, b) => a + b, 0) / h.length;
}

function fmtSec(sec: number): string {
  if (sec < 0 || !isFinite(sec)) return '–';
  const s = Math.round(sec);
  if (s < 60) return `${s}s`;
  return `${Math.floor(s / 60)}d ${s % 60}s`;
}

export function AutoScheduleProgressModal({
  open, isPending, onCancel,
  previewData, onApplyAll, applyPending, onDismiss,
}: AutoScheduleProgressModalProps) {
  // Backend'in dediği "target" yüzde — atlamalı (10→35→40→65→70→90→100)
  const [targetProgress, setTargetProgress] = useState<ScheduleProgressEvent>({
    stage: 'idle', percent: 0, current: 0, total: 0, detail: null,
  });
  // UI'da gösterilen smooth yüzde — target'a doğru yumuşak interpolasyon yapar
  const [displayPercent, setDisplayPercent] = useState(0);
  const [elapsedSec, setElapsedSec] = useState(0);
  const [stageDurations, setStageDurations] = useState<Partial<Record<ProgressStage, number>>>({});
  const [activeConnection, setActiveConnection] = useState<HubConnection | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  // Stage ilk görüldüğünde elapsed kaydet → stage süresini hesaplamak için
  const stageStartElapsedRef = useRef<Partial<Record<ProgressStage, number>>>({});
  const startedAtRef = useRef<number | null>(null);
  const avgDurationRef = useRef<number | null>(null);

  // ── Modal açılınca initial state + history yükle ──
  useEffect(() => {
    if (!open) {
      setTargetProgress({ stage: 'idle', percent: 0, current: 0, total: 0, detail: null });
      setDisplayPercent(0);
      setElapsedSec(0);
      setStageDurations({});
      setActiveConnection(null);
      setConfirmCancel(false);
      setCancelling(false);
      stageStartElapsedRef.current = {};
      startedAtRef.current = null;
      return;
    }
    avgDurationRef.current = averageRunDuration();
    startedAtRef.current = Date.now();

    const interval = setInterval(() => {
      if (startedAtRef.current)
        setElapsedSec((Date.now() - startedAtRef.current) / 1000);
    }, 250);
    return () => clearInterval(interval);
  }, [open]);

  // ── SignalR connection ──
  const { status: connectionStatus } = useSignalR({
    hubUrl: '/hubs/schedule-progress',
    enabled: open,
    onConnect: (conn) => {
      setActiveConnection(conn);
      conn.on('ScheduleProgress', (evt: ScheduleProgressEvent) => {
        setTargetProgress(prev => {
          // Stage değişimi: önceki stage'in süresini kaydet
          if (prev.stage !== evt.stage && prev.stage !== 'idle' && prev.stage !== 'started') {
            const startedAt = stageStartElapsedRef.current[prev.stage];
            const nowElapsed = startedAtRef.current ? (Date.now() - startedAtRef.current) / 1000 : 0;
            if (typeof startedAt === 'number') {
              const dur = nowElapsed - startedAt;
              if (dur > 0) setStageDurations(d => ({ ...d, [prev.stage]: dur }));
            }
          }
          // Yeni stage başladıysa, başlangıç elapsed'i kaydet
          if (!stageStartElapsedRef.current[evt.stage]) {
            const nowElapsed = startedAtRef.current ? (Date.now() - startedAtRef.current) / 1000 : 0;
            stageStartElapsedRef.current[evt.stage] = nowElapsed;
          }
          return evt;
        });

        if (evt.stage === 'complete' && startedAtRef.current) {
          const dur = (Date.now() - startedAtRef.current) / 1000;
          saveRunDuration(dur);
        }
      });
    },
  });

  // ── Smooth progress interpolation ──
  // Stage içinde backend sessiz iken UI'nin hareketsiz kalmaması için
  // beklenen stage süresine göre yumuşak ilerleme. Backend yeni event yayınlayınca
  // displayPercent target'a (gerekirse zıplayarak) eşitlenir, sonra rahat akar.
  useEffect(() => {
    if (!open) return;
    let raf = 0;
    let lastTs = performance.now();

    const tick = (ts: number) => {
      const dtMs = ts - lastTs;
      lastTs = ts;
      setDisplayPercent(prev => {
        const target = targetProgress.percent;
        if (target <= prev) return prev; // backend geri sayım yapmaz; aşağı interpolate etmiyoruz
        const gap = target - prev;
        // Hedefe yumuşak yaklaş. Backend artık v2-solving aşamasında 50→90 arası
        // sürekli event yolladığı için interpolasyon hızını yükselttik (eski %6/sn →
        // %10/sn). Yavaş hızda UI backend'in arkasında sürünüyordu.
        const easing = Math.min(gap, gap * 0.006 * dtMs); // ~10%/sn yaklaşım
        return Math.min(target, prev + easing);
      });
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [open, targetProgress.percent]);

  // ── Tahmini kalan süre ──
  // Sadece localStorage history varsa tahmin gösterilir. İlk kullanımda
  // anlamsız bir sabit (örn. "45s") göstermek yerine sayacın boş kalmasını tercih ediyoruz.
  const estimatedRemaining = useMemo(() => {
    if (targetProgress.stage === 'complete' || targetProgress.stage === 'failed') return null;
    const avg = avgDurationRef.current;
    if (avg == null) return null; // history yoksa tahmin yok
    const fromPercent = displayPercent > 0 ? avg * (100 - displayPercent) / 100 : avg;
    const fromElapsed = Math.max(0, avg - elapsedSec);
    return Math.max(fromPercent, fromElapsed);
  }, [displayPercent, elapsedSec, targetProgress.stage]);

  // ── Aksiyonlar ──
  const handleCancelClick = () => {
    // Eğer hızlı bitti veya başlamadı, direkt kapat
    if (targetProgress.stage === 'complete' || targetProgress.stage === 'failed') {
      onCancel?.();
      return;
    }
    setConfirmCancel(true);
  };

  const handleConfirmCancel = async () => {
    if (!activeConnection || cancelling) return;
    setCancelling(true);
    try {
      await activeConnection.invoke<boolean>('Cancel');
    } finally {
      setConfirmCancel(false);
      onCancel?.();
    }
  };

  // ── ESC tuşu ile cancel diyalog'unu aç ──
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !confirmCancel) {
        e.preventDefault();
        handleCancelClick();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, confirmCancel, targetProgress.stage]);

  if (!open) return null;

  const currentStageIdx = targetProgress.stage === 'idle' || targetProgress.stage === 'started'
    ? -1
    : STAGE_ORDER.indexOf(targetProgress.stage);
  const isFailed = targetProgress.stage === 'failed';
  const isComplete = targetProgress.stage === 'complete';

  const headerMeta = STAGE_META[targetProgress.stage] ?? STAGE_META.idle;
  const HeaderIcon = headerMeta.icon;
  const isLoading = !isComplete && !isFailed;

  // Mod ayrımı: aktif mutation varsa (isPending) progress UI; mutation yokken previewData varsa
  // ResultPanel. Bu sıra önemli — geçmiş run'lı bir oturumda "Otomatik Programla" basıldığında
  // previewData (önceki run) hâlâ duruyor; isPending kontrolü öncelikli olmazsa modal direkt eski
  // sonucu gösterir, kullanıcı yeni algoritmanın progress'ini hiç görmez.
  if (!isPending && previewData) {
    return (
      <ResultPanel
        previewData={previewData}
        elapsedSec={elapsedSec}
        onApplyAll={onApplyAll}
        applyPending={applyPending ?? false}
        onDismiss={onDismiss}
      />
    );
  }

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center"
        aria-modal="true"
        role="dialog"
        aria-labelledby="auto-schedule-progress-title"
      >
        <div className="w-[480px] max-w-[95vw] rounded-2xl bg-white shadow-2xl border border-slate-200 overflow-hidden">
          {/* Header */}
          <div className="px-6 py-4 border-b border-slate-100">
            <div className="flex items-center gap-3">
              <div className={`w-9 h-9 rounded-full flex items-center justify-center ${headerMeta.bg} ${headerMeta.text}`}>
                <HeaderIcon className={`h-5 w-5 ${isLoading ? 'animate-spin' : ''}`} strokeWidth={2.2} />
              </div>
              <div className="flex-1 min-w-0">
                <h3 id="auto-schedule-progress-title" className="font-semibold text-base text-slate-800 leading-tight">
                  Otomatik Programlama
                </h3>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  {headerMeta.label}
                  {connectionStatus !== 'connected' && connectionStatus !== 'idle' && (
                    <span className="ml-1.5 italic">• {
                      connectionStatus === 'connecting' ? 'bağlanıyor' :
                      connectionStatus === 'reconnecting' ? 'yeniden bağlanıyor' :
                      connectionStatus === 'failed' ? 'bağlantı yok' : 'bağlı değil'
                    }</span>
                  )}
                </p>
              </div>
              <div className="text-right text-xs font-mono tabular-nums whitespace-nowrap">
                <span className="font-medium text-slate-700">{fmtSec(elapsedSec)}</span>
                {!isComplete && !isFailed && estimatedRemaining != null && estimatedRemaining > 0 && (
                  <span className="text-slate-400"> · ~{fmtSec(estimatedRemaining)} kaldı</span>
                )}
              </div>
            </div>
          </div>

          {/* Progress bar — header altyazısı zaten stage label'ını gösteriyor; üstteki çift etiket kaldırıldı */}
          <div className="px-6 pt-5 pb-2">
            <div className="flex items-center justify-end mb-1.5">
              <span className="text-sm font-mono font-semibold text-slate-700 tabular-nums">
                %{Math.round(displayPercent)}
              </span>
            </div>
            <div className="h-2 bg-slate-100 rounded-full overflow-hidden relative">
              <div
                className={`h-full ${isFailed ? 'bg-red-500' : isComplete ? 'bg-emerald-500' : 'bg-gradient-to-r from-blue-500 via-violet-500 to-amber-500'}`}
                style={{
                  width: `${displayPercent}%`,
                  // CSS transition burada requestAnimationFrame ile çakışıyordu: her RAF tick'i
                  // yeni transition başlatıyor, bar hep 320ms gerideki target'a tween yapıyordu.
                  // RAF zaten frame-by-frame smooth artırıyor → transition yalnız renk değişimi için.
                  transition: 'background-color 200ms',
                }}
              />
              {isLoading && displayPercent < 100 && (
                <div
                  className="absolute top-0 h-full w-12 bg-white/40 blur-sm animate-pulse"
                  style={{ left: `calc(${displayPercent}% - 24px)` }}
                />
              )}
            </div>
          </div>

          {/* Detail */}
          {/* Eskiden burada "{current} / {total}" satırı vardı (V1 sequential mirası).
              V2 CP-SAT bütünü tek model olarak çözer; "X/Y processed" anlamını yitirdi.
              Backend payload'unda current/total alanları hâlâ var (protocol unchanged)
              ama UI'da render etmiyoruz — detail metni zaten "N ders için..." diye anlamlı. */}
          <div className="px-6 pt-3 pb-4 min-h-[64px]">
            {targetProgress.detail ? (
              <p className="text-sm text-slate-700 leading-snug">{targetProgress.detail}</p>
            ) : (
              <SkeletonLine />
            )}
          </div>

          {/* Stage indicator (3 step) — renkli + ikonlu + tamamlanma süresiyle.
              STAGE_ORDER 3 elemanlı; eski grid-cols-4 sağda boş 4. kolon bırakıyordu (sola yaslı).
              grid-cols-3 + justify-items-center yatayda simetrik dağıtır. */}
          <div className="px-6 pb-4">
            <div className="grid grid-cols-3 gap-2 justify-items-center">
              {STAGE_ORDER.map((stage, idx) => {
                const meta = STAGE_META[stage];
                const StageIcon = meta.icon;
                const isPast = idx < currentStageIdx || isComplete;
                const isCurrent = idx === currentStageIdx && !isComplete;
                const dur = stageDurations[stage];

                return (
                  <div key={stage} className="flex flex-col items-center gap-1">
                    <div
                      className={`w-8 h-8 rounded-full flex items-center justify-center transition-all ${
                        isPast
                          ? 'bg-emerald-500 text-white'
                          : isCurrent
                            ? `${meta.bg} ${meta.text} ring-4 ${meta.ring} animate-pulse`
                            : 'bg-slate-100 text-slate-300'
                      }`}
                    >
                      {isPast
                        ? <CheckCircle2 className="h-4 w-4" />
                        : <StageIcon className={`h-4 w-4 ${isCurrent && stage !== 'complete' ? 'animate-spin' : ''}`} />}
                    </div>
                    <span className={`text-[10px] uppercase tracking-wider text-center leading-tight ${
                      isPast || isCurrent ? 'text-slate-700 font-medium' : 'text-slate-300'
                    }`}>
                      {meta.label}
                    </span>
                    {isPast && dur && (
                      <span className="text-[9px] text-emerald-600 font-mono tabular-nums">
                        {fmtSec(dur)}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {/* Cancel butonu */}
          {!isComplete && !isFailed && (
            <div className="px-6 pb-4 flex justify-end border-t border-slate-100 pt-3">
              <button
                type="button"
                onClick={handleCancelClick}
                disabled={cancelling || connectionStatus !== 'connected'}
                className="px-3 py-1.5 text-xs font-medium text-slate-600 hover:text-red-600 hover:bg-red-50 rounded-md transition-colors disabled:opacity-50 disabled:cursor-not-allowed border border-slate-200"
              >
                {cancelling ? 'İptal ediliyor…' : 'İptal Et'}
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Cancel confirmation */}
      <AlertDialog open={confirmCancel} onOpenChange={setConfirmCancel}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>İşlemi iptal et?</AlertDialogTitle>
            <AlertDialogDescription>
              Algoritma %{Math.round(displayPercent)} ilerledi.
              {' '}
              {Math.round(elapsedSec)} saniyelik hesaplama kaybedilecek.
              <br />
              Devam edersen mevcut atamalar değişmeden kalır.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Devam Et</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmCancel}
              className="bg-red-600 hover:bg-red-700"
            >
              Evet, İptal Et
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

// Skeleton placeholder — bağlantı kurulurken veya backend henüz event yollamadıysa
function SkeletonLine() {
  return (
    <div className="space-y-1.5">
      <div className="h-3 bg-slate-100 rounded animate-pulse w-3/4" />
      <div className="h-2 bg-slate-100 rounded animate-pulse w-1/2" />
    </div>
  );
}

/**
 * Sonuç paneli — algoritma tamamlandığında progress UI yerine bu komponent render edilir.
 * Eski ScheduleResultModal'dan birleştirilmiş; tüm sayılar backend payload'undan gelir
 * (hardcoded yok). Frontend yalnız görsel gruplama yapar (departmana göre sayım, failure tipine
 * göre kümeleme) — kaynak veri backend'in <c>AutoSchedulePreviewDto</c> yapısıdır.
 */
function ResultPanel({
  previewData, elapsedSec, onApplyAll, applyPending, onDismiss,
}: {
  previewData: AutoSchedulePreview;
  elapsedSec: number;
  onApplyAll?: () => void;
  applyPending: boolean;
  onDismiss?: () => void;
}) {
  const { assignments, failures, stats } = previewData;
  const sectionTotal = assignments.length + failures.length;
  const pct = sectionTotal > 0 ? Math.round((assignments.length / sectionTotal) * 100) : 0;

  // Departmana göre başarılı şube dağılımı — backend her assignment için departmentCode yolluyor.
  const successByDept = useMemo(() => {
    const map = new Map<string, number>();
    for (const a of assignments) {
      const dept = a.departmentCode || '—';
      map.set(dept, (map.get(dept) ?? 0) + 1);
    }
    return Array.from(map.entries()).sort((a, b) => b[1] - a[1]);
  }, [assignments]);

  // Failure'ları suggestionType'a göre grupla (Türkçe etiketleme FAILURE_LABELS üzerinden).
  const failuresByReason = useMemo(() => {
    const map = new Map<string, typeof failures>();
    for (const f of failures) {
      const type = f.suggestions?.[0]?.suggestionType ?? 'Unknown';
      const arr = map.get(type) ?? [];
      arr.push(f);
      map.set(type, arr);
    }
    return Array.from(map.entries());
  }, [failures]);

  // Tonlama: yüksek başarı yeşil, orta amber, düşük kırmızı.
  const toneClass =
    pct >= 80 ? 'bg-emerald-500 text-white'
    : pct >= 50 ? 'bg-amber-500 text-white'
    : 'bg-red-500 text-white';
  const toneTextClass =
    pct >= 80 ? 'text-emerald-600'
    : pct >= 50 ? 'text-amber-600'
    : 'text-red-600';
  const toneBarClass =
    pct >= 80 ? 'bg-emerald-500'
    : pct >= 50 ? 'bg-amber-500'
    : 'bg-red-500';

  return (
    <div
      className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center"
      aria-modal="true"
      role="dialog"
      aria-labelledby="auto-schedule-result-title"
    >
      <div className="w-[560px] max-w-[95vw] rounded-2xl bg-white shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header */}
        <div className="px-6 pt-5 pb-4 relative">
          {onDismiss && (
            <button
              type="button"
              onClick={onDismiss}
              aria-label="Kapat"
              className="absolute top-3 right-3 p-1 rounded-md text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition-colors"
            >
              <X className="h-4 w-4" />
            </button>
          )}
          <div className="flex items-center gap-3 mb-4">
            <div className={cn('w-10 h-10 rounded-full flex items-center justify-center', toneClass)}>
              {pct >= 80 ? <CheckCircle2 className="h-5 w-5" />
                : pct >= 50 ? <AlertTriangle className="h-5 w-5" />
                : <XCircle className="h-5 w-5" />}
            </div>
            <div className="min-w-0">
              <h2 id="auto-schedule-result-title" className="text-lg font-semibold text-slate-800 leading-tight">
                Programlama Tamamlandı
              </h2>
              <p className="text-sm text-slate-500 mt-0.5">
                {assignments.length} / {sectionTotal} şube yerleştirildi
                {stats.distinctCourseCount > 0 && (
                  <span className="text-slate-400"> · {stats.distinctCourseCount} ders kapsanıyor</span>
                )}
              </p>
            </div>
          </div>

          {/* Progress bar */}
          <div className="relative h-3 bg-slate-100 rounded-full overflow-hidden mb-1.5">
            <div
              className={cn('h-full rounded-full transition-all duration-700', toneBarClass)}
              style={{ width: `${pct}%` }}
            />
          </div>
          <div className="flex items-center justify-between">
            <span className={cn('text-2xl font-bold tabular-nums', toneTextClass)}>%{pct}</span>
            <div className="flex items-center gap-3 text-xs text-slate-500 tabular-nums">
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" />
                {(stats.elapsedSeconds || elapsedSec).toFixed(1)}s
              </span>
              <span className="flex items-center gap-1">
                <Building2 className="h-3 w-3" />
                {stats.usedClassroomCount} derslik
              </span>
              <span className="flex items-center gap-1">
                <BookOpen className="h-3 w-3" />
                {stats.distinctCourseCount} ders
              </span>
            </div>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge variant="outline" className="text-[10px]">
              Motor: {stats.engine || 'v2-ortools'}
            </Badge>
            <Badge variant="outline" className="text-[10px]">
              Arama: {SEARCH_STRATEGY_LABELS[stats.searchStrategy] ?? stats.searchStrategy ?? 'auto'}
            </Badge>
            <Badge variant="outline" className="text-[10px]">
              UNSAT core: {stats.unsatCoreEnabled ? 'Açık' : 'Kapalı'}
            </Badge>
          </div>
        </div>

        {/* İçerik — iki sütun */}
        <div className="border-t border-slate-100">
          <ScrollArea className="max-h-[320px]">
            <div className="grid grid-cols-2 divide-x divide-slate-100">
              {/* Sol: Başarılı şubeler — departmana göre dağılım */}
              <div className="p-4">
                <div className="flex items-center gap-2 mb-3">
                  <CheckCircle2 className="h-4 w-4 text-emerald-500" />
                  <span className="text-sm font-semibold text-slate-700">Yerleştirilen</span>
                  <Badge className="text-[10px] bg-emerald-50 text-emerald-700 border border-emerald-200 hover:bg-emerald-50">
                    {assignments.length} şube
                  </Badge>
                </div>
                {successByDept.length === 0 ? (
                  <p className="text-xs text-slate-400 italic">Henüz şube yerleşmedi.</p>
                ) : (
                  <div className="space-y-2">
                    {successByDept.map(([dept, count]) => (
                      <div key={dept} className="flex items-center justify-between">
                        <span className="text-xs text-slate-600 flex items-center gap-1">
                          <Users className="h-3 w-3 text-slate-400" />
                          {dept}
                        </span>
                        <div className="flex items-center gap-2">
                          <div className="h-1.5 w-16 bg-slate-100 rounded-full overflow-hidden">
                            <div
                              className="h-full bg-emerald-400 rounded-full"
                              style={{ width: `${(count / assignments.length) * 100}%` }}
                            />
                          </div>
                          <span className="text-xs font-medium text-slate-700 tabular-nums w-6 text-right">{count}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Sağ: Başarısız şubeler — sebebe göre kümeleme */}
              <div className="p-4">
                <div className="flex items-center gap-2 mb-3">
                  <XCircle className="h-4 w-4 text-red-400" />
                  <span className="text-sm font-semibold text-slate-700">Yerleşemeyen</span>
                  {failures.length > 0 && (
                    <Badge className="text-[10px] bg-red-50 text-red-600 border border-red-200 hover:bg-red-50">
                      {failures.length} şube
                    </Badge>
                  )}
                </div>
                {failures.length === 0 ? (
                  <p className="text-xs text-slate-400 italic">Tüm şubeler başarıyla yerleştirildi.</p>
                ) : (
                  <div className="space-y-3">
                    {failuresByReason.map(([type, items]) => (
                      <div key={type}>
                        <div className="flex items-center gap-1.5 mb-1">
                          <AlertTriangle className="h-3 w-3 text-amber-500" />
                          <span className="text-[11px] font-medium text-slate-600">
                            {FAILURE_LABELS[type] ?? type}
                          </span>
                          <span className="text-[10px] text-slate-400">({items.length})</span>
                        </div>
                        <div className="flex flex-wrap gap-1 ml-4">
                          {items.map(f => (
                            <span
                              key={f.sectionId}
                              className="px-1.5 py-0.5 rounded text-[10px] bg-red-50 text-red-600 border border-red-200"
                              title={`${f.courseCode}·${f.sectionNumber} — ${f.reason}`}
                            >
                              {f.courseCode}·{f.sectionNumber}
                            </span>
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </ScrollArea>
        </div>

        {/* Footer — tek ana eylem: Tümünü Uygula. Kapama X butonu sağ üstte. */}
        <div className="flex items-center justify-end gap-2 px-6 py-4 border-t border-slate-100">
          {onApplyAll && (
            <Button
              onClick={onApplyAll}
              disabled={applyPending || assignments.length === 0}
              className="bg-[#003366] hover:bg-[#002244]"
            >
              {applyPending ? 'Uygulanıyor...' : `Tümünü Uygula (${assignments.length})`}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
