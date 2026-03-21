import { useActiveTerm } from '../../hooks/useActiveTerm';
import { Badge } from '@/components/ui/badge';
import { useLocation } from 'react-router-dom';
import { Calendar, CircleDot } from 'lucide-react';

const pageTitles: Record<string, string> = {
  '/': 'Ana Sayfa',
  '/ders-kayit': 'Ders Kayıt',
  '/plan': 'Kayıt Planı',
  '/alinan-dersler': 'Alınan Dersler',
  '/mufredat-durum': 'Müfredat Durum',
  '/derslerim': 'Derslerim',
  '/program-planlama': 'Program Planlama',
  '/not-girisi': 'Not Girişi',
};

export default function TopBar() {
  const { data: term } = useActiveTerm();
  const location = useLocation();

  const pageTitle = location.pathname.startsWith('/not-girisi/')
    ? 'Not Girişi'
    : pageTitles[location.pathname] || 'Sayfa';

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-4 border-b border-border bg-card/80 backdrop-blur-xl px-6 lg:px-8">
      {/* Page title */}
      <h2 className="font-heading text-lg font-semibold text-foreground">
        {pageTitle}
      </h2>

      {/* Spacer */}
      <div className="flex-1" />

      {/* Term info + status */}
      {term && (
        <div className="flex items-center gap-3">
          <div className="hidden md:flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="h-4 w-4" />
            <span className="font-medium">{term.name}</span>
          </div>

          <div className="flex gap-2">
            <Badge variant={term.isRegistrationOpen ? 'success' : 'secondary'} className="gap-1.5">
              <CircleDot className="h-3 w-3" />
              {term.isRegistrationOpen ? 'Kayıt Açık' : 'Kayıt Kapalı'}
            </Badge>
            <Badge variant={term.isAddDropOpen ? 'info' : 'secondary'} className="hidden sm:inline-flex gap-1.5">
              <CircleDot className="h-3 w-3" />
              {term.isAddDropOpen ? 'Ek/Bırak Açık' : 'Ek/Bırak Kapalı'}
            </Badge>
          </div>
        </div>
      )}
    </header>
  );
}
