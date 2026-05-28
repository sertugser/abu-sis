import { BookOpen, CalendarDays, History, Loader2, Printer } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { IMPLEMENTED_ROUTES, ROUTES } from '@/lib/constants';

interface RegistrationToolbarProps {
  onDownloadPdf: () => void;
  pdfLoading: boolean;
}

export function RegistrationToolbar({ onDownloadPdf, pdfLoading }: RegistrationToolbarProps) {
  return (
    <div className="border-b border-border/40 bg-muted/20 px-3 py-1.5 flex flex-wrap items-center gap-1">
      {IMPLEMENTED_ROUTES.has(ROUTES.INSTRUCTOR_SCHEDULING) && (
        <Button asChild size="sm" variant="ghost" className="h-7 text-xs gap-1.5">
          <Link to={ROUTES.INSTRUCTOR_SCHEDULING}>
            <CalendarDays className="h-3.5 w-3.5" />
            Ders Programı
          </Link>
        </Button>
      )}

      {IMPLEMENTED_ROUTES.has(ROUTES.MUFREDAT_DURUM) && (
        <Button asChild size="sm" variant="ghost" className="h-7 text-xs gap-1.5">
          <Link to={ROUTES.MUFREDAT_DURUM}>
            <BookOpen className="h-3.5 w-3.5" />
            Müfredat Durumu
          </Link>
        </Button>
      )}

      {IMPLEMENTED_ROUTES.has(ROUTES.ALINAN_DERSLER) && (
        <Button asChild size="sm" variant="ghost" className="h-7 text-xs gap-1.5">
          <Link to={ROUTES.ALINAN_DERSLER}>
            <History className="h-3.5 w-3.5" />
            Ders Kaydı Geçmişi
          </Link>
        </Button>
      )}

      <Button
        size="sm"
        variant="ghost"
        onClick={onDownloadPdf}
        disabled={pdfLoading}
        className="h-7 text-xs gap-1.5"
      >
        {pdfLoading ? (
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
        ) : (
          <Printer className="h-3.5 w-3.5" />
        )}
        Yazdır
      </Button>
    </div>
  );
}
