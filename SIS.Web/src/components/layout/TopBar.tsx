import { useMemo } from 'react';
import { useActiveTerm } from '../../hooks/useActiveTerm';
import { Badge } from '@/components/ui/badge';
import { useLocation, useNavigate } from 'react-router-dom';
import { Calendar, CircleDot } from 'lucide-react';
import RoleSwitcher from './RoleSwitcher';
import NotificationBell from '../notifications/NotificationBell';

import { flattenModuleItems } from './SidebarModules';
import { useMenu } from '../../hooks/useMenu';
import { TERM_TYPE_LABELS } from '../../lib/termTypes';

/**
 * Backend'den gelen menüden route → sayfa başlığı haritası kurar.
 * Önceden tüm 7 rol array'inin union'undan derive ediliyordu (Karar 2 öncesi).
 * Şimdi useMenu() user'ın kendi modüllerini verir → harita user-spesifik.
 * Permission'ı olmayan route için fallback (location.pathname) kullanılır.
 */
function usePageTitleMap(): Record<string, string> {
  const { data: modules = [] } = useMenu();
  return useMemo(() => {
    const map: Record<string, string> = {
      '/': 'Ana Sayfa',
      '/danisman-paneli': 'Danışman Paneli',
      '/danismanpaneli': 'Danışman Paneli',
    };
    for (const module of modules) {
      for (const child of flattenModuleItems(module)) {
        if (!map[child.to]) {
          map[child.to] = child.label;
        }
      }
    }
    return map;
  }, [modules]);
}

/**
 * Tam path eşleşmezse parent path'lere geri çekilerek arar.
 * Örn: `/secmeli-yapilandirma/gruplar` → `/secmeli-yapilandirma` menü kaydını yakalar.
 * Bu sayede tab tabanlı sayfalarda alt route'a inildiğinde başlık "Sayfa"'ya düşmez.
 */
function resolvePageTitle(pathname: string, map: Record<string, string>): string {
  if (map[pathname]) return map[pathname];
  const segments = pathname.split('/').filter(Boolean);
  for (let i = segments.length - 1; i > 0; i--) {
    const parent = '/' + segments.slice(0, i).join('/');
    if (map[parent]) return map[parent];
  }
  return 'Sayfa';
}

export default function TopBar() {
  const { data: term } = useActiveTerm();
  const location = useLocation();
  const navigate = useNavigate();
  const pageTitleMap = usePageTitleMap();

  const isHome = location.pathname === '/';
  const pageTitle = location.pathname.startsWith('/not-girisi/')
    ? 'Not Girişi'
    : resolvePageTitle(location.pathname, pageTitleMap);

  return (
    <header className="sticky top-0 z-30 flex h-20 items-center gap-4 border-b border-border/70 bg-white/90 shadow-[0_10px_35px_rgba(15,23,42,0.05)] backdrop-blur-xl px-6 lg:px-8">
      <div className="flex items-center gap-3">
        <div className="hidden sm:block h-10 w-1 rounded-full bg-gradient-to-b from-primary via-primary-light to-accent" />
        <h2
          onClick={isHome ? undefined : () => navigate('/')}
          className={`font-heading text-[1.35rem] font-bold text-foreground ${isHome ? '' : 'cursor-pointer hover:text-primary transition-colors'}`}
          title={isHome ? undefined : 'Ana Sayfaya Dön'}
        >
          {pageTitle}
        </h2>
      </div>

      <div className="flex-1" />

      <NotificationBell />

      <RoleSwitcher />

      {term && (
        <div className="flex items-center gap-3 rounded-2xl border border-border/70 bg-white/80 px-3 py-2 shadow-sm">
          <div className="hidden md:flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="h-4 w-4" />
            <span className="font-medium">{term.name}</span>
            <span className="text-xs text-muted-foreground/80">
              · {term.year} {TERM_TYPE_LABELS[term.semester] ?? term.semester}
            </span>
            {term.status && (
              <span className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground/80 rounded-sm bg-muted px-1.5 py-0.5">
                {term.status}
              </span>
            )}
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
