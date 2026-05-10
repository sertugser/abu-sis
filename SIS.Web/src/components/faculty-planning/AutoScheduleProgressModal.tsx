import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Loader2, CheckCircle2, AlertCircle,
  Database, Sparkles, ShieldCheck, Trophy,
} from 'lucide-react';
import { useSignalR } from '@/hooks/useSignalR';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import type { HubConnection } from '@microsoft/signalr';

export type ProgressStage = 'idle' | 'started' | 'construction' | 'sa' | 'lns' | 'complete' | 'failed';

interface ScheduleProgressEvent {
  stage: ProgressStage;
  percent: number;
  current: number;
  total: number;
  detail?: string | null;
}

interface AutoScheduleProgressModalProps {
  open: boolean;
  onCancel?: () => void;
}

interface StageMeta {
  label: string;
  icon: typeof Database;
  bg: string;
  text: string;
  ring: string;
}

const STAGE_ORDER: ProgressStage[] = ['construction', 'sa', 'lns', 'complete'];

const STAGE_META: Record<ProgressStage, StageMeta> = {
  idle:         { label: 'Hazırlanıyor',     icon: Loader2,      bg: 'bg-slate-100',   text: 'text-slate-400',   ring: 'ring-slate-200' },
  started:      { label: 'Başlatılıyor',     icon: Loader2,      bg: 'bg-slate-100',   text: 'text-slate-400',   ring: 'ring-slate-200' },
  construction: { label: 'Yerleşim',         icon: Database,     bg: 'bg-blue-500',    text: 'text-white',       ring: 'ring-blue-300' },
  sa:           { label: 'Optimizasyon',     icon: Sparkles,     bg: 'bg-violet-500',  text: 'text-white',       ring: 'ring-violet-300' },
  lns:          { label: 'Çakışma Çözümü',   icon: ShieldCheck,  bg: 'bg-amber-500',   text: 'text-white',       ring: 'ring-amber-300' },
  complete:     { label: 'Tamamlandı',       icon: Trophy,       bg: 'bg-emerald-500', text: 'text-white',       ring: 'ring-emerald-300' },
  failed:       { label: 'Hata',             icon: AlertCircle,  bg: 'bg-red-500',     text: 'text-white',       ring: 'ring-red-300' },
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

export function AutoScheduleProgressModal({ open, onCancel }: AutoScheduleProgressModalProps) {
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
        // Hedefe yumuşak yaklaş: gap'in 0.06's'ı (saniyede ~%6 hız)
        // Plus stage-aware drift: stage uzunsa UI yavaşça kendisi de ilerler
        const easing = Math.min(gap, gap * 0.0035 * dtMs); // ~6%/sn yaklaşım
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
                <HeaderIcon className={`h-4.5 w-4.5 ${isLoading ? 'animate-spin' : ''}`} strokeWidth={2.2} />
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
              <div className="text-right">
                <div className="text-xs font-mono font-medium text-slate-700">{fmtSec(elapsedSec)}</div>
                {!isComplete && !isFailed && estimatedRemaining != null && estimatedRemaining > 0 && (
                  <div className="text-[10px] text-slate-400 font-mono mt-0.5">
                    ~{fmtSec(estimatedRemaining)} kaldı
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Progress bar */}
          <div className="px-6 pt-5 pb-2">
            <div className="flex items-center justify-between mb-1.5">
              <span className="text-[11px] font-medium uppercase tracking-wider text-slate-500">
                {headerMeta.label}
              </span>
              <span className="text-xs font-mono text-slate-700 tabular-nums">
                %{Math.round(displayPercent)}
              </span>
            </div>
            <div className="h-2 bg-slate-100 rounded-full overflow-hidden relative">
              <div
                className={`h-full ${isFailed ? 'bg-red-500' : isComplete ? 'bg-emerald-500' : 'bg-gradient-to-r from-blue-500 via-violet-500 to-amber-500'}`}
                style={{
                  width: `${displayPercent}%`,
                  transition: 'width 320ms cubic-bezier(0.22, 1, 0.36, 1), background-color 200ms',
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
          <div className="px-6 pt-3 pb-4 min-h-[64px]">
            {targetProgress.detail ? (
              <p className="text-sm text-slate-700 leading-snug">{targetProgress.detail}</p>
            ) : (
              <SkeletonLine />
            )}
            {targetProgress.total > 0 && (
              <p className="text-[11px] text-slate-400 mt-1.5 font-mono tabular-nums">
                {targetProgress.current} / {targetProgress.total}
              </p>
            )}
          </div>

          {/* Stage indicator (4 step) — renkli + ikonlu + tamamlanma süresiyle */}
          <div className="px-6 pb-4">
            <div className="grid grid-cols-4 gap-2">
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
