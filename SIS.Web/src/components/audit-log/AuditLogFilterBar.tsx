import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Search } from 'lucide-react';
import { ACTION_CONFIG, ENTITY_TYPE_OPTIONS } from './audit-log.constants';
import { UserSearchCombobox } from './UserSearchCombobox';

interface AuditLogFilterBarProps {
  entityType: string;
  action: string;
  userId: string;
  entityId: string;
  search: string;
  from: string;
  to: string;
  onEntityTypeChange: (v: string) => void;
  onActionChange: (v: string) => void;
  onUserIdChange: (v: string) => void;
  onEntityIdChange: (v: string) => void;
  onSearchChange: (v: string) => void;
  onFromChange: (v: string) => void;
  onToChange: (v: string) => void;
  onApply: () => void;
}

function FilterField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label className="text-xs font-medium text-muted-foreground uppercase tracking-wider">{label}</label>
      {children}
    </div>
  );
}

export function AuditLogFilterBar({
  entityType, action, userId, entityId, search,
  from, to,
  onEntityTypeChange, onActionChange, onUserIdChange,
  onEntityIdChange, onSearchChange,
  onFromChange, onToChange, onApply,
}: AuditLogFilterBarProps) {
  // Kart sarmalayıcısı yok — sayfadaki tek yüzeyin (Card) içinde bir bölüm olarak render edilir.
  return (
    <div className="animate-fade-in px-4 py-4">
        <div className="flex flex-wrap items-end gap-3">
          <FilterField label="Varlık Tipi">
            <Select value={entityType || 'all'} onValueChange={(v: string) => onEntityTypeChange(v === 'all' ? '' : v)}>
              <SelectTrigger className="h-9 w-48">
                <SelectValue placeholder="Tüm Varlıklar" />
              </SelectTrigger>
              <SelectContent>
                {ENTITY_TYPE_OPTIONS.map(o => (
                  <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterField>

          <FilterField label="İşlem Türü">
            <Select value={action || 'all'} onValueChange={(v: string) => onActionChange(v === 'all' ? '' : v)}>
              <SelectTrigger className="h-9 w-48">
                <SelectValue placeholder="Tüm İşlemler" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm İşlemler</SelectItem>
                {Object.entries(ACTION_CONFIG).map(([key, cfg]) => (
                  <SelectItem key={key} value={key}>
                    <span className="flex items-center gap-2">
                      <span className={`text-xs ${cfg.color}`}>{cfg.icon}</span>
                      {cfg.label}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterField>

          <FilterField label="Kullanıcı">
            <UserSearchCombobox
              value={userId}
              onChange={onUserIdChange}
            />
          </FilterField>

          <FilterField label="Kayıt ID">
            <Input
              className="h-9 w-32"
              placeholder="örn: 42"
              value={entityId}
              onChange={e => onEntityIdChange(e.target.value)}
            />
          </FilterField>

          <FilterField label="İçerik Arama">
            <Input
              className="h-9 w-44"
              placeholder="En az 2 karakter..."
              value={search}
              onChange={e => onSearchChange(e.target.value)}
            />
          </FilterField>

          <FilterField label="Başlangıç">
            <Input
              type="date"
              className="h-9 w-40"
              value={from}
              onChange={e => onFromChange(e.target.value)}
            />
          </FilterField>

          <FilterField label="Bitiş">
            <Input
              type="date"
              className="h-9 w-40"
              value={to}
              onChange={e => onToChange(e.target.value)}
            />
          </FilterField>

          <Button size="sm" className="h-9" onClick={onApply}>
            <Search className="h-3.5 w-3.5 mr-1.5" />
            Ara
          </Button>
        </div>
    </div>
  );
}
