import { useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  BookOpen, CheckCircle, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight,
  Copy, Edit, Layers, MoreHorizontal, Package, Plus, Printer, Scissors,
  Settings, Trash2,
} from 'lucide-react';
import type {
  CurriculumCourseItemDto,
  CurriculumDetailDto,
  CurriculumListDto,
} from '../../types/curriculumManagement';

type TermTypeFilter = 'all' | 'fall' | 'spring';

interface RowTarget {
  id: number;
  label: string;
}

interface CourseLinksTarget {
  courseId: number;
  label: string;
  mode: 'prerequisite' | 'equivalency';
}

interface Props {
  detail: CurriculumDetailDto | undefined;
  selectedCurr: CurriculumListDto | undefined;
  selectedCurriculumId: number | null;
  isLoadingDetail: boolean;

  // Filter state (lift up)
  searchCode: string;
  searchName: string;
  classFilter: number | null;
  termTypeFilter: TermTypeFilter;
  showGroupedCourses: boolean;
  onSearchCodeChange: (val: string) => void;
  onSearchNameChange: (val: string) => void;
  onClassFilterChange: (val: number | null) => void;
  onTermTypeFilterChange: (val: TermTypeFilter) => void;
  onShowGroupedCoursesChange: (val: boolean) => void;

  // Course list + pagination
  pagedCourses: CurriculumCourseItemDto[];
  flatCoursesCount: number;
  flatCoursesCredit: number;
  flatCoursesEcts: number;
  coursesPage: number;
  coursesTotalPages: number;
  onCoursesPageChange: (page: number) => void;

  // Curriculum-level actions
  onBack: () => void;
  onOpenEditDialog: (c: CurriculumListDto) => void;
  onCopy: (target: RowTarget) => void;
  onActivate: (target: RowTarget) => void;
  onDelete: (target: RowTarget) => void;

  // Course-level actions
  onAddCourse: () => void;
  onShowCourseDetail: (courseId: number) => void;
  onOpenCourseLinks: (target: CourseLinksTarget) => void;
  onRemoveCourse: (courseId: number) => void;
  isRemovingCourse: boolean;
}

function curriculumLabel(c: CurriculumListDto): string {
  return `${c.version} — ${c.name ?? c.programName}`;
}

export default function CurriculumCoursesView({
  detail,
  selectedCurr,
  selectedCurriculumId,
  isLoadingDetail,
  searchCode,
  searchName,
  classFilter,
  termTypeFilter,
  showGroupedCourses,
  onSearchCodeChange,
  onSearchNameChange,
  onClassFilterChange,
  onTermTypeFilterChange,
  onShowGroupedCoursesChange,
  pagedCourses,
  flatCoursesCount,
  flatCoursesCredit,
  flatCoursesEcts,
  coursesPage,
  coursesTotalPages,
  onCoursesPageChange,
  onBack,
  onOpenEditDialog,
  onCopy,
  onActivate,
  onDelete,
  onAddCourse,
  onShowCourseDetail,
  onOpenCourseLinks,
  onRemoveCourse,
  isRemovingCourse,
}: Props) {
  // Müfredatın tamamı — filtreden bağımsız (tüm dersler)
  const curriculumTotal = useMemo(() => {
    if (!detail) return { count: 0, credit: 0, ects: 0 };
    return {
      count: detail.courses.length,
      credit: detail.courses.reduce((s, c) => s + (c.nationalCredit ?? 0), 0),
      ects: detail.courses.reduce((s, c) => s + c.ects, 0),
    };
  }, [detail]);

  return (
    <>
      {/* Breadcrumb + geri butonu + müfredat aksiyonları */}
      <Card>
        <CardContent className="py-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <Button variant="ghost" size="sm" className="h-8" onClick={onBack}>
                <ChevronLeft className="h-4 w-4 mr-1" /> Müfredat Listesi
              </Button>
              <span className="text-muted-foreground">/</span>
              <span className="font-medium">
                {detail ? `${detail.programName} — ${detail.version}` : 'Yükleniyor...'}
              </span>
              {detail?.isActive && <Badge variant="default" className="text-xs">Aktif</Badge>}
            </div>
            {/* Müfredat seviyesi aksiyonlar — Düzenle (primary) + kebab (Kopyala / Aktif Et / Sil) */}
            {detail && selectedCurr && (
              <div className="flex items-center gap-0.5">
                <Button
                  variant="ghost" size="icon" className="h-8 w-8"
                  title="Düzenle"
                  onClick={() => onOpenEditDialog(selectedCurr)}
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
                    <DropdownMenuItem onClick={() => onCopy({ id: selectedCurr.id, label: curriculumLabel(selectedCurr) })}>
                      <Copy className="h-4 w-4 mr-2" /> Kopyala
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      disabled={detail.isActive}
                      onClick={() => onActivate({ id: selectedCurr.id, label: curriculumLabel(selectedCurr) })}
                    >
                      <CheckCircle className={`h-4 w-4 mr-2 ${detail.isActive ? 'text-green-600' : ''}`} />
                      {detail.isActive ? 'Zaten Aktif' : 'Aktif Et'}
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      className="text-destructive focus:text-destructive"
                      onClick={() => onDelete({ id: selectedCurr.id, label: curriculumLabel(selectedCurr) })}
                    >
                      <Trash2 className="h-4 w-4 mr-2" /> Sil
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {!selectedCurriculumId ? null : (
        <>
          {/* ── Tek satır: Filtreler + Bilgi + Aksiyonlar ── */}
          {isLoadingDetail ? (
            <Skeleton className="h-16 w-full" />
          ) : detail && (
            <Card>
              <CardContent className="py-3">
                <div className="flex flex-wrap items-center gap-2">
                  {/* Proliz tarzı: iki ayrı arama alanı (Ders Kodu | Ders Adı) */}
                  <Input
                    placeholder="Ders Kodu"
                    className="h-9 w-full sm:w-32 text-sm"
                    value={searchCode}
                    onChange={e => onSearchCodeChange(e.target.value)}
                  />
                  <Input
                    placeholder="Ders Adı"
                    className="h-9 w-full sm:w-44 text-sm"
                    value={searchName}
                    onChange={e => onSearchNameChange(e.target.value)}
                  />

                  <Select
                    value={classFilter?.toString() ?? 'all'}
                    onValueChange={val => onClassFilterChange(val === 'all' ? null : Number(val))}
                  >
                    <SelectTrigger className="h-9 w-32 text-sm">
                      <SelectValue placeholder="Sınıf: Tümü" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">Sınıf: Tümü</SelectItem>
                      <SelectItem value="1">1. Sınıf</SelectItem>
                      <SelectItem value="2">2. Sınıf</SelectItem>
                      <SelectItem value="3">3. Sınıf</SelectItem>
                      <SelectItem value="4">4. Sınıf</SelectItem>
                    </SelectContent>
                  </Select>

                  <Select
                    value={termTypeFilter}
                    onValueChange={val => onTermTypeFilterChange(val as TermTypeFilter)}
                  >
                    <SelectTrigger className="h-9 w-32 text-sm">
                      <SelectValue placeholder="Dönem: Tümü" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">Dönem: Tümü</SelectItem>
                      <SelectItem value="fall">Güz</SelectItem>
                      <SelectItem value="spring">Bahar</SelectItem>
                    </SelectContent>
                  </Select>

                  <span className="hidden md:inline text-muted-foreground">|</span>

                  <label className="flex items-center gap-1.5 text-sm cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={showGroupedCourses}
                      onChange={e => onShowGroupedCoursesChange(e.target.checked)}
                      className="h-4 w-4 rounded border-input accent-primary cursor-pointer"
                    />
                    <span>Gruplu Dersleri Göster</span>
                  </label>

                  {/* Sağ aksiyonlar — Proliz: Ders Ekle | Grup Ekle | Ek İşlemler */}
                  <div className="ml-auto flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      className="h-9 text-sm"
                      onClick={onAddCourse}
                    >
                      <Plus className="h-4 w-4 mr-1" /> Ders Ekle
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      className="h-9 text-sm"
                      disabled
                      title="Yakında"
                    >
                      <Plus className="h-4 w-4 mr-1" /> Grup Ekle
                    </Button>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button size="sm" variant="outline" className="h-9 text-sm">
                          <MoreHorizontal className="h-4 w-4 mr-1" />
                          Ek İşlemler
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem disabled title="Yakında">
                          <Printer className="h-4 w-4 mr-2" /> Ders Listesi Yazdır
                        </DropdownMenuItem>
                        <DropdownMenuItem disabled title="Yakında">
                          <Package className="h-4 w-4 mr-2" /> Bilgi Paketi
                        </DropdownMenuItem>
                        <DropdownMenuItem disabled title="Yakında">
                          <Layers className="h-4 w-4 mr-2" /> Toplu Gruplandırma
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {isLoadingDetail ? (
            <Skeleton className="h-64 w-full" />
          ) : !detail ? (
            <Card>
              <CardContent className="py-8 text-center text-muted-foreground">
                Veri yüklenemedi
              </CardContent>
            </Card>
          ) : flatCoursesCount === 0 ? (
            <Card>
              <CardContent className="py-12 text-center text-muted-foreground">
                {searchCode || searchName || classFilter !== null || termTypeFilter !== 'all'
                  ? 'Filtrenizle eşleşen ders bulunamadı'
                  : 'Henüz ders eklenmemiş'}
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-0">
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-muted/50 border-b">
                      <tr className="text-left">
                        <th className="px-2 py-2.5 w-10"></th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">Ders Kodu</th>
                        <th className="px-3 py-2.5 font-medium min-w-[240px]">Ders Adı</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Z/S</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Krd</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">AKTS</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Teo</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Uyg</th>
                        <th className="px-3 py-2.5 font-medium text-right whitespace-nowrap">Lab</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Sınıf</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Dönem</th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">Kategori</th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">Öğr.Elemanı</th>
                        <th className="px-3 py-2.5 font-medium whitespace-nowrap">İntibak</th>
                        <th className="px-3 py-2.5 font-medium text-center whitespace-nowrap">Ön K</th>
                        <th className="px-3 py-2.5 font-medium text-center w-24">İşlem</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pagedCourses.map(c => {
                        const sinif = Math.ceil(c.suggestedSemester / 2);
                        const donem = c.suggestedSemester % 2 === 1 ? 'G' : 'B';
                        const courseLabel = `${c.courseCode} — ${c.courseName}`;
                        return (
                          <tr key={c.courseId} className="border-b hover:bg-muted/30">
                            <td className="px-2 py-2">
                              <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-7 w-7"
                                    title="Ders işlemleri"
                                  >
                                    <Settings className="h-4 w-4" />
                                  </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="start">
                                  <DropdownMenuItem disabled title="Yakında">
                                    <Package className="h-4 w-4 mr-2" /> Bilgi Paketi
                                  </DropdownMenuItem>
                                  <DropdownMenuItem
                                    onClick={() =>
                                      onOpenCourseLinks({
                                        courseId: c.courseId,
                                        label: courseLabel,
                                        mode: 'prerequisite',
                                      })
                                    }
                                  >
                                    <BookOpen className="h-4 w-4 mr-2" /> Ön Koşul Tanımları
                                  </DropdownMenuItem>
                                  <DropdownMenuItem disabled title="Yakında">
                                    <Layers className="h-4 w-4 mr-2" /> Gruplandırma İşlemleri
                                  </DropdownMenuItem>
                                  <DropdownMenuItem
                                    onClick={() =>
                                      onOpenCourseLinks({
                                        courseId: c.courseId,
                                        label: courseLabel,
                                        mode: 'equivalency',
                                      })
                                    }
                                  >
                                    <Scissors className="h-4 w-4 mr-2" /> Eşdeğer Ders Tanımları
                                  </DropdownMenuItem>
                                </DropdownMenuContent>
                              </DropdownMenu>
                            </td>
                            <td className="px-3 py-2 whitespace-nowrap font-medium">{c.courseCode}</td>
                            <td className="px-3 py-2">
                              <button
                                type="button"
                                onClick={() => onShowCourseDetail(c.courseId)}
                                className="text-left text-primary hover:underline"
                                title="Ders detayı"
                              >
                                {c.courseName}
                              </button>
                            </td>
                            <td className="px-3 py-2 text-center">
                              <Badge variant={c.isCompulsory ? 'default' : 'secondary'} className="text-xs">
                                {c.isCompulsory ? 'Z' : 'S'}
                              </Badge>
                            </td>
                            <td className="px-3 py-2 text-right">
                              {c.nationalCredit > 0 ? c.nationalCredit : <span className="text-muted-foreground">—</span>}
                            </td>
                            <td className="px-3 py-2 text-right">{c.ects}</td>
                            <td className="px-3 py-2 text-right">{c.theoryHours}</td>
                            <td className="px-3 py-2 text-right text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-right">{c.labHours}</td>
                            <td className="px-3 py-2 text-center">{sinif}</td>
                            <td className="px-3 py-2 text-center">{donem}</td>
                            <td className="px-3 py-2">{c.categoryName ?? '—'}</td>
                            <td className="px-3 py-2 text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-muted-foreground">—</td>
                            <td className="px-3 py-2 text-center text-muted-foreground">—</td>
                            <td className="px-3 py-2">
                              <div className="flex items-center justify-center gap-0.5">
                                <Button
                                  variant="ghost" size="icon" className="h-8 w-8"
                                  title="Düzenle"
                                  onClick={() => onShowCourseDetail(c.courseId)}
                                >
                                  <Edit className="h-4 w-4" />
                                </Button>
                                <Button
                                  variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive"
                                  title="Sil"
                                  onClick={() => onRemoveCourse(c.courseId)}
                                  disabled={isRemovingCourse}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Pagination — ders listesi (15 ders/sayfa) */}
                <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2.5 border-t bg-muted/20 text-sm">
                  <span className="text-muted-foreground">
                    {flatCoursesCount} ders — Sayfa {coursesPage}/{coursesTotalPages}
                  </span>
                  {coursesTotalPages > 1 && (
                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === 1}
                        onClick={() => onCoursesPageChange(1)}
                        title="İlk sayfa"
                      >
                        <ChevronsLeft className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === 1}
                        onClick={() => onCoursesPageChange(Math.max(1, coursesPage - 1))}
                        title="Önceki"
                      >
                        <ChevronLeft className="h-4 w-4" />
                      </Button>
                      <span className="px-2 min-w-[2rem] text-center">{coursesPage}</span>
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === coursesTotalPages}
                        onClick={() => onCoursesPageChange(Math.min(coursesTotalPages, coursesPage + 1))}
                        title="Sonraki"
                      >
                        <ChevronRight className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost" size="icon" className="h-8 w-8"
                        disabled={coursesPage === coursesTotalPages}
                        onClick={() => onCoursesPageChange(coursesTotalPages)}
                        title="Son sayfa"
                      >
                        <ChevronsRight className="h-4 w-4" />
                      </Button>
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          )}

          {/* ── Alt Summary Bar — Listelenen + Müfredat Dönem ── */}
          {!isLoadingDetail && detail && (
            <Card>
              <CardContent className="py-3 text-sm tabular-nums space-y-1">
                <div className="flex flex-wrap items-center gap-x-6 gap-y-1">
                  <span>
                    <span className="font-medium">Listelenen Ders Say:</span>{' '}
                    <span>{flatCoursesCount}</span>
                  </span>
                  <span>
                    <span className="font-medium">Kredi:</span>{' '}
                    <span>{flatCoursesCredit}</span>
                  </span>
                  <span>
                    <span className="font-medium">AKTS:</span>{' '}
                    <span>{flatCoursesEcts}</span>
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-x-6 gap-y-1">
                  <span>
                    <span className="font-medium">Müfredat Dönem Ders Say:</span>{' '}
                    <span>{curriculumTotal.count}</span>
                  </span>
                  <span>
                    <span className="font-medium">Kredi:</span>{' '}
                    <span>{curriculumTotal.credit}</span>
                  </span>
                  <span>
                    <span className="font-medium">AKTS:</span>{' '}
                    <span>{curriculumTotal.ects}</span>
                  </span>
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </>
  );
}
