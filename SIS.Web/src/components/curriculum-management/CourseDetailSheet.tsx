import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import ResizableDialogContent from '@/components/ui/resizable-dialog';
import {
  AlertTriangle, ExternalLink, Loader2,
} from 'lucide-react';
import { useAdminCourseDetail, useUpdateCourse } from '../../hooks/useAdminCourseCatalog';
import { useUpdateCurriculumCourse } from '../../hooks/useCurriculumManagement';
import { ROUTES } from '@/lib/constants';
import type { CurriculumCourseItemDto } from '../../types/curriculumManagement';

interface Props {
  courseId: number | null;
  curriculumId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The curriculum course row — provides all editable curriculum fields */
  curriculumCourse?: CurriculumCourseItemDto;
  onSaved?: () => void;
}

// ── Inner form component — mounted fresh whenever course/curriculumCourse identity changes ──
interface FormProps {
  course: NonNullable<ReturnType<typeof useAdminCourseDetail>['data']>;
  curriculumCourse?: CurriculumCourseItemDto;
  curriculumId: number | null;
  courseId: number | null;
  onSaved?: () => void;
  onClose: () => void;
}

function CourseDetailForm({ course, curriculumCourse, curriculumId, courseId, onSaved, onClose }: FormProps) {
  const updateCourseMut = useUpdateCourse();
  const updateCurrMut = useUpdateCurriculumCourse();

  // ── Course catalog fields — initialized directly from props ──
  const [courseName, setCourseName] = useState(course.name);
  const [englishName, setEnglishName] = useState(course.englishName ?? '');
  const [ects, setEcts] = useState(course.ects);
  const [theoryHours, setTheoryHours] = useState(course.theoryHours);
  const [labHours, setLabHours] = useState(course.labHours);
  const [examType, setExamType] = useState<string>(course.examType ?? 'auto');

  // ── Curriculum fields — initialized directly from props ──
  const [semester, setSemester] = useState(curriculumCourse?.suggestedSemester ?? 1);
  const [isCompulsory, setIsCompulsory] = useState(curriculumCourse?.isCompulsory ?? true);
  const [includeInGpa, setIncludeInGpa] = useState(curriculumCourse?.includeInGpa ?? true);
  const [showOnTranscript, setShowOnTranscript] = useState(curriculumCourse?.showOnTranscript ?? true);
  const [groupCode, setGroupCode] = useState(curriculumCourse?.groupCode ?? '');

  // Derived: sınıf and dönem tipi
  const sinif = Math.ceil(semester / 2);

  // Track dirty state
  const courseDirty = useMemo(() => (
    courseName !== course.name ||
    englishName !== (course.englishName ?? '') ||
    ects !== course.ects ||
    theoryHours !== course.theoryHours ||
    labHours !== course.labHours ||
    examType !== (course.examType ?? 'auto')
  ), [course, courseName, englishName, ects, theoryHours, labHours, examType]);

  const curriculumDirty = useMemo(() => {
    if (!curriculumCourse) return false;
    return (
      semester !== curriculumCourse.suggestedSemester ||
      isCompulsory !== curriculumCourse.isCompulsory ||
      includeInGpa !== curriculumCourse.includeInGpa ||
      showOnTranscript !== curriculumCourse.showOnTranscript ||
      groupCode !== (curriculumCourse.groupCode ?? '')
    );
  }, [curriculumCourse, semester, isCompulsory, includeInGpa, showOnTranscript, groupCode]);

  const isDirty = courseDirty || curriculumDirty;
  const isSaving = updateCourseMut.isPending || updateCurrMut.isPending;

  async function handleSave() {
    const promises: Promise<unknown>[] = [];

    if (courseDirty) {
      promises.push(
        updateCourseMut.mutateAsync({
          id: course.id,
          code: course.code,
          name: courseName,
          englishName: englishName || null,
          ects,
          theoryHours,
          labHours,
          isNonGpa: course.isNonGpa,
          examType: examType === 'auto' ? null : examType,
          departmentId: course.departmentId,
          language: course.language,
          teachingMethod: course.teachingMethod,
          status: course.status,
          description: course.description,
          level: course.level,
          nationalCredit: course.nationalCredit,
        }),
      );
    }

    if (curriculumDirty && curriculumId && courseId) {
      promises.push(
        updateCurrMut.mutateAsync({
          curriculumId,
          courseId,
          suggestedSemester: semester,
          isCompulsory,
          includeInGpa,
          showOnTranscript,
          groupCode: groupCode || undefined,
        }),
      );
    }

    await Promise.all(promises);
    onSaved?.();
    onClose();
  }

  return (
    <>
      <div className="flex-1 overflow-y-auto space-y-6 px-6 pb-4">
            {/* ═══ Temel Bilgiler ═══ */}
            <section>
              <div className="flex items-center gap-2 mb-3">
                <h4 className="text-sm font-semibold text-foreground">Temel Bilgiler</h4>
                {courseDirty && (
                  <span className="inline-flex items-center gap-1 text-[10px] text-amber-600 bg-amber-50 dark:bg-amber-950/30 px-1.5 py-0.5 rounded">
                    <AlertTriangle className="h-3 w-3" />
                    Bu alanlar ders katalogunu etkiler
                  </span>
                )}
              </div>
              <div className="space-y-3">
                {/* Ders Kodu - readonly */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">Ders Kodu</Label>
                  <div className="col-span-2">
                    <Input
                      value={course.code}
                      disabled
                      className="h-8 text-sm bg-muted/50"
                    />
                  </div>
                </div>

                {/* Ders Adı */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">Ders Adı</Label>
                  <div className="col-span-2">
                    <Input
                      value={courseName}
                      onChange={e => setCourseName(e.target.value)}
                      className="h-8 text-sm"
                    />
                  </div>
                </div>

                {/* İngilizce Adı */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">İngilizce Adı</Label>
                  <div className="col-span-2">
                    <Input
                      value={englishName}
                      onChange={e => setEnglishName(e.target.value)}
                      className="h-8 text-sm"
                      placeholder="English Name"
                    />
                  </div>
                </div>

                {/* AKTS */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">AKTS</Label>
                  <div className="col-span-2">
                    <Input
                      type="number"
                      min={0}
                      value={ects}
                      onChange={e => setEcts(Number(e.target.value))}
                      className="h-8 text-sm w-24"
                    />
                  </div>
                </div>

                {/* Teorik Saat */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">Teorik Saat</Label>
                  <div className="col-span-2">
                    <Input
                      type="number"
                      min={0}
                      value={theoryHours}
                      onChange={e => setTheoryHours(Number(e.target.value))}
                      className="h-8 text-sm w-24"
                    />
                  </div>
                </div>

                {/* Uygulama Saati */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">Uygulama Saati</Label>
                  <div className="col-span-2">
                    <Input
                      type="number"
                      min={0}
                      value={labHours}
                      onChange={e => setLabHours(Number(e.target.value))}
                      className="h-8 text-sm w-24"
                    />
                  </div>
                </div>

                {/* Sahip Bölüm - readonly */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">Sahip Bölüm</Label>
                  <div className="col-span-2">
                    <Input
                      value={course.departmentName ?? '-'}
                      disabled
                      className="h-8 text-sm bg-muted/50"
                    />
                  </div>
                </div>

                {/* Sınav Değer Tipi */}
                <div className="grid grid-cols-3 items-center gap-3">
                  <Label className="text-xs text-muted-foreground">Sınav Değer Tipi</Label>
                  <div className="col-span-2">
                    <Select value={examType} onValueChange={setExamType}>
                      <SelectTrigger className="h-8 text-sm">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="auto">Otomatik Hesapla ve Harflendir</SelectItem>
                        <SelectItem value="manual">Manuel</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              </div>
            </section>

            <Separator />

            {/* ═══ Müfredat Bilgileri ═══ */}
            {curriculumCourse && (
              <>
                <section>
                  <h4 className="text-sm font-semibold text-foreground mb-3">
                    Müfredat Bilgileri
                    <span className="text-[10px] font-normal text-muted-foreground ml-2">
                      (bu programa özgü)
                    </span>
                  </h4>
                  <div className="space-y-3">
                    {/* Yarıyıl */}
                    <div className="grid grid-cols-3 items-center gap-3">
                      <Label className="text-xs text-muted-foreground">Yarıyıl</Label>
                      <div className="col-span-2">
                        <Input
                          type="number"
                          min={1}
                          max={8}
                          value={semester}
                          onChange={e => setSemester(Number(e.target.value))}
                          className="h-8 text-sm w-24"
                        />
                      </div>
                    </div>

                    {/* Sınıf - auto-calculated */}
                    <div className="grid grid-cols-3 items-center gap-3">
                      <Label className="text-xs text-muted-foreground">Sınıf</Label>
                      <div className="col-span-2">
                        <span className="text-sm">{sinif}. Sınıf</span>
                      </div>
                    </div>

                    {/* Dönem Tipi - auto */}
                    <div className="grid grid-cols-3 items-center gap-3">
                      <Label className="text-xs text-muted-foreground">Dönem Tipi</Label>
                      <div className="col-span-2">
                        <span className="text-sm">{semester % 2 === 1 ? 'Güz' : 'Bahar'}</span>
                      </div>
                    </div>

                    {/* Zorunlu/Seçmeli */}
                    <div className="grid grid-cols-3 items-center gap-3">
                      <Label className="text-xs text-muted-foreground">Zorunlu/Seçmeli</Label>
                      <div className="col-span-2 flex items-center gap-2">
                        <button
                          onClick={() => setIsCompulsory(!isCompulsory)}
                          className="cursor-pointer"
                        >
                          <Badge
                            variant={isCompulsory ? 'default' : 'outline'}
                            className="text-[11px] px-2.5 py-0.5"
                          >
                            {isCompulsory ? 'Zorunlu' : 'Seçmeli'}
                          </Badge>
                        </button>
                      </div>
                    </div>

                    {/* Ortalamalara Katılım */}
                    <div className="grid grid-cols-3 items-center gap-3">
                      <Label className="text-xs text-muted-foreground">Ortalamalara Katılım</Label>
                      <div className="col-span-2 flex items-center gap-2">
                        <Switch
                          checked={includeInGpa}
                          onCheckedChange={setIncludeInGpa}
                        />
                        <span className="text-xs text-muted-foreground">
                          {includeInGpa ? 'Evet' : 'Hayır'}
                        </span>
                      </div>
                    </div>

                    {/* Transkriptte Göster */}
                    <div className="grid grid-cols-3 items-center gap-3">
                      <Label className="text-xs text-muted-foreground">Transkriptte Göster</Label>
                      <div className="col-span-2 flex items-center gap-2">
                        <Switch
                          checked={showOnTranscript}
                          onCheckedChange={setShowOnTranscript}
                        />
                        <span className="text-xs text-muted-foreground">
                          {showOnTranscript ? 'Evet' : 'Hayır'}
                        </span>
                      </div>
                    </div>

                    {/* Grup Kodu - only when Seçmeli */}
                    {!isCompulsory && (
                      <div className="grid grid-cols-3 items-center gap-3">
                        <Label className="text-xs text-muted-foreground">Grup Kodu</Label>
                        <div className="col-span-2">
                          <Input
                            value={groupCode}
                            onChange={e => setGroupCode(e.target.value)}
                            className="h-8 text-sm"
                            placeholder="Seçmeli ders grubu"
                          />
                        </div>
                      </div>
                    )}
                  </div>
                </section>

                <Separator />
              </>
            )}

            {/* ═══ Önkoşullar ═══ */}
            <section>
              <h4 className="text-sm font-semibold text-foreground mb-2">Önkoşullar</h4>
              {course.prerequisites.length === 0 ? (
                <p className="text-sm text-muted-foreground italic">Bu dersin önkoşulu yok</p>
              ) : (
                <ul className="space-y-1.5">
                  {course.prerequisites.map(p => (
                    <li key={p.courseId} className="text-sm flex items-center gap-2">
                      <span className="text-muted-foreground">&#x2022;</span>
                      <span className="font-mono text-xs">{p.code}</span>
                      <span>&#x2014; {p.name}</span>
                    </li>
                  ))}
                </ul>
              )}
              <Link
                to={ROUTES.COURSE_CATALOG_ADMIN}
                className="text-xs text-primary hover:underline inline-flex items-center gap-1 mt-2"
              >
                Ders Kataloğu'nda Düzenle
                <ExternalLink className="h-3 w-3" />
              </Link>
            </section>

            <Separator />

            {/* ═══ Eşdeğer Dersler ═══ */}
            <section>
              <h4 className="text-sm font-semibold text-foreground mb-2">Eşdeğer Dersler</h4>
              {course.equivalencies.length === 0 ? (
                <p className="text-sm text-muted-foreground italic">Eşdeğer ders yok</p>
              ) : (
                <ul className="space-y-1.5">
                  {course.equivalencies.map(e => (
                    <li key={e.courseId} className="text-sm flex items-center gap-2">
                      <span className="text-muted-foreground">&#x2022;</span>
                      <span className="font-mono text-xs">{e.code}</span>
                      <span>&#x2014; {e.name}</span>
                    </li>
                  ))}
                </ul>
              )}
              <Link
                to={ROUTES.COURSE_CATALOG_ADMIN}
                className="text-xs text-primary hover:underline inline-flex items-center gap-1 mt-2"
              >
                Ders Kataloğu'nda Düzenle
                <ExternalLink className="h-3 w-3" />
              </Link>
            </section>

            <Separator />

            {/* ═══ Ön Koşul Kuralları (read-only) ═══ */}
            <section>
              <h4 className="text-sm font-semibold text-foreground mb-2">Ön Koşul Kuralları</h4>
              {course.minCreditsRequired == null
                && course.minEctsRequired == null
                && !course.requiresNoFailedLowerCourses
                && !course.canBeTakenStandalone ? (
                <p className="text-sm text-muted-foreground italic">Tanımlı kural yok</p>
              ) : (
                <ul className="space-y-1 text-sm">
                  {course.minCreditsRequired != null && (
                    <li className="flex items-center gap-2">
                      <span className="text-muted-foreground">&#x2022;</span>
                      <span className="text-muted-foreground">Min Kredi:</span>
                      <span className="font-medium">{course.minCreditsRequired}</span>
                    </li>
                  )}
                  {course.minEctsRequired != null && (
                    <li className="flex items-center gap-2">
                      <span className="text-muted-foreground">&#x2022;</span>
                      <span className="text-muted-foreground">Min AKTS:</span>
                      <span className="font-medium">{course.minEctsRequired}</span>
                    </li>
                  )}
                  {course.requiresNoFailedLowerCourses && (
                    <li className="flex items-center gap-2">
                      <span className="text-muted-foreground">&#x2022;</span>
                      <span>Alttan dersi olmayanlar alabilir</span>
                    </li>
                  )}
                  {course.canBeTakenStandalone && (
                    <li className="flex items-center gap-2">
                      <span className="text-muted-foreground">&#x2022;</span>
                      <span>Tek başına alınabilir (başka derslerle birlikte alınamaz)</span>
                    </li>
                  )}
                </ul>
              )}
              <p className="text-xs text-muted-foreground mt-2">
                Düzenlemek için: Müfredat &rarr; Ders satırı <span className="font-medium">⚙</span> &rarr; "Ön Koşul Tanımları"
              </p>
            </section>
          </div>

      {/* ═══ Footer: İptal / Kaydet ═══ */}
      <DialogFooter className="shrink-0 px-6 py-4 border-t bg-background gap-2 sm:justify-between">
        <Button
          variant="outline"
          onClick={onClose}
          disabled={isSaving}
        >
          İptal
        </Button>
        <Button
          onClick={handleSave}
          disabled={!isDirty || isSaving}
        >
          {isSaving && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
          Kaydet
        </Button>
      </DialogFooter>
    </>
  );
}

export default function CourseDetailSheet({
  courseId,
  curriculumId,
  open,
  onOpenChange,
  curriculumCourse,
  onSaved,
}: Props) {
  const { data: course, isLoading } = useAdminCourseDetail(open ? courseId : null);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <ResizableDialogContent defaultWidth={672} minWidth={400} minHeight={300} className="flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 pt-6 pb-4 shrink-0">
          <DialogTitle>
            {isLoading ? (
              <Skeleton className="h-6 w-64" />
            ) : course ? (
              <span className="flex items-baseline gap-2">
                <span className="font-mono text-sm text-muted-foreground">{course.code}</span>
                <span>{course.name}</span>
              </span>
            ) : (
              'Ders Detay'
            )}
          </DialogTitle>
          <DialogDescription className="sr-only">Ders düzenleme formu</DialogDescription>
        </DialogHeader>

        {isLoading && (
          <div className="space-y-4 px-6 mt-2">
            <Skeleton className="h-4 w-48" />
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-4 w-56" />
            <Skeleton className="h-20 w-full" />
          </div>
        )}

        {!isLoading && !course && (
          <p className="text-sm text-muted-foreground px-6 mt-2">Ders bilgisi bulunamadı.</p>
        )}

        {!isLoading && course && (
          <CourseDetailForm
            key={course.id}
            course={course}
            curriculumCourse={curriculumCourse}
            curriculumId={curriculumId}
            courseId={courseId}
            onSaved={onSaved}
            onClose={() => onOpenChange(false)}
          />
        )}
      </ResizableDialogContent>
    </Dialog>
  );
}
