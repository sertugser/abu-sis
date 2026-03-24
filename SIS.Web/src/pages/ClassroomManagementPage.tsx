import { useState, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import HeatmapGrid, { type HeatmapCell } from '@/components/scheduling/HeatmapGrid';
import { cn } from '@/lib/utils';

/* ── Mock veri ── */

function generateMockHeatmap(): HeatmapCell[] {
  const rng = (seed: number) => {
    let s = seed;
    return () => {
      s = (s * 16807) % 2147483647;
      return (s - 1) / 2147483646;
    };
  };
  const rand = rng(42);
  const cells: HeatmapCell[] = [];
  const days = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma'];
  for (let day = 0; day < 5; day++) {
    for (let hour = 8; hour <= 17; hour++) {
      const peak = hour >= 9 && hour <= 14 ? 0.25 : 0;
      const midweek = day === 2 ? 0.15 : 0;
      const occ = Math.min(1, Math.max(0, rand() * 0.6 + peak + midweek));
      const used = Math.round(occ * 45);
      cells.push({
        day,
        hour,
        occupancy: occ,
        details: `${days[day]} ${String(hour).padStart(2, '0')}:00 — ${used}/45 derslik dolu`,
      });
    }
  }
  return cells;
}

interface ConflictItem {
  id: number;
  severity: 'critical' | 'warning' | 'info';
  title: string;
  description: string;
  location: string;
}

const MOCK_CONFLICTS: ConflictItem[] = [
  {
    id: 1,
    severity: 'critical',
    title: 'Derslik Çakışması',
    description: 'BM201 Şube-1 (Dr. Yılmaz) ↔ EE301 Şube-2 (Prof. Kaya)',
    location: 'A-301 | Pazartesi 10:00-12:00',
  },
  {
    id: 2,
    severity: 'warning',
    title: 'Hoca Müsaitlik İhlali',
    description: 'BM402 Şube-1 → Hoca bu saatte "Müsait Değil" olarak işaretlemiş',
    location: 'Prof. Demir | Çarşamba 14:00-16:00',
  },
  {
    id: 3,
    severity: 'info',
    title: 'Kapasite Uyarısı',
    description: 'Tahmin: 42 öğrenci → Derslik: B-102 (Kapasite: 35)',
    location: 'BM101 Şube-3',
  },
];

const SEVERITY_MAP = {
  critical: { badge: 'destructive' as const, icon: '🔴', label: 'KRİTİK' },
  warning: { badge: 'warning' as const, icon: '🟡', label: 'UYARI' },
  info: { badge: 'success' as const, icon: '🟢', label: 'BİLGİ' },
};

const CLASSROOM_TYPES = ['Lecture', 'Lab', 'Computer', 'Seminar'];

/* ────────────────────── Sayfa ────────────────────── */

export default function ClassroomManagementPage() {
  const heatmapData = useMemo(() => generateMockHeatmap(), []);
  const [typeFilters, setTypeFilters] = useState<Record<string, boolean>>({});

  const toggleType = (t: string) => {
    setTypeFilters(prev => ({ ...prev, [t]: !prev[t] }));
  };

  // Heatmap verisini filtrelere göre dinamik güncelle
  const displayHeatmapData = useMemo(() => {
    const activeFiltersCount = Object.values(typeFilters).filter(Boolean).length;
    const isAnyFilterActive = activeFiltersCount > 0 && activeFiltersCount < CLASSROOM_TYPES.length;
    
    if (!isAnyFilterActive) return heatmapData;

    // Sanal filtreleme efekti
    const days = ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma'];
    return heatmapData.map(cell => {
      // Çarpan uygulayarak doluluk oranını filtrelere göre düşür
      const multiplier = (activeFiltersCount / CLASSROOM_TYPES.length) * (0.5 + ((cell.day + cell.hour) % 3) * 0.25);
      const newOcc = Math.max(0, cell.occupancy * multiplier);
      const newUsed = Math.round(newOcc * 45);
      return {
        ...cell,
        occupancy: newOcc,
        details: `${days[cell.day]} ${String(cell.hour).padStart(2, '0')}:00 — ${newUsed}/45 derslik dolu`,
      };
    });
  }, [heatmapData, typeFilters]);

  const handleSuggestAlternative = (id: number) => {
    alert(`Çakışma #${id} için YZ alternatifleri hesaplanıyor...\nUygun derslik ve hoca programı taranıyor.`);
  };

  const handleEdit = (id: number) => {
    alert(`Çakışma #${id} manuel düzenleme paneli açılıyor.`);
  };

  const totalClassrooms = 45;
  const occupancy = 78;
  const conflicts = MOCK_CONFLICTS.length;
  const emptySlots = 112;

  return (
    <div className="space-y-6">
      {/* Başlık */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            🏛️ Merkezi Derslik Yönetimi
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Tüm dersliklerin doluluk durumu ve çakışma analizi
          </p>
        </div>
        <Badge variant="outline" className="text-sm px-3 py-1">
          2025-2026 Güz
        </Badge>
      </div>

      {/* Üst kartlar */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <StatCard label="Toplam Derslik" value={totalClassrooms} icon="🏫" />
        <StatCard label="Doluluk" value={`%${occupancy}`} icon="📊" color="text-amber-600" />
        <StatCard label="Çakışma" value={`${conflicts} ⚠️`} icon="🔴" color="text-red-500" />
        <StatCard label="Boş Slot" value={emptySlots} icon="✅" color="text-emerald-600" />
      </div>

      {/* Filtre + Heatmap */}
      <div className="grid grid-cols-1 xl:grid-cols-[220px_1fr] gap-4">
        {/* Sol filtre */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm">Derslik Tipi Filtresi</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 flex flex-col">
            {CLASSROOM_TYPES.map(t => (
              <Button
                key={t}
                variant={typeFilters[t] ? 'default' : 'outline'}
                className="justify-start text-sm h-9"
                onClick={() => toggleType(t)}
              >
                {t}
              </Button>
            ))}
          </CardContent>
        </Card>

        {/* Heatmap */}
        <HeatmapGrid data={displayHeatmapData} />
      </div>

      {/* Çakışma Analizi */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            ⚠️ Çakışma Analizi
            <Badge variant="secondary">{MOCK_CONFLICTS.length} adet</Badge>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {MOCK_CONFLICTS.map(conflict => {
            const sev = SEVERITY_MAP[conflict.severity];
            return (
              <div
                key={conflict.id}
                className={cn(
                  'rounded-xl border p-4 transition-colors',
                  conflict.severity === 'critical' && 'border-red-200 bg-red-50/50',
                  conflict.severity === 'warning' && 'border-amber-200 bg-amber-50/50',
                  conflict.severity === 'info' && 'border-emerald-200 bg-emerald-50/50',
                )}
              >
                <div className="flex items-start justify-between">
                  <div>
                    <div className="flex items-center gap-2 mb-1">
                      <span>{sev.icon}</span>
                      <Badge variant={sev.badge} className="text-[10px]">
                        {sev.label}
                      </Badge>
                      <span className="font-semibold text-sm">{conflict.title}</span>
                    </div>
                    <p className="text-sm text-muted-foreground">{conflict.location}</p>
                    <p className="text-sm mt-0.5">{conflict.description}</p>
                  </div>
                  <div className="flex gap-2 shrink-0 ml-4">
                    <Button size="sm" variant="outline" onClick={() => handleSuggestAlternative(conflict.id)}>Alternatif Öner 💡</Button>
                    <Button size="sm" variant="outline" onClick={() => handleEdit(conflict.id)}>Düzenle ✏️</Button>
                  </div>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>
    </div>
  );
}

/* ── Yardımcı stat kartı ── */
function StatCard({
  label,
  value,
  icon,
  color,
}: {
  label: string;
  value: string | number;
  icon: string;
  color?: string;
}) {
  return (
    <Card>
      <CardContent className="flex items-center gap-4 p-5">
        <span className="text-2xl">{icon}</span>
        <div>
          <p className="text-xs text-muted-foreground">{label}</p>
          <p className={cn('text-xl font-bold', color)}>{value}</p>
        </div>
      </CardContent>
    </Card>
  );
}
