import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertTitle, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { cn } from '@/lib/utils';
import {
  Loader2, AlertTriangle, CheckCircle2, ChevronDown, Info, RotateCcw,
} from 'lucide-react';

/* ── Accordion Step Shell ── */

interface StepShellProps {
  stepNum: number;
  title: string;
  description: string;
  icon: React.ElementType;
  isCompleted: boolean;
  isActive: boolean;
  isLocked: boolean;
  lockReason?: string;
  completedSummary?: string;
  completedAt?: string | null;
  completedBy?: string | null;
  onToggle: () => void;
  onUndo?: () => void;
  undoLoading?: boolean;
  undoWarning?: string;
  children: React.ReactNode;
}

export function StepAccordion({
  stepNum, title, description, icon: Icon,
  isCompleted, isActive, isLocked, lockReason,
  completedSummary, completedAt, completedBy,
  onToggle, onUndo, undoLoading, undoWarning,
  children,
}: StepShellProps) {
  const [expanded, setExpanded] = useState(false);
  const isOpen = isActive || expanded;

  return (
    <div className={cn(
      'rounded-xl border transition-all duration-250',
      isCompleted && 'border-emerald-200 bg-emerald-50/30',
      isActive && !isCompleted && 'border-primary/30 bg-primary/[0.02] shadow-sm shadow-primary/5',
      isLocked && 'border-border/60 bg-muted/20 opacity-75',
      !isCompleted && !isActive && !isLocked && 'border-border/80',
    )}>
      {/* Header — always visible */}
      <button
        type="button"
        onClick={() => isCompleted ? setExpanded(!expanded) : onToggle()}
        className="w-full flex items-center gap-3 p-4 sm:p-5 text-left group"
      >
        {/* Step number circle */}
        <div className={cn(
          'flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold transition-all',
          isCompleted && 'bg-emerald-500 text-white',
          isActive && !isCompleted && 'bg-primary text-white shadow-md shadow-primary/25',
          isLocked && 'bg-muted text-muted-foreground',
          !isCompleted && !isActive && !isLocked && 'bg-muted text-muted-foreground',
        )}>
          {isCompleted ? <CheckCircle2 className="h-5 w-5" /> : stepNum}
        </div>

        {/* Title + meta */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <Icon className={cn(
              'h-4 w-4 flex-shrink-0',
              isCompleted ? 'text-emerald-600' : isActive ? 'text-primary' : 'text-muted-foreground',
            )} />
            <h3 className={cn(
              'text-sm font-semibold truncate',
              isCompleted ? 'text-emerald-800' : isActive ? 'text-foreground' : 'text-muted-foreground',
            )}>
              {title}
            </h3>
          </div>

          {/* Completed meta or description */}
          {isCompleted && completedSummary ? (
            <p className="text-xs text-emerald-700/70 mt-0.5 truncate">
              {completedSummary}
              {completedBy && <span className="ml-1">· {completedBy}</span>}
              {completedAt && <span className="ml-1">· {new Date(completedAt).toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}</span>}
            </p>
          ) : isLocked && lockReason ? (
            <p className="text-xs text-muted-foreground mt-0.5">{lockReason}</p>
          ) : (
            <p className="text-xs text-muted-foreground mt-0.5 hidden sm:block">{description}</p>
          )}
        </div>

        {/* Right side: undo + chevron */}
        <div className="flex items-center gap-2 flex-shrink-0">
          {isCompleted && onUndo && (
            <ConfirmButton
              trigger={
                <Button
                  variant="ghost"
                  size="sm"
                  className="gap-1.5 text-muted-foreground hover:text-destructive h-8"
                  onClick={e => e.stopPropagation()}
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                  <span className="hidden sm:inline text-xs">Geri Al</span>
                </Button>
              }
              title="Bu adimi geri almak istiyor musunuz?"
              description={undoWarning ?? 'Bu islem geri alinabilir.'}
              isLoading={undoLoading ?? false}
              onConfirm={onUndo}
            />
          )}
          {(isCompleted || (!isLocked && !isActive)) && (
            <ChevronDown className={cn(
              'h-4 w-4 text-muted-foreground transition-transform duration-200',
              isOpen && 'rotate-180',
            )} />
          )}
        </div>
      </button>

      {/* Content — collapsible */}
      {isOpen && (
        <div className="px-4 sm:px-5 pb-5 pt-0 space-y-4 animate-fade-in">
          <div className="border-t border-border/50 pt-4" />
          {children}
        </div>
      )}
    </div>
  );
}

/* ── Confirm Button with AlertDialog ── */

interface ConfirmButtonProps {
  trigger?: React.ReactNode;
  label?: string;
  icon?: React.ElementType;
  isLoading: boolean;
  disabled?: boolean;
  variant?: 'default' | 'destructive' | 'outline';
  title: string;
  description: string;
  onConfirm: () => void;
}

export function ConfirmButton({
  trigger, label, icon: BtnIcon, isLoading, disabled, variant = 'default',
  title, description, onConfirm,
}: ConfirmButtonProps) {
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        {trigger ?? (
          <Button variant={variant} disabled={isLoading || disabled} className="gap-2">
            {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : BtnIcon && <BtnIcon className="h-4 w-4" />}
            {isLoading ? 'Isleniyor...' : label}
          </Button>
        )}
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Iptal</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Onayla</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

/* ── Mutation Feedback ── */

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function MutationFeedback({ mutation, successMsg }: { mutation: any; successMsg?: string }) {
  if (mutation.isError) {
    return (
      <Alert variant="destructive" className="py-2 animate-fade-in">
        <AlertTriangle className="h-4 w-4" />
        <AlertDescription>{(mutation.error as Error)?.message ?? 'Beklenmeyen bir hata olustu.'}</AlertDescription>
      </Alert>
    );
  }
  if (mutation.isSuccess && successMsg) {
    return (
      <Alert variant="success" className="py-2 animate-fade-in">
        <CheckCircle2 className="h-4 w-4" />
        <AlertDescription>{successMsg}</AlertDescription>
      </Alert>
    );
  }
  return null;
}

/* ── Prerequisite Alert ── */

export function PrerequisiteAlert({ stepName }: { stepName: string }) {
  return (
    <Alert variant="info">
      <Info className="h-4 w-4" />
      <AlertTitle>Onkosul</AlertTitle>
      <AlertDescription>
        Bu adima gecmek icin once <strong>{stepName}</strong> adiminin tamamlanmasi gerekmektedir.
      </AlertDescription>
    </Alert>
  );
}

/* ── Stat Pill (inline stat) ── */

export function StatPill({ label, value, variant = 'default' }: {
  label: string;
  value: string | number;
  variant?: 'default' | 'success' | 'warning' | 'danger';
}) {
  return (
    <div className={cn(
      'inline-flex items-center gap-2 rounded-lg border px-3 py-2',
      variant === 'success' && 'border-emerald-200 bg-emerald-50/60',
      variant === 'warning' && 'border-amber-200 bg-amber-50/60',
      variant === 'danger' && 'border-red-200 bg-red-50/60',
      variant === 'default' && 'border-border/80 bg-muted/30',
    )}>
      <span className="text-xs text-muted-foreground whitespace-nowrap">{label}</span>
      <span className={cn(
        'text-sm font-bold tabular-nums',
        variant === 'success' && 'text-emerald-700',
        variant === 'warning' && 'text-amber-700',
        variant === 'danger' && 'text-red-700',
      )}>{value}</span>
    </div>
  );
}
