import { useActiveTerm } from '../../hooks/useActiveTerm';
import { Badge } from '@/components/ui/badge';
import { useLocation, useNavigate } from 'react-router-dom';
import { Calendar, CircleDot } from 'lucide-react';
import RoleSwitcher from './RoleSwitcher';

import {
  studentMenuGroups,
  instructorMenuGroups,
  advisorMenuGroups,
  departmentManagerMenuGroups,
  administrativeStaffMenuGroups,
  deanMenuGroups,
  systemAdminMenuGroups,
} from './SidebarMenuGroups';

const allMenuGroups = [
  ...studentMenuGroups,
  ...instructorMenuGroups,
  ...advisorMenuGroups,
  ...departmentManagerMenuGroups,
  ...administrativeStaffMenuGroups,
  ...deanMenuGroups,
  ...systemAdminMenuGroups,
];

const pageTitles: Record<string, string> = {
  '/': 'Ana Sayfa',
};

// Menülerden gelen yolları dinamik olarak ekle
allMenuGroups.forEach(group => {
  group.children.forEach(child => {
    if (!pageTitles[child.to]) {
      pageTitles[child.to] = child.label;
    }
  });
});

export default function TopBar() {
  const { data: term } = useActiveTerm();
  const location = useLocation();
  const navigate = useNavigate();

  const isHome = location.pathname === '/';
  const pageTitle = location.pathname.startsWith('/not-girisi/')
    ? 'Not Girişi'
    : pageTitles[location.pathname] || 'Sayfa';

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

      <RoleSwitcher />

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
