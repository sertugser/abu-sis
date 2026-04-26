import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  BookOpen, CheckCircle, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight,
  Copy, Edit, GraduationCap, Loader2, MoreHorizontal, Plus, Printer, Scissors,
  Settings, Trash2,
} from 'lucide-react';
import type { CurriculumListDto } from '../../types/curriculumManagement';

interface RowTarget {
  id: number;
  label: string;
}

interface Props {
  hasFilter: boolean;
  isLoading: boolean;
  filteredCount: number;
  pagedCurricula: CurriculumListDto[];
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onOpenCreateDialog: () => void;
  onOpenEditDialog: (c: CurriculumListDto) => void;
  onViewCourses: (id: number) => void;
  onOpenTermRules: (target: RowTarget) => void;
  onCopy: (target: RowTarget) => void;
  onActivate: (target: RowTarget) => void;
  onDelete: (target: RowTarget) => void;
  onNavigatePromotionRules: () => void;
}

function rowLabel(c: CurriculumListDto): string {
  return `${c.version} — ${c.name ?? c.programName}`;
}

export default function CurriculumListView({
  hasFilter,
  isLoading,
  filteredCount,
  pagedCurricula,
  currentPage,
  totalPages,
  onPageChange,
  onOpenCreateDialog,
  onOpenEditDialog,
  onViewCourses,
  onOpenTermRules,
  onCopy,
  onActivate,
  onDelete,
  onNavigatePromotionRules,
}: Props) {
  // Filtre yok — başlangıç durumu
  if (!hasFilter) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          <BookOpen className="h-12 w-12 mx-auto mb-3 opacity-30" />
          <p>Müfredatları listelemek için en az bir fakülte seçin</p>
        </CardContent>
      </Card>
    );
  }

  if (isLoading) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-muted-foreground">
          <Loader2 className="h-6 w-6 animate-spin mx-auto mb-2" />
          Yükleniyor...
        </CardContent>
      </Card>
    );
  }

  if (filteredCount === 0) {
    return (
      <Card>
        <CardContent className="py-16 text-center text-muted-foreground">
          <p className="mb-2">Filtre kriterlerinize uygun müfredat bulunamadı.</p>
          <Button variant="outline" onClick={onOpenCreateDialog}>
            <Plus className="h-4 w-4 mr-1" /> Yeni Müfredat Oluştur
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 border-b">
              <tr className="text-left">
                <th className="px-3 py-2.5 font-medium w-10"></th>
                <th className="px-3 py-2.5 font-medium whitespace-nowrap">Yıl</th>
                <th className="px-3 py-2.5 font-medium min-w-[200px]">Müfredat Adı</th>
                <th className="px-3 py-2.5 font-medium whitespace-nowrap">Başl.Tarihi</th>
                <th className="px-3 py-2.5 font-medium whitespace-nowrap">Bit.Tarihi</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Aktif</th>
                <th className="px-3 py-2.5 font-medium min-w-[180px]">Fakülte</th>
                <th className="px-3 py-2.5 font-medium min-w-[200px]">Program</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.Kredi</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.AKTS</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.Ders</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Zor.Ders</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Seç.Ders</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Akt.Öğr.Say</th>
                <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Top.Öğr.Say</th>
                <th className="px-3 py-2.5 font-medium w-20 text-center">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {pagedCurricula.map(c => (
                <tr key={c.id} className="border-b hover:bg-muted/30">
                  {/* Sol dişli menü */}
                  <td className="px-3 py-2">
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8" title="İşlemler">
                          <Settings className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="start" className="w-56">
                        <DropdownMenuItem onClick={() => onViewCourses(c.id)}>
                          <BookOpen className="h-4 w-4 mr-2" /> Müfredat Dersleri
                        </DropdownMenuItem>
                        <DropdownMenuItem onClick={() => onOpenTermRules({ id: c.id, label: `(${c.version}) ${c.name ?? c.programName}` })}>
                          <Settings className="h-4 w-4 mr-2" /> Dönem Kuralları
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={onNavigatePromotionRules}
                          title="Müfredat Sınıf Dönem Tanımları sayfasını aç"
                        >
                          <GraduationCap className="h-4 w-4 mr-2" /> Sınıf Atlatma Kuralları
                        </DropdownMenuItem>
                        <DropdownMenuItem disabled title="Yakında">
                          <Scissors className="h-4 w-4 mr-2" /> Enstitü İlişik Kesme Kr.
                        </DropdownMenuItem>
                        <DropdownMenuItem disabled title="Yakında">
                          <Printer className="h-4 w-4 mr-2" /> Ders Listesi Yazdır
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </td>
                  <td className="px-3 py-2 whitespace-nowrap font-medium">{c.version}</td>
                  <td className="px-3 py-2">{c.name ?? '—'}</td>
                  <td className="px-3 py-2 whitespace-nowrap">{c.startDate ? c.startDate.slice(0, 10) : '—'}</td>
                  <td className="px-3 py-2 whitespace-nowrap">{c.endDate ? c.endDate.slice(0, 10) : '—'}</td>
                  <td className="px-3 py-2 text-center">
                    {c.isActive && <CheckCircle className="h-4 w-4 text-green-600 mx-auto" />}
                  </td>
                  <td className="px-3 py-2">{c.facultyName}</td>
                  <td className="px-3 py-2">{c.programName}</td>
                  <td className="px-3 py-2 text-center">{c.totalCredit}</td>
                  <td className="px-3 py-2 text-center">{c.totalEcts}</td>
                  <td className="px-3 py-2 text-center">{c.courseCount}</td>
                  <td className="px-3 py-2 text-center">{c.compulsoryCount}</td>
                  <td className="px-3 py-2 text-center">{c.electiveCount}</td>
                  <td className="px-3 py-2 text-center">{c.activeStudentCount}</td>
                  <td className="px-3 py-2 text-center">{c.totalStudentCount}</td>
                  {/* Sağ aksiyonlar — Düzenle (primary) + kebab (Kopyala / Aktif Et / Sil) */}
                  <td className="px-3 py-2">
                    <div className="flex items-center justify-center gap-0.5">
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        title="Düzenle"
                        onClick={() => onOpenEditDialog(c)}
                      >
                        <Edit className="h-4 w-4" />
                      </Button>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8" title="Daha fazla">
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => onCopy({ id: c.id, label: rowLabel(c) })}>
                            <Copy className="h-4 w-4 mr-2" /> Kopyala
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            disabled={c.isActive}
                            onClick={() => onActivate({ id: c.id, label: rowLabel(c) })}
                          >
                            <CheckCircle className={`h-4 w-4 mr-2 ${c.isActive ? 'text-green-600' : ''}`} />
                            {c.isActive ? 'Zaten Aktif' : 'Aktif Et'}
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            className="text-destructive focus:text-destructive"
                            onClick={() => onDelete({ id: c.id, label: rowLabel(c) })}
                          >
                            <Trash2 className="h-4 w-4 mr-2" /> Sil
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination — mobilde flex-wrap */}
        <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2.5 border-t bg-muted/20 text-sm">
          <span className="text-muted-foreground">
            {filteredCount} kayıt — Sayfa {currentPage}/{totalPages}
          </span>
          {totalPages > 1 && (
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === 1} onClick={() => onPageChange(1)}>
                <ChevronsLeft className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === 1} onClick={() => onPageChange(Math.max(1, currentPage - 1))}>
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span className="px-2 min-w-[2rem] text-center">{currentPage}</span>
              <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === totalPages} onClick={() => onPageChange(Math.min(totalPages, currentPage + 1))}>
                <ChevronRight className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="icon" className="h-8 w-8" disabled={currentPage === totalPages} onClick={() => onPageChange(totalPages)}>
                <ChevronsRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
