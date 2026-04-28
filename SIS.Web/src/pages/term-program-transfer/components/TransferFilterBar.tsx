import { ArrowRight, AlertTriangle } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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

interface TransferFilterBarProps {
  departments: DepartmentOption[];
  selectedDeptId: number | null;
  onDeptChange: (deptId: number) => void;

  sourceTerms: TermOption[];
  sourceTermId: number | null;
  onSourceChange: (termId: number) => void;

  programs: ProgramOption[];
  programId: number | null;
  onProgramChange: (programId: number | null) => void;

  classLevel: number | null;
  onClassLevelChange: (level: number | null) => void;

  activeTermName: string | null;
  termStatus: string | undefined;
  isTermSuitable: boolean;

  totalCount: number;
  transferableCount: number;
}

export function TransferFilterBar({
  departments,
  selectedDeptId,
  onDeptChange,
  sourceTerms,
  sourceTermId,
  onSourceChange,
  programs,
  programId,
  onProgramChange,
  classLevel,
  onClassLevelChange,
  activeTermName,
  termStatus,
  isTermSuitable,
  totalCount,
  transferableCount,
}: TransferFilterBarProps) {
  return (
    <>
      {!isTermSuitable && termStatus && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Dönem durumu uygun değil</AlertTitle>
          <AlertDescription>
            Aktif dönem şu anda <strong>{termStatus}</strong> durumunda. Aktarım yapabilmek için dönemin{' '}
            <strong>Active</strong> (tarih aralığı içinde ve iptal edilmemiş) olması gerekiyor.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardContent className="pt-3 pb-3">
          <div className="flex items-center gap-3 flex-wrap">
            {departments.length > 1 && (
              <>
                <Select value={String(selectedDeptId ?? '')} onValueChange={(v) => onDeptChange(Number(v))}>
                  <SelectTrigger className="h-8 w-[180px] text-xs">
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
                <div className="h-5 w-px bg-border" />
              </>
            )}

            <Select value={String(sourceTermId ?? '')} onValueChange={(v) => onSourceChange(Number(v))}>
              <SelectTrigger className="h-8 w-[170px] text-xs">
                <SelectValue placeholder="Kaynak dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                {sourceTerms.map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <ArrowRight className="h-4 w-4 text-muted-foreground shrink-0" />

            <div className="h-8 px-3 flex items-center border rounded-md bg-muted/50 text-xs text-muted-foreground">
              {activeTermName ?? 'Aktif dönem yok'}
            </div>

            {sourceTermId && (
              <>
                <div className="h-5 w-px bg-border" />
                <Select
                  value={programId == null ? 'all' : String(programId)}
                  onValueChange={(v) => onProgramChange(v === 'all' ? null : Number(v))}
                >
                  <SelectTrigger className="h-8 w-[160px] text-xs">
                    <SelectValue placeholder="Program" />
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

                <Select
                  value={classLevel == null ? 'all' : String(classLevel)}
                  onValueChange={(v) => onClassLevelChange(v === 'all' ? null : Number(v))}
                >
                  <SelectTrigger className="h-8 w-[110px] text-xs">
                    <SelectValue placeholder="Sınıf" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tüm sınıflar</SelectItem>
                    {CLASS_LEVELS.map((lvl) => (
                      <SelectItem key={lvl.value} value={String(lvl.value)}>
                        {lvl.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </>
            )}

            {sourceTermId && totalCount > 0 && (
              <>
                <div className="h-5 w-px bg-border" />
                <span className="text-xs text-muted-foreground whitespace-nowrap">
                  {transferableCount} aktarılabilir / {totalCount} ders
                </span>
              </>
            )}
          </div>
        </CardContent>
      </Card>
    </>
  );
}
