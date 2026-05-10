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

  const labelClass = 'text-xs font-medium text-muted-foreground';
  const triggerClass = 'h-9 text-sm w-full';

  return (
    <Card>
      <CardContent className="pt-4 pb-4">
        {/* 3 kolon × 2 satır — üst: cascade (Fakülte/Bölüm/Program), alt: filtre (Yıl/Durum/Ara) */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-x-4 gap-y-3">
          {/* Fakülte */}
          <div className="space-y-1">
            <Label className={labelClass}>Fakülte</Label>
            <Select value={facultyId?.toString() ?? ''} onValueChange={onFacultyChange}>
              <SelectTrigger className={triggerClass}>
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

          {/* Bölüm */}
          <div className="space-y-1">
            <Label className={labelClass}>Bölüm</Label>
            <Select
              value={departmentId?.toString() ?? ''}
              onValueChange={onDepartmentChange}
              disabled={!facultyId}
            >
              <SelectTrigger className={triggerClass}>
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

          {/* Program */}
          <div className="space-y-1">
            <Label className={labelClass}>Program</Label>
            <Select
              value={programId?.toString() ?? ''}
              onValueChange={onProgramChange}
              disabled={!departmentId}
            >
              <SelectTrigger className={triggerClass}>
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

          {/* Müfredat Yılı */}
          <div className="space-y-1">
            <Label className={labelClass}>Müfredat Yılı</Label>
            <Select value={yearFilter} onValueChange={val => onYearFilterChange(val as YearFilter)}>
              <SelectTrigger className={triggerClass}>
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

          {/* Durum */}
          <div className="space-y-1">
            <Label className={labelClass}>Durum</Label>
            <Select value={statusFilter} onValueChange={val => onStatusFilterChange(val as StatusFilter)}>
              <SelectTrigger className={triggerClass}>
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

          {/* Ara — yarısı arama input, yarısı Müfredat Ekle butonu */}
          <div className="space-y-1">
            <Label className={labelClass}>Ara</Label>
            <div className="grid grid-cols-2 gap-2">
              <div className="relative">
                <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
                <Input
                  placeholder="Fakülte, program..."
                  className="h-9 text-sm pl-8 w-full"
                  value={searchKeyword}
                  onChange={e => onSearchKeywordChange(e.target.value)}
                />
              </div>
              <Button
                variant="outline"
                className="h-9 w-full border-dashed text-primary hover:bg-primary/5 hover:border-primary hover:text-primary"
                onClick={onOpenCreateDialog}
                title="Yeni müfredat ekle"
              >
                <Plus className="h-4 w-4 mr-1" /> Müfredat Ekle
              </Button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
