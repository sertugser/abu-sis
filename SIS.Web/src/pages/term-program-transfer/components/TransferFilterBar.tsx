import { AlertTriangle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

interface TransferFilterBarProps {
  termStatus: string | undefined;
  isTermSuitable: boolean;
}

/**
 * Sayfanın en üstünde sadece dönem uygunluk uyarısı gösterir.
 * Bölüm seçimi her panelin kendi başlığında.
 */
export function TransferFilterBar({ termStatus, isTermSuitable }: TransferFilterBarProps) {
  if (isTermSuitable || !termStatus) return null;
  return (
    <Alert variant="destructive">
      <AlertTriangle className="h-4 w-4" />
      <AlertTitle>Dönem durumu uygun değil</AlertTitle>
      <AlertDescription>
        Aktif dönem şu anda <strong>{termStatus}</strong> durumunda. Aktarım yapabilmek için dönemin{' '}
        <strong>Active</strong> (tarih aralığı içinde ve iptal edilmemiş) olması gerekiyor.
      </AlertDescription>
    </Alert>
  );
}
