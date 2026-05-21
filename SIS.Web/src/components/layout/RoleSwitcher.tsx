import { Check, ChevronDown, UserCog } from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useActiveRole } from '../../context/ActiveRoleContext';
import { getRoleDisplayLabel, type AppRole } from '../../lib/roles';
import { cn } from '@/lib/utils';

export default function RoleSwitcher() {
  const { activeRole, availableRoles, isMultiRole, isDelegated, setActiveRole } = useActiveRole();

  if (!activeRole) return null;

  const activeIsDelegated = isDelegated(activeRole);

  // Single-role users just see a static label (no dropdown clutter).
  if (!isMultiRole) {
    return (
      <div className="hidden md:inline-flex items-center gap-2 rounded-2xl border border-border/70 bg-white/80 px-3 py-2 text-sm text-muted-foreground shadow-sm">
        <UserCog className="h-4 w-4 text-primary/70" />
        <span className="font-medium text-foreground">{getRoleDisplayLabel(activeRole)}</span>
        {activeIsDelegated && (
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-700">
            VEKÂLET
          </span>
        )}
      </div>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className={cn(
            'inline-flex items-center gap-2 rounded-2xl border border-border/70 bg-white/80 px-3 py-2 text-sm shadow-sm',
            'transition-colors hover:border-primary/40 hover:bg-primary/5',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40',
          )}
          title="Aktif rolü değiştir"
        >
          <UserCog className="h-4 w-4 text-primary/80" />
          <span className="hidden sm:inline text-muted-foreground">Rol:</span>
          <span className="font-semibold text-foreground">{getRoleDisplayLabel(activeRole)}</span>
          {activeIsDelegated && (
            <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-semibold text-amber-700">
              VEKÂLET
            </span>
          )}
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-[240px]">
        <DropdownMenuLabel className="text-[11px] uppercase tracking-wide text-muted-foreground">
          Rol Değiştir
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {availableRoles.map((role: AppRole) => {
          const isActive = role === activeRole;
          const delegated = isDelegated(role);
          return (
            <DropdownMenuItem
              key={role}
              onSelect={() => {
                if (!isActive) setActiveRole(role);
              }}
              className={cn(
                'flex items-center justify-between gap-3 cursor-pointer',
                isActive && 'bg-primary/5 text-primary focus:bg-primary/10',
              )}
            >
              <div className="flex items-center gap-2">
                <span className="font-medium">{getRoleDisplayLabel(role)}</span>
                {delegated && (
                  <span className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[9px] font-semibold text-amber-700">
                    VEKÂLET
                  </span>
                )}
              </div>
              {isActive && <Check className="h-4 w-4" />}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
