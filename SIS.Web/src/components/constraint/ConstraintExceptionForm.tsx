import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Save, X } from 'lucide-react';
import { useAddConstraintException } from '../../hooks/useConstraints';
import { getConstraintErrorMessage } from './helpers';
import type { AddExceptionRequest } from '../../types/constraint';

type EntityType = 'instructor' | 'student' | 'department' | 'courseSection' | 'term';

const ENTITY_OPTIONS: { value: EntityType; label: string }[] = [
  { value: 'instructor', label: 'Ogretim Uyesi' },
  { value: 'student', label: 'Ogrenci' },
  { value: 'department', label: 'Bolum' },
  { value: 'courseSection', label: 'Ders Subesi' },
  { value: 'term', label: 'Donem' },
];

export default function ConstraintExceptionForm({
  constraintId,
  onClose,
}: {
  constraintId: number;
  onClose: () => void;
}) {
  const addMutation = useAddConstraintException();
  const [entityType, setEntityType] = useState<EntityType>('instructor');
  const [entityId, setEntityId] = useState('');
  const [isExempt, setIsExempt] = useState(true);
  const [paramOverride, setParamOverride] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  function handleSubmit() {
    const numId = Number(entityId);
    if (!entityId || isNaN(numId)) {
      setError('Gecerli bir ID giriniz.');
      return;
    }

    const request: AddExceptionRequest = {
      isExempt,
      reason: reason || null,
      parameterOverride: paramOverride || null,
    };

    if (entityType === 'instructor') request.instructorId = numId;
    else if (entityType === 'student') request.studentId = numId;
    else if (entityType === 'department') request.departmentId = numId;
    else if (entityType === 'courseSection') request.courseSectionId = numId;
    else if (entityType === 'term') request.termId = numId;

    setError(null);
    addMutation.mutate(
      { constraintId, request },
      {
        onSuccess: () => onClose(),
        onError: (err) => setError(getConstraintErrorMessage(err, 'Istisna eklenemedi.')),
      },
    );
  }

  const entityLabel = ENTITY_OPTIONS.find(o => o.value === entityType)?.label ?? '';

  return (
    <Card className="mt-3 border-dashed">
      <CardContent className="pt-4 space-y-3">
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium">Yeni Istisna</p>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onClose}>
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label className="text-xs">Varlik Tipi</Label>
            <Select value={entityType} onValueChange={(v) => setEntityType(v as EntityType)}>
              <SelectTrigger className="h-8 text-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ENTITY_OPTIONS.map(o => (
                  <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label className="text-xs">{entityLabel} ID</Label>
            <Input
              className="h-8 text-sm"
              type="number"
              value={entityId}
              onChange={e => setEntityId(e.target.value)}
              placeholder="ornek: 42"
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Switch checked={isExempt} onCheckedChange={setIsExempt} />
          <Label className="text-xs">Muaf (Istisna)</Label>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs">Parametre Gecersiz Kilma (JSON, opsiyonel)</Label>
          <Textarea
            className="text-xs font-mono h-16"
            value={paramOverride}
            onChange={e => setParamOverride(e.target.value)}
            placeholder='{"maxHours": 10}'
          />
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs">Aciklama</Label>
          <Textarea
            className="text-xs h-12"
            value={reason}
            onChange={e => setReason(e.target.value)}
            placeholder="Istisna nedeni..."
          />
        </div>

        {error && <p className="text-xs text-red-600">{error}</p>}

        <Button size="sm" onClick={handleSubmit} disabled={addMutation.isPending}>
          <Save className="mr-1.5 h-3.5 w-3.5" />
          {addMutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      </CardContent>
    </Card>
  );
}
