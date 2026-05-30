import { useEffect, useState } from 'react';
import { Search, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useSectionPicker } from '@/hooks/useSectionPicker';
import {
  useFacultyLookup,
  useAdminPrograms,
  useAdminAdvisors,
} from '@/hooks/useAdminLookup';
import { SectionPickerRow } from './SectionPickerRow';
import type { CatalogCourseRow, SectionPickerQuery } from '@/types/registrationCatalog';

const ALL = 'all';
/** Select string değerini opsiyonel sayıya çevirir ("all" → undefined). */
function toId(value: string): number | undefined {
  return value === ALL ? undefined : Number(value);
}

const PAGE_SIZE = 20;

const EMPTY_QUERY: SectionPickerQuery = {
  instructorId: undefined,
  facultyId: undefined,
  programId: undefined,
  search: undefined,
  page: 1,
  pageSize: PAGE_SIZE,
};

interface Props {
  course: CatalogCourseRow | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  registeredSectionIds: number[];
  registrationActionsOpen: boolean;
  /** Caller provides: how to add a section. Returns promise that resolves on success. */
  onAddSection: (sectionId: number) => Promise<void>;
  /** Is an add currently in flight (for disabling buttons). */
  isAdding: boolean;
  /** Add error message if any (display in dialog). */
  addError?: string | null;
  /** Called after a successful add (parent decides whether to close dialog, etc.). */
  onAdded?: () => void;
}

export function SectionPickerDialog({
  course,
  open,
  onOpenChange,
  registeredSectionIds,
  registrationActionsOpen,
  onAddSection,
  isAdding,
  addError,
  onAdded,
}: Props) {
  // Select değerleri "all" | "<id>" string'i tutar (shadcn Select string ister).
  const [pendingInstructorId, setPendingInstructorId] = useState(ALL);
  const [pendingFacultyId, setPendingFacultyId] = useState(ALL);
  const [pendingProgramId, setPendingProgramId] = useState(ALL);
  const [pendingSearch, setPendingSearch] = useState('');

  const [appliedQuery, setAppliedQuery] = useState<SectionPickerQuery>(EMPTY_QUERY);
  const [page, setPage] = useState(1);

  // Lookup'lar (lookup.* — her authenticated kullanıcıya açık, scope-filtreli).
  const { data: faculties = [] } = useFacultyLookup();
  const { data: advisors = [] } = useAdminAdvisors();
  // Programlar fakülteye göre daraltılmaz (bölüm zinciri gerektirir); tümünü çek, fakülte
  // seçilince istemci tarafında filtrelemeye gerek yok — backend zaten scope koruyor.
  const { data: programs = [] } = useAdminPrograms();

  const { data, isLoading, isError, error } = useSectionPicker(
    course?.courseId ?? null,
    { ...appliedQuery, page, pageSize: PAGE_SIZE },
    open,
  );

  // Reset all state when dialog closes
  useEffect(() => {
    if (!open) {
      setPendingInstructorId(ALL);
      setPendingFacultyId(ALL);
      setPendingProgramId(ALL);
      setPendingSearch('');
      setAppliedQuery(EMPTY_QUERY);
      setPage(1);
    }
  }, [open]);

  function handleSearch() {
    setAppliedQuery({
      instructorId: toId(pendingInstructorId),
      facultyId: toId(pendingFacultyId),
      programId: toId(pendingProgramId),
      search: pendingSearch || undefined,
      page: 1,
      pageSize: PAGE_SIZE,
    });
    setPage(1);
  }

  function handleClear() {
    setPendingInstructorId(ALL);
    setPendingFacultyId(ALL);
    setPendingProgramId(ALL);
    setPendingSearch('');
    setAppliedQuery(EMPTY_QUERY);
    setPage(1);
  }

  async function handleAdd(sectionId: number) {
    try {
      await onAddSection(sectionId);
      onAdded?.();
      onOpenChange(false);
    } catch {
      // error already surfaced via addError prop; don't re-handle here
    }
  }

  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  if (!course) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-5xl max-h-[85vh] overflow-hidden flex flex-col">
        <DialogHeader>
          <DialogTitle>
            <span className="font-mono">{course.code}</span>{' · '}{course.name}{' · '}
            <Badge variant="default" className="align-middle text-xs">{course.ects} AKTS</Badge>{' '}
            <Badge variant="outline" className="align-middle text-xs">{course.nationalCredit} KRD</Badge>
          </DialogTitle>
          <DialogDescription>
            Açık şubeleri filtreleyip plana ekleyebilirsiniz.
          </DialogDescription>
        </DialogHeader>

        {/* Filter form */}
        <div className="rounded-lg border bg-muted/30 p-3 shrink-0">
          <div className="flex flex-wrap gap-3 items-end">
            <div className="flex flex-col gap-1">
              <label className="text-[11px] font-medium text-muted-foreground">Öğretim Elemanı</label>
              <Select value={pendingInstructorId} onValueChange={setPendingInstructorId}>
                <SelectTrigger className="h-11 w-48 text-xs">
                  <SelectValue placeholder="Tümü" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>Tümü</SelectItem>
                  {advisors.map((a) => (
                    <SelectItem key={a.id} value={String(a.id)}>{a.fullName}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-[11px] font-medium text-muted-foreground">Fakülte</label>
              <Select value={pendingFacultyId} onValueChange={setPendingFacultyId}>
                <SelectTrigger className="h-11 w-44 text-xs">
                  <SelectValue placeholder="Tümü" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>Tümü</SelectItem>
                  {faculties.map((f) => (
                    <SelectItem key={f.id} value={String(f.id)}>{f.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-[11px] font-medium text-muted-foreground">Program</label>
              <Select value={pendingProgramId} onValueChange={setPendingProgramId}>
                <SelectTrigger className="h-11 w-44 text-xs">
                  <SelectValue placeholder="Tümü" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>Tümü</SelectItem>
                  {programs.map((p) => (
                    <SelectItem key={p.id} value={String(p.id)}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-[11px] font-medium text-muted-foreground">Ara</label>
              <Input
                className="h-11 w-48 text-xs"
                placeholder="Ders kodu veya adı"
                value={pendingSearch}
                onChange={(e) => setPendingSearch(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </div>
            <Button type="button" size="sm" onClick={handleSearch}>
              <Search className="h-4 w-4" />
              Ara
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={handleClear}>
              Temizle
            </Button>
          </div>
        </div>

        {/* Add error */}
        {addError && (
          <Alert variant="destructive" className="shrink-0 py-2">
            <AlertDescription className="text-xs">{addError}</AlertDescription>
          </Alert>
        )}

        {/* Results table */}
        <div className="flex-1 overflow-auto border rounded-md">
          {isLoading ? (
            <div className="p-3 space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : isError ? (
            <Alert variant="destructive" className="m-3">
              <AlertDescription className="text-xs">
                {error instanceof Error ? error.message : 'Şubeler yüklenirken bir hata oluştu.'}
              </AlertDescription>
            </Alert>
          ) : data && data.items.length === 0 ? (
            <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
              Bu filtrelerle şube bulunamadı.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="text-[10px] uppercase tracking-wide">
                  <TableHead className="w-12 py-3 px-3 text-center">Şube</TableHead>
                  <TableHead className="w-24 py-3 px-3">Ders Kodu</TableHead>
                  <TableHead className="py-3 px-3">Ders Adı</TableHead>
                  <TableHead className="w-10 py-3 px-3 text-center">Z/S</TableHead>
                  <TableHead className="w-12 py-3 px-3 text-center">T+U</TableHead>
                  <TableHead className="w-10 py-3 px-3 text-center">KRD</TableHead>
                  <TableHead className="w-12 py-3 px-3 text-center">AKTS</TableHead>
                  <TableHead className="py-3 px-3">Öğretim Üyesi</TableHead>
                  <TableHead className="hidden md:table-cell w-12 py-3 px-3 text-center">Sınıf</TableHead>
                  <TableHead className="hidden lg:table-cell py-3 px-3">AcNeden</TableHead>
                  <TableHead className="hidden lg:table-cell py-3 px-3">Alma Tipi</TableHead>
                  <TableHead className="w-20 py-3 px-3 text-center">Kontenjan</TableHead>
                  <TableHead className="hidden xl:table-cell w-20 py-3 px-3">Grup Kod</TableHead>
                  <TableHead className="w-28 py-3 px-3 text-right">Aksiyon</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.map((row) => (
                  <SectionPickerRow
                    key={row.sectionId}
                    row={row}
                    onAdd={handleAdd}
                    isAdding={isAdding}
                    isInPlan={registeredSectionIds.includes(row.sectionId)}
                    registrationActionsOpen={registrationActionsOpen}
                  />
                ))}
              </TableBody>
            </Table>
          )}
        </div>

        {/* Pagination footer */}
        <div className="flex items-center justify-between shrink-0 pt-1 text-xs text-muted-foreground">
          <span>
            {totalCount} sonuç · Sayfa {page} / {totalPages}
          </span>
          <div className="flex items-center gap-1">
            <Button type="button" size="icon" variant="outline" className="h-11 w-8"
              disabled={page <= 1 || isLoading} onClick={() => setPage((p) => Math.max(1, p - 1))}>
              {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ChevronLeft className="h-4 w-4" />}
            </Button>
            <Button type="button" size="icon" variant="outline" className="h-11 w-8"
              disabled={page >= totalPages || isLoading} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>
              {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <ChevronRight className="h-4 w-4" />}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
