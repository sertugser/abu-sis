import { Badge } from '@/components/ui/badge';
import { AlertTriangle } from 'lucide-react';
import type { AutoScheduleFailure } from '../../../types/facultyPlanning';

const TYPE_LABELS: Record<string, string> = {
  CapacityIncrease: 'Kapasite Yetersiz',
  ClassroomAdd: 'Derslik Tipi Eksik',
  TimeChange: 'Zaman Çakışması',
  InstructorReassign: 'Hoca Çakışması',
  ConstraintRelax: 'Kısıt Sorunu',
  Unknown: 'Bilinmeyen',
};

interface ActionLink {
  icon: string;
  title: string;
  description: string;
  linkText: string;
  tabTarget: string;
}

function getActions(constraintType: string): ActionLink[] {
  switch (constraintType) {
    case 'CapacityIncrease':
      return [
        { icon: '📐', title: 'Derslik Ekle', description: 'Daha büyük kapasiteli derslik ata', linkText: 'Derslik Yönetimi →', tabTarget: '/derslik-yonetimi' },
        { icon: '🗓', title: 'Manuel Ata', description: 'Takvimde elle derslik seç', linkText: 'Takvime Git →', tabTarget: 'calendar' },
      ];
    case 'ClassroomAdd':
      return [
        { icon: '🏫', title: 'Derslik Tipi Ekle', description: 'Uygun tipte derslik ata', linkText: 'Derslik Yönetimi →', tabTarget: '/derslik-yonetimi' },
        { icon: '🗓', title: 'Manuel Ata', description: 'Takvimde elle derslik seç', linkText: 'Takvime Git →', tabTarget: 'calendar' },
      ];
    case 'TimeChange':
      return [
        { icon: '🗓', title: 'Takvimde İncele', description: 'Çakışan slotları gör', linkText: 'Takvime Git →', tabTarget: 'calendar' },
        { icon: '👤', title: 'Hoca Programı', description: 'Hocanın müsait saatlerini kontrol et', linkText: 'Listeye Git →', tabTarget: 'list' },
      ];
    case 'InstructorReassign':
      return [
        { icon: '👤', title: 'Hoca Değiştir', description: 'Başka öğretim üyesi ata', linkText: 'Listeye Git →', tabTarget: 'list' },
        { icon: '🗓', title: 'Takvimde İncele', description: 'Çakışan slotları gör', linkText: 'Takvime Git →', tabTarget: 'calendar' },
      ];
    case 'ConstraintRelax':
      return [
        { icon: '🗓', title: 'Manuel Ata', description: 'Takvimde elle derslik seç', linkText: 'Takvime Git →', tabTarget: 'calendar' },
        { icon: '⚙', title: 'Kısıtları İncele', description: 'Kısıt ayarlarını kontrol et', linkText: 'Ayarlara Git →', tabTarget: '/ders-programi-kisitlamalari' },
      ];
    default:
      return [
        { icon: '🗓', title: 'Manuel Ata', description: 'Takvimde elle derslik seç', linkText: 'Takvime Git →', tabTarget: 'calendar' },
      ];
  }
}

interface FailureDetailProps {
  item: AutoScheduleFailure;
  onNavigate: (target: string) => void;
}

export function FailureDetail({ item, onNavigate }: FailureDetailProps) {
  const constraintType = item.suggestions?.[0]?.suggestionType ?? 'Unknown';
  const typeLabel = TYPE_LABELS[constraintType] ?? TYPE_LABELS.Unknown;
  const actions = getActions(constraintType);

  return (
    <div className="p-4 h-full overflow-y-auto">
      <div className="grid grid-cols-2 gap-2">
        {/* R1C1: Ders bilgisi */}
        <div className="bg-muted/50 rounded-lg p-3">
          <div className="text-base font-bold text-foreground">{item.courseCode}-{String(item.sectionNumber).padStart(2, '0')}</div>
          <div className="text-xs text-muted-foreground">{item.courseName}</div>
        </div>

        {/* R1C2: Durum + Kapasite */}
        <div className="bg-muted/50 rounded-lg p-3 flex justify-between items-center">
          <div>
            <Badge variant="destructive" className="text-[10px]">Atanamadı</Badge>
            <div className="text-[10px] text-muted-foreground mt-1 font-mono">{constraintType}</div>
          </div>
          <div className="text-right">
            <div className="text-[10px] text-muted-foreground">Kapasite</div>
            <div className="text-lg font-bold text-foreground">{item.capacity || '?'}</div>
          </div>
        </div>

        {/* R2: Sebep — full width */}
        <div className="col-span-2 bg-red-500/5 border border-red-500/15 rounded-lg px-3 py-2.5 flex items-center gap-2.5">
          <AlertTriangle className="h-4 w-4 text-red-500 shrink-0" />
          <div className="min-w-0">
            <div className="text-[13px] font-medium text-red-400">{typeLabel}</div>
            <div className="text-[11px] text-muted-foreground truncate">{item.reason}</div>
          </div>
        </div>

        {/* R3: Aksiyonlar */}
        {actions.map((action, i) => (
          <button
            key={i}
            type="button"
            onClick={() => onNavigate(action.tabTarget)}
            className="bg-muted/50 border border-border/50 rounded-lg p-3 text-left hover:border-indigo-500/30 transition-colors"
          >
            <div className="flex items-center gap-1.5 mb-1">
              <span className="text-sm">{action.icon}</span>
              <span className="text-xs font-semibold text-foreground">{action.title}</span>
            </div>
            <div className="text-[10px] text-muted-foreground">{action.description}</div>
            <div className="text-[10px] text-indigo-500 mt-1">{action.linkText}</div>
          </button>
        ))}
        {/* Tek aksiyon varsa boş hücre ile simetri koru */}
        {actions.length === 1 && <div />}
      </div>
    </div>
  );
}
