import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Save, Trash2, Plus } from 'lucide-react';
import {
  useConstraintDetail,
  useUpdateConstraintParameters,
  useDeleteConstraintException,
} from '../../hooks/useConstraints';
import { getConstraintErrorMessage, exceptionEntityInfo } from './helpers';
import ConstraintExceptionForm from './ConstraintExceptionForm';
import type { ConstraintDefinitionDto } from '../../types/constraint';

export default function ConstraintDetailPanel({
  constraint,
  isAdmin,
}: {
  constraint: ConstraintDefinitionDto;
  isAdmin: boolean;
}) {
  const { data: detail, isLoading } = useConstraintDetail(constraint.id);
  const updateParams = useUpdateConstraintParameters();
  const deleteMutation = useDeleteConstraintException();
  const [params, setParams] = useState(constraint.parameters ?? '');
  const [paramsEdited, setParamsEdited] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [paramError, setParamError] = useState<string | null>(null);

  function handleSaveParams() {
    setParamError(null);
    updateParams.mutate(
      { id: constraint.id, parameters: params },
      {
        onSuccess: () => setParamsEdited(false),
        onError: (err) => setParamError(getConstraintErrorMessage(err, 'Parametreler kaydedilemedi.')),
      },
    );
  }

  return (
    <div className="space-y-4 px-2 pb-3">
      {constraint.description && (
        <p className="text-sm text-muted-foreground">{constraint.description}</p>
      )}

      {/* Parameter Editor */}
      <div className="space-y-1.5">
        <Label className="text-xs font-medium">Parametreler (JSON)</Label>
        <Textarea
          className="text-xs font-mono h-20"
          value={params}
          onChange={e => {
            setParams(e.target.value);
            setParamsEdited(true);
          }}
          disabled={!isAdmin}
        />
        {paramError && <p className="text-xs text-red-600">{paramError}</p>}
        {paramsEdited && isAdmin && (
          <Button size="sm" variant="outline" onClick={handleSaveParams} disabled={updateParams.isPending}>
            <Save className="mr-1.5 h-3.5 w-3.5" />
            {updateParams.isPending ? 'Kaydediliyor...' : 'Parametreleri Kaydet'}
          </Button>
        )}
      </div>

      {/* Exceptions */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label className="text-xs font-medium">Istisnalar</Label>
          {isAdmin && !showForm && (
            <Button size="sm" variant="outline" onClick={() => setShowForm(true)}>
              <Plus className="mr-1.5 h-3.5 w-3.5" />
              Istisna Ekle
            </Button>
          )}
        </div>

        {showForm && (
          <ConstraintExceptionForm
            constraintId={constraint.id}
            onClose={() => setShowForm(false)}
          />
        )}

        {isLoading ? (
          <Skeleton className="h-16 rounded" />
        ) : detail?.exceptions && detail.exceptions.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="text-xs">Varlik</TableHead>
                <TableHead className="text-xs w-16">Muaf</TableHead>
                <TableHead className="text-xs">Aciklama</TableHead>
                <TableHead className="text-xs">Parametre</TableHead>
                {isAdmin && <TableHead className="text-xs w-16">Sil</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {detail.exceptions.map(ex => (
                <TableRow key={ex.id}>
                  <TableCell className="text-xs">{exceptionEntityInfo(ex)}</TableCell>
                  <TableCell>
                    <Badge variant={ex.isExempt ? 'success' : 'secondary'} className="text-[10px]">
                      {ex.isExempt ? 'Evet' : 'Hayir'}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">{ex.reason ?? '-'}</TableCell>
                  <TableCell className="text-xs font-mono">{ex.parameterOverride ?? '-'}</TableCell>
                  {isAdmin && (
                    <TableCell>
                      <Button
                        size="icon"
                        variant="ghost"
                        className="h-7 w-7"
                        onClick={() => deleteMutation.mutate(ex.id)}
                        disabled={deleteMutation.isPending}
                      >
                        <Trash2 className="h-3.5 w-3.5 text-red-500" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <p className="text-xs text-muted-foreground">Henuz istisna tanimlanmamis.</p>
        )}
      </div>
    </div>
  );
}
