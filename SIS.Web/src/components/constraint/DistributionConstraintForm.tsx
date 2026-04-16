import { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { X, ArrowUpDown, Plus } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { getCourses } from '../../api/course.api';
import { QUERY_KEYS } from '../../lib/constants';
import type { CreateDistributionConstraintRequest } from '../../types/distributionConstraint';
import type { DistributionConstraintDto } from '../../types/distributionConstraint';

const DISTRIBUTION_TYPES = [
  { value: 'SameDay', label: 'Ayn\u0131 G\u00fcn', desc: '\u0130ki ders ayn\u0131 g\u00fcnde olmal\u0131' },
  { value: 'DifferentDay', label: 'Farkl\u0131 G\u00fcn', desc: '\u0130ki ders farkl\u0131 g\u00fcnlerde olmal\u0131' },
  { value: 'BackToBack', label: 'Ard\u0131\u015f\u0131k', desc: 'Dersler ard\u0131\u015f\u0131k saatlerde olmal\u0131' },
  { value: 'NotBackToBack', label: 'Ard\u0131\u015f\u0131k Olmas\u0131n', desc: 'Dersler ard\u0131\u015f\u0131k saatlerde olmamal\u0131' },
  { value: 'SameRoom', label: 'Ayn\u0131 Derslik', desc: 'Dersler ayn\u0131 derslikte olmal\u0131' },
  { value: 'Precedence', label: 'S\u0131ralama', desc: 'Dersler belirli bir s\u0131rada programlanmal\u0131' },
] as const;

const PREFERENCE_LEVELS = [
  { value: 'Preferred', label: 'Tercih Edilir', color: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200' },
  { value: 'StronglyPreferred', label: 'Kesinlikle Tercih Edilir', color: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900 dark:text-emerald-200' },
  { value: 'Discouraged', label: 'Tercih Edilmez', color: 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200' },
  { value: 'StronglyDiscouraged', label: 'Kesinlikle Tercih Edilmez', color: 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200' },
  { value: 'Unavailable', label: 'Kesin Yasak (Hard)', color: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' },
] as const;

interface SelectedCourse {
  courseId: number;
  courseCode: string;
  courseName: string;
  courseSectionId?: number | null;
  sortOrder: number;
}

interface DistributionConstraintFormProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  termId: number;
  onSubmit: (request: CreateDistributionConstraintRequest) => void;
  isPending: boolean;
  editing?: DistributionConstraintDto | null;
}

export default function DistributionConstraintForm({
  open, onOpenChange, termId, onSubmit, isPending, editing,
}: DistributionConstraintFormProps) {
  const [distType, setDistType] = useState(editing?.distributionType ?? 'SameDay');
  const [prefLevel, setPrefLevel] = useState(editing?.preferenceLevel ?? 'Discouraged');
  const [reason, setReason] = useState(editing?.reason ?? '');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCourses, setSelectedCourses] = useState<SelectedCourse[]>(() => {
    if (!editing) return [];
    return editing.courses.map(c => ({
      courseId: c.courseId,
      courseCode: c.courseCode,
      courseName: c.courseName,
      courseSectionId: c.courseSectionId,
      sortOrder: c.sortOrder,
    }));
  });

  const { data: allCourses } = useQuery({
    queryKey: QUERY_KEYS.courses(),
    queryFn: () => getCourses(),
    staleTime: 5 * 60 * 1000,
  });

  const filteredCourses = useMemo(() => {
    if (!allCourses || !searchQuery.trim()) return [];
    const q = searchQuery.toLowerCase();
    const selectedIds = new Set(selectedCourses.map(c => c.courseId));
    return allCourses
      .filter(c => !selectedIds.has(c.courseId))
      .filter(c => c.code.toLowerCase().includes(q) || c.name.toLowerCase().includes(q))
      .slice(0, 20);
  }, [allCourses, searchQuery, selectedCourses]);

  function addCourse(courseId: number, courseCode: string, courseName: string) {
    setSelectedCourses(prev => [
      ...prev,
      { courseId, courseCode, courseName, sortOrder: prev.length + 1 },
    ]);
    setSearchQuery('');
  }

  function removeCourse(courseId: number) {
    setSelectedCourses(prev => {
      const filtered = prev.filter(c => c.courseId !== courseId);
      return filtered.map((c, i) => ({ ...c, sortOrder: i + 1 }));
    });
  }

  function moveCourse(index: number, direction: 'up' | 'down') {
    setSelectedCourses(prev => {
      const arr = [...prev];
      const target = direction === 'up' ? index - 1 : index + 1;
      if (target < 0 || target >= arr.length) return arr;
      [arr[index], arr[target]] = [arr[target], arr[index]];
      return arr.map((c, i) => ({ ...c, sortOrder: i + 1 }));
    });
  }

  function handleSubmit() {
    if (selectedCourses.length < 2) return;
    onSubmit({
      distributionType: distType,
      preferenceLevel: prefLevel,
      termId,
      reason: reason.trim() || null,
      courses: selectedCourses.map(c => ({
        courseId: c.courseId,
        courseSectionId: c.courseSectionId ?? null,
        sortOrder: c.sortOrder,
      })),
    });
  }

  const isValid = selectedCourses.length >= 2;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {editing ? 'Kural Duzenle' : 'Yeni Iliskisel Kisitlama'}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Distribution Type */}
          <div className="space-y-1.5">
            <Label>Kisitlama Tipi</Label>
            <Select value={distType} onValueChange={setDistType}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DISTRIBUTION_TYPES.map(t => (
                  <SelectItem key={t.value} value={t.value}>
                    <span className="font-medium">{t.label}</span>
                    <span className="text-xs text-muted-foreground ml-2">
                      {t.desc}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Preference Level */}
          <div className="space-y-1.5">
            <Label>Tercih Seviyesi</Label>
            <Select value={prefLevel} onValueChange={setPrefLevel}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PREFERENCE_LEVELS.map(p => (
                  <SelectItem key={p.value} value={p.value}>
                    <Badge variant="outline" className={`text-[10px] ${p.color}`}>
                      {p.label}
                    </Badge>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Course selector */}
          <div className="space-y-1.5">
            <Label>Dersler (en az 2)</Label>
            <div className="relative">
              <Input
                placeholder="Ders kodu veya adi ile arama..."
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
              />
              {filteredCourses.length > 0 && (
                <div className="absolute z-50 mt-1 w-full rounded-md border bg-popover shadow-md max-h-48 overflow-y-auto">
                  {filteredCourses.map(c => (
                    <button
                      key={c.courseId}
                      type="button"
                      className="flex items-center gap-2 w-full px-3 py-2 text-left text-sm hover:bg-accent"
                      onClick={() => addCourse(c.courseId, c.code, c.name)}
                    >
                      <span className="font-mono text-xs font-medium">{c.code}</span>
                      <span className="truncate text-muted-foreground">{c.name}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Selected courses list */}
            <div className="space-y-1 mt-2">
              {selectedCourses.map((c, i) => (
                <div
                  key={c.courseId}
                  className="flex items-center gap-2 rounded-md border px-3 py-1.5 bg-muted/30"
                >
                  {distType === 'Precedence' && (
                    <span className="text-xs font-bold text-muted-foreground w-5 text-center">
                      {c.sortOrder}
                    </span>
                  )}
                  <Badge variant="secondary" className="text-[10px] font-mono shrink-0">
                    {c.courseCode}
                  </Badge>
                  <span className="text-sm truncate flex-1">{c.courseName}</span>
                  {distType === 'Precedence' && (
                    <div className="flex gap-0.5">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        disabled={i === 0}
                        onClick={() => moveCourse(i, 'up')}
                      >
                        <ArrowUpDown className="h-3 w-3 rotate-180" />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        disabled={i === selectedCourses.length - 1}
                        onClick={() => moveCourse(i, 'down')}
                      >
                        <ArrowUpDown className="h-3 w-3" />
                      </Button>
                    </div>
                  )}
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-6 w-6 text-destructive"
                    onClick={() => removeCourse(c.courseId)}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                </div>
              ))}
              {selectedCourses.length === 0 && (
                <p className="text-xs text-muted-foreground text-center py-3">
                  Henuz ders eklenmedi. Yukaridaki arama kutusundan ders ekleyin.
                </p>
              )}
            </div>
          </div>

          {/* Reason */}
          <div className="space-y-1.5">
            <Label>Aciklama (opsiyonel)</Label>
            <Input
              placeholder="Bu kisitlamanin nedeni..."
              value={reason}
              onChange={e => setReason(e.target.value)}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            Iptal
          </Button>
          <Button onClick={handleSubmit} disabled={!isValid || isPending}>
            <Plus className="h-4 w-4 mr-1" />
            {editing ? 'Guncelle' : 'Olustur'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
