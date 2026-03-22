import { useActiveTerm } from '../../hooks/useActiveTerm';
import { Badge } from '@/components/ui/badge';
import { useLocation } from 'react-router-dom';
import { Calendar, CircleDot, PanelLeftClose, PanelLeftOpen } from 'lucide-react';

const pageTitles: Record<string, string> = {
  '/': 'Ana Sayfa',
  '/kullanim-kilavuzu': 'Kullanım Kılavuzu',
  '/akademik-takvim': 'Akademik Takvim',
  '/ders-kayit': 'Ders Kayıt',
  '/plan': 'Kayıt Planı',
  '/alinan-dersler': 'Alınan Dersler',
  '/donem-ortalamalari': 'Dönem Ortalamaları',
  '/mufredat-durum': 'Müfredat Durum',
  '/derslerim': 'Derslerim',
  '/program-planlama': 'Program Planlama',
  '/not-girisi': 'Not Girişi',
  '/planlama-merkezi': 'Planlama Merkezi',
  '/sinif-listeleri': 'Sınıf Listeleri',
  '/mufredat-gorunumu': 'Müfredat Görünümü',
};

interface TopBarProps {
  isSidebarCollapsed: boolean;
  onToggleSidebar: () => void;
}

export default function TopBar({ isSidebarCollapsed, onToggleSidebar }: TopBarProps) {
  const { data: term } = useActiveTerm();
  const location = useLocation();

  const pageTitle = location.pathname.startsWith('/not-girisi/')
    ? 'Not Girişi'
    : pageTitles[location.pathname] || 'Sayfa';

  return (
    <header className="sticky top-0 z-30 flex h-20 items-center gap-4 border-b border-border/70 bg-white/90 shadow-[0_10px_35px_rgba(15,23,42,0.05)] backdrop-blur-xl px-6 lg:px-8">
      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={onToggleSidebar}
          className="hidden h-11 w-11 items-center justify-center rounded-2xl border border-border/80 bg-white text-muted-foreground transition-colors duration-150 hover:bg-muted hover:text-foreground lg:inline-flex"
          title={isSidebarCollapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
        >
          {isSidebarCollapsed ? <PanelLeftOpen className="h-5 w-5" /> : <PanelLeftClose className="h-5 w-5" />}
        </button>
        <div className="hidden sm:block h-10 w-1 rounded-full bg-gradient-to-b from-primary via-primary-light to-accent" />
        <div className="space-y-0.5">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            Ankara Bilim Üniversitesi
          </p>
          <h2 className="font-heading text-[1.35rem] font-bold text-foreground">
            {pageTitle}
          </h2>
        </div>
      </div>

      <div className="flex-1" />

      {term && (
        <div className="flex items-center gap-3 rounded-2xl border border-border/70 bg-white/80 px-3 py-2 shadow-sm">
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
