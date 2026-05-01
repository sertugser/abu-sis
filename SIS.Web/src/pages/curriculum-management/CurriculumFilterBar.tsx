import { Loader2, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  useAdminFaculties,
  useAdminDepartmentsByStructure,
  useAdminProgramsByStructure,
} from '../../hooks/useAdminAcademicStructure';

export type StatusFilter = 'all' | 'active' | 'inactive' | 'archived';
export type YearFilter = 'all' | string;

interface Props {
  facultyId: number | undefined;
  departmentId: number | undefined;
  programId: number | undefined;
  yearFilter: YearFilter;
  statusFilter: StatusFilter;
  searchKeyword: string;
  availableYears: string[];
  onFacultyChange: (val: string) => void;
  onDepartmentChange: (val: string) => void;
  onProgramChange: (val: string) => void;
  onYearFilterChange: (val: YearFilter) => void;
  onStatusFilterChange: (val: StatusFilter) => void;
  onSearchKeywordChange: (val: string) => void;
  onOpenCreateDialog: () => void;
}

export default function CurriculumFilterBar({
  facultyId,
  departmentId,
  programId,
  yearFilter,
  statusFilter,
  searchKeyword,
  availableYears,
  onFacultyChange,
  onDepartmentChange,
  onProgramChange,
  onYearFilterChange,
  onStatusFilterChange,
  onSearchKeywordChange,
  onOpenCreateDialog,
}: Props) {
  const { data: faculties, isLoading: loadingFaculties } = useAdminFaculties();
  const { data: departments, isLoading: loadingDepts } = useAdminDepartmentsByStructure(facultyId);
  const { data: programs, isLoading: loadingPrograms } = useAdminProgramsByStructure(departmentId);

  return (
    <Card>
      <CardContent className="pt-4 pb-4 space-y-3">
        {/* Üst: sadece Müfredat Ekle (ince çizgi altta ayraç) */}
        <div className="flex items-center justify-end border-b pb-2">
          <Button size="sm" className="h-9" onClick={onOpenCreateDialog} title="Yeni müfredat ekle">
            <Plus className="h-4 w-4 mr-1" /> Müfredat Ekle
          </Button>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-x-6 gap-y-2.5">
          {/* Sol: Fakülte */}
          <div className="grid grid-cols-[65px_1fr] md:grid-cols-[80px_1fr] items-center gap-4">
            <Label className="text-sm text-left">Fakülte</Label>
            <Select value={facultyId?.toString() ?? ''} onValueChange={onFacultyChange}>
              <SelectTrigger className="h-9 text-sm">
                {loadingFaculties
                  ? <Loader2 className="h-4 w-4 animate-spin" />
                  : <SelectValue placeholder="Seçin" />}
              </SelectTrigger>
              <SelectContent>
                {(faculties ?? []).map(f => (
                  <SelectItem key={f.id} value={f.id.toString()}>{f.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Sağ: Müfredat Yılı */}
          <div className="grid grid-cols-[90px_1fr] md:grid-cols-[110px_1fr] items-center gap-4">
            <Label className="text-sm text-left">Müfredat Yılı</Label>
            <Select value={yearFilter} onValueChange={val => onYearFilterChange(val as YearFilter)}>
              <SelectTrigger className="h-9 text-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {availableYears.map(y => (
                  <SelectItem key={y} value={y}>{y}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Sol: Bölüm */}
          <div className="grid grid-cols-[65px_1fr] md:grid-cols-[80px_1fr] items-center gap-4">
            <Label className="text-sm text-left">Bölüm</Label>
            <Select
              value={departmentId?.toString() ?? ''}
              onValueChange={onDepartmentChange}
              disabled={!facultyId}
            >
              <SelectTrigger className="h-9 text-sm">
                {loadingDepts
                  ? <Loader2 className="h-4 w-4 animate-spin" />
                  : <SelectValue placeholder="Seçin" />}
              </SelectTrigger>
              <SelectContent>
                {(departments ?? []).map(d => (
                  <SelectItem key={d.id} value={d.id.toString()}>{d.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Sağ: Durum */}
          <div className="grid grid-cols-[90px_1fr] md:grid-cols-[110px_1fr] items-center gap-4">
            <Label className="text-sm text-left">Durum</Label>
            <Select value={statusFilter} onValueChange={val => onStatusFilterChange(val as StatusFilter)}>
              <SelectTrigger className="h-9 text-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                <SelectItem value="active">Aktif</SelectItem>
                <SelectItem value="inactive">Pasif</SelectItem>
                <SelectItem value="archived">Arşiv</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Sol: Program */}
          <div className="grid grid-cols-[65px_1fr] md:grid-cols-[80px_1fr] items-center gap-4">
            <Label className="text-sm text-left">Program</Label>
            <Select
              value={programId?.toString() ?? ''}
              onValueChange={onProgramChange}
              disabled={!departmentId}
            >
              <SelectTrigger className="h-9 text-sm">
                {loadingPrograms
                  ? <Loader2 className="h-4 w-4 animate-spin" />
                  : <SelectValue placeholder="Seçin" />}
              </SelectTrigger>
              <SelectContent>
                {(programs ?? []).map(p => (
                  <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Sağ: Anahtar kelimeyle ara — fakülte / program / müfredat adı / yıl / program kodu içinde */}
          <div className="grid grid-cols-[90px_1fr] md:grid-cols-[110px_1fr] items-center gap-4">
            <Label className="text-sm text-left">Ara</Label>
            <div className="relative">
              <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
              <Input
                placeholder="Fakülte, program, müfredat, yıl..."
                className="h-9 text-sm pl-8"
                value={searchKeyword}
                onChange={e => onSearchKeywordChange(e.target.value)}
              />
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
