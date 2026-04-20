import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import {
  ChevronRight,
  LogOut,
  PanelLeftClose,
  PanelLeftOpen,
  Search,
  Settings2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { IMPLEMENTED_ROUTES } from '../../lib/constants';
import { useFavorites } from '../../hooks/useFavorites';
import { FavoritesDialog } from './FavoritesDialog';
import logo from '/logo.png';
import {
  studentMenuGroups,
  instructorMenuGroups,
  advisorMenuGroups,
  departmentManagerMenuGroups,
  administrativeStaffMenuGroups,
  deanMenuGroups,
  systemAdminMenuGroups,
} from './SidebarMenuGroups';
import type { MenuGroup } from './SidebarMenuGroups';

const HOME_ROUTE = '/';

function openInNewTab(url: string) {
  window.open(url, '_blank', 'noopener,noreferrer');
}

function findActiveGroup(pathname: string, menuGroups: MenuGroup[]): string | null {
  for (const group of menuGroups) {
    if (group.children.some(c => c.to === pathname || (c.to !== '/' && pathname.startsWith(c.to)))) {
      return group.id;
    }
  }
  return menuGroups[0]?.id ?? null;
}

function getMenuGroupsForRole(roles: string[]): MenuGroup[] {
  // Kullanıcının TÜM rollerine ait menüleri topla ve birleştir
  const roleMenuMap: [string, MenuGroup[]][] = [
    ['SystemAdministrator', systemAdminMenuGroups],
    ['Dean', deanMenuGroups],
    ['DepartmentManager', departmentManagerMenuGroups],
    ['AdministrativeStaff', administrativeStaffMenuGroups],
    ['AcademicAdvisor', advisorMenuGroups],
    ['Instructor', instructorMenuGroups],
    ['Student', studentMenuGroups],
  ];

  // Multi-rol kullanıcılar için: aynı id'li grupları birleştir.
  // Aynı route farklı gruplarda görünebilir (ör. Sık Kullanılanlar + kendi kategorisi)
  // — dedup yalnızca TEK bir grup içinde yapılır.
  const groupsById = new Map<string, MenuGroup>();
  const orderedIds: string[] = [];

  for (const [role, menus] of roleMenuMap) {
    if (!roles.includes(role)) continue;

    for (const group of menus) {
      const existing = groupsById.get(group.id);
      if (existing) {
        // Grup merge — sadece bu grup içinde route dedup
        const existingRoutes = new Set(existing.children.map(c => c.to));
        const additions = group.children.filter(c => !existingRoutes.has(c.to));
        groupsById.set(group.id, {
          ...existing,
          children: [...existing.children, ...additions],
        });
      } else {
        // Yeni grup — children zaten rolün kendi tanımında; mutation'a karşı shallow copy
        groupsById.set(group.id, { ...group, children: [...group.children] });
        orderedIds.push(group.id);
      }
    }
  }

  const allGroups = orderedIds.map(id => groupsById.get(id)!);
  return allGroups.length > 0 ? allGroups : studentMenuGroups;
}

function getRoleLabel(roles: string[]): string {
  const labels: string[] = [];
  if (roles.includes('SystemAdministrator')) labels.push('Yönetici');
  if (roles.includes('Dean')) labels.push('Dekan');
  if (roles.includes('DepartmentManager')) labels.push('Bölüm Başkanı');
  if (roles.includes('AdministrativeStaff')) labels.push('İdari Personel');
  if (roles.includes('AcademicAdvisor')) labels.push('Danışman');
  if (roles.includes('Instructor')) labels.push('Öğretim Üyesi');
  if (roles.includes('Student')) labels.push('Öğrenci');

  if (labels.length === 0) return 'Öğrenci Bilgi Sistemi';
  return labels.join(' / ') + ' Paneli';
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
  const location = useLocation();

  const roles = user?.roles ?? [];
  const rawMenuGroups = useMemo(() => getMenuGroupsForRole(roles), [roles]);
  const { favorites, hasCustomized, save: saveFavorites, MAX } = useFavorites();
  const [favoritesOpen, setFavoritesOpen] = useState(false);

  // Apply user's favorite pins to the Sık Kullanılanlar group (if customized)
  const menuGroups = useMemo<MenuGroup[]>(() => {
    if (!hasCustomized) return rawMenuGroups;
    const lookup = new Map<string, MenuGroup['children'][number]>();
    for (const group of rawMenuGroups) {
      if (group.id === 'sik-kullanilanlar') continue;
      for (const child of group.children) lookup.set(child.to, child);
    }
    return rawMenuGroups.map(group => {
      if (group.id !== 'sik-kullanilanlar') return group;
      const anaSayfa = group.children.find(c => c.to === HOME_ROUTE);
      const pins = (favorites ?? [])
        .map(route => lookup.get(route))
        .filter((c): c is MenuGroup['children'][number] => c !== undefined);
      const children = anaSayfa ? [anaSayfa, ...pins] : pins;
      return { ...group, children };
    });
  }, [rawMenuGroups, favorites, hasCustomized]);

  const [prevPathname, setPrevPathname] = useState(location.pathname);
  const [openGroup, setOpenGroup] = useState<string | null>(() => findActiveGroup(location.pathname, menuGroups));
  const [search, setSearch] = useState('');

  // Sync openGroup when location changes (setState during render — React-approved derived state pattern)
  if (prevPathname !== location.pathname) {
    setPrevPathname(location.pathname);
    const active = findActiveGroup(location.pathname, menuGroups);
    if (active) setOpenGroup(active);
  }

  const [flyoutGroup, setFlyoutGroup] = useState<string | null>(null);
  const [flyoutPos, setFlyoutPos] = useState<{ top: number; left: number }>({ top: 0, left: 0 });
  const groupBtnRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  const toggleGroup = (id: string) => {
    if (isCollapsed) {
      const btn = groupBtnRefs.current[id];
      if (btn) {
        const rect = btn.getBoundingClientRect();
        const top = Math.min(rect.top - 8, window.innerHeight - 400);
        setFlyoutPos({ top: Math.max(8, top), left: rect.right + 8 });
      }
      setFlyoutGroup(prev => prev === id ? null : id);
      return;
    }
    setOpenGroup(prev => prev === id ? null : id);
  };

  // Close flyout when clicking outside
  useEffect(() => {
    if (!flyoutGroup) return;
    const handler = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      if (!target.closest('[data-flyout]') && !target.closest('[data-group-btn]')) {
        setFlyoutGroup(null);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [flyoutGroup]);

  const initials = user?.name
    ? user.name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
    : '??';

  const textCls = cn('transition-opacity', DURATION, isCollapsed ? 'opacity-0' : 'opacity-100');

  return (
    <>
    <aside
      className="fixed inset-y-0 left-0 z-40 hidden lg:flex flex-col bg-sidebar border-r border-border overflow-hidden"
      style={{
        width: isCollapsed ? COLLAPSED_W : EXPANDED_W,
        transition: `width 200ms cubic-bezier(0.25, 0.1, 0.25, 1)`,
      }}
    >
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

      <div
        className="flex flex-col flex-1 min-h-0 transition-all duration-200"
        style={{ width: isCollapsed ? COLLAPSED_W : EXPANDED_W, minWidth: isCollapsed ? COLLAPSED_W : EXPANDED_W }}
      >
        <div className={cn(
          'shrink-0 overflow-hidden transition-all', DURATION,
          isCollapsed ? 'max-h-0 py-0 opacity-0' : 'max-h-10 px-5 pt-3 pb-1 opacity-100'
        )}>
          <p className="text-[11px] font-semibold text-primary tracking-[0.08em] whitespace-nowrap">
            {getRoleLabel(roles).toLocaleUpperCase('tr-TR')}
          </p>
        </div>

        {/* ── Arama Barı ── */}
        <div className={cn(
          'shrink-0 overflow-hidden transition-all', DURATION,
          isCollapsed ? 'max-h-0 py-0 px-0 opacity-0' : 'max-h-12 px-3 pt-2 pb-1 opacity-100'
        )}>
          <div className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
            <input
              type="text"
              placeholder="Menüde ara..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="h-8 w-full rounded-md border border-border/60 bg-muted/40 pl-8 pr-3 text-[13px] text-foreground placeholder:text-muted-foreground/60 outline-none focus:border-primary/50 focus:bg-background transition-colors"
            />
          </div>
        </div>

        <div className={cn("flex-1 overflow-y-auto overflow-x-hidden", isCollapsed ? "px-2" : "px-3")}>
          <nav className={cn("py-2 space-y-0.5", isCollapsed && "flex flex-col items-center")}>
            {search.trim() ? (
              /* Arama sonuçları — düz liste */
              (() => {
                const q = search.trim().toLocaleLowerCase('tr-TR');
                const results = menuGroups.flatMap(group =>
                  group.children
                    .filter(c => c.label.toLocaleLowerCase('tr-TR').includes(q))
                    .map(c => ({ ...c, groupLabel: group.label }))
                );
                return results.length === 0 ? (
                  <p className="px-3 py-4 text-[13px] text-muted-foreground/60 text-center">
                    Sonuç bulunamadı
                  </p>
                ) : (
                  results.map(child => {
                    const isDead = !IMPLEMENTED_ROUTES.has(child.to);
                    const baseClassName = cn(
                      'flex flex-col rounded-md px-3 py-2 text-[13px] transition-all duration-150',
                      isDead
                        ? 'text-muted-foreground/50 hover:text-destructive hover:bg-destructive/10'
                        : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                    );

                    if (child.openInNewTab) {
                      return (
                        <button
                          key={child.to}
                          type="button"
                          onClick={() => {
                            setSearch('');
                            openInNewTab(child.to);
                          }}
                          className={cn(baseClassName, 'w-full text-left')}
                        >
                          <span className="font-medium">{child.label}</span>
                          <span className="text-[11px] text-muted-foreground/60">{child.groupLabel}</span>
                        </button>
                      );
                    }

                    return (
                      <NavLink
                        key={child.to}
                        to={child.to}
                        end={child.to === '/'}
                        onClick={() => setSearch('')}
                        className={({ isActive }) =>
                          cn(
                            baseClassName,
                            !isDead && isActive && 'text-primary font-semibold bg-primary/5'
                          )
                        }
                      >
                        <span className="font-medium">{child.label}</span>
                        <span className="text-[11px] text-muted-foreground/60">{child.groupLabel}</span>
                      </NavLink>
                    );
                  })
                );
              })()
            ) : (
              /* Normal gruplu görünüm */
              menuGroups.map(group => {
                const isOpen = openGroup === group.id;
                const Icon = group.icon;

                return (
                  <div key={group.id} className="mb-1">
                    <button
                      ref={el => { groupBtnRefs.current[group.id] = el; }}
                      data-group-btn
                      onClick={() => toggleGroup(group.id)}
                      title={group.label}
                      className={cn(
                        'group flex items-center rounded-md text-sm font-medium transition-colors duration-150',
                        isCollapsed
                          ? cn('w-10 h-10 justify-center',
                              (isOpen || flyoutGroup === group.id) ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground')
                          : cn('w-full gap-3 px-3 py-2',
                              isOpen ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground')
                      )}
                    >
                      <Icon className="h-[18px] w-[18px] shrink-0" />
                      {!isCollapsed && (
                        <>
                          <span className="flex-1 text-left truncate">
                            {group.label}
                          </span>
                          <ChevronRight
                            className={cn(
                              'h-4 w-4 shrink-0 transition-transform', DURATION,
                              isOpen ? 'rotate-90' : 'opacity-50'
                            )}
                          />
                        </>
                      )}
                    </button>

                    {isOpen && !isCollapsed && (
                      <div className="mt-1 ml-[22px] border-l-2 border-primary/20 pl-3 space-y-1 pb-1">
                        {group.children.map(child => {
                          const isDead = !IMPLEMENTED_ROUTES.has(child.to);
                          const baseClassName = cn(
                            'block rounded-md px-3 py-1.5 text-[13px] transition-all duration-150 whitespace-nowrap',
                            isDead
                              ? 'text-muted-foreground/50 hover:text-destructive hover:bg-destructive/10'
                              : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                          );

                          if (child.openInNewTab) {
                            return (
                              <button
                                key={child.to}
                                type="button"
                                onClick={() => openInNewTab(child.to)}
                                className={cn(baseClassName, 'w-full text-left')}
                              >
                                {child.label}
                              </button>
                            );
                          }

                          return (
                            <NavLink
                              key={child.to}
                              to={child.to}
                              end={child.to === '/'}
                              className={({ isActive }) =>
                                cn(
                                  baseClassName,
                                  !isDead && isActive && 'text-primary font-semibold bg-primary/5'
                                )
                              }
                            >
                              {child.label}
                            </NavLink>
                          );
                        })}
                        {group.id === 'sik-kullanilanlar' && (
                          <button
                            type="button"
                            onClick={() => setFavoritesOpen(true)}
                            className="w-full flex items-center gap-1.5 rounded-md px-3 py-1.5 text-[12px] text-muted-foreground/70 hover:text-primary hover:bg-primary/5 transition-colors"
                          >
                            <Settings2 className="h-3.5 w-3.5" />
                            Sık Kullanılanları Düzenle
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </nav>
        </div>

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

      {/* Flyout portal — rendered outside sidebar to avoid overflow clipping */}
      {isCollapsed && flyoutGroup && (() => {
        const group = menuGroups.find(g => g.id === flyoutGroup);
        if (!group) return null;
        return createPortal(
          <div
            data-flyout
            className="fixed z-[60] w-56 bg-card border rounded-lg shadow-xl py-2 animate-in fade-in-0 zoom-in-95 duration-150"
            style={{ top: flyoutPos.top, left: flyoutPos.left }}
          >
            <p className="px-3 py-1.5 text-xs font-semibold text-muted-foreground uppercase tracking-wide">{group.label}</p>
            <div className="max-h-80 overflow-y-auto">
              {group.children.map(child => {
                const isDead = !IMPLEMENTED_ROUTES.has(child.to);
                const baseClassName = cn(
                  'block px-3 py-1.5 text-[13px] transition-colors duration-150',
                  isDead
                    ? 'text-muted-foreground/50 hover:text-destructive hover:bg-destructive/10'
                    : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                );

                if (child.openInNewTab) {
                  return (
                    <button
                      key={child.to}
                      type="button"
                      onClick={() => {
                        setFlyoutGroup(null);
                        openInNewTab(child.to);
                      }}
                      className={cn(baseClassName, 'w-full text-left')}
                    >
                      {child.label}
                    </button>
                  );
                }

                return (
                  <NavLink
                    key={child.to}
                    to={child.to}
                    end={child.to === '/'}
                    onClick={() => setFlyoutGroup(null)}
                    className={({ isActive }) =>
                      cn(
                        baseClassName,
                        !isDead && isActive && 'text-primary font-semibold bg-primary/5'
                      )
                    }
                  >
                    {child.label}
                  </NavLink>
                );
              })}
              {group.id === 'sik-kullanilanlar' && (
                <button
                  type="button"
                  onClick={() => {
                    setFlyoutGroup(null);
                    setFavoritesOpen(true);
                  }}
                  className="w-full flex items-center gap-1.5 px-3 py-1.5 text-[12px] text-muted-foreground/70 hover:text-primary hover:bg-primary/5 transition-colors"
                >
                  <Settings2 className="h-3.5 w-3.5" />
                  Sık Kullanılanları Düzenle
                </button>
              )}
            </div>
          </div>,
          document.body,
        );
      })()}

      <FavoritesDialog
        open={favoritesOpen}
        onOpenChange={setFavoritesOpen}
        menuGroups={rawMenuGroups}
        currentFavorites={favorites ?? []}
        maxFavorites={MAX}
        onSave={saveFavorites}
      />
    </>
  );
}
