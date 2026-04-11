import { useMemo, useState } from 'react';
import { Search, Download } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useAdminFaculties, useAdminDepartmentsByStructure } from '../hooks/useAdminAcademicStructure';
import { useDepartmentInstructors } from '../hooks/useCourseOpening';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { InstructorReportRow } from '../components/instructor-report/InstructorReportRow';

type SortKey = 'name' | 'hours' | 'courses' | 'sections';
type SortDir = 'asc' | 'desc';

export default function InstructorCoursesReportPage() {
  const { data: faculties = [] } = useAdminFaculties();
  const [selectedFacultyId, setSelectedFacultyId] = useState<number | ''>('');
  const [selectedDeptId, setSelectedDeptId] = useState<number | ''>('');
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDir, setSortDir] = useState<SortDir>('asc');

  const { data: allDepartments = [] } = useAdminDepartmentsByStructure(
    selectedFacultyId !== '' ? selectedFacultyId : undefined,
  );
  const { data: activeTerm } = useActiveTerm();
  const termId = activeTerm?.id ?? null;

  const deptId = selectedDeptId !== '' ? selectedDeptId : null;
  const { data: instructors = [], isLoading } = useDepartmentInstructors(
    deptId,
  );

  // Instructor listesi sadece { instructorId, fullName } içeriyor, bölüm adını ekle
  const selectedDeptName = useMemo(() => {
    if (selectedDeptId === '') return '';
    return allDepartments.find(d => d.id === selectedDeptId)?.name ?? '';
  }, [allDepartments, selectedDeptId]);

  const enrichedInstructors = useMemo(
    () => instructors.map(i => ({ ...i, departmentName: selectedDeptName })),
    [instructors, selectedDeptName],
  );

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q
      ? enrichedInstructors.filter(i => i.fullName.toLowerCase().includes(q))
      : enrichedInstructors;
  }, [enrichedInstructors, search]);

  // Sıralama: workload verileri row içinde lazy-fetch edildiğinden
  // name dışındaki kriterler henüz yüklenmiş veri olmayabilir; name sıralaması her zaman çalışır.
  const sorted = useMemo(() => {
    return [...filtered].sort((a, b) => {
      let cmp = 0;
      if (sortKey === 'name') cmp = a.fullName.localeCompare(b.fullName, 'tr');
      // Diğer kriterler için workload lazım; adı fallback olarak kullan
      if (cmp === 0) cmp = a.fullName.localeCompare(b.fullName, 'tr');
      return sortDir === 'asc' ? cmp : -cmp;
    });
  }, [filtered, sortKey, sortDir]);

  function toggleSort(key: SortKey) {
    if (sortKey === key) setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    else { setSortKey(key); setSortDir('asc'); }
  }

  function sortIcon(key: SortKey) {
    if (sortKey !== key) return ' ↕';
    return sortDir === 'asc' ? ' ↑' : ' ↓';
  }

  function exportCsv() {
    if (!sorted.length) return;
    const rows = sorted.map(i =>
      `"${i.fullName}","${selectedDeptName}","${i.instructorId}"`,
    );
    const csv = 'Ad Soyad,Bölüm,Personel ID\n' + rows.join('\n');
    const url = URL.createObjectURL(new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' }));
    Object.assign(document.createElement('a'), { href: url, download: 'ders-veren-ogretim-uyeleri.csv' }).click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="p-6 space-y-5">
      <div className="flex flex-wrap gap-3 items-center bg-white border border-border rounded-xl p-4">
        <select
          value={selectedFacultyId}
          onChange={e => {
            setSelectedFacultyId(e.target.value !== '' ? Number(e.target.value) : '');
            setSelectedDeptId('');
          }}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
        >
          <option value="">Tüm Fakülteler</option>
          {faculties.map(f => (
            <option key={f.id} value={f.id}>{f.name}</option>
          ))}
        </select>

        <select
          value={selectedDeptId}
          onChange={e => setSelectedDeptId(e.target.value !== '' ? Number(e.target.value) : '')}
          className="border border-border rounded-lg px-3 py-2 text-sm bg-white text-foreground"
          disabled={allDepartments.length === 0}
        >
          <option value="">Tüm Bölümler</option>
          {allDepartments.map(d => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>

        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Öğretim üyesi adı ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>

        <Button variant="outline" size="sm" onClick={exportCsv} disabled={sorted.length === 0}>
          <Download className="h-4 w-4 mr-2" />
          CSV İndir
        </Button>
      </div>

      {!deptId && !isLoading && (
        <div className="text-center py-12 text-muted-foreground text-sm">
          Listeyi görmek için bir bölüm seçin.
        </div>
      )}

      {deptId && (
        <div className="bg-white border border-border rounded-xl overflow-hidden overflow-x-auto">
          <table className="w-full text-sm min-w-[700px]">
            <thead className="bg-muted/40 border-b border-border">
              <tr>
                <th className="px-4 py-3 w-8" />
                <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Unvan</th>
                <th
                  className="text-left px-4 py-3 font-semibold text-muted-foreground cursor-pointer hover:text-foreground"
                  onClick={() => toggleSort('name')}
                >
                  Ad Soyad{sortIcon('name')}
                </th>
                <th className="text-left px-4 py-3 font-semibold text-muted-foreground">Bölüm</th>
                <th
                  className="text-left px-4 py-3 font-semibold text-muted-foreground cursor-pointer hover:text-foreground"
                  onClick={() => toggleSort('hours')}
                >
                  Haftalık Saat{sortIcon('hours')}
                </th>
                <th
                  className="text-center px-4 py-3 font-semibold text-muted-foreground cursor-pointer hover:text-foreground"
                  onClick={() => toggleSort('courses')}
                >
                  Ders Sayısı{sortIcon('courses')}
                </th>
                <th
                  className="text-center px-4 py-3 font-semibold text-muted-foreground cursor-pointer hover:text-foreground"
                  onClick={() => toggleSort('sections')}
                >
                  Şube Sayısı{sortIcon('sections')}
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    <td colSpan={7} className="px-4 py-3">
                      <Skeleton className="h-5 w-full" />
                    </td>
                  </tr>
                ))
              ) : sorted.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">
                    {search ? 'Arama sonucu bulunamadı.' : 'Bu bölümde öğretim üyesi bulunmuyor.'}
                  </td>
                </tr>
              ) : (
                sorted.map(instructor => (
                  <InstructorReportRow
                    key={instructor.instructorId}
                    instructor={instructor}
                    termId={termId ?? 0}
                  />
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
