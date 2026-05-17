import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Settings, Info, BarChart3, BookText, ClipboardList, CalendarDays } from 'lucide-react';

interface CourseActionMenuProps {
  onOpenCourseInfo: () => void;
  onOpenCourseDetail: () => void;
}

/**
 * "Alınan Dersler" tablosundaki dişli ikonu menüsü.
 * Faz 1: "Ders Bilgileri" + "Dersin Ayrıntıları" aktif.
 * Faz 2/3: "Haftalık Konular" + "Ödevler" + "Öğr. Elemanı Programı" disabled (Yakında).
 */
export function CourseActionMenu({ onOpenCourseInfo, onOpenCourseDetail }: CourseActionMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="inline-flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20"
          aria-label="Ders işlemleri"
        >
          <Settings className="h-4 w-4" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        <DropdownMenuItem onClick={onOpenCourseInfo} className="gap-2">
          <Info className="h-4 w-4" />
          Ders Bilgileri
        </DropdownMenuItem>
        <DropdownMenuItem onClick={onOpenCourseDetail} className="gap-2">
          <BarChart3 className="h-4 w-4" />
          Dersin Ayrıntıları
        </DropdownMenuItem>

        <Tooltip>
          <TooltipTrigger asChild>
            <div>
              <DropdownMenuItem disabled className="gap-2">
                <BookText className="h-4 w-4" />
                Haftalık Ders Konuları
              </DropdownMenuItem>
            </div>
          </TooltipTrigger>
          <TooltipContent side="right">Yakında</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <div>
              <DropdownMenuItem disabled className="gap-2">
                <ClipboardList className="h-4 w-4" />
                Dersin Ödevleri
              </DropdownMenuItem>
            </div>
          </TooltipTrigger>
          <TooltipContent side="right">Yakında</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <div>
              <DropdownMenuItem disabled className="gap-2">
                <CalendarDays className="h-4 w-4" />
                Öğr. Elemanı Ders Programı
              </DropdownMenuItem>
            </div>
          </TooltipTrigger>
          <TooltipContent side="right">Yakında</TooltipContent>
        </Tooltip>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
