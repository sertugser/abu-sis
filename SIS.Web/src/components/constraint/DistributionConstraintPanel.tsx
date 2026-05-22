import { useState } from 'react';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Tooltip, TooltipContent, TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  Link2, Plus, Pencil, Trash2, ArrowRight, Calendar, Building2,
  ArrowLeftRight, Clock, Ban,
} from 'lucide-react';
import {
  useDistributionConstraints,
  useCreateDistributionConstraint,
  useUpdateDistributionConstraint,
  useDeleteDistributionConstraint,
} from '../../hooks/useDistributionConstraints';
import DistributionConstraintForm from './DistributionConstraintForm';
import type { DistributionConstraintDto, CreateDistributionConstraintRequest } from '../../types/distributionConstraint';

const TYPE_CONFIG: Record<string, { label: string; icon: typeof Link2; color: string }> = {
  SameDay: { label: 'Ayni Gun', icon: Calendar, color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200' },
  DifferentDay: { label: 'Farkli Gun', icon: ArrowLeftRight, color: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200' },
  BackToBack: { label: 'Ardisik', icon: Clock, color: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200' },
  NotBackToBack: { label: 'Ardisik Olmasin', icon: Ban, color: 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200' },
  SameRoom: { label: 'Ayni Derslik', icon: Building2, color: 'bg-cyan-100 text-cyan-800 dark:bg-cyan-900 dark:text-cyan-200' },
  Precedence: { label: 'Siralama', icon: ArrowRight, color: 'bg-pink-100 text-pink-800 dark:bg-pink-900 dark:text-pink-200' },
};

const PREF_LEVEL_BADGE: Record<string, { label: string; className: string }> = {
  Preferred: { label: 'Tercih', className: 'bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300' },
  StronglyPreferred: { label: 'Guclu Tercih', className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900 dark:text-emerald-300' },
  Discouraged: { label: 'Tercih Edilmez', className: 'bg-amber-100 text-amber-700 dark:bg-amber-900 dark:text-amber-300' },
  StronglyDiscouraged: { label: 'Kesinlikle Edilmez', className: 'bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300' },
  Unavailable: { label: 'Yasak (Hard)', className: 'bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300' },
};

interface DistributionConstraintPanelProps {
  termId: number | null;
}

export default function DistributionConstraintPanel({ termId }: DistributionConstraintPanelProps) {
  const { data: constraints, isLoading } = useDistributionConstraints(termId);
  const createMutation = useCreateDistributionConstraint();
  const updateMutation = useUpdateDistributionConstraint();
  const deleteMutation = useDeleteDistributionConstraint();

  const [formOpen, setFormOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<DistributionConstraintDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DistributionConstraintDto | null>(null);

  function handleSubmit(request: CreateDistributionConstraintRequest) {
    if (editingItem) {
      updateMutation.mutate(
        { id: editingItem.id, request },
        { onSuccess: () => { setFormOpen(false); setEditingItem(null); } },
      );
    } else {
      createMutation.mutate(request, {
        onSuccess: () => setFormOpen(false),
      });
    }
  }

  function handleDelete() {
    if (!deleteTarget || !termId) return;
    deleteMutation.mutate(
      { id: deleteTarget.id, termId },
      { onSuccess: () => setDeleteTarget(null) },
    );
  }

  if (!termId) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Link2 className="h-4 w-4 text-muted-foreground" />
          <h2 className="text-sm font-semibold">Iliskisel Kisitlamalar</h2>
          {constraints && (
            <Badge variant="secondary" className="text-[10px]">
              {constraints.length}
            </Badge>
          )}
        </div>
        <Button size="sm" variant="outline" className="h-7 text-xs" onClick={() => { setEditingItem(null); setFormOpen(true); }}>
          <Plus className="h-3 w-3 mr-1" />
          Yeni Kural
        </Button>
      </div>

      {isLoading && <Skeleton className="h-24 rounded-lg" />}

      {constraints && constraints.length === 0 && (
        <Card>
          <CardContent className="py-6 text-center">
            <p className="text-sm text-muted-foreground">
              Bu donem icin henuz iliskisel kisitlama tanimlanmamis.
            </p>
          </CardContent>
        </Card>
      )}

      {constraints && constraints.length > 0 && (
        <div className="space-y-2">
          {constraints.map(c => (
            <DistributionRow
              key={c.id}
              constraint={c}
              onEdit={() => { setEditingItem(c); setFormOpen(true); }}
              onDelete={() => setDeleteTarget(c)}
            />
          ))}
        </div>
      )}

      {/* Create/edit form */}
      {formOpen && (
        <DistributionConstraintForm
          open={formOpen}
          onOpenChange={(open) => {
            setFormOpen(open);
            if (!open) setEditingItem(null);
          }}
          termId={termId}
          onSubmit={handleSubmit}
          isPending={createMutation.isPending || updateMutation.isPending}
          editing={editingItem}
        />
      )}

      {/* Delete confirmation */}
      <AlertDialog open={deleteTarget != null} onOpenChange={() => setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Kisitlamayi sil?</AlertDialogTitle>
            <AlertDialogDescription>
              Bu iliskisel kisitlama kalici olarak silinecek.
              {deleteTarget && (
                <span className="block mt-1 font-medium">
                  {TYPE_CONFIG[deleteTarget.distributionType]?.label ?? deleteTarget.distributionType}
                  {' — '}
                  {deleteTarget.courses.map(c => c.courseCode).join(', ')}
                </span>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Iptal</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={handleDelete}
              disabled={deleteMutation.isPending}
            >
              Sil
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function DistributionRow({
  constraint,
  onEdit,
  onDelete,
}: {
  constraint: DistributionConstraintDto;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const typeConf = TYPE_CONFIG[constraint.distributionType] ?? {
    label: constraint.distributionType, icon: Link2, color: 'bg-gray-100 text-gray-800',
  };
  const Icon = typeConf.icon;
  const prefConf = PREF_LEVEL_BADGE[constraint.preferenceLevel] ?? {
    label: constraint.preferenceLevel, className: 'bg-gray-100 text-gray-700',
  };

  return (
    <div className="flex items-center gap-2 rounded-lg border px-3 py-2 bg-card">
      {/* Type badge */}
      <Tooltip>
        <TooltipTrigger asChild>
          <Badge variant="outline" className={`text-[10px] shrink-0 gap-1 ${typeConf.color}`}>
            <Icon className="h-3 w-3" />
            {typeConf.label}
          </Badge>
        </TooltipTrigger>
        <TooltipContent>
          <p className="text-xs">{constraint.distributionType}</p>
        </TooltipContent>
      </Tooltip>

      {/* Preference level */}
      <Badge variant="outline" className={`text-[10px] shrink-0 ${prefConf.className}`}>
        {prefConf.label}
      </Badge>

      {/* Courses */}
      <div className="flex-1 flex items-center gap-1 flex-wrap min-w-0">
        {constraint.courses.map((c, i) => (
          <span key={c.id} className="inline-flex items-center gap-1">
            {i > 0 && (
              constraint.distributionType === 'Precedence'
                ? <ArrowRight className="h-3 w-3 text-muted-foreground shrink-0" />
                : <span className="text-xs text-muted-foreground">&amp;</span>
            )}
            <Badge variant="secondary" className="text-[10px] font-mono">
              {c.courseCode}
            </Badge>
          </span>
        ))}
      </div>

      {/* Reason */}
      {constraint.reason && (
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="text-xs text-muted-foreground truncate max-w-32 cursor-help">
              {constraint.reason}
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <p className="text-xs max-w-60">{constraint.reason}</p>
          </TooltipContent>
        </Tooltip>
      )}

      {/* Actions */}
      <div className="flex gap-0.5 shrink-0">
        <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onEdit}>
          <Pencil className="h-3 w-3" />
        </Button>
        <Button variant="ghost" size="icon" className="h-7 w-7 text-destructive" onClick={onDelete}>
          <Trash2 className="h-3 w-3" />
        </Button>
      </div>
    </div>
  );
}
