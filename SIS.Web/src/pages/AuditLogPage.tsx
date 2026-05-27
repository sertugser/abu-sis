import { useState, useCallback } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Sheet, SheetContent,
} from '@/components/ui/sheet';
import { AlertCircle, RotateCcw } from 'lucide-react';

import { useAuditLogs } from '../hooks/useAuditLog';
import { useAuditLogFilters } from '../hooks/useAuditLogFilters';
import { exportAuditLogsCsv, type AuditLogSummaryDto } from '../api/auditLog.api';
import { downloadBlob } from '../lib/download';
import { getBlobErrorMessage } from '../components/audit-log/audit-log.utils';

import { AnomalyBanner } from '../components/audit-log/AnomalyBanner';
import { AuditLogToolbar } from '../components/audit-log/AuditLogToolbar';
import { AuditLogFilterBar } from '../components/audit-log/AuditLogFilterBar';
import { AuditLogTable } from '../components/audit-log/AuditLogTable';
import { AuditLogPagination } from '../components/audit-log/AuditLogPagination';
import { AuditLogDetailPanel } from '../components/audit-log/AuditLogDetailPanel';

export default function AuditLogPage() {
  const {
    filters, apiFilters, hasActiveFilter, activeFilterCount,
    setFilter, setPage, setPageSize, setSortBy, clearFilters, applyPreset,
  } = useAuditLogFilters();

  const [showFilters, setShowFilters] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  // Selected log for detail panel: store id + summary for instant header display
  const [selectedLogId, setSelectedLogId] = useState<number | null>(null);
  const [selectedSummary, setSelectedSummary] = useState<AuditLogSummaryDto | null>(null);

  const { data, isLoading, isFetching, isError, refetch } = useAuditLogs(apiFilters);

  const totalPages = data ? Math.ceil(data.totalCount / apiFilters.pageSize!) : 0;

  const handleViewDetail = useCallback((logId: number) => {
    const item = data?.items.find(i => i.id === logId);
    if (item) setSelectedSummary(item);
    setSelectedLogId(logId);
  }, [data]);

  const handleViewEntityHistory = useCallback((entityType: string, entityId: string) => {
    setSelectedLogId(null);
    setSelectedSummary(null);
    // applyPreset diğer aktif filtreleri (action/userId/search/tarih) temizler ve yalnızca bu kaydı
    // hedefler — aksi halde "tüm geçmiş" mevcut filtrelerle kısıtlı kalıp yanıltıcı olurdu.
    applyPreset({ entityType, entityId });
    setShowFilters(true);
  }, [applyPreset]);

  async function handleExportCsv() {
    setExportError(null);
    setIsExporting(true);
    try {
      const file = await exportAuditLogsCsv(apiFilters);
      downloadBlob(file, 'audit-logs.csv');
    } catch (error) {
      setExportError(await getBlobErrorMessage(error, 'Denetim kayıtları CSV olarak indirilemedi.'));
    } finally {
      setIsExporting(false);
    }
  }

  // Current filter values for presets
  const currentFilterValues: Record<string, string> = {
    entityType: filters.entityType,
    entityId: filters.entityId,
    userId: filters.userId,
    action: filters.action,
    search: filters.search,
    from: filters.from,
    to: filters.to,
  };

  return (
    <div className="space-y-5 animate-fade-in">
      {/* Anomaly alerts */}
      <AnomalyBanner />

      {/* Tek yüzey: başlık + araç çubuğu + filtreler + tablo + sayfalama aynı kartta */}
      <Card className="overflow-hidden">
        {/* Başlık çubuğu — araç çubuğu butonları burada, tabloyla bağlı durur */}
        <div className="flex flex-col gap-3 border-b bg-muted/30 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-sm font-semibold text-foreground">Denetim Kayıtları</h2>
            <p className="text-xs text-muted-foreground">Sistemdeki kritik işlemlerin denetim kaydı</p>
          </div>
          <AuditLogToolbar
            isExporting={isExporting}
            isFetching={isFetching}
            hasActiveFilter={hasActiveFilter}
            activeFilterCount={activeFilterCount}
            showFilters={showFilters}
            currentFilters={currentFilterValues}
            onToggleFilters={() => setShowFilters(f => !f)}
            onClearFilters={clearFilters}
            onExportCsv={handleExportCsv}
            onRefresh={() => refetch()}
            onApplyPreset={applyPreset}
          />
        </div>

        {/* Export progress */}
        {isExporting && (
          <div className="h-1 w-full bg-muted overflow-hidden">
            <div className="h-full bg-primary animate-pulse" style={{ width: '100%' }} />
          </div>
        )}

        {/* Export error */}
        {exportError && (
          <div className="border-b px-4 py-3">
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{exportError}</AlertDescription>
            </Alert>
          </div>
        )}

        {/* Filters */}
        {showFilters && (
          <div className="border-b bg-muted/10">
            <AuditLogFilterBar
              entityType={filters.entityType}
              action={filters.action}
              userId={filters.userId}
              entityId={filters.entityId}
              search={filters.search}
              from={filters.from}
              to={filters.to}
              onEntityTypeChange={v => setFilter('entityType', v)}
              onActionChange={v => setFilter('action', v)}
              onUserIdChange={v => setFilter('userId', v)}
              onEntityIdChange={v => setFilter('entityId', v)}
              onSearchChange={v => setFilter('search', v)}
              onFromChange={v => setFilter('from', v)}
              onToChange={v => setFilter('to', v)}
              onApply={() => setPage(1)}
            />
          </div>
        )}

        {/* Results */}
        {isLoading ? (
          <div className="space-y-3 p-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-16 rounded-lg" />
            ))}
          </div>
        ) : isError ? (
          <div className="p-4">
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription className="flex items-center gap-3">
                Denetim kayıtları yüklenemedi. Lütfen tekrar deneyin.
                <Button variant="outline" size="sm" onClick={() => refetch()}>
                  <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
                  Tekrar Dene
                </Button>
              </AlertDescription>
            </Alert>
          </div>
        ) : (
          <AuditLogTable
            items={data?.items ?? []}
            onViewDetail={handleViewDetail}
            sortBy={filters.sortBy}
            sortDir={filters.sortDir as 'asc' | 'desc'}
            onSortChange={setSortBy}
            footer={
              <AuditLogPagination
                page={apiFilters.page!}
                pageSize={apiFilters.pageSize!}
                totalCount={data?.totalCount ?? 0}
                totalPages={totalPages}
                hasActiveFilter={hasActiveFilter}
                onPageChange={setPage}
                onPageSizeChange={setPageSize}
              />
            }
          />
        )}
      </Card>

      {/* Detail Sheet */}
      <Sheet open={!!selectedLogId} onOpenChange={open => { if (!open) { setSelectedLogId(null); setSelectedSummary(null); } }}>
        <SheetContent side="right" className="w-full sm:max-w-lg overflow-y-auto">
          {selectedLogId && selectedSummary && (
            <AuditLogDetailPanel
              logId={selectedLogId}
              summary={selectedSummary}
              onViewEntityHistory={handleViewEntityHistory}
            />
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}
