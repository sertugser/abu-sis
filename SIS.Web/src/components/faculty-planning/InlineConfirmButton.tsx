import { useState, useEffect, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface InlineConfirmButtonProps {
  label: string;
  confirmLabel?: string;
  variant?: 'default' | 'destructive' | 'outline' | 'ghost';
  confirmVariant?: 'default' | 'destructive';
  size?: 'default' | 'sm' | 'lg' | 'icon';
  icon?: React.ReactNode;
  disabled?: boolean;
  timeoutMs?: number;
  onConfirm: () => void;
  className?: string;
}

export default function InlineConfirmButton({
  label,
  confirmLabel = 'Emin misin? Onayla',
  variant = 'outline',
  confirmVariant = 'destructive',
  size = 'sm',
  icon,
  disabled,
  timeoutMs = 3000,
  onConfirm,
  className,
}: InlineConfirmButtonProps) {
  const [confirming, setConfirming] = useState(false);

  useEffect(() => {
    if (!confirming) return;
    const timer = setTimeout(() => setConfirming(false), timeoutMs);
    return () => clearTimeout(timer);
  }, [confirming, timeoutMs]);

  const handleClick = useCallback(() => {
    if (confirming) {
      setConfirming(false);
      onConfirm();
    } else {
      setConfirming(true);
    }
  }, [confirming, onConfirm]);

  return (
    <Button
      variant={confirming ? confirmVariant : variant}
      size={size}
      disabled={disabled}
      onClick={handleClick}
      className={cn(
        'transition-all duration-200',
        // Onay modunda: animate-pulse opacity'yi %50'ye düşürüp yazıyı
        // silikleştiriyordu. Yerine kalın yazı + ring vurgusu + hafif scale.
        confirming && 'font-bold ring-2 ring-red-300 ring-offset-1 scale-[1.02]',
        className,
      )}
      // text-white class'ı Button'un destructive variant'ı tarafından
      // override edilebildiği için inline style ile zorla — kesin beyaz.
      style={confirming ? { color: '#ffffff' } : undefined}
    >
      {confirming ? confirmLabel : (<>{icon}{label}</>)}
    </Button>
  );
}
