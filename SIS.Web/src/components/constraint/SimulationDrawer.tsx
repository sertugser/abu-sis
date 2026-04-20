import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription,
} from '@/components/ui/sheet';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Loader2, FlaskConical } from 'lucide-react';
import { useSimulateMove, useSuggestions, useSimulateConstraintChange } from '../../hooks/useSimulation';
import BeforeAfterDiffCard from './BeforeAfterDiffCard';
import SuggestionTable from './SuggestionTable';
import type { SimulationResult, SuggestionItem } from '../../types/simulation';
import type { ConstraintDefinitionDto } from '../../types/constraint';

const DAY_OPTIONS = [
  { value: '1', label: 'Pazartesi' },
  { value: '2', label: 'Sali' },
  { value: '3', label: 'Carsamba' },
  { value: '4', label: 'Persembe' },
  { value: '5', label: 'Cuma' },
  { value: '6', label: 'Cumartesi' },
];

interface SimulationDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  termId: number | null;
  constraints: ConstraintDefinitionDto[];
}

export default function SimulationDrawer({
  open,
  onOpenChange,
  termId,
  constraints,
}: SimulationDrawerProps) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full sm:max-w-2xl overflow-y-auto">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <FlaskConical className="h-5 w-5" />
            Simulasyon Motoru
          </SheetTitle>
          <SheetDescription>
            Degisiklikleri kaydetmeden once etkilerini gorun.
          </SheetDescription>
        </SheetHeader>

        <Tabs defaultValue="move" className="mt-6">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="move">Ders Tasima</TabsTrigger>
            <TabsTrigger value="constraint">Kisitlama Degisikligi</TabsTrigger>
          </TabsList>

          <TabsContent value="move" className="space-y-6 mt-4">
            <MoveSimulationTab termId={termId} />
          </TabsContent>

          <TabsContent value="constraint" className="space-y-6 mt-4">
            <ConstraintChangeTab termId={termId} constraints={constraints} />
          </TabsContent>
        </Tabs>
      </SheetContent>
    </Sheet>
  );
}

// ── Move Simulation Tab ──────────────────────────────────────────

function MoveSimulationTab({ termId }: { termId: number | null }) {
  const [sectionId, setSectionId] = useState('');
  const [day, setDay] = useState('');
  const [timeSlot, setTimeSlot] = useState('');
  const [classroomId, setClassroomId] = useState('');
  const [moveResult, setMoveResult] = useState<SimulationResult | null>(null);

  const moveMutation = useSimulateMove();
  const parsedSectionId = sectionId ? Number(sectionId) : null;
  const suggestionsQuery = useSuggestions(parsedSectionId, termId, 15);

  function handleSimulateMove() {
    if (!termId || !sectionId || !day || !timeSlot) return;
    moveMutation.mutate(
      {
        termId,
        sectionId: Number(sectionId),
        newDayOfWeek: Number(day),
        newTimeSlotStart: timeSlot,
        newClassroomId: classroomId ? Number(classroomId) : null,
      },
      { onSuccess: setMoveResult },
    );
  }

  function handleSuggestionSelect(item: SuggestionItem) {
    setDay(String(item.dayOfWeek));
    setTimeSlot(item.timeSlot);
    setClassroomId(item.classroomId ? String(item.classroomId) : '');
  }

  return (
    <>
      {/* Section ID input */}
      <div className="space-y-2">
        <Label htmlFor="sim-section-id">Sube ID</Label>
        <Input
          id="sim-section-id"
          type="number"
          placeholder="Ornegin: 142"
          value={sectionId}
          onChange={(e) => {
            setSectionId(e.target.value);
            setMoveResult(null);
          }}
        />
      </div>

      {/* Suggestions */}
      {parsedSectionId && termId && (
        <div className="space-y-2">
          <h4 className="text-sm font-medium">Oneriler</h4>
          <p className="text-xs text-muted-foreground">
            Secilen sube icin en uygun slot/derslik kombinasyonlari.
            Bir satira tiklayarak degerlerini form alanina aktarabilirsiniz.
          </p>
          <SuggestionTable
            suggestions={suggestionsQuery.data ?? []}
            isLoading={suggestionsQuery.isLoading}
            onSelect={handleSuggestionSelect}
          />
        </div>
      )}

      {/* Manual move form */}
      <div className="space-y-3 border-t pt-4">
        <h4 className="text-sm font-medium">Manuel Tasima</h4>
        <div className="grid grid-cols-3 gap-3">
          <div className="space-y-1">
            <Label className="text-xs">Gun</Label>
            <Select value={day} onValueChange={setDay}>
              <SelectTrigger className="h-9">
                <SelectValue placeholder="Sec" />
              </SelectTrigger>
              <SelectContent>
                {DAY_OPTIONS.map(d => (
                  <SelectItem key={d.value} value={d.value}>{d.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Baslangic Saati</Label>
            <Input
              type="time"
              className="h-9"
              value={timeSlot}
              onChange={(e) => setTimeSlot(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Derslik ID</Label>
            <Input
              type="number"
              className="h-9"
              placeholder="Opsiyonel"
              value={classroomId}
              onChange={(e) => setClassroomId(e.target.value)}
            />
          </div>
        </div>

        <Button
          className="w-full"
          disabled={!termId || !sectionId || !day || !timeSlot || moveMutation.isPending}
          onClick={handleSimulateMove}
        >
          {moveMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Simule Et
        </Button>
      </div>

      {/* Result */}
      {moveResult && <BeforeAfterDiffCard result={moveResult} />}
    </>
  );
}

// ── Constraint Change Tab ────────────────────────────────────────

interface DraftChange {
  constraintId: number;
  code: string;
  name: string;
  field: 'isEnabled' | 'weight';
  originalValue: boolean | number;
  newValue: boolean | number;
}

function ConstraintChangeTab({
  termId,
  constraints,
}: {
  termId: number | null;
  constraints: ConstraintDefinitionDto[];
}) {
  const [drafts, setDrafts] = useState<DraftChange[]>([]);
  const [result, setResult] = useState<SimulationResult | null>(null);
  const mutation = useSimulateConstraintChange();

  function toggleConstraintDraft(c: ConstraintDefinitionDto) {
    setDrafts(prev => {
      const existing = prev.find(d => d.constraintId === c.id && d.field === 'isEnabled');
      if (existing) {
        return prev.filter(d => d !== existing);
      }
      return [...prev, {
        constraintId: c.id,
        code: c.code,
        name: c.name,
        field: 'isEnabled',
        originalValue: c.isEnabled,
        newValue: !c.isEnabled,
      }];
    });
    setResult(null);
  }

  function changeWeightDraft(c: ConstraintDefinitionDto, newWeight: number) {
    setDrafts(prev => {
      const filtered = prev.filter(d => !(d.constraintId === c.id && d.field === 'weight'));
      if (newWeight === c.weight) return filtered;
      return [...filtered, {
        constraintId: c.id,
        code: c.code,
        name: c.name,
        field: 'weight',
        originalValue: c.weight,
        newValue: newWeight,
      }];
    });
    setResult(null);
  }

  function handleSimulate() {
    if (!termId || drafts.length === 0) return;
    mutation.mutate(
      {
        termId,
        changes: drafts.map(d => ({
          constraintId: d.constraintId,
          field: d.field,
          value: d.newValue,
        })),
      },
      { onSuccess: setResult },
    );
  }

  function getEnabledDraftValue(c: ConstraintDefinitionDto): boolean {
    const draft = drafts.find(d => d.constraintId === c.id && d.field === 'isEnabled');
    return draft ? (draft.newValue as boolean) : c.isEnabled;
  }

  function getWeightDraftValue(c: ConstraintDefinitionDto): number {
    const draft = drafts.find(d => d.constraintId === c.id && d.field === 'weight');
    return draft ? (draft.newValue as number) : c.weight;
  }

  function isDrafted(c: ConstraintDefinitionDto): boolean {
    return drafts.some(d => d.constraintId === c.id);
  }

  return (
    <>
      <p className="text-xs text-muted-foreground">
        Kisitlamalari acip/kapatarak veya agirliklarini degistirerek
        program uzerindeki etkisini simule edin.
      </p>

      <div className="border rounded-md divide-y max-h-[400px] overflow-y-auto">
        {constraints.map(c => (
          <div
            key={c.id}
            className={`flex items-center justify-between px-3 py-2 ${isDrafted(c) ? 'bg-amber-50/50 dark:bg-amber-950/20' : ''}`}
          >
            <div className="flex items-center gap-2 min-w-0">
              <Switch
                checked={getEnabledDraftValue(c)}
                onCheckedChange={() => toggleConstraintDraft(c)}
                className="shrink-0"
              />
              <div className="min-w-0">
                <span className="text-xs font-mono text-muted-foreground">{c.code}</span>
                <p className="text-sm truncate">{c.name}</p>
              </div>
            </div>
            {c.category === 'Soft' && (
              <Input
                type="number"
                min={1}
                max={10}
                className="w-16 h-7 text-xs text-center"
                value={getWeightDraftValue(c)}
                onChange={(e) => changeWeightDraft(c, Number(e.target.value))}
              />
            )}
          </div>
        ))}
      </div>

      {drafts.length > 0 && (
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            {drafts.length} degisiklik taslakta.
          </p>
          <Button
            className="w-full"
            disabled={!termId || mutation.isPending}
            onClick={handleSimulate}
          >
            {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Simule Et
          </Button>
        </div>
      )}

      {result && <BeforeAfterDiffCard result={result} />}
    </>
  );
}
