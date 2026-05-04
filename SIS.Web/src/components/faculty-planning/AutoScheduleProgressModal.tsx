import { useEffect, useState } from 'react';
import { Loader2, CheckCircle2, AlertCircle } from 'lucide-react';
import { useSignalR } from '@/hooks/useSignalR';
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
  /** previewMutation.isPending true iken modal görünür. */
  open: boolean;
  /** Kullanıcı "İptal Et" butonuna basınca tetiklenir — frontend mutation cleanup için. */
  onCancel?: () => void;
}

const STAGE_LABELS: Record<ProgressStage, string> = {
  idle: 'Hazırlanıyor',
  started: 'Başlatılıyor',
  construction: '1/3 Yerleşim',
  sa: '2/3 Optimizasyon',
  lns: '3/3 Çakışma Çözümü',
  complete: 'Tamamlandı',
  failed: 'Hata',
};

const STAGE_ORDER: ProgressStage[] = ['construction', 'sa', 'lns', 'complete'];

export function AutoScheduleProgressModal({ open, onCancel }: AutoScheduleProgressModalProps) {
  const [progress, setProgress] = useState<ScheduleProgressEvent>({
    stage: 'idle',
    percent: 0,
    current: 0,
    total: 0,
    detail: 'Bağlantı kuruluyor...',
  });
  const [elapsedSec, setElapsedSec] = useState(0);
  const [cancelling, setCancelling] = useState(false);
  const [activeConnection, setActiveConnection] = useState<HubConnection | null>(null);

  // Hub bağlantısı — açıkken modal açık, kapanınca durur.
  const { status: connectionStatus } = useSignalR({
    hubUrl: '/hubs/schedule-progress',
    enabled: open,
    onConnect: (conn: HubConnection) => {
      setActiveConnection(conn);
      conn.on('ScheduleProgress', (evt: ScheduleProgressEvent) => {
        setProgress(evt);
      });
    },
  });

  const handleCancel = async () => {
    if (!activeConnection || cancelling) return;
    setCancelling(true);
    try {
      await activeConnection.invoke<boolean>('Cancel');
      onCancel?.();
    } catch {
      // Backend çağrı hatası — yine de onCancel'ı çağır, kullanıcı modal'dan kurtulsun.
      onCancel?.();
    }
  };

  // Modal açıldıkça progress sıfırla, elapsed sayacı başlat.
  useEffect(() => {
    if (!open) {
      setProgress({ stage: 'idle', percent: 0, current: 0, total: 0, detail: null });
      setElapsedSec(0);
      setCancelling(false);
      setActiveConnection(null);
      return;
    }
    const startedAt = Date.now();
    const interval = setInterval(() => {
      setElapsedSec(Math.floor((Date.now() - startedAt) / 1000));
    }, 1000);
    return () => clearInterval(interval);
  }, [open]);

  if (!open) return null;

  const currentStageIdx = progress.stage === 'idle' || progress.stage === 'started'
    ? -1
    : STAGE_ORDER.indexOf(progress.stage);

  const isFailed = progress.stage === 'failed';
  const isComplete = progress.stage === 'complete';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
      <div className="w-[460px] rounded-xl bg-white shadow-xl border border-slate-200">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-100">
          <div className="flex items-center gap-3">
            {isFailed ? (
              <AlertCircle className="h-5 w-5 text-red-600" />
            ) : isComplete ? (
              <CheckCircle2 className="h-5 w-5 text-emerald-600" />
            ) : (
              <Loader2 className="h-5 w-5 text-blue-600 animate-spin" />
            )}
            <h3 className="font-semibold text-base text-slate-800">
              Otomatik Programlama
            </h3>
            <span className="ml-auto text-xs text-slate-400 font-mono">
              {elapsedSec}s
            </span>
          </div>
        </div>

        {/* Progress bar */}
        <div className="px-6 pt-5">
          <div className="flex items-center justify-between mb-1.5">
            <span className="text-xs font-medium text-slate-500 uppercase tracking-wide">
              {STAGE_LABELS[progress.stage]}
            </span>
            <span className="text-xs font-mono text-slate-600">
              %{progress.percent}
            </span>
          </div>
          <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
            <div
              className={`h-full transition-all duration-300 ${
                isFailed ? 'bg-red-500' : isComplete ? 'bg-emerald-500' : 'bg-blue-500'
              }`}
              style={{ width: `${progress.percent}%` }}
            />
          </div>
        </div>

        {/* Detail */}
        <div className="px-6 py-4 min-h-[60px]">
          <p className="text-sm text-slate-600">
            {progress.detail || 'İşlem devam ediyor...'}
          </p>
          {progress.total > 0 && (
            <p className="text-xs text-slate-400 mt-1 font-mono">
              {progress.current} / {progress.total}
            </p>
          )}
        </div>

        {/* Stage indicator (4 step) */}
        <div className="px-6 pb-5">
          <div className="flex items-center justify-between">
            {STAGE_ORDER.map((stage, idx) => {
              const isPast = idx < currentStageIdx || isComplete;
              const isCurrent = idx === currentStageIdx && !isComplete;
              return (
                <div key={stage} className="flex flex-col items-center gap-1.5 flex-1">
                  <div
                    className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-semibold transition-colors ${
                      isPast
                        ? 'bg-emerald-500 text-white'
                        : isCurrent
                          ? 'bg-blue-500 text-white animate-pulse'
                          : 'bg-slate-100 text-slate-400'
                    }`}
                  >
                    {isPast ? '✓' : idx + 1}
                  </div>
                  <span className={`text-[10px] uppercase tracking-wider ${
                    isPast || isCurrent ? 'text-slate-700 font-medium' : 'text-slate-400'
                  }`}>
                    {STAGE_LABELS[stage].replace(/^\d+\/\d+\s/, '')}
                  </span>
                </div>
              );
            })}
          </div>
        </div>

        {/* Connection state hint */}
        {connectionStatus === 'connecting' && (
          <div className="px-6 pb-2 text-[11px] text-slate-400 italic">
            Canlı ilerleme bağlantısı kuruluyor...
          </div>
        )}
        {connectionStatus === 'failed' && (
          <div className="px-6 pb-2 text-[11px] text-amber-600 italic">
            Canlı bağlantı kurulamadı — işlem arka planda devam ediyor.
          </div>
        )}

        {/* Cancel butonu — sadece aktif çalışırken (complete/failed dışında) */}
        {!isComplete && !isFailed && (
          <div className="px-6 pb-5 pt-2 flex justify-end">
            <button
              type="button"
              onClick={handleCancel}
              disabled={cancelling || connectionStatus !== 'connected'}
              className="px-3 py-1.5 text-xs font-medium text-slate-600 hover:text-red-600 hover:bg-red-50 rounded-md transition-colors disabled:opacity-50 disabled:cursor-not-allowed border border-slate-200"
            >
              {cancelling ? 'İptal ediliyor...' : 'İptal Et'}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
