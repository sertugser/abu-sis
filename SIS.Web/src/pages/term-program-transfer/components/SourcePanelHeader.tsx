import { CalendarClock, BookOpen } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { CLASS_LEVELS } from '../types';

interface DepartmentOption {
  departmentId: number;
  departmentName: string;
}

interface TermOption {
  id: number;
  name: string;
}

interface ProgramOption {
  id: number;
  name: string;
}

interface SourcePanelHeaderProps {
  departments: DepartmentOption[];
  departmentId: number | null;
  onDepartmentChange: (id: number) => void;

  sourceTerms: TermOption[];
  sourceTermId: number | null;
  onSourceChange: (termId: number) => void;

  programs: ProgramOption[];
  programId: number | null;
  onProgramChange: (id: number | null) => void;

  classLevel: number | null;
  onClassLevelChange: (level: number | null) => void;

  searchCode: string;
  onSearchCodeChange: (q: string) => void;

  searchName: string;
  onSearchNameChange: (q: string) => void;

  selectedCount: number;
  totalCount: number;
  transferableCount: number;
  totalCredits: number;
  totalEcts: number;

  includeSchedule: boolean;
  onIncludeScheduleChange: (v: boolean) => void;

  onTransferClick: () => void;
  isPending: boolean;
  canTransfer: boolean;
}

export function SourcePanelHeader({
  departments,
  departmentId,
  onDepartmentChange,
  sourceTerms,
  sourceTermId,
  onSourceChange,
  programs,
  programId,
  onProgramChange,
  classLevel,
  onClassLevelChange,
  searchCode,
  onSearchCodeChange,
  searchName,
  onSearchNameChange,
  selectedCount,
  totalCount,
  transferableCount,
  totalCredits,
  totalEcts,
  includeSchedule,
  onIncludeScheduleChange,
  onTransferClick,
  isPending,
  canTransfer,
}: SourcePanelHeaderProps) {
  return (
    <div className="border-b bg-muted/30">
      {/* Başlık */}
      <div className="px-4 py-2.5 flex items-center gap-2 border-b border-border/40">
        <BookOpen className="h-4 w-4 text-primary" />
        <h3 className="text-base font-semibold">Referans Alınacak Dersler</h3>
        {sourceTermId != null && totalCount > 0 && (
          <span className="text-xs text-muted-foreground ml-auto">
            {transferableCount}/{totalCount} aktarılabilir
          </span>
        )}
      </div>

      {/* Dikey filtre formu */}
      <div className="px-4 py-3 space-y-2">
        <FilterRow label="Dönem">
          <Select value={String(sourceTermId ?? '')} onValueChange={(v) => onSourceChange(Number(v))}>
            <SelectTrigger className="h-9 text-sm">
              <SelectValue placeholder="Dönem seçin" />
            </SelectTrigger>
            <SelectContent>
              {sourceTerms.map((t) => (
                <SelectItem key={t.id} value={String(t.id)}>
                  {t.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterRow>

        <FilterRow label="Bölüm">
          <Select
            value={departmentId == null ? '' : String(departmentId)}
            onValueChange={(v) => onDepartmentChange(Number(v))}
            disabled={departments.length === 0}
          >
            <SelectTrigger className="h-9 text-sm">
              <SelectValue placeholder="Bölüm seçin" />
            </SelectTrigger>
            <SelectContent>
              {departments.map((d) => (
                <SelectItem key={d.departmentId} value={String(d.departmentId)}>
                  {d.departmentName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterRow>

        <FilterRow label="Program">
          <Select value={programId == null ? 'all' : String(programId)} onValueChange={(v) => onProgramChange(v === 'all' ? null : Number(v))}>
            <SelectTrigger className="h-9 text-sm">
              <SelectValue placeholder="Tüm programlar" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tüm programlar</SelectItem>
              {programs.map((p) => (
                <SelectItem key={p.id} value={String(p.id)}>
                  {p.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterRow>

        <FilterRow label="Sınıf">
          <Select value={classLevel == null ? 'all' : String(classLevel)} onValueChange={(v) => onClassLevelChange(v === 'all' ? null : Number(v))}>
            <SelectTrigger className="h-9 text-sm">
              <SelectValue placeholder="Tüm sınıflar" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tümü</SelectItem>
              {CLASS_LEVELS.map((lvl) => (
                <SelectItem key={lvl.value} value={String(lvl.value)}>
                  {lvl.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterRow>

        <FilterRow label="Ders Kod">
          <Input
            value={searchCode}
            onChange={(e) => onSearchCodeChange(e.target.value)}
            className="h-9 text-sm"
            placeholder="Örn. CENG101"
          />
        </FilterRow>

        <FilterRow label="Ders Ad">
          <Input
            value={searchName}
            onChange={(e) => onSearchNameChange(e.target.value)}
            className="h-9 text-sm"
            placeholder="Ders adında ara..."
          />
        </FilterRow>
      </div>

      {/* Aksiyon barı */}
      <div className="px-4 py-2.5 border-t border-border/40 flex items-center gap-3 flex-wrap">
        <label
          htmlFor="include-schedule"
          className="flex items-center gap-1.5 text-xs cursor-pointer select-none"
          title="Geçen dönemin gün/saat/derslik bilgisini de aktarır."
        >
          <input
            id="include-schedule"
            type="checkbox"
            checked={includeSchedule}
            onChange={(e) => onIncludeScheduleChange(e.target.checked)}
            className="h-3.5 w-3.5 cursor-pointer"
          />
          <CalendarClock className="h-3.5 w-3.5" />
          <Label htmlFor="include-schedule" className="text-xs cursor-pointer">
            Programı kopyala
          </Label>
        </label>

        <span className="ml-auto text-xs text-muted-foreground whitespace-nowrap">
          {selectedCount} seçili
          {selectedCount > 0 && (
            <>
              {' · '}
              <strong className="text-foreground">{totalCredits}</strong> Krd ·{' '}
              <strong className="text-foreground">{totalEcts}</strong> AKTS
            </>
          )}
        </span>

        <Button
          size="sm"
          className="h-8 text-sm px-4"
          onClick={onTransferClick}
          disabled={selectedCount === 0 || isPending || !canTransfer}
        >
          {isPending ? '...' : `Aktar (${selectedCount})`}
        </Button>
      </div>
    </div>
  );
}

function FilterRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[88px_1fr] items-center gap-3">
      <Label className="text-xs text-muted-foreground font-medium">{label}</Label>
      {children}
    </div>
  );
}
