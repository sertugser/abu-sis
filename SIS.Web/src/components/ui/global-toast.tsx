import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { CheckCircle2, AlertCircle, X } from 'lucide-react';

type ToastVariant = 'success' | 'error' | 'info';

interface ToastItem {
  id: number;
  variant: ToastVariant;
  title: string;
  description?: string;
}

interface ToastContextValue {
  showSuccess: (title: string, description?: string) => void;
  showError: (title: string, description?: string) => void;
  showInfo: (title: string, description?: string) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

const AUTO_CLOSE_MS = 3500;

let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);

  const dismiss = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const enqueue = useCallback((variant: ToastVariant, title: string, description?: string) => {
    const id = nextId++;
    setToasts((prev) => [...prev, { id, variant, title, description }]);
  }, []);

  const showSuccess = useCallback((title: string, description?: string) =>
    enqueue('success', title, description), [enqueue]);
  const showError = useCallback((title: string, description?: string) =>
    enqueue('error', title, description), [enqueue]);
  const showInfo = useCallback((title: string, description?: string) =>
    enqueue('info', title, description), [enqueue]);

  return (
    <ToastContext.Provider value={{ showSuccess, showError, showInfo }}>
      {children}
      {toasts.length > 0 && (
        <div className="pointer-events-none fixed inset-0 z-[100] flex items-start justify-center pt-24">
          <div className="pointer-events-auto flex w-full max-w-md flex-col gap-2 px-4">
            {toasts.map((t) => (
              <ToastCard key={t.id} toast={t} onDismiss={() => dismiss(t.id)} />
            ))}
          </div>
        </div>
      )}
    </ToastContext.Provider>
  );
}

function ToastCard({ toast, onDismiss }: { toast: ToastItem; onDismiss: () => void }) {
  useEffect(() => {
    const timer = setTimeout(onDismiss, AUTO_CLOSE_MS);
    return () => clearTimeout(timer);
  }, [onDismiss]);

  const palette =
    toast.variant === 'success'
      ? { ring: 'ring-green-200', bg: 'bg-green-50', icon: 'text-green-600', title: 'text-green-900' }
      : toast.variant === 'error'
        ? { ring: 'ring-red-200', bg: 'bg-red-50', icon: 'text-red-600', title: 'text-red-900' }
        : { ring: 'ring-blue-200', bg: 'bg-blue-50', icon: 'text-blue-600', title: 'text-blue-900' };

  const Icon = toast.variant === 'error' ? AlertCircle : CheckCircle2;

  return (
    <div
      role="status"
      className={`flex items-start gap-3 rounded-xl border bg-white px-4 py-3 shadow-lg ring-2 ${palette.ring} ${palette.bg}`}
    >
      <Icon className={`mt-0.5 h-5 w-5 shrink-0 ${palette.icon}`} />
      <div className="min-w-0 flex-1">
        <p className={`text-sm font-semibold ${palette.title}`}>{toast.title}</p>
        {toast.description && (
          <p className="mt-0.5 text-xs text-muted-foreground">{toast.description}</p>
        )}
      </div>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Kapat"
        className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>');
  return ctx;
}
