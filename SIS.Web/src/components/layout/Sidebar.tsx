import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useActiveRole } from '../../context/ActiveRoleContext';
import { getRoleDisplayLabel } from '../../lib/roles';
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
  flattenModuleItems,
  isModule,
} from './SidebarModules';
import type { Module, ModuleItem, ModuleNode } from './SidebarModules';
import { useMenu } from '../../hooks/useMenu';

const HOME_ROUTE = '/';

function openInNewTab(url: string) {
  window.open(url, '_blank', 'noopener,noreferrer');
}

/**
 * Verilen pathname'e karşılık gelen leaf'i bulur ve onu içeren MODÜL id'lerinin
 * dizisini döndürür (root → leaf yönünde).
 *  - `null` → eşleşme yok
 *  - `[]`   → leaf doğrudan bu seviyede
 *  - `['yonetim', 'kullanici-islemleri']` → 2-seviye derin
 */
function findPathToRoute(nodes: ModuleNode[], pathname: string): string[] | null {
  for (const node of nodes) {
    if (isModule(node)) {
      const sub = findPathToRoute(node.children, pathname);
      if (sub !== null) return [node.id, ...sub];
    } else {
      const matches = node.to === pathname || (node.to !== '/' && pathname.startsWith(node.to));
      if (matches) return [];
    }
  }
  return null;
}

/**
 * Sidebar açık-yol state'i (`openPath`) için route bazlı çözüm.
 * 4 strateji:
 *   1) Kullanıcı niyeti — mevcut top-level modül hâlâ pathname'i içeriyorsa korunur
 *   2) Canonical (favori-dışı) ilk eşleşme
 *   3) Son çare favori modülü
 *   4) Default ilk modül (alt-state yok)
 */
function resolveOpenPath(
  pathname: string,
  modules: Module[],
  currentPath: string[],
): string[] {
  // 1) Kullanıcı niyetine saygı — currentPath[0] hâlâ pathname'i içeriyorsa onu kullan,
  //    nested seviye yeni pathname'e göre güncellenir (alt-modül intent korunmaz)
  if (currentPath.length > 0) {
    const currentTop = modules.find(m => m.id === currentPath[0]);
    if (currentTop) {
      const sub = findPathToRoute(currentTop.children, pathname);
      if (sub !== null) return [currentTop.id, ...sub];
    }
  }

  // 2) Canonical: ilk favori-dışı eşleşme
  for (const module of modules) {
    if (module.id === 'sik-kullanilanlar') continue;
    const sub = findPathToRoute(module.children, pathname);
    if (sub !== null) return [module.id, ...sub];
  }

  // 3) Son çare: favoriler (sadece orada bulunan route — ör. Ana Sayfa)
  const favorites = modules.find(m => m.id === 'sik-kullanilanlar');
  if (favorites) {
    const sub = findPathToRoute(favorites.children, pathname);
    if (sub !== null) return [favorites.id, ...sub];
  }

  // 4) Default: ilk modül, alt-state yok
  return modules[0] ? [modules[0].id] : [];
}

function getActiveRoleLabel(activeRole: string | null): string {
  if (!activeRole) return 'Öğrenci Bilgi Sistemi';
  return `${getRoleDisplayLabel(activeRole)} Paneli`;
}

const EXPANDED_W = 290;
const COLLAPSED_W = 72;
const DURATION = 'duration-200';

interface SidebarProps {
  isCollapsed?: boolean;
  onToggleSidebar?: () => void;
}

export default function Sidebar({ isCollapsed = false, onToggleSidebar }: SidebarProps) {
  const { user, logout } = useAuth();
  const { activeRole } = useActiveRole();
  const location = useLocation();

  // Sidebar artık sadece aktif rolün modülünü gösterir (multi-rol için switcher TopBar'da).
  // Active rol henüz resolve edilmediyse user.roles ilk elemanını kullan — hydration flash önler.
  const effectiveRole = activeRole ?? user?.roles?.[0] ?? null;

  // v2 Karar 2: Modüller backend'den dinamik gelir (DB-driven, permission-filtered).
  // useMenu() data layer; render kısmı (renderNestedNode, openPath, vb.) aynı kalır.
  const { data: rawModulesFromBackend = [] } = useMenu();
  const rawModules = useMemo<Module[]>(() => rawModulesFromBackend, [rawModulesFromBackend]);
  const { favorites, hasCustomized, save: saveFavorites, MAX } = useFavorites();
  const [favoritesOpen, setFavoritesOpen] = useState(false);

  // Apply user's favorite pins to the Sık Kullanılanlar modülüne (if customized)
  const modules = useMemo<Module[]>(() => {
    if (!hasCustomized) return rawModules;
    // Tüm modül leaf'lerini topla (recursive — Yönetim altındaki nested item'lar dahil)
    const lookup = new Map<string, ModuleItem>();
    for (const module of rawModules) {
      if (module.id === 'sik-kullanilanlar') continue;
      for (const child of flattenModuleItems(module)) lookup.set(child.to, child);
    }
    return rawModules.map(module => {
      if (module.id !== 'sik-kullanilanlar') return module;
      // Sık Kullanılanlar her zaman flat (sadece ModuleItem children — sözleşme)
      const existingItems = module.children.filter((c): c is ModuleItem => !isModule(c));
      const anaSayfa = existingItems.find(c => c.to === HOME_ROUTE);
      const pins = (favorites ?? [])
        .map(route => lookup.get(route))
        .filter((c): c is ModuleItem => c !== undefined);
      const children: ModuleNode[] = anaSayfa ? [anaSayfa, ...pins] : pins;
      return { ...module, children };
    });
  }, [rawModules, favorites, hasCustomized]);

  const [prevPathname, setPrevPathname] = useState(location.pathname);
  const [prevRole, setPrevRole] = useState<string | null>(effectiveRole);
  const [openPath, setOpenPath] = useState<string[]>(() =>
    resolveOpenPath(location.pathname, modules, []),
  );
  const [search, setSearch] = useState('');
  const [flyoutModule, setFlyoutModule] = useState<string | null>(null);
  const [flyoutPos, setFlyoutPos] = useState<{ top: number; left: number }>({ top: 0, left: 0 });
  const moduleBtnRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  // Sync openPath when location OR active role changes (setState during render — React-approved derived state pattern).
  // Rol değişirken mevcut path geçerli olmayabilir — o yüzden currentPath'i sıfırla.
  if (prevPathname !== location.pathname || prevRole !== effectiveRole) {
    const roleChanged = prevRole !== effectiveRole;
    setPrevPathname(location.pathname);
    setPrevRole(effectiveRole);
    const next = resolveOpenPath(
      location.pathname,
      modules,
      roleChanged ? [] : openPath,
    );
    setOpenPath(next);
    setFlyoutModule(null);
  }

  const toggleNode = (id: string, depth: number) => {
    // Collapsed mode flyout sadece top-level (depth 0) için anlamlıdır.
    // Nested toggle collapsed mode'da YOK — flyout içinde nested modüller her zaman expanded.
    if (isCollapsed && depth === 0) {
      const btn = moduleBtnRefs.current[id];
      if (btn) {
        const rect = btn.getBoundingClientRect();
        const top = Math.min(rect.top - 8, window.innerHeight - 400);
        setFlyoutPos({ top: Math.max(8, top), left: rect.right + 8 });
      }
      setFlyoutModule(prev => prev === id ? null : id);
      return;
    }

    // Akkordeon: aynı node tıklanırsa kapat (ve descendants'ı), farklıysa aynı seviyede değiştir
    setOpenPath(prev => {
      if (prev[depth] === id) return prev.slice(0, depth);
      return [...prev.slice(0, depth), id];
    });
  };

  // Close flyout when clicking outside
  useEffect(() => {
    if (!flyoutModule) return;
    const handler = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      if (!target.closest('[data-flyout]') && !target.closest('[data-module-btn]')) {
        setFlyoutModule(null);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [flyoutModule]);

  const initials = user?.name
    ? user.name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
    : '??';

  const textCls = cn('transition-opacity', DURATION, isCollapsed ? 'opacity-0' : 'opacity-100');

  // Tek bir leaf item'ı render eder — flat, nested ve flyout layoutlarda paylaşılır
  function renderModuleItem(child: ModuleItem) {
    const isDead = !IMPLEMENTED_ROUTES.has(child.to);
    const baseClassName = cn(
      'block rounded-md px-3 py-1.5 text-[13px] transition-all duration-150 truncate',
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
          title={child.label}
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
        title={child.label}
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
  }

  // Nested modül veya leaf render — depth >= 1.
  // Akkordeon: openPath[depth] === node.id ise içindekiler render'lanır.
  function renderNestedNode(node: ModuleNode, depth: number): React.ReactNode {
    if (!isModule(node)) return renderModuleItem(node);

    const isOpen = openPath[depth] === node.id;
    const Icon = node.icon;

    return (
      <div key={node.id} className="space-y-1">
        <button
          type="button"
          onClick={() => toggleNode(node.id, depth)}
          className={cn(
            'flex w-full items-center gap-2 rounded-md px-3 py-1.5 text-[13px] font-semibold transition-colors',
            isOpen
              ? 'text-foreground bg-muted/60'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted/40',
          )}
        >
          {Icon && <Icon className="h-4 w-4 shrink-0" />}
          <span className="flex-1 text-left truncate">{node.label}</span>
          <ChevronRight
            className={cn(
              'h-4 w-4 shrink-0 transition-transform',
              DURATION,
              isOpen ? 'rotate-90' : 'opacity-60',
            )}
          />
        </button>

        {isOpen && (
          <div className="pl-2 border-l border-border/40 space-y-1 pb-1">
            {node.children.map(child => renderNestedNode(child, depth + 1))}
          </div>
        )}
      </div>
    );
  }

  // Flyout (collapsed sidebar dropdown) için leaf item render
  function renderFlyoutItem(child: ModuleItem) {
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
            setFlyoutModule(null);
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
        onClick={() => setFlyoutModule(null)}
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
  }

  // Flyout — nested modüller her zaman expanded (akkordeon yok, küçük popover sözleşmesi)
  function renderFlyoutNode(node: ModuleNode): React.ReactNode {
    if (!isModule(node)) return renderFlyoutItem(node);

    return (
      <div key={node.id}>
        <p className="px-3 pt-2 pb-0.5 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground/60">
          {node.label}
        </p>
        {node.children.map(child => renderFlyoutNode(child))}
      </div>
    );
  }

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
            {getActiveRoleLabel(effectiveRole).toLocaleUpperCase('tr-TR')}
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
                // Recursive flatten — Yönetim altındaki nested item'lar dahil
                const results = modules.flatMap(module =>
                  flattenModuleItems(module)
                    .filter(c => c.label.toLocaleLowerCase('tr-TR').includes(q))
                    .map(c => ({ ...c, moduleLabel: module.label }))
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
                          <span className="text-[11px] text-muted-foreground/60">{child.moduleLabel}</span>
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
                        <span className="text-[11px] text-muted-foreground/60">{child.moduleLabel}</span>
                      </NavLink>
                    );
                  })
                );
              })()
            ) : (
              /* Normal modül görünümü */
              modules.map(module => {
                const isOpen = openPath[0] === module.id;
                const Icon = module.icon;

                return (
                  <div key={module.id} className="mb-1">
                    <button
                      ref={el => { moduleBtnRefs.current[module.id] = el; }}
                      data-module-btn
                      onClick={() => toggleNode(module.id, 0)}
                      title={module.label}
                      className={cn(
                        'group flex items-center rounded-md text-sm font-medium transition-colors duration-150',
                        isCollapsed
                          ? cn('w-10 h-10 justify-center',
                              (isOpen || flyoutModule === module.id) ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground')
                          : cn('w-full gap-3 px-3 py-2',
                              isOpen ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground')
                      )}
                    >
                      {Icon && <Icon className="h-[18px] w-[18px] shrink-0" />}
                      {!isCollapsed && (
                        <>
                          <span className="flex-1 text-left truncate">
                            {module.label}
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
                        {module.children.map(child => renderNestedNode(child, 1))}
                        {module.id === 'sik-kullanilanlar' && (
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
      {isCollapsed && flyoutModule && (() => {
        const module = modules.find(m => m.id === flyoutModule);
        if (!module) return null;
        return createPortal(
          <div
            data-flyout
            className="fixed z-[60] w-56 bg-card border rounded-lg shadow-xl py-2 animate-in fade-in-0 zoom-in-95 duration-150"
            style={{ top: flyoutPos.top, left: flyoutPos.left }}
          >
            <p className="px-3 py-1.5 text-xs font-semibold text-muted-foreground uppercase tracking-wide">{module.label}</p>
            <div className="max-h-80 overflow-y-auto">
              {module.children.map(child => renderFlyoutNode(child))}
            </div>
            {module.id === 'sik-kullanilanlar' && (
              <button
                type="button"
                onClick={() => {
                  setFlyoutModule(null);
                  setFavoritesOpen(true);
                }}
                className="w-full flex items-center gap-1.5 px-3 py-1.5 text-[12px] text-muted-foreground/70 hover:text-primary hover:bg-primary/5 transition-colors"
              >
                <Settings2 className="h-3.5 w-3.5" />
                Sık Kullanılanları Düzenle
              </button>
            )}
          </div>,
          document.body,
        );
      })()}

      <FavoritesDialog
        open={favoritesOpen}
        onOpenChange={setFavoritesOpen}
        modules={rawModules}
        currentFavorites={favorites ?? []}
        maxFavorites={MAX}
        onSave={saveFavorites}
      />
    </>
  );
}
