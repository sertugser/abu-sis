import type { ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { cn } from '@/lib/utils';

export type ConfirmTone = 'default' | 'destructive';

interface ConfirmActionDialogProps {
  open: boolean;
  title: string;
  description: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  tone?: ConfirmTone;
  isSubmitting: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function ConfirmActionDialog({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel = 'İptal',
  tone = 'default',
  isSubmitting,
  onCancel,
  onConfirm,
}: ConfirmActionDialogProps) {
  return (
    <AlertDialog open={open} onOpenChange={isOpen => { if (!isOpen) onCancel(); }}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="text-sm text-muted-foreground">{description}</div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isSubmitting}>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            disabled={isSubmitting}
            className={cn(
              tone === 'destructive' && 'bg-red-600 text-white hover:bg-red-700 focus:ring-red-600',
            )}
          >
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
