import { useState } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { AlertCircle, Edit2, Plus, Trash2 } from 'lucide-react';
import { usePromotionRules, useCreatePromotionRule, useUpdatePromotionRule, useDeletePromotionRule } from '../hooks/usePromotionRule';
import { useAdminPrograms } from '../hooks/useAdminLookup';
import type { PromotionRuleDto } from '../api/promotionRule.api';

interface RuleForm {
  fromYear: number;
  toYear: number;
  minCgpa: number;
  minEcts: number;
  minCourseCount: number;
}

const emptyForm: RuleForm = { fromYear: 1, toYear: 2, minCgpa: 1.80, minEcts: 60, minCourseCount: 0 };

export default function PromotionRulePage() {
  const { data: programs = [], isLoading: programsLoading } = useAdminPrograms();
  const [selectedProgramId, setSelectedProgramId] = useState<number | undefined>();
  const programId = selectedProgramId ?? programs[0]?.id;

  const { data: rules, isLoading, isError } = usePromotionRules(programId);
  const createMutation = useCreatePromotionRule();
  const updateMutation = useUpdatePromotionRule();
  const deleteMutation = useDeletePromotionRule();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<PromotionRuleDto | null>(null);
  const [form, setForm] = useState<RuleForm>(emptyForm);
  const [error, setError] = useState('');

  const openCreate = () => {
    setEditingRule(null);
    setForm(emptyForm);
    setError('');
    setDialogOpen(true);
  };

  const openEdit = (rule: PromotionRuleDto) => {
    setEditingRule(rule);
    setForm({
      fromYear: rule.fromYear,
      toYear: rule.toYear,
      minCgpa: rule.minCgpa,
      minEcts: rule.minEcts,
      minCourseCount: rule.minCourseCount,
    });
    setError('');
    setDialogOpen(true);
  };

  const handleSubmit = () => {
    setError('');
    if (form.fromYear >= form.toYear) {
      setError('Hedef sinif, kaynak siniftan buyuk olmalidir.');
      return;
    }

    if (editingRule) {
      updateMutation.mutate(
        { id: editingRule.id, dto: form },
        {
          onSuccess: () => setDialogOpen(false),
          onError: () => setError('Guncelleme basarisiz oldu.'),
        },
      );
    } else {
      if (!programId) return;
      createMutation.mutate(
        { ...form, programId },
        {
          onSuccess: () => setDialogOpen(false),
          onError: () => setError('Olusturma basarisiz oldu.'),
        },
      );
    }
  };

  const handleDelete = (id: number) => {
    if (confirm('Bu kurali silmek istediginize emin misiniz?')) {
      deleteMutation.mutate(id);
    }
  };

  if (programsLoading) return <Skeleton className="h-96 rounded-xl" />;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">Sinif Atlatma Kurallari</h1>
        <Button size="sm" onClick={openCreate} disabled={!programId}>
          <Plus className="h-4 w-4 mr-1" /> Yeni Kural
        </Button>
      </div>

      {/* Program selector */}
      <div className="flex items-center gap-3">
        <label className="text-sm font-medium">Program:</label>
        <select
          className="h-9 rounded-md border border-input bg-background px-3 text-sm min-w-[300px]"
          value={programId ?? ''}
          onChange={e => setSelectedProgramId(Number(e.target.value))}
        >
          {programs.map(p => (
            <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
          ))}
        </select>
      </div>

      {/* Rules table */}
      {isLoading ? (
        <Skeleton className="h-48 rounded-xl" />
      ) : isError ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Kurallar yuklenemedi.</AlertDescription>
        </Alert>
      ) : (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">
              {programs.find(p => p.id === programId)?.name ?? 'Program'} — {(rules ?? []).length} kural
            </CardTitle>
          </CardHeader>
          <CardContent>
            {(rules ?? []).length === 0 ? (
              <div className="text-center py-8 text-muted-foreground">
                Bu program icin henuz sinif atlatma kurali tanimlanmamis.
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Gecis</TableHead>
                    <TableHead>Min GANO</TableHead>
                    <TableHead>Min AKTS</TableHead>
                    <TableHead>Min Ders Sayisi</TableHead>
                    <TableHead className="w-24">Islemler</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(rules ?? []).map(rule => (
                    <TableRow key={rule.id}>
                      <TableCell>
                        <Badge variant="outline">
                          {rule.fromYear}. Sinif &rarr; {rule.toYear}. Sinif
                        </Badge>
                      </TableCell>
                      <TableCell className="font-mono">{rule.minCgpa.toFixed(2)}</TableCell>
                      <TableCell className="font-mono">{rule.minEcts}</TableCell>
                      <TableCell className="font-mono">{rule.minCourseCount}</TableCell>
                      <TableCell>
                        <div className="flex gap-1">
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => openEdit(rule)}>
                            <Edit2 className="h-4 w-4" />
                          </Button>
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-7 w-7 text-destructive"
                            onClick={() => handleDelete(rule.id)}
                            disabled={deleteMutation.isPending}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingRule ? 'Kural Duzenle' : 'Yeni Kural Ekle'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-xs font-medium">Kaynak Sinif</label>
                <Input
                  type="number"
                  min={1}
                  max={6}
                  value={form.fromYear}
                  onChange={e => setForm({ ...form, fromYear: Number(e.target.value) })}
                />
              </div>
              <div className="space-y-1">
                <label className="text-xs font-medium">Hedef Sinif</label>
                <Input
                  type="number"
                  min={2}
                  max={7}
                  value={form.toYear}
                  onChange={e => setForm({ ...form, toYear: Number(e.target.value) })}
                />
              </div>
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Minimum GANO</label>
              <Input
                type="number"
                step="0.01"
                min={0}
                max={4}
                value={form.minCgpa}
                onChange={e => setForm({ ...form, minCgpa: Number(e.target.value) })}
              />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Minimum AKTS</label>
              <Input
                type="number"
                min={0}
                value={form.minEcts}
                onChange={e => setForm({ ...form, minEcts: Number(e.target.value) })}
              />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium">Minimum Ders Sayisi</label>
              <Input
                type="number"
                min={0}
                value={form.minCourseCount}
                onChange={e => setForm({ ...form, minCourseCount: Number(e.target.value) })}
              />
            </div>
            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Iptal</Button>
            <Button
              onClick={handleSubmit}
              disabled={createMutation.isPending || updateMutation.isPending}
            >
              {editingRule ? 'Guncelle' : 'Olustur'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
