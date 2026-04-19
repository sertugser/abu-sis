import { Button } from '@/components/ui/button';
import { Download, Filter, RotateCcw, RefreshCw } from 'lucide-react';
import { FilterPresets } from './FilterPresets';

interface AuditLogToolbarProps {
  isExporting: boolean;
  isFetching: boolean;
  hasActiveFilter: boolean;
  activeFilterCount: number;
  showFilters: boolean;
  currentFilters: Record<string, string>;
  onToggleFilters: () => void;
  onClearFilters: () => void;
  onExportCsv: () => void;
  onRefresh: () => void;
  onApplyPreset: (filters: Record<string, string>) => void;
}

export function AuditLogToolbar({
  isExporting, isFetching, hasActiveFilter, activeFilterCount,
  showFilters, currentFilters,
  onToggleFilters, onClearFilters, onExportCsv, onRefresh, onApplyPreset,
}: AuditLogToolbarProps) {
  return (
    <div className="flex items-center justify-end gap-2">
      <Button
        variant="outline"
        size="sm"
        onClick={onRefresh}
        disabled={isFetching}
      >
        <RefreshCw className={`h-3.5 w-3.5 mr-1.5 ${isFetching ? 'animate-spin' : ''}`} />
        Yenile
      </Button>
      <Button variant="outline" size="sm" onClick={onExportCsv} disabled={isExporting}>
        <Download className="h-3.5 w-3.5 mr-1.5" />
        {isExporting ? 'Hazırlanıyor...' : 'CSV İndir'}
      </Button>
      <FilterPresets
        currentFilters={currentFilters}
        onApplyPreset={onApplyPreset}
      />
      {hasActiveFilter && (
        <Button variant="ghost" size="sm" onClick={onClearFilters} className="text-muted-foreground">
          <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
          Temizle
        </Button>
      )}
      <Button
        variant={showFilters ? 'default' : 'outline'}
        size="sm"
        onClick={onToggleFilters}
      >
        <Filter className="h-3.5 w-3.5 mr-1.5" />
        Filtreler
        {hasActiveFilter && (
          <span className="ml-1.5 flex h-4 w-4 items-center justify-center rounded-full bg-white/20 text-[10px] font-bold">
            {activeFilterCount}
          </span>
        )}
      </Button>
    </div>
  );
}
