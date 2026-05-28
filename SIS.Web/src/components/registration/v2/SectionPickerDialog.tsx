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
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useSectionPicker } from '@/hooks/useSectionPicker';
import { useAddToPlan } from '@/hooks/useAddToPlan';
import { SectionPickerRow } from './SectionPickerRow';
import type { CatalogCourseRow, SectionPickerQuery } from '@/types/registrationCatalog';

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
  onAdded?: () => void;
}

export function SectionPickerDialog({
  course,
  open,
  onOpenChange,
  registeredSectionIds,
  registrationActionsOpen,
  onAdded,
}: Props) {
  const [pendingInstructorId, setPendingInstructorId] = useState('');
  const [pendingFacultyId, setPendingFacultyId] = useState('');
  const [pendingProgramId, setPendingProgramId] = useState('');
  const [pendingSearch, setPendingSearch] = useState('');

  const [appliedQuery, setAppliedQuery] = useState<SectionPickerQuery>(EMPTY_QUERY);
  const [page, setPage] = useState(1);

  const addToPlan = useAddToPlan();

  const { data, isLoading, isError, error } = useSectionPicker(
    course?.courseId ?? null,
    { ...appliedQuery, page, pageSize: PAGE_SIZE },
    open,
  );

  // Reset all state when dialog closes
  useEffect(() => {
    if (!open) {
      setPendingInstructorId('');
      setPendingFacultyId('');
      setPendingProgramId('');
      setPendingSearch('');
      setAppliedQuery(EMPTY_QUERY);
      setPage(1);
    }
  }, [open]);

  function handleSearch() {
    setAppliedQuery({
      instructorId: pendingInstructorId ? Number(pendingInstructorId) : undefined,
      facultyId: pendingFacultyId ? Number(pendingFacultyId) : undefined,
      programId: pendingProgramId ? Number(pendingProgramId) : undefined,
      search: pendingSearch || undefined,
      page: 1,
      pageSize: PAGE_SIZE,
    });
    setPage(1);
  }

  function handleClear() {
    setPendingInstructorId('');
    setPendingFacultyId('');
    setPendingProgramId('');
    setPendingSearch('');
    setAppliedQuery(EMPTY_QUERY);
    setPage(1);
  }

  function handleAdd(sectionId: number) {
    addToPlan.mutate(sectionId, {
      onSuccess: () => {
        onAdded?.();
        onOpenChange(false);
      },
    });
  }

  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const addErrorMessage = addToPlan.error instanceof Error
    ? addToPlan.error.message
    : addToPlan.error ? 'Eklenirken bir hata oluştu.' : null;

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
        <div className="border rounded-md p-3 space-y-2 shrink-0">
          <p className="text-xs text-muted-foreground">Filtreleri girip Ara&apos;ya basın.</p>
          <div className="flex flex-wrap gap-2 items-end">
            <div className="flex flex-col gap-1">
              <label className="text-[10px] text-muted-foreground uppercase tracking-wide">
                Öğretim Elemanı ID
              </label>
              <Input
                type="number"
                className="h-8 w-36 text-xs"
                placeholder="ID girin"
                value={pendingInstructorId}
                onChange={(e) => setPendingInstructorId(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-[10px] text-muted-foreground uppercase tracking-wide">
                Fakülte ID
              </label>
              <Input
                type="number"
                className="h-8 w-28 text-xs"
                placeholder="ID girin"
                value={pendingFacultyId}
                onChange={(e) => setPendingFacultyId(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-[10px] text-muted-foreground uppercase tracking-wide">
                Program ID
              </label>
              <Input
                type="number"
                className="h-8 w-28 text-xs"
                placeholder="ID girin"
                value={pendingProgramId}
                onChange={(e) => setPendingProgramId(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-[10px] text-muted-foreground uppercase tracking-wide">
                Ara
              </label>
              <Input
                className="h-8 w-48 text-xs"
                placeholder="Ders kodu veya adı"
                value={pendingSearch}
                onChange={(e) => setPendingSearch(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </div>
            <Button size="sm" className="h-8 gap-1.5" onClick={handleSearch}>
              <Search className="h-3.5 w-3.5" />
              Ara
            </Button>
            <Button size="sm" variant="ghost" className="h-8 text-xs" onClick={handleClear}>
              Temizle
            </Button>
          </div>
        </div>

        {/* Add error */}
        {addErrorMessage && (
          <Alert variant="destructive" className="shrink-0 py-2">
            <AlertDescription className="text-xs">{addErrorMessage}</AlertDescription>
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
                  <TableHead className="w-12 py-2 px-2 text-center">Şube</TableHead>
                  <TableHead className="w-24 py-2 px-2">Ders Kodu</TableHead>
                  <TableHead className="py-2 px-2">Ders Adı</TableHead>
                  <TableHead className="w-10 py-2 px-2 text-center">Z/S</TableHead>
                  <TableHead className="w-12 py-2 px-2 text-center">T+U</TableHead>
                  <TableHead className="w-10 py-2 px-2 text-center">KRD</TableHead>
                  <TableHead className="w-12 py-2 px-2 text-center">AKTS</TableHead>
                  <TableHead className="py-2 px-2">Öğretim Üyesi</TableHead>
                  <TableHead className="hidden md:table-cell w-12 py-2 px-2 text-center">Sınıf</TableHead>
                  <TableHead className="hidden lg:table-cell py-2 px-2">AcNeden</TableHead>
                  <TableHead className="hidden lg:table-cell py-2 px-2">Alma Tipi</TableHead>
                  <TableHead className="w-20 py-2 px-2 text-center">Kontenjan</TableHead>
                  <TableHead className="hidden xl:table-cell w-20 py-2 px-2">Grup Kod</TableHead>
                  <TableHead className="w-28 py-2 px-2 text-right">Aksiyon</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.map((row) => (
                  <SectionPickerRow
                    key={row.sectionId}
                    row={row}
                    onAdd={handleAdd}
                    isAdding={addToPlan.isPending}
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
            <Button size="icon" variant="ghost" className="h-7 w-7"
              disabled={page <= 1 || isLoading} onClick={() => setPage((p) => Math.max(1, p - 1))}>
              {isLoading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <ChevronLeft className="h-3.5 w-3.5" />}
            </Button>
            <Button size="icon" variant="ghost" className="h-7 w-7"
              disabled={page >= totalPages || isLoading} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>
              <ChevronRight className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
