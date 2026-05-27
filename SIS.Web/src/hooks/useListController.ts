import { useMemo, useState } from 'react';

export type SortDir = 'asc' | 'desc';

interface UseListControllerOptions<TItem, TSortKey extends string> {
  /** Ham liste (server'dan gelen). */
  items: TItem[];
  initialSortKey: TSortKey;
  initialSortDir?: SortDir;
  defaultPageSize?: number;
  /** Arama sorgusu (lowercase, trim'li) bir öğeyle eşleşiyor mu. */
  matchesSearch: (item: TItem, query: string) => boolean;
  /** İki öğeyi verilen anahtara göre artan yönde karşılaştırır (yön hook'ta uygulanır). */
  compare: (a: TItem, b: TItem, sortKey: TSortKey) => number;
}

/**
 * Liste sayfaları (Fakülte / Bölüm / Program) için ortak, başsız (headless) kontrolör.
 * Arama + sıralama + sayfalama state'ini ve türetilmiş `visible`/`paged` dizilerini
 * yönetir. Sayfa-özel mantık (hangi alanlar aranır, nasıl karşılaştırılır) callback
 * olarak verilir; görünüm/kolon/detay sayfada kalır.
 *
 * Arama/sıralama/sayfa-boyutu değişince otomatik 1. sayfaya döner (effect yok).
 */
export function useListController<TItem, TSortKey extends string>(
  opts: UseListControllerOptions<TItem, TSortKey>,
) {
  const {
    items,
    initialSortKey,
    initialSortDir = 'asc',
    defaultPageSize = 15,
    matchesSearch,
    compare,
  } = opts;

  const [search, setSearchRaw] = useState('');
  const [sortKey, setSortKey] = useState<TSortKey>(initialSortKey);
  const [sortDir, setSortDir] = useState<SortDir>(initialSortDir);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);

  function setSearch(value: string) {
    setSearchRaw(value);
    setPage(1);
  }

  function changePageSize(size: number) {
    setPageSize(size);
    setPage(1);
  }

  function toggleSort(key: TSortKey) {
    if (sortKey === key) setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    else {
      setSortKey(key);
      setSortDir('asc');
    }
    setPage(1);
  }

  function clearAll() {
    setSearchRaw('');
    setPage(1);
  }

  const hasAnyConstraint = search.trim().length > 0;

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    const result = q ? items.filter(i => matchesSearch(i, q)) : items;
    return [...result].sort((a, b) => {
      const cmp = compare(a, b, sortKey);
      return sortDir === 'asc' ? cmp : -cmp;
    });
  }, [items, search, sortKey, sortDir, matchesSearch, compare]);

  const totalPages = Math.max(1, Math.ceil(visible.length / pageSize));
  const safePage = Math.min(page, totalPages);
  const paged = useMemo(
    () => visible.slice((safePage - 1) * pageSize, safePage * pageSize),
    [visible, safePage, pageSize],
  );

  return {
    search,
    setSearch,
    sortKey,
    sortDir,
    toggleSort,
    page,
    setPage,
    pageSize,
    changePageSize,
    safePage,
    totalPages,
    hasAnyConstraint,
    clearAll,
    visible,
    paged,
  };
}
