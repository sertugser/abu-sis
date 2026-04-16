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
import { Send, X, Search, AlertTriangle } from 'lucide-react';
import { useCreateExceptionRequest } from '../../hooks/useConstraints';
import { useAdminDepartments } from '../../hooks/useAdminLookup';
import { useAdminTerms } from '../../hooks/useAdminTerms';
import { useAdminInstructors } from '../../hooks/useAdminInstructors';
import { useAdminStudents } from '../../hooks/useAdminStudents';
import { useDebounce } from '../../hooks/useDebounce';
import { getConstraintErrorMessage, ENTITY_TYPE_LABELS, PARAMETER_SCHEMAS } from './helpers';
import type { CreateExceptionRequestDto } from '../../types/constraint';

type EntityType = 'instructor' | 'student' | 'department' | 'courseSection' | 'term';

const ENTITY_OPTIONS: { value: EntityType; label: string }[] = [
  { value: 'department', label: 'Bolum' },
  { value: 'instructor', label: 'Ogretim Uyesi' },
  { value: 'student', label: 'Ogrenci' },
  { value: 'term', label: 'Donem' },
  { value: 'courseSection', label: 'Ders Subesi (ID)' },
];

type DurationType = 'permanent' | 'termEnd' | 'custom';

// ─── Entity Selector (reused pattern) ──────────────────────────
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

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

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
          <SelectValue placeholder="Bolum secin..." />
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
          <SelectValue placeholder="Donem secin..." />
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

  if (entityType === 'courseSection') {
    return (
      <Input
        type="number"
        className="h-8 text-sm"
        value={selectedId ?? ''}
        onChange={e => {
          const id = e.target.value ? Number(e.target.value) : null;
          onSelect(id, id ? `Sube #${id}` : '');
        }}
        placeholder="Ders subesi ID girin..."
      />
    );
  }

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

  return (
    <div className="relative" ref={wrapperRef}>
      <div className="relative">
        <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
        <Input
          className="h-8 text-sm pl-7"
          value={search}
          onChange={e => { setSearch(e.target.value); setDropdownOpen(true); }}
          onFocus={() => search && setDropdownOpen(true)}
          placeholder={entityType === 'instructor' ? 'Isim ile arayin...' : 'Isim veya numara ile arayin...'}
        />
      </div>
      {selectedId && (
        <p className="text-[11px] text-muted-foreground mt-1">
          Secili: #{selectedId}
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

// ─── Main Form ──────────────────────────────────────────────────
export default function ExceptionRequestForm({
  constraintId,
  constraintCode,
  onClose,
}: {
  constraintId: number;
  constraintCode: string;
  onClose: () => void;
}) {
  const createMutation = useCreateExceptionRequest();
  const [entityType, setEntityType] = useState<EntityType>('department');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [isExempt, setIsExempt] = useState(true);
  const [paramOverride, setParamOverride] = useState('');
  const [reason, setReason] = useState('');
  const [durationType, setDurationType] = useState<DurationType>('permanent');
  const [customDate, setCustomDate] = useState('');
  const [error, setError] = useState<string | null>(null);

  function handleEntityTypeChange(type: EntityType) {
    setEntityType(type);
    setSelectedId(null);
  }

  function handleSubmit() {
    if (!selectedId) {
      setError(`Lutfen bir ${ENTITY_TYPE_LABELS[entityType]?.toLowerCase() ?? 'varlik'} secin.`);
      return;
    }
    if (!reason.trim()) {
      setError('Lutfen bir aciklama girin.');
      return;
    }

    let expiresAt: string | null = null;
    if (durationType === 'custom' && customDate) {
      expiresAt = new Date(customDate).toISOString();
    }

    const dto: CreateExceptionRequestDto = {
      constraintDefinitionId: constraintId,
      isExempt,
      parameterOverride: paramOverride || null,
      reason: reason.trim(),
      expiresAt,
    };

    if (entityType === 'instructor') dto.instructorId = selectedId;
    else if (entityType === 'student') dto.studentId = selectedId;
    else if (entityType === 'department') dto.departmentId = selectedId;
    else if (entityType === 'courseSection') dto.courseSectionId = selectedId;
    else if (entityType === 'term') dto.termId = selectedId;

    setError(null);
    createMutation.mutate(dto, {
      onSuccess: () => onClose(),
      onError: (err) => setError(getConstraintErrorMessage(err, 'Talep olusturulamadi.')),
    });
  }

  return (
    <Card className="mt-3 border-dashed border-amber-300 dark:border-amber-700">
      <CardContent className="pt-4 space-y-3">
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium">Istisna Talebi Olustur</p>
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onClose}>
            <X className="h-4 w-4" />
          </Button>
        </div>

        <Alert variant="warning" className="py-2">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription className="text-xs">
            Bu talep onay surecinden gececektir. Kisitlama kategorisine gore bolum muduru, dekan ve/veya sistem yoneticisi onaylamalidir.
          </AlertDescription>
        </Alert>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label className="text-xs">Varlik Tipi</Label>
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
            <Label className="text-xs">{ENTITY_TYPE_LABELS[entityType] ?? 'Varlik'}</Label>
            <EntitySelector
              entityType={entityType}
              selectedId={selectedId}
              onSelect={(id) => setSelectedId(id)}
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Switch checked={isExempt} onCheckedChange={setIsExempt} />
          <Label className="text-xs">Muaf (bu kural bu varlik icin uygulanmaz)</Label>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs">Sure</Label>
          <Select value={durationType} onValueChange={v => setDurationType(v as DurationType)}>
            <SelectTrigger className="h-8 text-sm">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="permanent">Suresiz</SelectItem>
              <SelectItem value="custom">Tarih sec</SelectItem>
            </SelectContent>
          </Select>
          {durationType === 'custom' && (
            <Input
              type="date"
              className="h-8 text-sm mt-1"
              value={customDate}
              onChange={e => setCustomDate(e.target.value)}
            />
          )}
        </div>

        {!isExempt && (
          <div className="space-y-1.5">
            <Label className="text-xs">Parametre Gecersiz Kilma (JSON, opsiyonel)</Label>
            <Textarea
              className="text-xs font-mono h-16"
              value={paramOverride}
              onChange={e => setParamOverride(e.target.value)}
              placeholder='{"MaxDailyHours": 10}'
            />
          </div>
        )}

        <div className="space-y-1.5">
          <Label className="text-xs">Aciklama (zorunlu)</Label>
          <Textarea
            className="text-xs h-16"
            value={reason}
            onChange={e => setReason(e.target.value)}
            placeholder="Istisna talep nedeninizi aciklayin..."
          />
        </div>

        {error && <p className="text-xs text-red-600">{error}</p>}

        <Button size="sm" onClick={handleSubmit} disabled={createMutation.isPending}>
          <Send className="mr-1.5 h-3.5 w-3.5" />
          {createMutation.isPending ? 'Gonderiliyor...' : 'Talep Gonder'}
        </Button>
      </CardContent>
    </Card>
  );
}
