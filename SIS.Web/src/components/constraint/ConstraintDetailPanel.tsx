import { useState, useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Save, Trash2, Plus, Code2 } from 'lucide-react';
import {
  useConstraintDetail,
  useUpdateConstraintParameters,
  useDeleteConstraintException,
} from '../../hooks/useConstraints';
import {
  getConstraintErrorMessage, exceptionEntityInfo,
  PARAMETER_SCHEMAS, type ParameterField,
} from './helpers';
import ConstraintExceptionForm from './ConstraintExceptionForm';
import type { ConstraintDefinitionDto } from '../../types/constraint';

// ─── Form-based Parameter Editor ────────────────────────────────
function ParameterFormEditor({
  code,
  initialJson,
  isAdmin,
  onSave,
  isSaving,
}: {
  code: string;
  initialJson: string;
  isAdmin: boolean;
  onSave: (json: string) => void;
  isSaving: boolean;
}) {
  const schema = PARAMETER_SCHEMAS[code];
  const [showRaw, setShowRaw] = useState(false);
  const [rawJson, setRawJson] = useState(initialJson);
  const [rawEdited, setRawEdited] = useState(false);

  const parsed = useMemo(() => {
    try {
      return JSON.parse(initialJson || '{}') as Record<string, unknown>;
    } catch {
      return {} as Record<string, unknown>;
    }
  }, [initialJson]);

  const [formValues, setFormValues] = useState<Record<string, string>>(() => {
    const vals: Record<string, string> = {};
    if (schema) {
      for (const field of schema) {
        vals[field.key] = parsed[field.key]?.toString() ?? '';
      }
    }
    return vals;
  });

  const [formEdited, setFormEdited] = useState(false);

  function handleFieldChange(key: string, value: string) {
    setFormValues(prev => ({ ...prev, [key]: value }));
    setFormEdited(true);
  }

  function handleFormSave() {
    const result: Record<string, unknown> = { ...parsed };
    if (schema) {
      for (const field of schema) {
        const v = formValues[field.key];
        if (field.type === 'number') {
          const num = Number(v);
          if (!isNaN(num)) result[field.key] = num;
        } else if (field.type === 'boolean') {
          result[field.key] = v === 'true';
        } else {
          if (v) result[field.key] = v;
        }
      }
    }
    onSave(JSON.stringify(result));
    setFormEdited(false);
  }

  // No known schema → raw JSON only
  if (!schema) {
    return (
      <div className="space-y-1.5">
        <Label className="text-xs font-medium">Parametreler (JSON)</Label>
        <Textarea
          className="text-xs font-mono h-20"
          value={rawJson}
          onChange={e => { setRawJson(e.target.value); setRawEdited(true); }}
          disabled={!isAdmin}
        />
        {rawEdited && isAdmin && (
          <Button size="sm" variant="outline" onClick={() => { onSave(rawJson); setRawEdited(false); }} disabled={isSaving}>
            <Save className="mr-1.5 h-3.5 w-3.5" />
            {isSaving ? 'Kaydediliyor...' : 'Parametreleri Kaydet'}
          </Button>
        )}
      </div>
    );
  }

  // Known schema → form fields with raw JSON toggle
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <Label className="text-xs font-medium">Parametreler</Label>
        {isAdmin && (
          <Button
            variant="ghost"
            size="sm"
            className="h-6 text-[10px] gap-1"
            onClick={() => setShowRaw(v => !v)}
          >
            <Code2 className="h-3 w-3" />
            {showRaw ? 'Form Görünümü' : 'JSON Görünümü'}
          </Button>
        )}
      </div>

      {showRaw ? (
        <div className="space-y-1.5">
          <Textarea
            className="text-xs font-mono h-20"
            value={rawJson}
            onChange={e => { setRawJson(e.target.value); setRawEdited(true); }}
            disabled={!isAdmin}
          />
          {rawEdited && isAdmin && (
            <Button size="sm" variant="outline" onClick={() => { onSave(rawJson); setRawEdited(false); }} disabled={isSaving}>
              <Save className="mr-1.5 h-3.5 w-3.5" />
              Kaydet
            </Button>
          )}
        </div>
      ) : (
        <div className="space-y-2">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
            {schema.map(field => (
              <ParameterFieldInput
                key={field.key}
                field={field}
                value={formValues[field.key] ?? ''}
                onChange={v => handleFieldChange(field.key, v)}
                disabled={!isAdmin}
              />
            ))}
          </div>
          {formEdited && isAdmin && (
            <Button size="sm" variant="outline" onClick={handleFormSave} disabled={isSaving}>
              <Save className="mr-1.5 h-3.5 w-3.5" />
              {isSaving ? 'Kaydediliyor...' : 'Parametreleri Kaydet'}
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

function ParameterFieldInput({
  field, value, onChange, disabled,
}: {
  field: ParameterField;
  value: string;
  onChange: (v: string) => void;
  disabled: boolean;
}) {
  if (field.type === 'select' && field.options) {
    return (
      <div className="space-y-1">
        <Label className="text-[11px] text-muted-foreground">{field.label}</Label>
        <Select value={value} onValueChange={onChange} disabled={disabled}>
          <SelectTrigger className="h-8 text-sm">
            <SelectValue placeholder="Seçin..." />
          </SelectTrigger>
          <SelectContent>
            {field.options.map(opt => (
              <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    );
  }

  if (field.type === 'boolean') {
    return (
      <div className="space-y-1">
        <Label className="text-[11px] text-muted-foreground">{field.label}</Label>
        <div className="flex items-center gap-2 h-8">
          <Switch
            checked={value === 'true'}
            onCheckedChange={checked => onChange(checked ? 'true' : 'false')}
            disabled={disabled}
          />
          <span className="text-xs text-muted-foreground">{value === 'true' ? 'Evet' : 'Hayır'}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-1">
      <Label className="text-[11px] text-muted-foreground">{field.label}</Label>
      <div className="flex items-center gap-1.5">
        <Input
          type={field.type === 'time' ? 'time' : 'number'}
          value={value}
          onChange={e => onChange(e.target.value)}
          disabled={disabled}
          className="h-8 text-sm"
          min={field.min}
          max={field.max}
        />
        {field.suffix && (
          <span className="text-xs text-muted-foreground shrink-0 w-12">{field.suffix}</span>
        )}
      </div>
    </div>
  );
}

// ─── Main Detail Panel ──────────────────────────────────────────
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
  const [showForm, setShowForm] = useState(false);
  const [paramError, setParamError] = useState<string | null>(null);

  function handleSaveParams(json: string) {
    setParamError(null);
    updateParams.mutate(
      { id: constraint.id, parameters: json },
      {
        onError: (err) => setParamError(getConstraintErrorMessage(err, 'Parametreler kaydedilemedi.')),
      },
    );
  }

  return (
    <div className="space-y-4 pb-2">
      {/* Parameter Editor */}
      {constraint.parameters && (
        <div className="space-y-1.5">
          <ParameterFormEditor
            code={constraint.code}
            initialJson={constraint.parameters}
            isAdmin={isAdmin}
            onSave={handleSaveParams}
            isSaving={updateParams.isPending}
          />
          {paramError && <p className="text-xs text-red-600">{paramError}</p>}
        </div>
      )}

      {/* Exceptions */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label className="text-xs font-medium">İstisnalar</Label>
          {isAdmin && !showForm && (
            <Button size="sm" variant="outline" onClick={() => setShowForm(true)}>
              <Plus className="mr-1.5 h-3.5 w-3.5" />
              İstisna Ekle
            </Button>
          )}
        </div>

        {showForm && (
          <ConstraintExceptionForm
            constraintId={constraint.id}
            constraintCode={constraint.code}
            onClose={() => setShowForm(false)}
          />
        )}

        {isLoading ? (
          <Skeleton className="h-16 rounded" />
        ) : detail?.exceptions && detail.exceptions.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="text-xs">Varlık</TableHead>
                <TableHead className="text-xs w-16">Muaf</TableHead>
                <TableHead className="text-xs">Açıklama</TableHead>
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
                      {ex.isExempt ? 'Evet' : 'Hayır'}
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
          <p className="text-xs text-muted-foreground">Henüz istisna tanımlanmamış.</p>
        )}
      </div>
    </div>
  );
}
