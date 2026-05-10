import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { CheckCircle2, AlertCircle, AlertTriangle, X } from 'lucide-react';

type ToastVariant = 'success' | 'error' | 'info' | 'warning';

interface ToastOptions {
  center?: boolean;
  durationMs?: number;
}

interface ToastItem {
  id: number;
  variant: ToastVariant;
  title: string;
  description?: string;
  center: boolean;
  durationMs: number;
}

interface ToastContextValue {
  showSuccess: (title: string, description?: string, options?: ToastOptions) => void;
  showError: (title: string, description?: string, options?: ToastOptions) => void;
  showInfo: (title: string, description?: string, options?: ToastOptions) => void;
  showWarning: (title: string, description?: string, options?: ToastOptions) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

const DEFAULT_DURATION_MS = 3500;

let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismiss = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const enqueue = useCallback((variant: ToastVariant, title: string, description?: string, options?: ToastOptions) => {
    const id = nextId++;
    setToasts((prev) => [
      ...prev,
      {
        id,
        variant,
        title,
        description,
        center: options?.center ?? false,
        durationMs: options?.durationMs ?? DEFAULT_DURATION_MS,
      },
    ]);
  }, []);

  const showSuccess = useCallback((title: string, description?: string, options?: ToastOptions) =>
    enqueue('success', title, description, options), [enqueue]);
  const showError = useCallback((title: string, description?: string, options?: ToastOptions) =>
    enqueue('error', title, description, options), [enqueue]);
  const showInfo = useCallback((title: string, description?: string, options?: ToastOptions) =>
    enqueue('info', title, description, options), [enqueue]);
  const showWarning = useCallback((title: string, description?: string, options?: ToastOptions) =>
    enqueue('warning', title, description, options), [enqueue]);

  const topToasts = toasts.filter((t) => !t.center);
  const centerToasts = toasts.filter((t) => t.center);

  // createPortal renders into document.body — outside any Radix Dialog Portal
  // so modal aria-hidden / inert siblings don't disable toast pointer events.
  // The wrapper enforces pointer-events:auto in case a parent route was inerted.
  const portalTarget = typeof document !== 'undefined' ? document.body : null;

  return (
    <ToastContext.Provider value={{ showSuccess, showError, showInfo, showWarning }}>
      {children}
      {portalTarget && topToasts.length > 0 && createPortal(
        <div
          className="pointer-events-none fixed inset-0 z-[9998] flex items-start justify-center pt-24"
          style={{ pointerEvents: 'none' }}
        >
          <div
            className="pointer-events-auto flex w-full max-w-xl flex-col gap-3 px-4"
            style={{ pointerEvents: 'auto' }}
          >
            {topToasts.map((t) => (
              <ToastCard key={t.id} toast={t} onDismiss={() => dismiss(t.id)} center={false} />
            ))}
          </div>
        </div>,
        portalTarget,
      )}
      {portalTarget && centerToasts.length > 0 && createPortal(
        <div
          className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/50 backdrop-blur-sm px-4 animate-in fade-in duration-200"
          style={{ pointerEvents: 'auto' }}
          onClick={(e) => {
            if (e.target === e.currentTarget) {
              const last = centerToasts[centerToasts.length - 1];
              if (last) dismiss(last.id);
            }
          }}
        >
          {centerToasts.map((t) => (
            <ToastCard key={t.id} toast={t} onDismiss={() => dismiss(t.id)} center={true} />
          ))}
        </div>,
        portalTarget,
      )}
    </ToastContext.Provider>
  );
}

interface VariantStyle {
  ring: string;
  iconBg: string;
  iconHalo: string;
  iconColor: string;
  title: string;
  closeHover: string;
}

const VARIANT_STYLES: Record<ToastVariant, VariantStyle> = {
  success: {
    ring: 'ring-green-200',
    iconBg: 'bg-green-100',
    iconHalo: 'ring-green-100/60',
    iconColor: 'text-green-600',
    title: 'text-green-950',
    closeHover: 'hover:bg-green-100',
  },
  error: {
    ring: 'ring-red-200',
    iconBg: 'bg-red-100',
    iconHalo: 'ring-red-100/60',
    iconColor: 'text-red-600',
    title: 'text-red-950',
    closeHover: 'hover:bg-red-100',
  },
  info: {
    ring: 'ring-blue-200',
    iconBg: 'bg-blue-100',
    iconHalo: 'ring-blue-100/60',
    iconColor: 'text-blue-600',
    title: 'text-blue-950',
    closeHover: 'hover:bg-blue-100',
  },
  warning: {
    ring: 'ring-amber-300',
    iconBg: 'bg-amber-100',
    iconHalo: 'ring-amber-100/70',
    iconColor: 'text-amber-600',
    title: 'text-amber-950',
    closeHover: 'hover:bg-amber-100',
  },
};

function getIcon(variant: ToastVariant) {
  if (variant === 'error') return AlertCircle;
  if (variant === 'warning') return AlertTriangle;
  return CheckCircle2;
}

function ToastCard({ toast, onDismiss, center }: { toast: ToastItem; onDismiss: () => void; center: boolean }) {
  useEffect(() => {
    const timer = setTimeout(onDismiss, toast.durationMs);
    return () => clearTimeout(timer);
  }, [onDismiss, toast.durationMs]);

  const palette = VARIANT_STYLES[toast.variant];
  const Icon = getIcon(toast.variant);

  if (center) {
    return (
      <div
        role="alertdialog"
        aria-modal="true"
        onClick={(e) => e.stopPropagation()}
        className={`relative w-full max-w-2xl overflow-hidden rounded-3xl bg-white shadow-2xl ring-2 ${palette.ring} animate-in zoom-in-95 fade-in duration-200`}
        style={{ pointerEvents: 'auto' }}
      >
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            onDismiss();
          }}
          aria-label="Kapat"
          className={`absolute right-4 top-4 z-10 flex h-11 w-11 items-center justify-center rounded-full text-foreground/60 transition-colors ${palette.closeHover} hover:text-foreground focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-foreground/30`}
        >
          <X className="h-6 w-6" strokeWidth={2.5} />
        </button>
        <div className="flex flex-col items-center px-12 pt-14 pb-12 text-center">
          <div className={`flex h-20 w-20 shrink-0 items-center justify-center rounded-full ring-8 ${palette.iconBg} ${palette.iconHalo}`}>
            <Icon className={`h-11 w-11 ${palette.iconColor}`} strokeWidth={2} />
          </div>
          <p className={`mt-7 text-3xl font-bold leading-tight tracking-tight ${palette.title}`}>{toast.title}</p>
          {toast.description && (
            <p className="mt-4 max-w-lg text-lg leading-relaxed text-foreground/75">{toast.description}</p>
          )}
        </div>
      </div>
    );
  }

  return (
    <div
      role="status"
      className={`flex items-start gap-4 rounded-2xl border bg-white px-6 py-5 shadow-xl ring-2 ${palette.ring}`}
    >
      <Icon className={`mt-0.5 h-7 w-7 shrink-0 ${palette.iconColor}`} />
      <div className="min-w-0 flex-1">
        <p className={`text-lg font-semibold leading-tight ${palette.title}`}>{toast.title}</p>
        {toast.description && (
          <p className="mt-1.5 text-sm leading-snug text-foreground/80">{toast.description}</p>
        )}
      </div>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Kapat"
        className="-mt-1 -mr-1 rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
      >
        <X className="h-5 w-5" />
      </button>
    </div>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>');
  return ctx;
}
