import type { ChangeEvent, ReactNode } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface FacultyOption {
  id: number;
  name: string;
}

interface DepartmentOption {
  departmentId: number;
  departmentCode: string;
  departmentName: string;
}

interface FilterPanelProps {
  activeTermName: string;
  availableFaculties: FacultyOption[];
  selectedFacultyId: number | null;
  onFacultyChange: (event: ChangeEvent<HTMLSelectElement>) => void;
  filteredDepartments: DepartmentOption[];
  currentDeptId: number | null;
  onDeptChange: (event: ChangeEvent<HTMLSelectElement>) => void;
  courseCodeFilter: string;
  setCourseCodeFilter: (value: string) => void;
  courseNameFilter: string;
  setCourseNameFilter: (value: string) => void;
  instructorFilter: string;
  setInstructorFilter: (value: string) => void;
  onClearAll: () => void;
}

export function FilterPanel(props: FilterPanelProps) {
  return (
    <Card className="overflow-hidden">
      <div className="border-b bg-gray-50 px-4 py-2">
        <span className="text-sm font-semibold text-gray-700">Ders Şubelendirme İşlemleri</span>
      </div>
      <div className="space-y-2.5 p-4">
        <FilterRow label="Dönem">
          <Input value={props.activeTermName} readOnly disabled className="h-9 text-sm" />
        </FilterRow>
        {props.availableFaculties.length > 1 && (
          <FilterRow label="Fakülte">
            <select
              className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
              value={props.selectedFacultyId ?? ''}
              onChange={props.onFacultyChange}
            >
              <option value="">Tümü</option>
              {props.availableFaculties.map(f => (
                <option key={f.id} value={f.id}>{f.name}</option>
              ))}
            </select>
          </FilterRow>
        )}
        <FilterRow label="Bölüm">
          <select
            className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
            value={props.currentDeptId ?? ''}
            onChange={props.onDeptChange}
          >
            {props.filteredDepartments.map(d => (
              <option key={d.departmentId} value={d.departmentId}>
                {d.departmentCode} — {d.departmentName}
              </option>
            ))}
          </select>
        </FilterRow>
        <FilterRow label="Ders Kodu / Adı">
          <div className="flex w-full gap-2">
            <Input
              placeholder="Kod"
              value={props.courseCodeFilter}
              onChange={e => props.setCourseCodeFilter(e.target.value)}
              className="h-9 w-28 text-sm"
            />
            <span className="self-center text-gray-400">/</span>
            <Input
              placeholder="Ders adı"
              value={props.courseNameFilter}
              onChange={e => props.setCourseNameFilter(e.target.value)}
              className="h-9 flex-1 text-sm"
            />
          </div>
        </FilterRow>
        <FilterRow label="Öğretim Elemanı">
          <Input
            placeholder="Hoca adı"
            value={props.instructorFilter}
            onChange={e => props.setInstructorFilter(e.target.value)}
            className="h-9 w-full text-sm"
          />
        </FilterRow>
        <div className="flex justify-end gap-2 pt-1">
          <Button variant="outline" size="sm" className="h-9 gap-2 text-sm" onClick={props.onClearAll}>
            Temizle
          </Button>
          <Button size="sm" className="h-9 gap-2 text-sm">
            <Search className="h-4 w-4" />
            Ara/Bul
          </Button>
        </div>
      </div>
    </Card>
  );
}

function FilterRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[140px_1fr] items-center gap-3">
      <Label className="text-sm font-medium text-gray-700">{label}</Label>
      {children}
    </div>
  );
}
