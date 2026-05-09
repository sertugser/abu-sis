import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import SharedFieldWrapper from './SharedFieldWrapper';
import type { CourseScheduleState } from './types';
import {
  CLASSROOM_TYPE_LABELS,
  CLASSROOM_TYPE_OPTIONS,
  type ClassroomTypeValue,
} from '@/hooks/useAdminCourseCatalog';

interface Props {
  value: CourseScheduleState;
  onChange: (next: CourseScheduleState) => void;
  showSharedHints?: boolean;
}

export default function ScheduleSection({ value, onChange, showSharedHints = false }: Props) {
  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-semibold mb-2">Ders Saatleri ve Krediler</legend>

      <div className="grid grid-cols-3 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Teori Saat</Label>
            <Input
              type="number"
              min={0}
              value={value.theoryHours}
              onChange={e => onChange({ ...value, theoryHours: Number(e.target.value) })}
            />
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Lab Saat</Label>
            <Input
              type="number"
              min={0}
              value={value.labHours}
              onChange={e => onChange({ ...value, labHours: Number(e.target.value) })}
            />
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">Uygulama Saat</Label>
            <Input
              type="number"
              min={0}
              value={value.applicationHours ?? ''}
              onChange={e => onChange({
                ...value,
                applicationHours: e.target.value === '' ? null : Number(e.target.value),
              })}
              placeholder="0"
            />
          </div>
        </SharedFieldWrapper>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">
              AKTS
              <span className="text-destructive ml-0.5 font-semibold" aria-hidden="true">*</span>
              <span className="sr-only"> (zorunlu)</span>
            </Label>
            <Input
              type="number"
              min={1}
              max={30}
              value={value.ects}
              onChange={e => onChange({ ...value, ects: Number(e.target.value) })}
              aria-required="true"
              aria-invalid={!value.ects || value.ects < 1}
            />
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">
              Kredi (Ulusal)
              <span className="text-destructive ml-0.5 font-semibold" aria-hidden="true">*</span>
              <span className="sr-only"> (zorunlu)</span>
            </Label>
            <Input
              type="number"
              min={0}
              max={20}
              value={value.nationalCredit}
              onChange={e => onChange({
                ...value,
                nationalCredit: e.target.value === '' ? 0 : Number(e.target.value),
              })}
              placeholder="örn. 4"
              aria-required="true"
              aria-invalid={value.nationalCredit < 0 || value.nationalCredit > 20}
            />
          </div>
        </SharedFieldWrapper>
      </div>

      {/* Plan 10 — Derslik tipi tercihleri (opsiyonel; H4 ClassroomType encoder okuyor) */}
      <div className="grid grid-cols-2 gap-3 pt-2 border-t mt-2">
        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">
              Teori Sınıf Tipi
              <span className="text-muted-foreground ml-1 text-[10px] font-normal">(opsiyonel)</span>
            </Label>
            <select
              className="w-full h-9 rounded-md border border-input bg-background px-2 text-sm"
              value={value.theoryClassroomType ?? ''}
              onChange={e => onChange({
                ...value,
                theoryClassroomType: e.target.value === '' ? null : (e.target.value as ClassroomTypeValue),
              })}
              disabled={value.theoryHours <= 0}
            >
              <option value="">— Otomatik (kısıt yok) —</option>
              {CLASSROOM_TYPE_OPTIONS.map(opt => (
                <option key={opt} value={opt}>{CLASSROOM_TYPE_LABELS[opt]}</option>
              ))}
            </select>
            <p className="text-[10px] text-muted-foreground">
              Seçilirse teori bu tipte derslikte yerleşir; boş ise otomatik seçim.
            </p>
          </div>
        </SharedFieldWrapper>

        <SharedFieldWrapper ownership="shared" showSharedHint={showSharedHints}>
          <div className="space-y-1">
            <Label className="text-xs">
              Lab Sınıf Tipi
              <span className="text-muted-foreground ml-1 text-[10px] font-normal">(opsiyonel)</span>
            </Label>
            <select
              className="w-full h-9 rounded-md border border-input bg-background px-2 text-sm"
              value={value.labClassroomType ?? ''}
              onChange={e => onChange({
                ...value,
                labClassroomType: e.target.value === '' ? null : (e.target.value as ClassroomTypeValue),
              })}
              disabled={value.labHours <= 0}
            >
              <option value="">— Otomatik (kısıt yok) —</option>
              {CLASSROOM_TYPE_OPTIONS.map(opt => (
                <option key={opt} value={opt}>{CLASSROOM_TYPE_LABELS[opt]}</option>
              ))}
            </select>
            <p className="text-[10px] text-muted-foreground">
              Seçilirse lab bu tipte derslikte yerleşir; boş ise otomatik seçim.
            </p>
          </div>
        </SharedFieldWrapper>
      </div>
    </fieldset>
  );
}
