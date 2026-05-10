import { useMemo, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, BookOpen, Search } from 'lucide-react';
import { useAdminCourses, type CourseListItemDto } from '../../hooks/useAdminCourseCatalog';
import { useCurriculumCoursesForOpening, useOpenCourse } from '../../hooks/useCourseOpening';
import { cn } from '@/lib/utils';

interface Props {
  departmentId: number;
  termId: number;
  onClose: () => void;
}

/**
 * Müfredata bağlı kalmadan serbest ders açma diyaloğu.
 * Bölümün sahip olduğu tüm dersleri listeler, müfredatta olanları işaretler,
 * müfredat-dışı bir dersi de tek tıkla açmaya izin verir.
 *
 * Backend (CourseOpeningService.OpenCourseAsync) müfredat kontrolü yapmaz —
 * sadece course.DepartmentId == departmentId şartı aranır.
 */
export function FreeCourseOpenDialog({ departmentId, termId, onClose }: Props) {
  const [search, setSearch] = useState('');
  const [selectedCourseId, setSelectedCourseId] = useState<number | null>(null);
  const [sectionCount, setSectionCount] = useState(1);
  const [capacity, setCapacity] = useState(40);

  const coursesQuery = useAdminCourses({
    departmentId,
    search: search.trim() ? search.trim() : undefined,
  });
  const curriculumQuery = useCurriculumCoursesForOpening(departmentId, termId, true);
  const openMutation = useOpenCourse(departmentId, termId);

  const curriculumCourseIds = useMemo(() => {
    const set = new Set<number>();
    for (const c of curriculumQuery.data ?? []) set.add(c.courseId);
    return set;
  }, [curriculumQuery.data]);

  const alreadyOpenedIds = useMemo(() => {
    const set = new Set<number>();
    for (const c of curriculumQuery.data ?? []) {
      if (c.alreadyOpened) set.add(c.courseId);
    }
    return set;
  }, [curriculumQuery.data]);

  const selectedCourse: CourseListItemDto | undefined = useMemo(() => {
    if (!selectedCourseId) return undefined;
    return coursesQuery.data?.find(c => c.id === selectedCourseId);
  }, [coursesQuery.data, selectedCourseId]);

  const handleOpen = () => {
    if (!selectedCourseId) return;
    openMutation.mutate(
      {
        courseId: selectedCourseId,
        sectionCount,
        capacityPerSection: capacity,
      },
      {
        onSuccess: () => {
          setSelectedCourseId(null);
          setSearch('');
          onClose();
        },
      },
    );
  };

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-3xl max-h-[85vh] flex flex-col">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <BookOpen className="h-4 w-4" />
            Serbest Ders Aç (Müfredat Dışı)
          </DialogTitle>
          <DialogDescription>
            Bölümün sahip olduğu herhangi bir dersi, müfredatta olmasa bile açabilirsiniz.
            Müfredatta olanlar işaretlenmiştir.
          </DialogDescription>
        </DialogHeader>

        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
          <Input
            placeholder="Ders kodu veya adıyla ara..."
            className="pl-9"
            value={search}
            onChange={e => setSearch(e.target.value)}
            autoFocus
          />
        </div>

        <div className="flex-1 min-h-0 overflow-y-auto border rounded-md">
          {coursesQuery.isLoading ? (
            <div className="p-3 space-y-2">
              {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-10 rounded" />)}
            </div>
          ) : coursesQuery.isError ? (
            <Alert variant="destructive" className="m-3">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Dersler yüklenemedi</AlertTitle>
              <AlertDescription>Yetki bilgileri veya ağ hatası.</AlertDescription>
            </Alert>
          ) : (coursesQuery.data ?? []).length === 0 ? (
            <div className="text-center text-sm text-gray-500 py-12">
              Eşleşen ders bulunamadı.
            </div>
          ) : (
            <ul className="divide-y">
              {(coursesQuery.data ?? []).map(c => {
                const inCurriculum = curriculumCourseIds.has(c.id);
                const alreadyOpened = alreadyOpenedIds.has(c.id);
                const isSelected = selectedCourseId === c.id;
                const isDisabled = alreadyOpened;
                return (
                  <li
                    key={c.id}
                    onClick={() => !isDisabled && setSelectedCourseId(c.id)}
                    className={cn(
                      'flex items-center gap-3 px-3 py-2 text-sm transition-colors',
                      isDisabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer hover:bg-gray-50',
                      isSelected && 'bg-blue-50/60',
                    )}
                  >
                    <span className="font-mono font-semibold text-blue-700 min-w-[80px]">{c.code}</span>
                    <span className="flex-1 truncate text-gray-800">{c.name}</span>
                    <span className="text-xs text-gray-400 tabular-nums">{c.ects} AKTS</span>
                    {inCurriculum && (
                      <Badge variant="outline" className="text-[10px]">Müfredatta</Badge>
                    )}
                    {!inCurriculum && (
                      <Badge variant="outline" className="text-[10px] border-amber-300 text-amber-700 bg-amber-50">
                        Müfredat dışı
                      </Badge>
                    )}
                    {alreadyOpened && (
                      <Badge variant="secondary" className="text-[10px]">Zaten açık</Badge>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        {selectedCourse && (
          <div className="border rounded-md p-3 bg-gray-50/60 space-y-2">
            <div className="flex items-center gap-2 text-sm">
              <span className="font-mono font-semibold text-blue-700">{selectedCourse.code}</span>
              <span className="text-gray-800">{selectedCourse.name}</span>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="text-xs text-gray-600 space-y-1">
                <span>Şube sayısı</span>
                <Input
                  type="number"
                  min={1}
                  max={20}
                  value={sectionCount}
                  onChange={e => setSectionCount(Math.max(1, Number(e.target.value) || 1))}
                />
              </label>
              <label className="text-xs text-gray-600 space-y-1">
                <span>Şube başı kapasite</span>
                <Input
                  type="number"
                  min={1}
                  max={500}
                  value={capacity}
                  onChange={e => setCapacity(Math.max(1, Number(e.target.value) || 1))}
                />
              </label>
            </div>
          </div>
        )}

        {openMutation.isError && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Ders açılamadı</AlertTitle>
            <AlertDescription>
              {(() => {
                const e = openMutation.error as {
                  response?: { data?: { error?: string; detail?: string; title?: string } };
                  message?: string;
                };
                return (
                  e?.response?.data?.error ??
                  e?.response?.data?.detail ??
                  e?.response?.data?.title ??
                  e?.message ??
                  'Bilinmeyen hata.'
                );
              })()}
            </AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>İptal</Button>
          <Button
            onClick={handleOpen}
            disabled={!selectedCourseId || openMutation.isPending}
          >
            {openMutation.isPending ? 'Açılıyor...' : 'Aç'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
