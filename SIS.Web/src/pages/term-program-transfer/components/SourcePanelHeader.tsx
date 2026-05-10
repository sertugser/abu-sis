import { Download, HelpCircle, Mail, Printer } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
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

  /** Toolbar buton'larının disabled durumu için — kaynak dönem seçili mi + ders var mı. */
  totalCount: number;

  /** Panel araç çubuğu — CSV / Yazdır / Mail */
  onExportCsv: () => void;
  onPrint: () => void;
  onMail: () => void;

  /** Aktar aksiyonu — header'ın sağ ucunda primary CTA. */
  selectedCount: number;
  onTransferClick: () => void;
  isPending: boolean;
  canTransfer: boolean;
  targetDepartmentLabel: string | null;
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
  totalCount,
  onExportCsv,
  onPrint,
  onMail,
  selectedCount,
  onTransferClick,
  isPending,
  canTransfer,
  targetDepartmentLabel,
}: SourcePanelHeaderProps) {
  const hasRows = sourceTermId != null && totalCount > 0;
  return (
    <div className="border-b bg-muted/30">
      {/* Başlık + araç çubuğu */}
      <div className="px-3 py-1.5 flex items-center gap-2 border-b border-border/40">
        <h3 className="text-sm font-semibold">Referans Alınacak Dersler</h3>
        <TooltipProvider delayDuration={200}>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                type="button"
                className="text-muted-foreground hover:text-foreground transition-colors"
                aria-label="Bu panel hakkında bilgi"
              >
                <HelpCircle className="h-3.5 w-3.5" />
              </button>
            </TooltipTrigger>
            <TooltipContent side="bottom" align="start" className="max-w-sm text-xs leading-relaxed">
              <p className="font-semibold mb-1.5">Geçmiş dönemden ders aktarmak için bu panel kullanılır.</p>
              <ol className="list-decimal pl-4 space-y-0.5">
                <li>Yukarıdaki filtrelerden <strong>kaynak dönemi</strong> ve <strong>bölümü</strong> seç.</li>
                <li>Aktarmak istediğin dersleri sol kutucuktan işaretle.</li>
                <li>Sağ üstteki <strong>Aktar</strong> butonuyla aktif döneme kopyala.</li>
              </ol>
              <p className="mt-2 text-[11px] text-muted-foreground">
                İpucu: <strong>Programı kopyala</strong> seçili olursa şubelerin gün, saat ve derslik bilgileri de aktarılır.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <div className="ml-auto flex items-center gap-0.5">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 w-7 p-0"
            onClick={onPrint}
            disabled={!hasRows}
            title="Yazdır"
          >
            <Printer className="h-3.5 w-3.5" />
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 w-7 p-0"
            onClick={onMail}
            disabled={!hasRows}
            title="Mail olarak gönder"
          >
            <Mail className="h-3.5 w-3.5" />
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 w-7 p-0"
            onClick={onExportCsv}
            disabled={!hasRows}
            title="CSV / Excel olarak indir"
          >
            <Download className="h-3.5 w-3.5" />
          </Button>
        </div>
        <Button
          size="sm"
          className="h-7 text-xs px-3"
          onClick={onTransferClick}
          disabled={selectedCount === 0 || isPending || !canTransfer}
          title={
            targetDepartmentLabel
              ? `Seçili ${selectedCount} ders ${targetDepartmentLabel} bölümünde aktif döneme açılır`
              : 'Seçili dersleri aktif döneme aç'
          }
        >
          {isPending ? '...' : `Seçilenleri Ekle (${selectedCount})`}
        </Button>
      </div>

      {/* Dikey filtre formu — kompakt */}
      <div className="px-3 py-2 space-y-1.5">
        <FilterRow label="Dönem">
          <Select value={String(sourceTermId ?? '')} onValueChange={(v) => onSourceChange(Number(v))}>
            <SelectTrigger className="h-7 text-xs">
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
            <SelectTrigger className="h-7 text-xs">
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
            <SelectTrigger className="h-7 text-xs">
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
            <SelectTrigger className="h-7 text-xs">
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
            className="h-7 text-xs"
            placeholder="Örn. CENG101"
          />
        </FilterRow>

        <FilterRow label="Ders Ad">
          <Input
            value={searchName}
            onChange={(e) => onSearchNameChange(e.target.value)}
            className="h-7 text-xs"
            placeholder="Ders adında ara..."
          />
        </FilterRow>
      </div>
    </div>
  );
}

function FilterRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[64px_1fr] items-center gap-2">
      <Label className="text-xs text-muted-foreground font-medium">{label}</Label>
      {children}
    </div>
  );
}
