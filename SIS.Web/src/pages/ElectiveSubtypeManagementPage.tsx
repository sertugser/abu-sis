import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { AlertCircle, FolderOpen, Loader2, Plus } from 'lucide-react';
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

  const includeInactive = statusFilter === 'all';
  const { data: subtypes, isLoading, error } = useElectiveSubtypeList(includeInactive);

  return (
    <div className="space-y-5">
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Tipler yüklenirken hata oluştu.</AlertDescription>
        </Alert>
      )}

      <div className="bg-background border border-border rounded-xl overflow-hidden">
        {/* ── Toolbar ── */}
        <div className="flex flex-wrap gap-3 items-end px-4 py-3 border-b border-border bg-muted/40">
          <div className="space-y-1">
            <span className="text-xs text-muted-foreground">Durum</span>
            <Select value={statusFilter} onValueChange={v => setStatusFilter(v as 'active' | 'all')}>
              <SelectTrigger className="w-[160px] h-9 bg-white">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="active">Sadece aktif</SelectItem>
                <SelectItem value="all">Tümü (pasif dahil)</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <Button onClick={() => setCreateOpen(true)} className="h-9 ml-auto">
            <Plus className="h-4 w-4 mr-1.5" />
            Yeni Tip
          </Button>
        </div>

        {/* ── Table ── */}
        <table className="w-full">
          <thead className="bg-muted/20 border-b border-border">
            <tr>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground w-12">Sıra</th>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground w-48">Kod</th>
              <th className="text-left px-4 py-3 text-xs font-semibold text-muted-foreground">Türkçe Ad</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-72">Scope Kuralları</th>
              <th className="text-center px-4 py-3 text-xs font-semibold text-muted-foreground w-20">Durum</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-10 text-center text-sm text-muted-foreground">
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

            {!isLoading && subtypes && subtypes.map(s => (
              <tr
                key={s.id}
                className="cursor-pointer hover:bg-muted/20 transition-colors"
                onClick={() => setEditTarget(s)}
              >
                <td className="px-4 py-3 text-sm tabular-nums text-muted-foreground">{s.sortOrder}</td>
                <td className="px-4 py-3 font-mono text-xs font-medium">{s.code}</td>
                <td className="px-4 py-3 text-sm font-medium">{s.name}</td>
                <td className="px-4 py-3">
                  <FlagBadges subtype={s} />
                </td>
                <td className="px-4 py-3 text-center">
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

        {!isLoading && subtypes && subtypes.length > 0 && (
          <div className="px-4 py-2.5 border-t border-border bg-muted/30">
            <p className="text-xs text-muted-foreground">
              Toplam <span className="font-medium text-foreground">{subtypes.length}</span> tip
            </p>
          </div>
        )}
      </div>

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
    </div>
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
