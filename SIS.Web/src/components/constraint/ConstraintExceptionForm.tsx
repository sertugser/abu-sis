import { useState, useMemo, useRef, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Save, X, Search, AlertTriangle } from 'lucide-react';
import { useAddConstraintException } from '../../hooks/useConstraints';
import { useAdminDepartments } from '../../hooks/useAdminLookup';
import { useAdminTerms } from '../../hooks/useAdminTerms';
import { useAdminInstructors } from '../../hooks/useAdminInstructors';
import { useAdminStudents } from '../../hooks/useAdminStudents';
import { useDebounce } from '../../hooks/useDebounce';
import { getConstraintErrorMessage, ENTITY_TYPE_LABELS, PARAMETER_SCHEMAS } from './helpers';
import type { AddExceptionRequest } from '../../types/constraint';

type EntityType = 'instructor' | 'student' | 'department' | 'courseSection' | 'term';

const ENTITY_OPTIONS: { value: EntityType; label: string }[] = [
  { value: 'department', label: 'Bölüm' },
  { value: 'instructor', label: 'Öğretim Üyesi' },
  { value: 'student', label: 'Öğrenci' },
  { value: 'term', label: 'Dönem' },
  { value: 'courseSection', label: 'Ders Şubesi (ID)' },
];

// ─── Entity Selector ────────────────────────────────────────────
function EntitySelector({
  entityType,
  selectedId,
  onSelect,
}: {
  entityType: EntityType;
  selectedId: number | null;
  onSelect: (id: number | null, label: string) => void;
}) {
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 300);
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Fetch data based on entity type
  const { data: departments } = useAdminDepartments();
  const { data: terms } = useAdminTerms();
  const { data: instructors } = useAdminInstructors(
    entityType === 'instructor' ? { search: debouncedSearch } : { search: '__DISABLED__' },
  );
  const { data: students } = useAdminStudents(
    entityType === 'student' && debouncedSearch.length >= 2
      ? { search: debouncedSearch, pageSize: 15 }
      : { search: '__DISABLED__', pageSize: 0 },
  );

  // Instructor / Student → search input with dropdown
  const results: { id: number; label: string; sub?: string }[] = useMemo(() => {
    if (entityType === 'instructor' && instructors) {
      return instructors.map(i => ({
        id: i.id,
        label: `${i.firstName} ${i.lastName}`,
        sub: `${i.title} - ${i.departmentName}`,
      }));
    }
    if (entityType === 'student' && students && 'items' in students) {
      return (students as { items: Array<{ id: number; firstName: string; lastName: string; studentNumber: string; departmentName: string }> }).items.map(s => ({
        id: s.id,
        label: `${s.firstName} ${s.lastName}`,
        sub: `${s.studentNumber} - ${s.departmentName}`,
      }));
    }
    return [];
  }, [entityType, instructors, students]);

  // Department → simple Select
  if (entityType === 'department') {
    return (
      <Select
        value={selectedId?.toString() ?? ''}
        onValueChange={v => {
          const dept = departments?.find(d => d.id === Number(v));
          onSelect(Number(v), dept ? `${dept.name} (${dept.code})` : '');
        }}
      >
        <SelectTrigger className="h-8 text-sm">
          <SelectValue placeholder="Bölüm seçin..." />
        </SelectTrigger>
        <SelectContent>
          {departments?.map(d => (
            <SelectItem key={d.id} value={d.id.toString()}>
              {d.name} ({d.code})
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  // Term → simple Select
  if (entityType === 'term') {
    return (
      <Select
        value={selectedId?.toString() ?? ''}
        onValueChange={v => {
          const term = terms?.find(t => t.id === Number(v));
          onSelect(Number(v), term?.name ?? '');
        }}
      >
        <SelectTrigger className="h-8 text-sm">
          <SelectValue placeholder="Dönem seçin..." />
        </SelectTrigger>
        <SelectContent>
          {terms?.map(t => (
            <SelectItem key={t.id} value={t.id.toString()}>
              {t.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }

  // CourseSection → manual ID
  if (entityType === 'courseSection') {
    return (
      <Input
        type="number"
        className="h-8 text-sm"
        value={selectedId ?? ''}
        onChange={e => {
          const id = e.target.value ? Number(e.target.value) : null;
          onSelect(id, id ? `Şube #${id}` : '');
        }}
        placeholder="Ders şubesi ID girin..."
      />
    );
  }

  return (
    <div className="relative" ref={wrapperRef}>
      <div className="relative">
        <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
        <Input
          className="h-8 text-sm pl-7"
          value={search}
          onChange={e => { setSearch(e.target.value); setDropdownOpen(true); }}
          onFocus={() => search && setDropdownOpen(true)}
          placeholder={entityType === 'instructor' ? 'İsim ile arayın...' : 'İsim veya numara ile arayın...'}
        />
      </div>
      {selectedId && (
        <p className="text-[11px] text-muted-foreground mt-1">
          Seçili: #{selectedId}
        </p>
      )}
      {dropdownOpen && search.length >= (entityType === 'student' ? 2 : 1) && results.length > 0 && (
        <div className="absolute z-20 top-full left-0 right-0 mt-1 max-h-48 overflow-auto rounded-md border bg-popover shadow-md">
          {results.map(r => (
            <button
              key={r.id}
              type="button"
              className="w-full text-left px-3 py-2 hover:bg-accent text-sm transition-colors"
              onClick={() => {
                onSelect(r.id, r.label);
                setSearch(r.label);
                setDropdownOpen(false);
              }}
            >
              <span className="font-medium">{r.label}</span>
              {r.sub && <span className="text-xs text-muted-foreground block">{r.sub}</span>}
            </button>
          ))}
        </div>
      )}
      {dropdownOpen && entityType === 'student' && search.length < 2 && (
        <p className="text-[10px] text-muted-foreground mt-1">En az 2 karakter girin...</p>
      )}
    </div>
  );
}

// ─── JSON Validation ────────────────────────────────────────────
function validateParameterOverrideJson(json: string, constraintCode: string): { valid: boolean; error?: string } {
  if (!json.trim()) return { valid: true };
  try {
    const parsed = JSON.parse(json);
    if (typeof parsed !== 'object' || Array.isArray(parsed))
      return { valid: false, error: 'JSON bir obje olmalıdır (örn: {"key": value})' };
    const schema = PARAMETER_SCHEMAS[constraintCode];
    if (schema && schema.length > 0) {
      const validKeys = new Set(schema.map((f: { key: string }) => f.key));
      const unknownKeys = Object.keys(parsed).filter(k => !validKeys.has(k));
      if (unknownKeys.length > 0)
        return { valid: true, error: `Bilinmeyen alanlar: ${unknownKeys.join(', ')}. Typo olabilir mi?` };
    }
    return { valid: true };
  } catch {
    return { valid: false, error: 'Geçersiz JSON formatı' };
  }
}

// ─── Main Form ──────────────────────────────────────────────────
export default function ConstraintExceptionForm({
  constraintId,
  constraintCode,
  onClose,
}: {
  constraintId: number;
  constraintCode: string;
  onClose: () => void;
}) {
  const addMutation = useAddConstraintException();
  const [entityType, setEntityType] = useState<EntityType>('department');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [, setSelectedLabel] = useState('');
  const [isExempt, setIsExempt] = useState(true);
  const [paramOverride, setParamOverride] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const jsonValidation = useMemo(
    () => validateParameterOverrideJson(paramOverride, constraintCode),
    [paramOverride, constraintCode],
  );

  function handleEntityTypeChange(type: EntityType) {
    setEntityType(type);
    setSelectedId(null);
    setSelectedLabel('');
  }

  function handleSubmit() {
    if (!selectedId) {
      setError(`Lütfen bir ${ENTITY_TYPE_LABELS[entityType]?.toLowerCase() ?? 'varlık'} seçin.`);
      return;
    }

    const request: AddExceptionRequest = {
      isExempt,
      reason: reason || null,
      parameterOverride: paramOverride || null,
    };

    if (entityType === 'instructor') request.instructorId = selectedId;
    else if (entityType === 'student') request.studentId = selectedId;
    else if (entityType === 'department') request.departmentId = selectedId;
    else if (entityType === 'courseSection') request.courseSectionId = selectedId;
    else if (entityType === 'term') request.termId = selectedId;

    setError(null);
    addMutation.mutate(
      { constraintId, request },
      {
        onSuccess: () => onClose(),
        onError: (err) => setError(getConstraintErrorMessage(err, 'İstisna eklenemedi.')),
      },
    );
  }

  return (
    <Card className="mt-3 border-dashed">
      <CardContent className="pt-4 space-y-3">
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium">Yeni İstisna</p>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onClose}>
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label className="text-xs">Varlık Tipi</Label>
            <Select value={entityType} onValueChange={v => handleEntityTypeChange(v as EntityType)}>
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
            <Label className="text-xs">{ENTITY_TYPE_LABELS[entityType] ?? 'Varlık'}</Label>
            <EntitySelector
              entityType={entityType}
              selectedId={selectedId}
              onSelect={(id, label) => { setSelectedId(id); setSelectedLabel(label); }}
            />
          </div>
        </div>

        {selectedId && (
          <Alert variant="warning" className="py-2.5">
            <AlertTriangle className="h-4 w-4" />
            <AlertDescription className="text-xs">
              Bu istisna belirtilmeyen alanlar için tüm değerlere uygulanacaktır. Örneğin sadece bölüm seçerseniz, o bölümdeki tüm öğretim üyeleri için geçerli olur.
            </AlertDescription>
          </Alert>
        )}

        <div className="flex items-center gap-2">
          <Switch checked={isExempt} onCheckedChange={setIsExempt} />
          <Label className="text-xs">Muaf (bu kural bu varlık için uygulanmaz)</Label>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs">Parametre Geçersiz Kılma (JSON, opsiyonel)</Label>
          <Textarea
            className={`text-xs font-mono h-16 ${!jsonValidation.valid ? 'border-red-500' : ''}`}
            value={paramOverride}
            onChange={e => setParamOverride(e.target.value)}
            placeholder='{"MaxDailyHours": 10}'
          />
          {jsonValidation.error && (
            <p className={`text-[11px] ${jsonValidation.valid ? 'text-amber-600' : 'text-red-600'}`}>
              {jsonValidation.error}
            </p>
          )}
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs">Açıklama</Label>
          <Textarea
            className="text-xs h-12"
            value={reason}
            onChange={e => setReason(e.target.value)}
            placeholder="İstisna nedeni..."
          />
        </div>

        {error && <p className="text-xs text-red-600">{error}</p>}

        <Button size="sm" onClick={handleSubmit} disabled={addMutation.isPending || !jsonValidation.valid}>
          <Save className="mr-1.5 h-3.5 w-3.5" />
          {addMutation.isPending ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      </CardContent>
    </Card>
  );
}
