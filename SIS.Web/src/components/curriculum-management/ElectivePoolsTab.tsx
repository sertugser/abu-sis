import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Loader2, Package, Pencil, Plus, Trash2, Check, X } from 'lucide-react';
import {
  useCurriculumPools,
  useUpdatePool,
  useDeletePool,
} from '../../hooks/useCurriculumManagement';
import { useElectiveSubtypeList } from '../../hooks/useElectiveSubtypes';
import { findSubtypeById } from '../../lib/electiveSubtypeUtils';
import type { CurriculumElectivePoolDto } from '../../types/curriculumManagement';
import AddPoolDialog from './AddPoolDialog';

interface Props {
  curriculumId: number;
}

export default function ElectivePoolsTab({ curriculumId }: Props) {
  const { data: pools, isLoading } = useCurriculumPools(curriculumId);
  const { data: subtypes } = useElectiveSubtypeList(false);
  const updatePool = useUpdatePool();
  const deletePool = useDeletePool();

  const [showAddDialog, setShowAddDialog] = useState(false);
  const [editingPoolId, setEditingPoolId] = useState<number | null>(null);
  const [editValues, setEditValues] = useState({
    suggestedSemesterStart: 1,
    suggestedSemesterEnd: 8,
    requiredCourseCount: 1,
    requiredEcts: 5,
  });

  function startEdit(pool: CurriculumElectivePoolDto) {
    setEditingPoolId(pool.id);
    setEditValues({
      suggestedSemesterStart: pool.suggestedSemesterStart,
      suggestedSemesterEnd: pool.suggestedSemesterEnd,
      requiredCourseCount: pool.requiredCourseCount,
      requiredEcts: pool.requiredEcts,
    });
  }

  function handleSaveEdit(poolId: number) {
    updatePool.mutate(
      { curriculumId, poolId, ...editValues },
      { onSuccess: () => setEditingPoolId(null) },
    );
  }

  function handleDelete(poolId: number) {
    deletePool.mutate({ curriculumId, poolId });
  }

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {[1, 2, 3].map(i => <Skeleton key={i} className="h-40" />)}
      </div>
    );
  }

  if (!pools || pools.length === 0) {
    return (
      <Card>
        <CardContent className="py-12 text-center text-muted-foreground">
          <Package className="h-12 w-12 mx-auto mb-3 opacity-30" />
          <p className="mb-3">Henuz secmeli havuzu eklenmemis</p>
          <Button variant="outline" onClick={() => setShowAddDialog(true)}>
            <Plus className="h-4 w-4 mr-1" /> Havuz Ekle
          </Button>
          <AddPoolDialog
            curriculumId={curriculumId}
            open={showAddDialog}
            onOpenChange={setShowAddDialog}
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setShowAddDialog(true)}>
          <Plus className="h-3.5 w-3.5 mr-1" /> Havuz Ekle
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {pools.map(pool => {
          const isEditing = editingPoolId === pool.id;
          const poolSubtype = findSubtypeById(subtypes, pool.electiveSubtypeId);

          return (
            <Card key={pool.id} className="relative">
              <CardContent className="pt-4 pb-4 space-y-3">
                {/* Header: name + badge */}
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-semibold text-sm leading-tight">{pool.groupName}</p>
                    <p className="text-xs text-muted-foreground font-mono">{pool.groupCode}</p>
                  </div>
                  {poolSubtype && (
                    <Badge variant="outline" className="text-xs shrink-0" title={poolSubtype.name}>
                      {poolSubtype.name}
                    </Badge>
                  )}
                </div>

                {isEditing ? (
                  /* Edit mode */
                  <div className="space-y-2">
                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1">
                        <Label className="text-[10px]">Baslangic Yariyili</Label>
                        <Input
                          type="number"
                          min={1}
                          max={8}
                          value={editValues.suggestedSemesterStart}
                          onChange={e => setEditValues(v => ({ ...v, suggestedSemesterStart: Number(e.target.value) }))}
                          className="h-7 text-xs"
                        />
                      </div>
                      <div className="space-y-1">
                        <Label className="text-[10px]">Bitis Yariyili</Label>
                        <Input
                          type="number"
                          min={1}
                          max={8}
                          value={editValues.suggestedSemesterEnd}
                          onChange={e => setEditValues(v => ({ ...v, suggestedSemesterEnd: Number(e.target.value) }))}
                          className="h-7 text-xs"
                        />
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div className="space-y-1">
                        <Label className="text-[10px]">Ders Sayisi</Label>
                        <Input
                          type="number"
                          min={1}
                          value={editValues.requiredCourseCount}
                          onChange={e => setEditValues(v => ({ ...v, requiredCourseCount: Number(e.target.value) }))}
                          className="h-7 text-xs"
                        />
                      </div>
                      <div className="space-y-1">
                        <Label className="text-[10px]">AKTS</Label>
                        <Input
                          type="number"
                          min={1}
                          value={editValues.requiredEcts}
                          onChange={e => setEditValues(v => ({ ...v, requiredEcts: Number(e.target.value) }))}
                          className="h-7 text-xs"
                        />
                      </div>
                    </div>
                    <div className="flex items-center gap-1 pt-1">
                      <Button
                        size="sm"
                        className="h-7 text-xs"
                        onClick={() => handleSaveEdit(pool.id)}
                        disabled={updatePool.isPending}
                      >
                        {updatePool.isPending
                          ? <Loader2 className="h-3 w-3 animate-spin mr-1" />
                          : <Check className="h-3 w-3 mr-1" />}
                        Kaydet
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 text-xs"
                        onClick={() => setEditingPoolId(null)}
                      >
                        <X className="h-3 w-3 mr-1" /> Iptal
                      </Button>
                    </div>
                  </div>
                ) : (
                  /* Read mode */
                  <>
                    <div className="space-y-1 text-xs text-muted-foreground">
                      <p>Gerekli: <span className="text-foreground font-medium">{pool.requiredCourseCount} ders, {pool.requiredEcts} AKTS</span></p>
                      <p>Donem araligi: <span className="text-foreground font-medium">{pool.suggestedSemesterStart}-{pool.suggestedSemesterEnd}. donem</span></p>
                      <p>Havuzdaki ders sayisi: <span className="text-foreground font-medium">{pool.poolCourseCount}</span></p>
                    </div>
                    <div className="flex items-center gap-1 pt-1">
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 text-xs"
                        onClick={() => startEdit(pool)}
                      >
                        <Pencil className="h-3 w-3 mr-1" /> Duzenle
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 text-xs text-destructive hover:text-destructive"
                        onClick={() => handleDelete(pool.id)}
                        disabled={deletePool.isPending}
                      >
                        {deletePool.isPending
                          ? <Loader2 className="h-3 w-3 animate-spin mr-1" />
                          : <Trash2 className="h-3 w-3 mr-1" />}
                        Sil
                      </Button>
                    </div>
                  </>
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <AddPoolDialog
        curriculumId={curriculumId}
        open={showAddDialog}
        onOpenChange={setShowAddDialog}
      />
    </div>
  );
}
