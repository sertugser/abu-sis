// SIS.Web/src/components/grade/TemplateDownloadMenu.tsx
// "Şablon" indirme menüsü: bileşen seçilir → o bileşenin Excel şablonu indirilir.

import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { ChevronDown, Download } from 'lucide-react';
import type { GradeComponent } from '../../types/grade';

interface TemplateDownloadMenuProps {
  components: GradeComponent[];
  disabled?: boolean;
  isDownloading: boolean;
  onDownload: (componentId: number) => void;
}

export function TemplateDownloadMenu({
  components,
  disabled = false,
  isDownloading,
  onDownload,
}: TemplateDownloadMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || isDownloading || components.length === 0}
          className="gap-1.5 text-sm"
        >
          <Download className="h-4 w-4" />
          {isDownloading ? 'İndiriliyor...' : 'Şablon'}
          <ChevronDown className="h-3.5 w-3.5 opacity-60" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel>Şablon İndir (bileşen)</DropdownMenuLabel>
        {components.map((comp) => (
          <DropdownMenuItem key={comp.id} onSelect={() => onDownload(comp.id)}>
            {comp.name} <span className="ml-1 font-mono text-xs text-muted-foreground">%{comp.weight}</span>
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
