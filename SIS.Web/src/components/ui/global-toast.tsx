import {
  createContext,
  useContext,
  useMemo,
  useSyncExternalStore,
  type ComponentType,
  type ReactNode,
} from 'react';
import { Toaster as SonnerToaster, toast as sonnerToast } from 'sonner';
import { CheckCircle2, XCircle, AlertTriangle, Info, X } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * Uygulama genel geri-bildirim (toast) sistemi.
 *
 * Motor: **sonner** — istifleme, hover'da duraklatma, kaydırarak kapatma ve
 * yaşam döngüsünü o yönetir. Görsel tamamen özel ({@link ToastCard}); marka
 * tokenlarıyla (success/warning/info/destructive, font-heading) hizalıdır.
 *
 * `useToast()` facade'ı sabit kalır: tüm çağrı yerleri
 * `showSuccess/showError/showInfo/showWarning(title, description?, options?)`
 * imzasını kullanır. Tüm toast'lar üstte (top-center) tek bir akışta istiflenir.
 * Birden çok kart yığıldığında "Tümünü temizle" butonu çıkar.
 */

type ToastVariant = 'success' | 'error' | 'info' | 'warning';

interface ToastOptions {
  /** Otomatik kapanma süresi (ms). Verilmezse türe göre varsayılan kullanılır. */
  durationMs?: number;
}

interface ToastContextValue {
  showSuccess: (title: string, description?: string, options?: ToastOptions) => void;
  showError: (title: string, description?: string, options?: ToastOptions) => void;
  showInfo: (title: string, description?: string, options?: ToastOptions) => void;
  showWarning: (title: string, description?: string, options?: ToastOptions) => void;
}

// Tür → varsayılan görünürlük süresi. Hata/uyarı daha uzun durur (okunup aksiyon
// alınabilsin); başarı/bilgi kısa. Hepsi hover'da (sonner) duraklar.
const DEFAULT_DURATIONS: Record<ToastVariant, number> = {
  success: 4000,
  info: 4000,
  warning: 6000,
  error: 8000,
};

// Bu sayıya (ve üstüne) ulaşınca "Tümünü temizle" butonu görünür.
const CLEAR_ALL_VISIBLE_FROM = 2;

interface VariantStyle {
  Icon: ComponentType<{ className?: string; strokeWidth?: number }>;
  /** İkon kapsülü: yumuşak tonlu zemin + ikon rengi + halo ring. */
  iconWrap: string;
  /** İkon kapsülünün hover'da koyulaşan halo'su. */
  iconHover: string;
  /** Alt geri-sayım çubuğu rengi. */
  bar: string;
}

const VARIANT_STYLES: Record<ToastVariant, VariantStyle> = {
  success: { Icon: CheckCircle2, iconWrap: 'bg-success/10 text-success ring-success/10', iconHover: 'group-hover:bg-success/15 group-hover:ring-success/20', bar: 'bg-success' },
  error: { Icon: XCircle, iconWrap: 'bg-destructive/10 text-destructive ring-destructive/10', iconHover: 'group-hover:bg-destructive/15 group-hover:ring-destructive/20', bar: 'bg-destructive' },
  warning: { Icon: AlertTriangle, iconWrap: 'bg-warning/15 text-warning ring-warning/10', iconHover: 'group-hover:bg-warning/20 group-hover:ring-warning/25', bar: 'bg-warning' },
  info: { Icon: Info, iconWrap: 'bg-info/10 text-info ring-info/10', iconHover: 'group-hover:bg-info/15 group-hover:ring-info/20', bar: 'bg-info' },
};

const ToastContext = createContext<ToastContextValue | null>(null);

// ── Aktif toast sayacı (Tümünü temizle butonu için) ──────────────────────────
// sonner aktif toast sayısını dışa vermediği için kendimiz takip ederiz.
// id bazlı Set: dedup (aynı id) ve her kapanış yolu (X / swipe / timeout) doğru sayılır.
const activeToastIds = new Set<string | number>();
const countListeners = new Set<() => void>();

function emitCount(): void {
  countListeners.forEach((listener) => listener());
}
function registerToast(id: string | number): void {
  activeToastIds.add(id);
  emitCount();
}
function unregisterToast(id: string | number): void {
  if (activeToastIds.delete(id)) emitCount();
}
function clearAllToasts(): void {
  sonnerToast.dismiss();
  activeToastIds.clear();
  emitCount();
}
const toastCountStore = {
  subscribe(listener: () => void): () => void {
    countListeners.add(listener);
    return () => {
      countListeners.delete(listener);
    };
  },
  getSnapshot(): number {
    return activeToastIds.size;
  },
};

// ── sonner köprüsü (imperatif, context gerektirmez) ──────────────────────────

/**
 * Aynı içerikli (tür + başlık + açıklama) toast'lar tek satıra indirgenir:
 * sonner aynı `id` ile çağrıldığında yeni kart eklemek yerine mevcut olanı
 * günceller. Toplu işlemlerde tekrarlı mesaj yığılması böyle önlenir.
 */
function contentId(variant: ToastVariant, title: string, description?: string): string {
  return `${variant}:${title}:${description ?? ''}`;
}

function pushToast(
  variant: ToastVariant,
  title: string,
  description?: string,
  options?: ToastOptions,
): void {
  const duration = options?.durationMs ?? DEFAULT_DURATIONS[variant];
  const id = contentId(variant, title, description);
  registerToast(id);
  sonnerToast.custom(
    (toastId) => (
      <ToastCard
        toastId={toastId}
        variant={variant}
        title={title}
        description={description}
        durationMs={duration}
      />
    ),
    {
      id,
      duration,
      unstyled: true,
      className: 'w-full',
      onDismiss: () => unregisterToast(id),
      onAutoClose: () => unregisterToast(id),
    },
  );
}

// ── İkonlar ──────────────────────────────────────────────────────────────────

/** Başarı toast'ında tik bir kez "çizilir" (stroke-dashoffset animasyonu, index.css). */
function AnimatedCheck({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" className={className} aria-hidden>
      <path
        d="M20 6 9 17l-5-5"
        stroke="currentColor"
        strokeWidth={2.5}
        strokeLinecap="round"
        strokeLinejoin="round"
        pathLength={1}
        className="toast-check-path"
      />
    </svg>
  );
}

// ── Toast kartı (sonner içeriği) ─────────────────────────────────────────────

interface ToastCardProps {
  toastId: string | number;
  variant: ToastVariant;
  title: string;
  description?: string;
  durationMs: number;
}

function ToastCard({ toastId, variant, title, description, durationMs }: ToastCardProps) {
  const { Icon, iconWrap, iconHover, bar } = VARIANT_STYLES[variant];
  const assertive = variant === 'error' || variant === 'warning';
  const iconClass = 'h-[22px] w-[22px] transition-transform duration-200 group-hover:scale-110';

  return (
    <div
      data-toast-card
      role={assertive ? 'alert' : 'status'}
      aria-live={assertive ? 'assertive' : 'polite'}
      className="group relative flex w-full items-start gap-3.5 overflow-hidden rounded-2xl border border-border/60 bg-card p-4 pr-2.5 shadow-[0_16px_50px_-16px_oklch(0.2_0.05_260/0.45),0_2px_6px_-2px_oklch(0.2_0.05_260/0.1)]"
    >
      <span
        className={cn(
          'mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ring-4 transition-all duration-200 group-hover:scale-110 group-hover:ring-[6px]',
          iconWrap,
          iconHover,
        )}
      >
        {variant === 'success' ? (
          <AnimatedCheck className={iconClass} />
        ) : (
          <Icon className={iconClass} strokeWidth={2.25} />
        )}
      </span>
      <div className="min-w-0 flex-1 pt-0.5">
        <p className="font-heading text-[15px] font-semibold leading-snug tracking-tight text-foreground">{title}</p>
        {description && (
          <p className="mt-1 break-words text-[13px] leading-relaxed text-muted-foreground">{description}</p>
        )}
      </div>
      <button
        type="button"
        onClick={() => sonnerToast.dismiss(toastId)}
        aria-label="Kapat"
        className="-mr-0.5 mt-0.5 shrink-0 rounded-lg p-1.5 text-muted-foreground/50 transition-colors hover:bg-muted hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring/40"
      >
        <X className="h-4 w-4" />
      </button>
      {Number.isFinite(durationMs) && (
        <span
          className={cn('toast-progress absolute bottom-1.5 left-4 right-3 h-1 rounded-full', bar)}
          style={{ animationDuration: `${durationMs}ms` }}
          aria-hidden
        />
      )}
    </div>
  );
}

// ── Provider ─────────────────────────────────────────────────────────────────

export function ToastProvider({ children }: { children: ReactNode }) {
  const value = useMemo<ToastContextValue>(() => {
    const make = (variant: ToastVariant) =>
      (title: string, description?: string, options?: ToastOptions) =>
        pushToast(variant, title, description, options);
    return {
      showSuccess: make('success'),
      showError: make('error'),
      showInfo: make('info'),
      showWarning: make('warning'),
    };
  }, []);

  const activeCount = useSyncExternalStore(toastCountStore.subscribe, toastCountStore.getSnapshot, () => 0);
  const showClearAll = activeCount >= CLEAR_ALL_VISIBLE_FROM;

  return (
    <ToastContext.Provider value={value}>
      {children}
      <SonnerToaster
        position="top-center"
        visibleToasts={4}
        gap={10}
        offset={showClearAll ? 64 : 20}
        toastOptions={{ unstyled: true }}
        style={{ ['--width' as string]: '400px' }}
        className="pointer-events-auto"
      />
      {showClearAll && (
        <button
          type="button"
          onClick={clearAllToasts}
          style={{ zIndex: 2147483647 }}
          className="fixed left-1/2 top-4 -translate-x-1/2 inline-flex items-center gap-1.5 rounded-full border border-border/70 bg-card/95 px-3.5 py-1.5 text-xs font-semibold text-muted-foreground shadow-lg backdrop-blur-sm transition-colors hover:bg-muted hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring/40 animate-fade-in"
        >
          <X className="h-3.5 w-3.5" />
          Tümünü temizle ({activeCount})
        </button>
      )}
    </ToastContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>');
  return ctx;
}
