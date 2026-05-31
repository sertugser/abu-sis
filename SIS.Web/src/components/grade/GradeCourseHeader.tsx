// SIS.Web/src/components/grade/GradeCourseHeader.tsx
// Proliz tarzı "Ders Bilgileri" başlık bloğu: etiket + değer satırları.

import { ArrowLeft, Info, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

interface GradeCourseHeaderProps {
  courseCode: string;
  courseName: string;
  studentCount: number;
  hideStatistics: boolean;
  isFinalized: boolean;
  isRelative: boolean;
  totalWeight: number;
  /** İlan edilmiş bileşen sayısı / toplam bileşen sayısı (özet). */
  publishedCount: number;
  componentCount: number;
  onBack: () => void;
}

interface InfoRowProps {
  label: string;
  children: React.ReactNode;
}

function InfoRow({ label, children }: InfoRowProps) {
  return (
    <div className="flex items-start gap-3 py-1">
      <span className="w-28 shrink-0 text-sm font-medium text-muted-foreground">{label}</span>
      <span className="text-sm text-foreground">{children}</span>
    </div>
  );
}

export function GradeCourseHeader({
  courseCode,
  courseName,
  studentCount,
  hideStatistics,
  isFinalized,
  isRelative,
  totalWeight,
  publishedCount,
  componentCount,
  onBack,
}: GradeCourseHeaderProps) {
  return (
    <div className="bg-card">
      <div className="flex items-center justify-between border-b px-4 py-2.5">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" onClick={onBack} title="Listeye dön">
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm font-semibold text-foreground">Ders Bilgileri</span>
        </div>
        <Link
          to="/sinav-tanimlama"
          className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-primary transition-colors"
        >
          <Info className="h-3.5 w-3.5" />
          Sınav tanımları Sınav Tanımlama'da
        </Link>
      </div>

      <div className="px-4 py-3 grid gap-x-8 gap-y-1 sm:grid-cols-2">
        <InfoRow label="Ders Kodu">
          <span className="font-mono font-semibold text-primary">{courseCode}</span>
        </InfoRow>
        <InfoRow label="Ders Adı">
          <span className="font-medium">{courseName}</span>
        </InfoRow>
        <InfoRow label="Ek Bilgiler">
          <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className={`font-mono font-semibold ${
              totalWeight === 100 ? 'text-emerald-600' : totalWeight > 100 ? 'text-red-600' : 'text-muted-foreground'
            }`}>
              Toplam: %{totalWeight}{totalWeight === 100 && ' ✓'}
            </span>
            {componentCount > 0 && (
              <>
                <span className="text-muted-foreground">·</span>
                <span className={publishedCount > 0 ? 'text-emerald-600 font-medium' : 'text-muted-foreground'}>
                  İlan: {publishedCount}/{componentCount}
                </span>
              </>
            )}
          </span>
        </InfoRow>
        <InfoRow label="Durum">
          <span className="inline-flex flex-wrap items-center gap-2">
            <Badge variant="secondary" className="text-xs">
              {isFinalized ? 'Kesinleştirilmiş' : 'Kesinleştirilmemiş'}
            </Badge>
            <Badge variant="outline" className="text-xs" title="Değerlendirme yöntemi (admin tarafından belirlenir)">
              Değerlendirme: {isRelative ? 'Bağıl' : 'Mutlak'}
            </Badge>
            {!hideStatistics && (
              <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                <Users className="h-3.5 w-3.5" />
                {studentCount} öğrenci
              </span>
            )}
          </span>
        </InfoRow>
      </div>
    </div>
  );
}
