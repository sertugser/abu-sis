import { MoreVertical, Pencil, Plus, RotateCcw, Trash2, UserPlus, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { usePermissions } from '@/hooks/usePermissions';
import type { DepartmentSectionClassList } from '@/types/academicOperations';
import type { CourseGroup } from '../types';
import { EmptyMessage } from './EmptyMessage';

interface SectionsPanelProps {
  course: CourseGroup | null;
  selectedSectionId: number | null;
  onSelectSection: (sectionId: number) => void;
  isLoading: boolean;
  // Bulk operations (header buttons)
  onAddSection: () => void;
  onBulkAssignInstructor: () => void;
  onDeleteAllSections: () => void;
  // Single section operations (row dropdown)
  onAssignInstructor: (section: DepartmentSectionClassList) => void;
  onEditCapacity: (section: DepartmentSectionClassList) => void;
  onCloseSection: (section: DepartmentSectionClassList) => void;
  onReactivateSection: (section: DepartmentSectionClassList) => void;
}

export function SectionsPanel({
  course,
  selectedSectionId,
  onSelectSection,
  isLoading,
  onAddSection,
  onBulkAssignInstructor,
  onDeleteAllSections,
  onAssignInstructor,
  onEditCapacity,
  onCloseSection,
  onReactivateSection,
}: SectionsPanelProps) {
  const { hasPermission } = usePermissions();
  const canAddSection = hasPermission('menu.ders-acma-islemleri.open');
  const canBulkAssign = hasPermission('menu.ogretim-elemani-atama.bulk-assign');
  const canCloseAll = hasPermission('menu.ders-acma-islemleri.close-all');
  const hasSections = !!course && course.sections.length > 0;

  return (
    <Card className="overflow-hidden">
      <div className="flex items-center justify-between border-b bg-gray-50 px-4 py-2">
        <span className="text-sm font-semibold text-gray-700">
          Şubeler
          {course && (
            <span className="ml-2 font-normal text-gray-500">
              — {course.courseCode} {course.courseName}
            </span>
          )}
        </span>
        <div className="flex gap-2">
          {canAddSection && (
            <Button
              size="sm"
              variant="outline"
              className="h-8 gap-1.5 text-xs"
              disabled={!course}
              onClick={onAddSection}
            >
              <Plus className="h-3.5 w-3.5" />
              Yeni Şube
            </Button>
          )}
          {canBulkAssign && (
            <Button
              size="sm"
              variant="outline"
              className="h-8 gap-1.5 text-xs"
              disabled={!hasSections}
              onClick={onBulkAssignInstructor}
            >
              <UserPlus className="h-3.5 w-3.5" />
              Öğretim Elemanı Seç
            </Button>
          )}
          {canCloseAll && (
            <Button
              size="sm"
              variant="outline"
              className="h-8 gap-1.5 text-xs text-red-700 hover:text-red-800"
              disabled={!hasSections}
              onClick={onDeleteAllSections}
            >
              <Trash2 className="h-3.5 w-3.5" />
              Tüm Şubelerini Sil
            </Button>
          )}
        </div>
      </div>
      <div className="max-h-[280px] min-h-[200px] overflow-y-auto">
        {isLoading ? (
          <div className="p-4">
            <Skeleton className="h-20 w-full" />
          </div>
        ) : !course ? (
          <EmptyMessage text="Bir ders seçin" />
        ) : course.sections.length === 0 ? (
          <EmptyMessage text="Bu derste şube yok" />
        ) : (
          <Table>
            <TableHeader className="sticky top-0 z-10 bg-gray-100">
              <TableRow>
                <TableHead className="h-9 w-14 text-xs font-semibold text-gray-700">Sb</TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Ders Kodu</TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Ders Adı</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Kontenjan</TableHead>
                <TableHead className="h-9 w-20 text-center text-xs font-semibold text-gray-700">Ö.Say</TableHead>
                <TableHead className="h-9 text-xs font-semibold text-gray-700">Öğretim Elemanı</TableHead>
                <TableHead className="h-9 w-12 text-xs font-semibold text-gray-700"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {course.sections.map(section => (
                <SectionRow
                  key={section.sectionId}
                  section={section}
                  isSelected={section.sectionId === selectedSectionId}
                  onSelect={() => onSelectSection(section.sectionId)}
                  onAssignInstructor={() => onAssignInstructor(section)}
                  onEditCapacity={() => onEditCapacity(section)}
                  onCloseSection={() => onCloseSection(section)}
                  onReactivateSection={() => onReactivateSection(section)}
                />
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </Card>
  );
}

interface SectionRowProps {
  section: DepartmentSectionClassList;
  isSelected: boolean;
  onSelect: () => void;
  onAssignInstructor: () => void;
  onEditCapacity: () => void;
  onCloseSection: () => void;
  onReactivateSection: () => void;
}

function SectionRow({
  section,
  isSelected,
  onSelect,
  onAssignInstructor,
  onEditCapacity,
  onCloseSection,
  onReactivateSection,
}: SectionRowProps) {
  const { hasPermission } = usePermissions();
  const canEdit = hasPermission('menu.ders-acma-islemleri.edit');
  const canCloseSection = hasPermission('menu.ders-acma-islemleri.close-section');
  const canReactivate = hasPermission('menu.ders-acma-islemleri.reactivate');
  return (
    <TableRow
      onClick={onSelect}
      className={cn(
        'cursor-pointer',
        isSelected ? 'bg-blue-100 hover:bg-blue-100' : 'hover:bg-blue-50',
        !section.isActive && 'opacity-60',
      )}
    >
      <TableCell className="font-mono text-sm font-semibold text-gray-800">{section.sectionNumber}</TableCell>
      <TableCell className="font-mono text-sm text-blue-700">{section.courseCode}</TableCell>
      <TableCell className="text-sm text-gray-800">{section.courseName}</TableCell>
      <TableCell className="text-center text-sm tabular-nums text-gray-700">{section.capacity}</TableCell>
      <TableCell className="text-center text-sm tabular-nums text-gray-700">{section.activeStudentCount}</TableCell>
      <TableCell className="text-sm text-gray-700">
        {section.instructorName || <span className="text-amber-600">— Atanmamış</span>}
      </TableCell>
      <TableCell className="text-right">
        <DropdownMenu>
          <DropdownMenuTrigger asChild onClick={e => e.stopPropagation()}>
            <Button variant="ghost" size="icon" className="h-7 w-7" aria-label="Şube işlemleri">
              <MoreVertical className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" onClick={e => e.stopPropagation()}>
            {canEdit && (
              <DropdownMenuItem onClick={onAssignInstructor}>
                <UserPlus className="mr-2 h-4 w-4" />
                Hoca Ata / Değiştir
              </DropdownMenuItem>
            )}
            {canEdit && (
              <DropdownMenuItem onClick={onEditCapacity}>
                <Pencil className="mr-2 h-4 w-4" />
                Kontenjan Düzenle
              </DropdownMenuItem>
            )}
            <DropdownMenuSeparator />
            {section.isActive
              ? canCloseSection && (
                  <DropdownMenuItem onClick={onCloseSection} className="text-red-700 focus:text-red-700">
                    <XCircle className="mr-2 h-4 w-4" />
                    Şubeyi Kapat
                  </DropdownMenuItem>
                )
              : canReactivate && (
                  <DropdownMenuItem onClick={onReactivateSection}>
                    <RotateCcw className="mr-2 h-4 w-4" />
                    Şubeyi Yeniden Aç
                  </DropdownMenuItem>
                )}
          </DropdownMenuContent>
        </DropdownMenu>
      </TableCell>
    </TableRow>
  );
}

