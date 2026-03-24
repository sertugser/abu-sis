import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import ConfidenceGauge from '@/components/ui/ConfidenceGauge';
import { cn } from '@/lib/utils';

/* ── Mock veri ── */
interface CourseForecast {
  id: number;
  courseCode: string;
  courseName: string;
  predictedDemand: number;
  demandLow: number;
  demandHigh: number;
  confidenceLevel: number;
  historicalAccuracy: number;
  firstTimers: number;
  retakers: number;
  upperClass: number;
  currentSections: number;
  suggestedSections: number;
  sectionCapacity: number;
  requiredClassroomType: string;
  hasSurveyData: boolean;
  status: 'pending' | 'approved' | 'rejected';
}

const MOCK_COURSES: CourseForecast[] = [
  {
    id: 1, courseCode: 'BM201', courseName: 'Veri Yapıları',
    predictedDemand: 112, demandLow: 98, demandHigh: 126,
    confidenceLevel: 82, historicalAccuracy: 91,
    firstTimers: 82, retakers: 18, upperClass: 12,
    currentSections: 2, suggestedSections: 3, sectionCapacity: 38,
    requiredClassroomType: 'Computer Lab', hasSurveyData: true, status: 'pending',
  },
  {
    id: 2, courseCode: 'BM103', courseName: 'Matematik I',
    predictedDemand: 65, demandLow: 58, demandHigh: 72,
    confidenceLevel: 88, historicalAccuracy: 94,
    firstTimers: 52, retakers: 8, upperClass: 5,
    currentSections: 2, suggestedSections: 2, sectionCapacity: 35,
    requiredClassroomType: 'Lecture', hasSurveyData: false, status: 'approved',
  },
  {
    id: 3, courseCode: 'BM301', courseName: 'Veritabanı Yönetimi',
    predictedDemand: 78, demandLow: 68, demandHigh: 88,
    confidenceLevel: 75, historicalAccuracy: 85,
    firstTimers: 55, retakers: 15, upperClass: 8,
    currentSections: 2, suggestedSections: 2, sectionCapacity: 40,
    requiredClassroomType: 'Computer Lab', hasSurveyData: true, status: 'pending',
  },
  {
    id: 4, courseCode: 'BM401', courseName: 'Yazılım Mühendisliği',
    predictedDemand: 45, demandLow: 38, demandHigh: 52,
    confidenceLevel: 68, historicalAccuracy: 80,
    firstTimers: 35, retakers: 5, upperClass: 5,
    currentSections: 1, suggestedSections: 2, sectionCapacity: 30,
    requiredClassroomType: 'Lecture', hasSurveyData: false, status: 'pending',
  },
];

/* ────────────────────── Sayfa ────────────────────── */

export default function SectionApprovalPage() {
  const [courses, setCourses] = useState(MOCK_COURSES);
  const [expandedId, setExpandedId] = useState<number | null>(1);

  const total = courses.length;
  const pending = courses.filter(c => c.status === 'pending').length;
  const approved = courses.filter(c => c.status === 'approved').length;

  const updateStatus = (id: number, status: 'approved' | 'rejected') => {
    setCourses(prev => prev.map(c => (c.id === id ? { ...c, status } : c)));
  };

  return (
    <div className="space-y-6">
      {/* Başlık */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            📊 Şube Açma Onay Paneli
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Bölüm: Bilgisayar Mühendisliği &middot; Program: Lisans
          </p>
        </div>
        <Badge variant="outline" className="text-sm px-3 py-1">
          2025-2026 Güz
        </Badge>
      </div>

      {/* Özet satırı */}
      <div className="grid grid-cols-3 gap-4">
        <SumCard label="Toplam Ders" value={total} />
        <SumCard label="Onay Bekliyor" value={pending} color="text-amber-600" />
        <SumCard label="Onaylanmış" value={approved} color="text-emerald-600" />
      </div>

      {/* Ders kartları */}
      <div className="space-y-4">
        {courses.map(course => {
          const isExpanded = expandedId === course.id;
          const delta = course.suggestedSections - course.currentSections;

          return (
            <Card
              key={course.id}
              className={cn(
                'transition-all',
                course.status === 'approved' && 'border-emerald-200 bg-emerald-50/30',
                course.status === 'rejected' && 'border-red-200 bg-red-50/30 opacity-60',
              )}
            >
              {/* Kısa görünüm */}
              <CardHeader
                className="cursor-pointer select-none"
                onClick={() => setExpandedId(isExpanded ? null : course.id)}
              >
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base flex items-center gap-3">
                    <span className="font-mono">{course.courseCode}</span>
                    <span className="font-normal text-muted-foreground">—</span>
                    <span>{course.courseName}</span>
                    {course.status === 'approved' && <Badge variant="success">Onaylandı ✅</Badge>}
                    {course.status === 'rejected' && <Badge variant="destructive">Reddedildi</Badge>}
                  </CardTitle>
                  <div className="flex items-center gap-4 text-sm text-muted-foreground">
                    <span>Tahmin: {course.predictedDemand} [{course.demandLow}-{course.demandHigh}]</span>
                    <span>Güven: %{course.confidenceLevel}</span>
                    <span>
                      Şube: {course.currentSections} → {course.suggestedSections}
                      {delta > 0 && <span className="text-amber-600 font-semibold ml-1">(+{delta})</span>}
                      {delta === 0 && <span className="text-emerald-600 font-semibold ml-1"> ✅</span>}
                    </span>
                    <span className="text-lg">{isExpanded ? '▾' : '▸'}</span>
                  </div>
                </div>
              </CardHeader>

              {/* Genişletilmiş detay */}
              {isExpanded && (
                <CardContent className="pt-0 space-y-6">
                  <div className="grid grid-cols-1 lg:grid-cols-[200px_1fr] gap-6">
                    {/* Sol: Gauge */}
                    <div className="flex flex-col items-center gap-2">
                      <ConfidenceGauge value={course.confidenceLevel} label="Güven Düzeyi" />
                      <div className="text-xs text-muted-foreground text-center space-y-0.5">
                        <p>Geçmiş İsabet: %{course.historicalAccuracy}</p>
                        <p>Veri: 3 dönem {course.hasSurveyData ? '+ anket ✓' : ''}</p>
                      </div>
                    </div>

                    {/* Sağ: Detaylar */}
                    <div className="space-y-4">
                      {/* Tahmin metrikleri bar */}
                      <div>
                        <h4 className="text-sm font-semibold mb-2">📈 Tahmin Metrikleri</h4>
                        <div className="relative h-10 bg-muted rounded-lg overflow-hidden">
                          <div
                            className="absolute inset-y-0 left-0 bg-primary/20 rounded-lg"
                            style={{ width: `${(course.demandHigh / (course.demandHigh + 20)) * 100}%` }}
                          />
                          <div
                            className="absolute inset-y-0 left-0 bg-primary/40 rounded-lg"
                            style={{ width: `${(course.predictedDemand / (course.demandHigh + 20)) * 100}%` }}
                          />
                          <div className="absolute inset-0 flex items-center justify-between px-3 text-xs font-semibold">
                            <span>Alt: {course.demandLow}</span>
                            <span className="text-primary text-sm">Tahmin: {course.predictedDemand}</span>
                            <span>Üst: {course.demandHigh}</span>
                          </div>
                        </div>
                      </div>

                      {/* Talep bileşenleri */}
                      <div>
                        <h4 className="text-sm font-semibold mb-2">📋 Talep Bileşenleri</h4>
                        <div className="space-y-2">
                          <DemandBar label="İlk Kez Alanlar" value={course.firstTimers} max={course.predictedDemand} color="bg-blue-400" />
                          <DemandBar label="Tekrar (FF/NA)" value={course.retakers} max={course.predictedDemand} color="bg-amber-400" />
                          <DemandBar label="Üstten Alanlar" value={course.upperClass} max={course.predictedDemand} color="bg-violet-400" />
                        </div>
                      </div>

                      {/* Öneri */}
                      <div className="rounded-lg border bg-muted/30 p-4">
                        <h4 className="text-sm font-semibold mb-1">🎯 Öneri</h4>
                        <p className="text-sm">
                          Mevcut Şube: <strong>{course.currentSections}</strong> → Önerilen: <strong>{course.suggestedSections}</strong>
                          {delta > 0 && <span className="text-amber-600"> (+{delta})</span>}
                        </p>
                        {delta > 0 && (
                          <p className="text-xs text-muted-foreground mt-1">
                            Sebebi: Talep ({course.predictedDemand}) &gt; Mevcut Kapasite ({course.currentSections}×{course.sectionCapacity}={course.currentSections * course.sectionCapacity})
                          </p>
                        )}
                        <p className="text-xs text-muted-foreground mt-1">
                          Gerekli Derslik Tipi: {course.requiredClassroomType}
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* Eylem butonları */}
                  {course.status === 'pending' && (
                    <div className="flex gap-3 pt-2 border-t">
                      <Button onClick={() => updateStatus(course.id, 'approved')}>Onayla ✅</Button>
                      <Button variant="outline">Şube Sayısını Düzenle ✏️</Button>
                      <Button variant="destructive" onClick={() => updateStatus(course.id, 'rejected')}>Reddet ❌</Button>
                    </div>
                  )}
                </CardContent>
              )}
            </Card>
          );
        })}
      </div>

      {/* Alt toplu eylem */}
      <div className="flex gap-3">
        <Button
          variant="outline"
          onClick={() => setCourses(prev => prev.map(c => (c.status === 'pending' ? { ...c, status: 'approved' } : c)))}
        >
          Tümünü Toplu Onayla 📋
        </Button>
        <Button variant="outline">CSV Dışa Aktar 📊</Button>
        <Button variant="outline">Rapor Oluştur 📄</Button>
      </div>
    </div>
  );
}

/* ── Yardımcılar ── */
function SumCard({ label, value, color }: { label: string; value: number; color?: string }) {
  return (
    <Card>
      <CardContent className="flex items-center justify-between p-5">
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className={cn('text-2xl font-bold', color)}>{value}</p>
      </CardContent>
    </Card>
  );
}

function DemandBar({ label, value, max, color }: { label: string; value: number; max: number; color: string }) {
  const pct = Math.round((value / max) * 100);
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs w-32 text-right text-muted-foreground">{label}</span>
      <div className="flex-1 h-5 bg-muted rounded-full overflow-hidden relative">
        <div className={cn('h-full rounded-full transition-all', color)} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs font-medium w-8">{value}</span>
    </div>
  );
}
