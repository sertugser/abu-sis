import { useEffect, useMemo, useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  AlertCircle, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight,
  FolderOpen, Loader2, Plus,
} from 'lucide-react';

const PAGE_SIZE = 10;
import { useElectiveSubtypeList } from '../hooks/useElectiveSubtypes';
import type { ElectiveSubtypeDto } from '../types/electiveSubtype';
import ElectiveSubtypeFormDialog from '../components/elective-subtype/ElectiveSubtypeFormDialog';

/**
 * Faz B B3c — DB-driven seçmeli ders tipi yönetim sayfası.
 * Backend: SIS.API/Controllers/ElectiveSubtypeController.cs
 * Karar #14: Code immutable; Name + flag'ler + SortOrder + IsActive editable.
 * Hard delete YOK — sadece IsActive=false (soft delete).
 */
export default function ElectiveSubtypeManagementPage() {
  const [statusFilter, setStatusFilter] = useState<'active' | 'all'>('active');
  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ElectiveSubtypeDto | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  const includeInactive = statusFilter === 'all';
  const { data: subtypes, isLoading, error } = useElectiveSubtypeList(includeInactive);

  const totalCount = subtypes?.length ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const pagedSubtypes = useMemo(
    () => (subtypes ?? []).slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE),
    [subtypes, currentPage],
  );

  useEffect(() => {
    setCurrentPage(1);
  }, [statusFilter]);

  useEffect(() => {
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);

  return (
    <>
      {error && (
        <div className="px-4 pt-4">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>Tipler yüklenirken hata oluştu.</AlertDescription>
          </Alert>
        </div>
      )}

      {/* ── Toolbar — simetrik: dropdown 200px, padding px-4 py-4, buton h-10 ── */}
      <div className="flex flex-col gap-3 px-4 py-4 border-b border-border/80 bg-card sm:flex-row sm:flex-wrap sm:items-end">
        <div className="space-y-1.5 w-full sm:w-[200px]">
          <span className="text-xs font-medium text-muted-foreground">Durum</span>
          <Select value={statusFilter} onValueChange={v => setStatusFilter(v as 'active' | 'all')}>
            <SelectTrigger className="w-full h-10 bg-white text-sm">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="active">Sadece aktif</SelectItem>
              <SelectItem value="all">Tümü (pasif dahil)</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Button onClick={() => setCreateOpen(true)} className="h-10 w-full sm:w-auto sm:ml-auto">
          <Plus className="h-4 w-4 mr-1.5" />
          Yeni Tip
        </Button>
      </div>

      {/* ── Table — kolonlar 8'in katlarında, tüm padding px-4 py-3.5 ── */}
      <div className="overflow-x-auto min-h-[484px]">
        <table className="w-full min-w-[720px]">
          <thead className="bg-card border-b border-border/80">
            <tr>
              <th className="text-left px-4 py-3.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground w-16">Sıra</th>
              <th className="text-left px-4 py-3.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground w-48">Kod</th>
              <th className="text-left px-4 py-3.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Türkçe Ad</th>
              <th className="text-center px-4 py-3.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground w-72">Scope Kuralları</th>
              <th className="text-center px-4 py-3.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground w-24">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/80">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-12 text-center text-sm text-muted-foreground">
                  <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2 text-muted-foreground" />
                  Yükleniyor...
                </td>
              </tr>
            )}

            {!isLoading && subtypes && subtypes.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-16 text-center">
                  <FolderOpen className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-sm font-medium text-foreground">
                    {includeInactive ? 'Hiç seçmeli tipi bulunamadı' : 'Aktif seçmeli tipi yok'}
                  </p>
                  <p className="text-xs text-muted-foreground mt-1">
                    Yukarıdaki "Yeni Tip" butonunu kullanarak ilk tipi ekleyin.
                  </p>
                </td>
              </tr>
            )}

            {!isLoading && subtypes && pagedSubtypes.map(s => (
              <tr
                key={s.id}
                className="cursor-pointer hover:bg-primary/5 transition-colors"
                onClick={() => setEditTarget(s)}
              >
                <td className="px-4 py-3.5 text-sm tabular-nums text-muted-foreground">{s.sortOrder}</td>
                <td className="px-4 py-3.5 font-mono text-xs font-medium">{s.code}</td>
                <td className="px-4 py-3.5 text-sm font-medium">{s.name}</td>
                <td className="px-4 py-3.5">
                  <FlagBadges subtype={s} />
                </td>
                <td className="px-4 py-3.5 text-center">
                  <Badge
                    variant="outline"
                    className={
                      s.isActive
                        ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                        : 'bg-red-50 text-red-600 border-red-200'
                    }
                  >
                    {s.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* ── Footer + Pagination — simetrik: px-4 py-3, butonlar h-9 ── */}
      {!isLoading && subtypes && subtypes.length > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 border-t border-border/80 bg-card">
          <p className="text-xs text-muted-foreground">
            Toplam <span className="font-medium text-foreground">{totalCount}</span> tip
            {totalPages > 1 && (
              <> — Sayfa <span className="font-medium text-foreground">{currentPage}</span>/{totalPages}</>
            )}
          </p>
          {totalPages > 1 && (
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="icon" className="h-9 w-9 hover:bg-primary/5 hover:text-primary" disabled={currentPage === 1} onClick={() => setCurrentPage(1)}>
                <ChevronsLeft className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="icon" className="h-9 w-9 hover:bg-primary/5 hover:text-primary" disabled={currentPage === 1} onClick={() => setCurrentPage(p => Math.max(1, p - 1))}>
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span className="px-3 min-w-[2.5rem] text-center text-sm font-medium">{currentPage}</span>
              <Button variant="ghost" size="icon" className="h-9 w-9 hover:bg-primary/5 hover:text-primary" disabled={currentPage === totalPages} onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}>
                <ChevronRight className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="icon" className="h-9 w-9 hover:bg-primary/5 hover:text-primary" disabled={currentPage === totalPages} onClick={() => setCurrentPage(totalPages)}>
                <ChevronsRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </div>
      )}

      <ElectiveSubtypeFormDialog
        mode="create"
        open={createOpen}
        onOpenChange={setCreateOpen}
        subtype={null}
      />
      <ElectiveSubtypeFormDialog
        key={editTarget?.id ?? 'edit-closed'}
        mode="edit"
        open={editTarget !== null}
        onOpenChange={open => { if (!open) setEditTarget(null); }}
        subtype={editTarget}
      />
    </>
  );
}

// ─── Flag rozet kümesi ──────────────────────────────────────────────

function FlagBadges({ subtype }: { subtype: ElectiveSubtypeDto }) {
  const items: { label: string; on: boolean; tone: 'req' | 'forbid' }[] = [
    { label: 'Fak. zor.', on: subtype.requiresFaculty,    tone: 'req' },
    { label: 'Fak. yasak', on: subtype.forbidsFaculty,    tone: 'forbid' },
    { label: 'Böl. zor.', on: subtype.requiresDepartment, tone: 'req' },
    { label: 'Böl. yasak', on: subtype.forbidsDepartment, tone: 'forbid' },
  ];

  const active = items.filter(i => i.on);
  if (active.length === 0) {
    return <span className="text-[10px] text-muted-foreground italic">Kısıtsız</span>;
  }

  return (
    <div className="flex flex-wrap gap-1 justify-center">
      {active.map(i => (
        <Badge
          key={i.label}
          variant="outline"
          className={
            i.tone === 'req'
              ? 'text-[10px] bg-blue-50 text-blue-700 border-blue-200'
              : 'text-[10px] bg-rose-50 text-rose-700 border-rose-200'
          }
        >
          {i.label}
        </Badge>
      ))}
    </div>
  );
}
