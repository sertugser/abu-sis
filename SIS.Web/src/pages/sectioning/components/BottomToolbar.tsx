import { Loader2, UserPlus, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { SPLIT_STRATEGIES } from '../constants';
import type { CourseGroup } from '../types';

interface BottomToolbarProps {
  course: CourseGroup | null;
  splitCount: string;
  setSplitCount: (value: string) => void;
  splitStrategy: string;
  setSplitStrategy: (value: string) => void;
  moveToSectionNumber: string;
  setMoveToSectionNumber: (value: string) => void;
  selectedStudentCount: number;
  // Auto-split (Faz 3)
  canAutoSplit: boolean;
  isAutoSplitting: boolean;
  onAutoSplit: () => void;
  // Transfer (Faz 4)
  sourceSectionId: number | null;
  canTransfer: boolean;
  isTransferring: boolean;
  onTransferStudents: (targetSectionId: number) => void;
}

export function BottomToolbar({
  course,
  splitCount,
  setSplitCount,
  splitStrategy,
  setSplitStrategy,
  moveToSectionNumber,
  setMoveToSectionNumber,
  selectedStudentCount,
  canAutoSplit,
  isAutoSplitting,
  onAutoSplit,
  sourceSectionId,
  canTransfer,
  isTransferring,
  onTransferStudents,
}: BottomToolbarProps) {
  // Auto-split validasyonu
  const targetCountNum = Number(splitCount);
  const targetCountValid = Number.isInteger(targetCountNum) && targetCountNum >= 1 && targetCountNum <= 20;
  const modOddEvenInvalid = splitStrategy === 'ModOddEven' && targetCountNum !== 2;

  const autoSplitDisabled =
    !canAutoSplit ||
    isAutoSplitting ||
    !course ||
    !targetCountValid ||
    modOddEvenInvalid;

  const autoSplitTooltip = !canAutoSplit
    ? 'Bu işlem için yetkiniz yok.'
    : !course
      ? 'Önce bir ders seçin.'
      : !targetCountValid
        ? 'Geçerli bir şube sayısı girin (1-20).'
        : modOddEvenInvalid
          ? 'MOD Tek/Çift stratejisi yalnızca 2 şube ile çalışır.'
          : `${course.courseCode} dersini ${targetCountNum} şubeye böl`;

  // Hedef şubeyi numara → ID'ye çevir
  const targetSection = course?.sections.find(s => String(s.sectionNumber) === moveToSectionNumber) ?? null;
  const sourceSection = course?.sections.find(s => s.sectionId === sourceSectionId) ?? null;
  const sameSection = !!sourceSection && !!targetSection && sourceSection.sectionId === targetSection.sectionId;

  const transferDisabled =
    !canTransfer ||
    isTransferring ||
    !sourceSection ||
    !targetSection ||
    sameSection ||
    selectedStudentCount === 0;

  const transferTooltip = !canTransfer
    ? 'Bu işlem için yetkiniz yok.'
    : !sourceSection
      ? 'Önce kaynak şube seçin (sağ üst tablodan).'
      : !targetSection
        ? 'Hedef şube bulunamadı.'
        : sameSection
          ? 'Kaynak ve hedef şube aynı olamaz.'
          : selectedStudentCount === 0
            ? 'Taşınacak öğrencileri seçin.'
            : `${selectedStudentCount} öğrenci → Şube ${targetSection.sectionNumber}`;

  const handleTransfer = () => {
    if (!targetSection || transferDisabled) return;
    onTransferStudents(targetSection.sectionId);
  };

  return (
    <Card className="p-4">
      <div className="flex flex-wrap items-center gap-6">
        <div className="flex items-center gap-2">
          <Label className="whitespace-nowrap text-sm font-semibold text-gray-700">Şube Sayısı:</Label>
          <Input
            type="number"
            min={1}
            max={20}
            value={splitCount}
            onChange={e => setSplitCount(e.target.value)}
            className="h-10 w-16 text-center text-sm"
          />
          <select
            value={splitStrategy}
            onChange={e => setSplitStrategy(e.target.value)}
            className="h-10 min-w-[260px] rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring"
          >
            {SPLIT_STRATEGIES.map(strategy => (
              <option key={strategy.value} value={strategy.value}>
                {strategy.label}
              </option>
            ))}
          </select>
          <Button
            size="default"
            disabled={autoSplitDisabled}
            title={autoSplitTooltip}
            onClick={onAutoSplit}
            className="gap-2"
          >
            {isAutoSplitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <UserPlus className="h-4 w-4" />}
            Şubelendir
          </Button>
        </div>

        <div className="ml-auto flex items-center gap-2">
          <Label className="whitespace-nowrap text-sm font-semibold text-gray-700">Seçilen Öğrenciyi</Label>
          <select
            value={moveToSectionNumber}
            onChange={e => setMoveToSectionNumber(e.target.value)}
            disabled={!course}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
          >
            {(course?.sections ?? []).map(section => (
              <option key={section.sectionId} value={section.sectionNumber}>
                {section.sectionNumber}
              </option>
            ))}
          </select>
          <Label className="whitespace-nowrap text-sm text-gray-700">nolu</Label>
          <Button
            variant="default"
            size="default"
            disabled={transferDisabled}
            title={transferTooltip}
            onClick={handleTransfer}
            className="gap-2"
          >
            {isTransferring ? <Loader2 className="h-4 w-4 animate-spin" /> : <Users className="h-4 w-4" />}
            Şubeye Taşı
            {selectedStudentCount > 0 && (
              <Badge variant="secondary" className="ml-1">{selectedStudentCount}</Badge>
            )}
          </Button>
        </div>
      </div>
    </Card>
  );
}
