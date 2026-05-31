import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import type { InstructorTerm } from '@/api/instructor.api';

// ── Sentinels (exported so callers can import and reuse) ─────────────────────

export const TERM_FILTER_ACTIVE = 'active' as const;
export const FACULTY_FILTER_ALL = 'all' as const;

// ── Props ─────────────────────────────────────────────────────────────────────

export interface InstructorCourseFiltersProps {
  terms: InstructorTerm[];
  /** null = "Aktif Dönem" sentinel */
  selectedTermId: number | null;
  onTermChange: (id: number | null) => void;
  /** Distinct, sorted facultyName values (empty-string items excluded by caller) */
  facultyOptions: string[];
  /** 'all' | facultyName */
  facultyFilter: string;
  onFacultyChange: (v: string) => void;
  showKontenjan: boolean;
  onShowKontenjanChange: (v: boolean) => void;
  courseCode: string;
  onCourseCodeChange: (v: string) => void;
  courseName: string;
  onCourseNameChange: (v: string) => void;
  showUnfinalized: boolean;
  onShowUnfinalizedChange: (v: boolean) => void;
  /** When provided, "Tüm Sınavları Göster" toggle is rendered. */
  showAllExams?: boolean;
  onShowAllExamsChange?: (v: boolean) => void;
  /** When provided, an "Ara" button is rendered (filtering is live; can be a no-op). */
  onSearch?: () => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function InstructorCourseFilters({
  terms,
  selectedTermId,
  onTermChange,
  facultyOptions,
  facultyFilter,
  onFacultyChange,
  showKontenjan,
  onShowKontenjanChange,
  courseCode,
  onCourseCodeChange,
  courseName,
  onCourseNameChange,
  showUnfinalized,
  onShowUnfinalizedChange,
  showAllExams,
  onShowAllExamsChange,
  onSearch,
}: InstructorCourseFiltersProps) {
  const termSelectValue =
    selectedTermId === null ? TERM_FILTER_ACTIVE : String(selectedTermId);

  const handleTermValueChange = (value: string) => {
    onTermChange(value === TERM_FILTER_ACTIVE ? null : Number(value));
  };

  return (
    <div className="flex flex-col gap-3 border-b border-border/80 bg-muted/20 p-3">
      {/* Üst satır — seçim + metin alanları */}
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1">
          <Label htmlFor="icf-term" className="text-xs text-muted-foreground">
            Dönem
          </Label>
          <Select value={termSelectValue} onValueChange={handleTermValueChange}>
            <SelectTrigger id="icf-term" className="h-9 w-[200px]">
              <SelectValue placeholder="Aktif Dönem" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={TERM_FILTER_ACTIVE}>Aktif Dönem</SelectItem>
              {terms.map((term) => (
                <SelectItem key={term.id} value={String(term.id)}>
                  {term.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1">
          <Label htmlFor="icf-faculty" className="text-xs text-muted-foreground">
            Fakülte Türü
          </Label>
          <Select value={facultyFilter} onValueChange={onFacultyChange}>
            <SelectTrigger id="icf-faculty" className="h-9 w-[200px]">
              <SelectValue placeholder="Tümü" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={FACULTY_FILTER_ALL}>Tümü</SelectItem>
              {facultyOptions.map((faculty) => (
                <SelectItem key={faculty} value={faculty}>
                  {faculty}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex flex-col gap-1">
          <Label htmlFor="icf-course-code" className="text-xs text-muted-foreground">
            Ders Kodu
          </Label>
          <Input
            id="icf-course-code"
            value={courseCode}
            onChange={(e) => onCourseCodeChange(e.target.value)}
            placeholder="Ders kodu"
            className="h-9 w-[180px]"
          />
        </div>

        <div className="flex flex-col gap-1">
          <Label htmlFor="icf-course-name" className="text-xs text-muted-foreground">
            Ders Adı
          </Label>
          <Input
            id="icf-course-name"
            value={courseName}
            onChange={(e) => onCourseNameChange(e.target.value)}
            placeholder="Ders adı"
            className="h-9 w-[180px]"
          />
        </div>

        {onSearch !== undefined && (
          <button
            type="button"
            onClick={onSearch}
            className="h-9 rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          >
            Ara
          </button>
        )}
      </div>

      {/* Alt satır — toggle'lar */}
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <div className="flex items-center gap-2">
          <Switch
            id="icf-show-kontenjan"
            checked={showKontenjan}
            onCheckedChange={onShowKontenjanChange}
          />
          <Label htmlFor="icf-show-kontenjan" className="cursor-pointer text-sm">
            Kontenjan Göster
          </Label>
        </div>

        <div className="flex items-center gap-2">
          <Switch
            id="icf-show-unfinalized"
            checked={showUnfinalized}
            onCheckedChange={onShowUnfinalizedChange}
          />
          <Label htmlFor="icf-show-unfinalized" className="cursor-pointer text-sm">
            Sonuçlanmayan Dersleri Göster
          </Label>
        </div>

        {showAllExams !== undefined && onShowAllExamsChange !== undefined && (
          <div className="flex items-center gap-2">
            <Switch
              id="icf-show-all-exams"
              checked={showAllExams}
              onCheckedChange={onShowAllExamsChange}
            />
            <Label htmlFor="icf-show-all-exams" className="cursor-pointer text-sm">
              Tüm Sınavları Göster
            </Label>
          </div>
        )}
      </div>
    </div>
  );
}
