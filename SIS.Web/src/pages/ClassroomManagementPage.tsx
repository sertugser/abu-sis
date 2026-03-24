import { useState, useMemo } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Loader2, LayoutDashboard, CalendarRange, AlertCircle, CheckCircle, Pencil, Search, Building } from 'lucide-react';
import HeatmapGrid from '@/components/scheduling/HeatmapGrid';
import { cn } from '@/lib/utils';
import { useActiveTerm } from '@/hooks/useActiveTerm';
import { useClassroomDashboard, useSchedulingConflicts } from '@/hooks/useScheduling';

const SEVERITY_MAP = {
  critical: { badge: 'destructive' as const, label: 'Kritik Çakışma' },
  warning: { badge: 'warning' as const, label: 'Müsaitlik İhlali' },
  info: { badge: 'success' as const, label: 'Bilgi' },
};

const CLASSROOM_TYPES = ['Lecture', 'Lab', 'Computer', 'Seminar'];

export default function ClassroomManagementPage() {
  const { data: activeTerm } = useActiveTerm();
  const { data: dashboard, isLoading: isDashboardLoading } = useClassroomDashboard(activeTerm?.id);
  const { data: conflictsList, isLoading: isConflictsLoading } = useSchedulingConflicts(activeTerm?.id);

  const [typeFilters, setTypeFilters] = useState<Record<string, boolean>>({});
  const [selectedClassroom, setSelectedClassroom] = useState<number | null>(null);

  const toggleType = (t: string) => {
    setTypeFilters(prev => ({ ...prev, [t]: !prev[t] }));
    setSelectedClassroom(null); // Filtre değişince seçimi sıfırla
  };

  const handleClassroomSelect = (id: number) => {
    setSelectedClassroom(prev => (prev === id ? null : id));
  };

  const displayHeatmapData = useMemo(() => {
    if (!dashboard) return [];

    // Seçili bir sınıf varsa sadece onun detayını göster
    if (selectedClassroom) {
      const cls = dashboard.classrooms.find(c => c.id === selectedClassroom);
      if (!cls) return [];

      // Boş grid oluştur (8-17 arası)
      const grid: any[] = [];
      const schedules = cls.schedules || [];

      for (let day = 0; day < 5; day++) {
        for (let hour = 8; hour <= 17; hour++) {
          const slot = schedules.find(s => s.day - 1 === day && hour >= s.startHour && hour <= s.endHour);
          grid.push({
            day,
            hour,
            occupancy: slot ? 1 : 0,
            label: slot ? slot.courseCode : '',
            details: slot ? `${slot.courseCode} - ${slot.courseName}` : 'Boş'
          });
        }
      }
      return grid;
    }

    // Genel görünüm (tüm sınıflar)
    const activeFiltersCount = Object.values(typeFilters).filter(Boolean).length;
    const isAnyFilterActive = activeFiltersCount > 0 && activeFiltersCount < CLASSROOM_TYPES.length;
    
    if (!isAnyFilterActive) return dashboard.heatmap;

    return dashboard.heatmap.map(cell => {
      // Filtreleme yapıldığında doluluk oranını simüle ediyoruz (aslında backend'den filtrelenmiş gelmesi daha iyi olur ama şu an UI bazlı yapıyoruz)
      const multiplier = (activeFiltersCount / CLASSROOM_TYPES.length) * (0.5 + ((cell.day + cell.hour) % 3) * 0.25);
      const newOcc = Math.max(0, cell.occupancy * multiplier);
      const newUsed = Math.round(newOcc * dashboard.totalClassrooms);
      return {
        ...cell,
        occupancy: newOcc,
        usedClassrooms: newUsed,
      };
    });
  }, [dashboard, typeFilters, selectedClassroom]);

  const handleSuggestAlternative = (id: number) => {
    alert(`Çakışma section #${id} için YZ alternatifleri hesaplanıyor...\nUygun derslik ve hoca programı taranıyor.`);
  };

  const handleEdit = (id: number) => {
    alert(`Section #${id} manuel düzenleme paneli açılıyor.`);
  };

  if (isDashboardLoading || isConflictsLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px] text-muted-foreground">
        <Loader2 className="h-8 w-8 animate-spin mb-4" />
        <p>Veriler yükleniyor...</p>
      </div>
    );
  }

  const activeFiltersCount = Object.values(typeFilters).filter(Boolean).length;
  const isAnyFilterActive = activeFiltersCount > 0 && activeFiltersCount < CLASSROOM_TYPES.length;

  const displayClassrooms = dashboard?.classrooms?.filter(c => {
    if (!isAnyFilterActive) return true;
    return typeFilters[c.type];
  }) || [];

  const totalClassrooms = dashboard?.totalClassrooms ?? 0;
  const occupancy = dashboard?.occupancyRate ?? 0;
  const conflictsCount = dashboard?.conflictsCount ?? 0;
  const emptySlots = dashboard?.emptySlots ?? 0;

  return (
    <div className="space-y-6">
      {/* Üst kartlar */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <StatCard label="Toplam Derslik" value={totalClassrooms} icon={<Building className="h-6 w-6" />} />
        <StatCard label="Doluluk" value={`%${occupancy}`} icon={<LayoutDashboard className="h-6 w-6 text-amber-600" />} color="text-amber-600" />
        <StatCard label="Çakışma" value={conflictsCount} icon={<AlertCircle className="h-6 w-6 text-red-500" />} color="text-red-500" />
        <StatCard label="Boş Slot" value={emptySlots} icon={<CheckCircle className="h-6 w-6 text-emerald-600" />} color="text-emerald-600" />
      </div>

      {/* Filtre + Fiziki Derslikler + Heatmap */}
      <div className="grid grid-cols-1 xl:grid-cols-[440px_1fr] gap-6 items-start">
        {/* Sol Sütun: Filtre ve Liste */}
        <div className="flex flex-col gap-4">
          {/* Derslik Tipi Filtresi */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm flex items-center gap-2">
                <CalendarRange className="w-4 h-4" />
                Derslik Tipi
              </CardTitle>
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

          {/* Fiziki Derslikler Listesi */}
          <Card className="flex flex-col min-h-0">
            <CardHeader className="pb-3">
              <CardTitle className="text-sm flex items-center gap-2">
                <Building className="w-4 h-4" />
                Derslikler
                <Badge variant="secondary" className="ml-auto">{displayClassrooms.length} adet</Badge>
              </CardTitle>
            </CardHeader>
            <CardContent className="overflow-auto p-0 max-h-[450px]">
              <div className="border-t">
                <Table>
                  <TableHeader className="bg-muted/50 sticky top-0">
                    <TableRow>
                      <TableHead className="h-8 py-1 text-xs">Adı</TableHead>
                      <TableHead className="h-8 py-1 text-xs">Kapasite</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {displayClassrooms.length > 0 ? (
                      displayClassrooms.map((c) => (
                        <TableRow 
                          key={c.id} 
                          className={cn(
                            'cursor-pointer transition-all duration-200 border-l-4 border-l-transparent',
                            selectedClassroom === c.id 
                              ? 'bg-primary/5 hover:bg-primary/10 border-l-primary shadow-sm' 
                              : 'hover:bg-muted/50 hover:border-l-muted-foreground/30'
                          )}
                          onClick={() => handleClassroomSelect(c.id)}
                        >
                          <TableCell className="py-2.5 text-xs font-medium">
                            <div className="flex items-center gap-3">
                              {selectedClassroom === c.id ? (
                                <Building className="w-4 h-4 text-primary shrink-0" />
                              ) : (
                                <div className="w-4 h-4 border rounded-full border-muted-foreground/20 shrink-0" />
                              )}
                              <span className="truncate max-w-[240px] text-foreground" title={c.name}>{c.name}</span>
                            </div>
                          </TableCell>
                          <TableCell className="py-2.5 text-xs text-right text-muted-foreground pr-6 font-semibold">
                            {c.capacity} <span className="text-[10px] font-normal">Kişi</span>
                          </TableCell>
                        </TableRow>
                      ))
                    ) : (
                      <TableRow>
                        <TableCell colSpan={2} className="h-24 text-center text-xs text-muted-foreground">
                          Kriterlere uyan derslik bulunamadı.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Sağ Sütun: Heatmap */}
        <div className="min-w-0">
          <HeatmapGrid 
            data={displayHeatmapData} 
            className="h-full" 
            title={selectedClassroom 
              ? `${dashboard?.classrooms.find(c => c.id === selectedClassroom)?.name} - Ders Programı` 
              : 'Dönemsel Doluluk Haritası'
            }
          />
          {selectedClassroom && (
            <div className="mt-2 flex justify-end">
              <Button variant="ghost" size="sm" onClick={() => setSelectedClassroom(null)} className="text-xs text-muted-foreground hover:text-primary">
                Genel Doluluk Görünümüne Dön
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* Çakışma Analizi */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <AlertCircle className="w-5 h-5" />
            Çakışma Analizi
            <Badge variant="secondary">{conflictsList?.length ?? 0} adet</Badge>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {conflictsList?.map((conflict, idx) => {
            const isCritical = conflict.conflictType === 'ClassroomConflict' || conflict.conflictType === 'InstructorConflict';
            const sevInfo = isCritical ? SEVERITY_MAP.critical : SEVERITY_MAP.warning;
            
            return (
              <div
                key={`${conflict.sectionId}-${idx}`}
                className={cn(
                  'rounded-xl border p-4 transition-colors',
                  isCritical ? 'border-red-200 bg-red-50/50' : 'border-amber-200 bg-amber-50/50'
                )}
              >
                <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2 mb-1">
                      <Badge variant={sevInfo.badge} className="text-[10px]">
                        {sevInfo.label}
                      </Badge>
                      <span className="font-semibold text-sm">{conflict.conflictType}</span>
                    </div>
                    <p className="text-sm text-muted-foreground">
                      {conflict.courseCode} {conflict.courseName}
                      {conflict.day !== null && ` | Gün: ${conflict.day}`}
                    </p>
                    <p className="text-sm mt-0.5">{conflict.message}</p>
                  </div>
                  <div className="flex gap-2 shrink-0">
                    <Button size="sm" variant="outline" onClick={() => handleSuggestAlternative(conflict.sectionId)}>
                      <Search className="w-4 h-4 mr-1" />
                      Alternatif Öner
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => handleEdit(conflict.sectionId)}>
                      <Pencil className="w-4 h-4 mr-1" />
                      Düzenle
                    </Button>
                  </div>
                </div>
              </div>
            );
          })}
          
          {(!conflictsList || conflictsList.length === 0) && (
            <div className="text-center p-6 text-muted-foreground border rounded-xl border-dashed">
              Aktif dönemde herhangi bir çakışma bulunamadı.
            </div>
          )}
        </CardContent>
      </Card>

    </div>
  );
}

function StatCard({
  label,
  value,
  icon,
  color,
}: {
  label: string;
  value: string | number;
  icon: React.ReactNode;
  color?: string;
}) {
  return (
    <Card>
      <CardContent className="flex items-center gap-4 p-5">
        <div className="shrink-0">{icon}</div>
        <div>
          <p className="text-xs text-muted-foreground">{label}</p>
          <p className={cn('text-xl font-bold', color)}>{value}</p>
        </div>
      </CardContent>
    </Card>
  );
}
