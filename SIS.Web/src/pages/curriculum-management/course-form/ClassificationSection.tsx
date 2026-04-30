import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import SharedFieldWrapper from './SharedFieldWrapper';
import { useOpeningReasons, useCourseCategories } from '../../../hooks/useCourseLookups';
import type { CourseClassificationState, CurriculumSpecificState } from './types';

interface Props {
  /** The shared classification (Course table) */
  classification: CourseClassificationState;
  onClassificationChange: (next: CourseClassificationState) => void;

  /** Curriculum-specific bits — when undefined, OpeningReason/Category dropdowns are hidden (catalog-only mode) */
  curriculumSpecific?: CurriculumSpecificState;
  onCurriculumSpecificChange?: (next: CurriculumSpecificState) => void;

  showSharedHints?: boolean;

  /** When true, Course-level fields (Dil, Öğretim Şekli, Sınav Tipi, Açma Tipi) are hidden —
   *  used in "Add Course" flow where Course already exists in catalog and these come from there. */
  hideCourseFields?: boolean;
}

export default function ClassificationSection({
  classification,
  onClassificationChange,
  curriculumSpecific,
  onCurriculumSpecificChange,
  showSharedHints = false,
  hideCourseFields = false,
}: Props) {
  const { data: openingReasons } = useOpeningReasons();
  const { data: categories } = useCourseCategories();

  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">Sınıflandırma</legend>

      {!hideCourseFields && (
      <>
      <div className="grid grid-cols-2 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Dil</Label>
            <Select
              value={classification.language}
              onValueChange={(v) => onClassificationChange({ ...classification, language: v as CourseClassificationState['language'] })}
            >
              <SelectTrigger className="h-9 text-sm"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Turkish">Türkçe</SelectItem>
                <SelectItem value="English">İngilizce</SelectItem>
                <SelectItem value="French">Fransızca</SelectItem>
                <SelectItem value="German">Almanca</SelectItem>
                <SelectItem value="Arabic">Arapça</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Öğretim Şekli</Label>
            <Select
              value={classification.teachingMethod}
              onValueChange={(v) => onClassificationChange({ ...classification, teachingMethod: v as CourseClassificationState['teachingMethod'] })}
            >
              <SelectTrigger className="h-9 text-sm"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="FaceToFace">Yüz Yüze</SelectItem>
                <SelectItem value="Online">Çevrimiçi</SelectItem>
                <SelectItem value="Hybrid">Hibrit</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </SharedFieldWrapper>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Sınav Değer Tipi</Label>
            <Select
              value={classification.examType ?? 'auto'}
              onValueChange={(v) => onClassificationChange({ ...classification, examType: v === 'auto' ? null : v })}
            >
              <SelectTrigger className="h-9 text-sm"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="auto">Otomatik Hesapla ve Harflendir</SelectItem>
                <SelectItem value="autoSU">Otomatik Hesapla ve Başarılı/Başarısız Harflendir</SelectItem>
                <SelectItem value="manualSU">Manuel Başarılı/Başarısız Değerlendir</SelectItem>
                <SelectItem value="manualScore">Manuel Sonuç Değeri Girilmesine İzin Ver</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Açma Tipi</Label>
            <Select
              value={classification.openType ?? '__none__'}
              onValueChange={(v) => onClassificationChange({
                ...classification,
                openType: v === '__none__' ? null : (v as 'Standart' | 'Closed' | 'PendingApproval'),
              })}
            >
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Standart (varsayılan)" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">
                  <span className="text-muted-foreground">— Standart (varsayılan) —</span>
                </SelectItem>
                <SelectItem value="Standart">Standart</SelectItem>
                <SelectItem value="Closed">Kapatıldı</SelectItem>
                <SelectItem value="PendingApproval">Açık (Onay Bekleniyor)</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </SharedFieldWrapper>
      </div>
      </>
      )}

      {curriculumSpecific !== undefined && onCurriculumSpecificChange !== undefined && (
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <Label className="text-xs">Açma Nedeni</Label>
            <Select
              value={curriculumSpecific.openingReasonId?.toString() ?? '__none__'}
              onValueChange={(v) => onCurriculumSpecificChange({
                ...curriculumSpecific,
                openingReasonId: v === '__none__' ? null : Number(v),
              })}
            >
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Seçilmedi" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__"><span className="text-muted-foreground">— Seçilmedi —</span></SelectItem>
                {(openingReasons ?? []).map(r => (
                  <SelectItem key={r.id} value={r.id.toString()} title={r.description ?? undefined}>
                    {r.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1">
            <Label className="text-xs">Kategori</Label>
            <Select
              value={curriculumSpecific.categoryId?.toString() ?? '__none__'}
              onValueChange={(v) => onCurriculumSpecificChange({
                ...curriculumSpecific,
                categoryId: v === '__none__' ? null : Number(v),
              })}
            >
              <SelectTrigger className="h-9 text-sm">
                <SelectValue placeholder="Seçilmedi" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__"><span className="text-muted-foreground">— Seçilmedi —</span></SelectItem>
                {(categories ?? []).map(c => (
                  <SelectItem key={c.id} value={c.id.toString()}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      )}
    </fieldset>
  );
}
