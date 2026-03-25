import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import {
  ChevronRight,
  LogOut,
  PanelLeftClose,
  PanelLeftOpen,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { IMPLEMENTED_ROUTES } from '../../lib/constants';
import logo from '/logo.png';
import { studentMenuGroups, buildInstructorMenuGroups } from './SidebarMenuGroups';
import type { MenuGroup } from './SidebarMenuGroups';

// Moved to SidebarMenuGroups.ts

function getMenuGroupsForRole(roles: string[]): MenuGroup[] {
  if (roles.includes('Instructor') || roles.includes('AcademicAdvisor') || roles.includes('DepartmentManager')) {
    return buildInstructorMenuGroups(
      roles.includes('Instructor')
    );
  }
  return studentMenuGroups;
}

function getRoleLabel(roles: string[]): string {
  if (roles.includes('Instructor')) return 'Öğretim Üyesi Paneli';
  if (roles.includes('AcademicAdvisor')) return 'Danışman Paneli';
  if (roles.includes('DepartmentManager')) return 'Bölüm Başkanı Paneli';
  if (roles.includes('SystemAdministrator')) return 'Yönetici Paneli';
  return 'Öğrenci Bilgi Sistemi';
}

const EXPANDED_W = 260;
const COLLAPSED_W = 72;
const DURATION = 'duration-200';

interface SidebarProps {
  isCollapsed?: boolean;
  onToggleSidebar?: () => void;
}

export default function Sidebar({ isCollapsed = false, onToggleSidebar }: SidebarProps) {
  const { user, logout } = useAuth();
  const [openGroup, setOpenGroup] = useState<string | null>(null);

  const roles = user?.roles ?? [];
  const menuGroups = getMenuGroupsForRole(roles);

  const toggleGroup = (id: string) => {
    if (isCollapsed) {
      onToggleSidebar?.();
      return;
    }
    setOpenGroup(prev => prev === id ? null : id);
  };

  const initials = user?.name
    ? user.name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
    : '??';

  // Collapsed = text invisible, layout unchanged
  const textCls = cn('transition-opacity', DURATION, isCollapsed ? 'opacity-0' : 'opacity-100');

  return (
    <aside
      className="fixed inset-y-0 left-0 z-40 hidden lg:flex flex-col bg-sidebar border-r border-border overflow-hidden"
      style={{
        width: isCollapsed ? COLLAPSED_W : EXPANDED_W,
        transition: `width 200ms cubic-bezier(0.25, 0.1, 0.25, 1)`,
      }}
    >
      {/* ── Header — outside inner wrapper so it respects aside width ── */}
      <div className="shrink-0 h-20 flex items-center border-b border-border/70">
        {isCollapsed ? (
          <button
            type="button"
            onClick={onToggleSidebar}
            className="group relative flex h-full w-full items-center justify-center transition-colors hover:bg-muted/50"
            title="Menüyü genişlet"
          >
            <img
              src={logo}
              alt="Logo"
              className="h-10 w-10 shrink-0 object-contain transition-all duration-200 group-hover:scale-75 group-hover:opacity-0"
            />
            <span className="pointer-events-none absolute inset-0 flex items-center justify-center">
              <PanelLeftOpen className="h-5 w-5 scale-75 text-foreground opacity-0 transition-all duration-200 group-hover:scale-100 group-hover:opacity-100" />
            </span>
          </button>
        ) : (
          <div className="flex h-full w-full items-center justify-between px-5">
            <div className="flex items-center gap-3">
              <img src={logo} alt="Logo" className="h-9 w-9 shrink-0 object-contain" />
              <div className="flex flex-col">
                <span className="text-[13px] font-bold tracking-wide text-foreground leading-none">
                  ANKARA BİLİM
                </span>
                <span className="text-[10px] font-medium tracking-[0.12em] text-muted-foreground leading-none mt-1">
                  ÜNİVERSİTESİ
                </span>
              </div>
            </div>

            <button
              type="button"
              onClick={onToggleSidebar}
              className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              title="Menüyü daralt"
            >
              <PanelLeftClose className="h-5 w-5" />
            </button>
          </div>
        )}
      </div>

      {/* Inner wrapper — always 260px, aside clips overflow */}
      <div
        className="flex flex-col flex-1 min-h-0"
        style={{ width: EXPANDED_W, minWidth: EXPANDED_W }}
      >
        {/* Role label */}
        <div className={cn(
          'shrink-0 overflow-hidden transition-all', DURATION,
          isCollapsed ? 'max-h-0 py-0 opacity-0' : 'max-h-10 px-5 pt-3 pb-1 opacity-100'
        )}>
          <p className="text-[11px] font-semibold text-primary tracking-[0.08em] whitespace-nowrap">
            {getRoleLabel(roles).toLocaleUpperCase('tr-TR')}
          </p>
        </div>

        {/* ── Nav ── */}
        <div className="flex-1 overflow-y-auto overflow-x-hidden px-3">
          <nav className="py-2 space-y-0.5">
            {menuGroups.map(group => {
              const isOpen = openGroup === group.id;
              const Icon = group.icon;

              return (
                <div key={group.id} className="mb-1">
                  <button
                    onClick={() => toggleGroup(group.id)}
                    title={group.label}
                    className={cn(
                      'group flex items-center w-full gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors duration-150',
                      isOpen && isCollapsed
                        ? 'bg-primary/10 text-primary shadow-sm'
                        : isOpen && !isCollapsed
                          ? 'bg-primary/10 text-primary'
                          : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                    )}
                  >
                    <Icon className="h-[18px] w-[18px] shrink-0" />
                    <span className={cn('flex-1 text-left truncate', textCls)}>
                      {group.label}
                    </span>
                    <ChevronRight
                      className={cn(
                        'h-4 w-4 shrink-0 transition-all', DURATION,
                        isCollapsed ? 'opacity-0' : isOpen ? 'rotate-90 opacity-100' : 'opacity-50'
                      )}
                    />
                  </button>

                  {isOpen && !isCollapsed && (
                    <div className="mt-1 ml-[22px] border-l-2 border-primary/20 pl-3 space-y-1 pb-1">
                      {group.children.map(child => {
                        const isDead = !IMPLEMENTED_ROUTES.has(child.to);
                        return (
                          <NavLink
                            key={child.to}
                            to={child.to}
                            end={child.to === '/'}
                            className={({ isActive }) =>
                              cn(
                                'block rounded-md px-3 py-1.5 text-[13px] transition-all duration-150 whitespace-nowrap',
                                isDead
                                  ? 'text-muted-foreground/50 hover:text-destructive hover:bg-destructive/10'
                                  : isActive
                                    ? 'text-primary font-semibold bg-primary/5'
                                    : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                              )
                            }
                          >
                            {child.label}
                          </NavLink>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}
          </nav>
        </div>

        {/* ── Bottom user ── */}
        <div className="shrink-0 mt-auto border-t bg-muted/30 p-4 whitespace-nowrap">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[#003366] text-white text-xs font-bold shadow-sm">
              {initials}
            </div>
            <div className={cn('flex-1 min-w-0', textCls)}>
              <p className="text-sm font-semibold text-foreground truncate">{user?.name}</p>
              <p className="text-[11px] text-muted-foreground truncate">{user?.email}</p>
            </div>
            <button
              onClick={logout}
              className={cn(
                'flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-all',
                DURATION,
                isCollapsed && 'opacity-0 pointer-events-none'
              )}
              title="Güvenli Çıkış"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>
    </aside>
  );
}
