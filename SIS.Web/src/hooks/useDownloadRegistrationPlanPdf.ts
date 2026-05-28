import { useMutation } from '@tanstack/react-query';
import { downloadRegistrationPlanPdf } from '../api/registrationCatalog.api';

/**
 * "Yazdır" — backend `GET /registrations/plan/pdf` Blob'unu indirir ve browser indirme'sini tetikler.
 */
export function useDownloadRegistrationPlanPdf() {
  return useMutation({
    mutationFn: downloadRegistrationPlanPdf,
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `ders-kayit-plani-${new Date().toISOString().slice(0, 10)}.pdf`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    },
  });
}
