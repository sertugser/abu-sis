import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebounce } from './useDebounce';
import { DEFAULT_PAGE_SIZE } from '../components/audit-log/audit-log.constants';

const FILTER_KEYS = ['entityType', 'entityId', 'userId', 'action', 'search', 'from', 'to'] as const;
type FilterKey = typeof FILTER_KEYS[number];

export function useAuditLogFilters() {
  const [searchParams, setSearchParams] = useSearchParams();

  const get = useCallback((key: string) => searchParams.get(key) ?? '', [searchParams]);

  const filters = useMemo(() => ({
    entityType: get('entityType'),
    entityId: get('entityId'),
    userId: get('userId'),
    action: get('action'),
    search: get('search'),
    from: get('from'),
    to: get('to'),
    sortBy: get('sortBy') || 'Timestamp',
    sortDir: (get('sortDir') || 'desc') as 'asc' | 'desc',
    page: Number(get('page')) || 1,
    pageSize: Number(get('pageSize')) || DEFAULT_PAGE_SIZE,
  }), [get]);

  // Debounce text inputs
  const debouncedUserId = useDebounce(filters.userId, 400);
  const debouncedEntityId = useDebounce(filters.entityId, 400);
  const debouncedSearch = useDebounce(filters.search, 400);

  const setFilter = useCallback((key: string, value: string) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev);
      if (value) {
        next.set(key, value);
      } else {
        next.delete(key);
      }
      // Reset to page 1 when changing filters (not page/pageSize)
      if (key !== 'page' && key !== 'pageSize') {
        next.delete('page');
      }
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const setPage = useCallback((page: number) => setFilter('page', page > 1 ? String(page) : ''), [setFilter]);
  const setPageSize = useCallback((size: number) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev);
      next.set('pageSize', String(size));
      next.delete('page');
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const setSortBy = useCallback((column: string) => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev);
      const currentSortBy = next.get('sortBy') || 'Timestamp';
      const currentDir = next.get('sortDir') || 'desc';

      if (currentSortBy === column) {
        next.set('sortDir', currentDir === 'asc' ? 'desc' : 'asc');
      } else {
        next.set('sortBy', column);
        next.set('sortDir', 'desc');
      }
      next.delete('page');
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const clearFilters = useCallback(() => {
    setSearchParams({}, { replace: true });
  }, [setSearchParams]);

  const applyPreset = useCallback((preset: Record<string, string>) => {
    setSearchParams(prev => {
      const next = new URLSearchParams();
      // Keep non-filter params (pageSize, sortBy, sortDir)
      const keep = ['pageSize', 'sortBy', 'sortDir'];
      keep.forEach(k => { const v = prev.get(k); if (v) next.set(k, v); });
      // Apply preset filters
      Object.entries(preset).forEach(([k, v]) => { if (v) next.set(k, v); });
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const hasActiveFilter = FILTER_KEYS.some(k => !!filters[k]);
  const activeFilterCount = FILTER_KEYS.filter(k => !!filters[k]).length;

  // API filters use debounced text values
  const apiFilters = useMemo(() => ({
    entityType: filters.entityType || undefined,
    entityId: debouncedEntityId || undefined,
    userId: debouncedUserId || undefined,
    action: filters.action || undefined,
    search: debouncedSearch || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    sortBy: filters.sortBy,
    sortDir: filters.sortDir,
    page: filters.page,
    pageSize: filters.pageSize,
  }), [filters, debouncedUserId, debouncedEntityId, debouncedSearch]);

  return {
    filters,
    apiFilters,
    hasActiveFilter,
    activeFilterCount,
    setFilter,
    setPage,
    setPageSize,
    setSortBy,
    clearFilters,
    applyPreset,
  };
}
