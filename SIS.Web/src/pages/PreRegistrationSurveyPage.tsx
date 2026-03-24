import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/* ── Mock müfredat verileri ── */
interface SurveyCourse {
  id: number;
  courseCode: string;
  courseName: string;
  ects: number;
  isRetake: boolean;
  requiredClassroomType: string;
}

const MOCK_COURSES: SurveyCourse[] = [
  { id: 1, courseCode: 'BM301', courseName: 'Veritabanı Yönetimi', ects: 5, isRetake: true, requiredClassroomType: 'Computer Lab' },
  { id: 2, courseCode: 'BM303', courseName: 'İşletim Sistemleri', ects: 5, isRetake: false, requiredClassroomType: 'Lecture' },
  { id: 3, courseCode: 'BM305', courseName: 'Bilgisayar Ağları', ects: 5, isRetake: false, requiredClassroomType: 'Lab' },
  { id: 4, courseCode: 'BM307', courseName: 'Yapay Zekaya Giriş', ects: 4, isRetake: false, requiredClassroomType: 'Lecture' },
  { id: 5, courseCode: 'BM309', courseName: 'Mikroişlemciler', ects: 4, isRetake: false, requiredClassroomType: 'Lab' },
  { id: 6, courseCode: 'SEC-01', courseName: 'Seçmeli Ders (Slot 1)', ects: 3, isRetake: false, requiredClassroomType: '' },
];

type Priority = 'kesin' | 'muhtemel' | 'dusunuyor' | null;



/* ────────────────────── Sayfa ────────────────────── */

export default function PreRegistrationSurveyPage() {
  const [selections, setSelections] = useState<Record<number, Priority>>(() => {
    // Tekrar dersler otomatik "kesin"
    const init: Record<number, Priority> = {};
    MOCK_COURSES.forEach(c => {
      init[c.id] = c.isRetake ? 'kesin' : null;
    });
    return init;
  });

  const [submitted, setSubmitted] = useState(false);

  const setPriority = (courseId: number, p: Priority) => {
    setSelections(prev => ({ ...prev, [courseId]: p }));
  };

  const selectedCount = Object.values(selections).filter(v => v !== null).length;
  const totalEcts = MOCK_COURSES.filter(c => selections[c.id] === 'kesin' || selections[c.id] === 'muhtemel')
    .reduce((sum, c) => sum + c.ects, 0);

  return (
    <div className="space-y-6">
      {/* Başlık */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            📋 Ön Kayıt Anketi
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            2025-2026 Güz Dönemi &middot; Son Tarih: 15 Temmuz 2025
          </p>
        </div>
        <Badge variant="outline" className="text-sm px-3 py-1">
          {selectedCount}/{MOCK_COURSES.length} ders seçildi &middot; ~{totalEcts} AKTS
        </Badge>
      </div>

      {submitted && (
        <Card className="border-emerald-300 bg-emerald-50/50">
          <CardContent className="flex items-center gap-4 p-5">
            <span className="text-3xl">✅</span>
            <div>
              <h3 className="font-bold text-emerald-800">Anketiniz başarıyla gönderildi!</h3>
              <p className="text-sm text-emerald-700">Ders kayıt döneminde bu bilgiler dikkate alınacaktır.</p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* İnfo */}
      <Card>
        <CardContent className="p-4">
          <p className="text-sm text-muted-foreground">
            Müfredatınıza göre gelecek dönem alabileceğiniz dersler aşağıda listelenmiştir.
            Her ders için alma niyetinizi belirtin. Bu bilgi, şube sayısı planlamasında kullanılacaktır.
          </p>
          <div className="flex items-center gap-4 mt-3 text-xs">
            <div className="flex items-center gap-1.5">
              <span className="text-red-500">🔴</span>
              <span>= Tekrar alınması gereken ders (otomatik "Kesin" işaretli)</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Ders tablosu */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Ders Listesi</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-lg border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-4 py-3 text-left font-semibold">Ders</th>
                  <th className="px-4 py-3 text-center font-semibold">AKTS</th>
                  <th className="px-4 py-3 text-center font-semibold">Alma Niyetiniz</th>
                </tr>
              </thead>
              <tbody>
                {MOCK_COURSES.map(course => {
                  const selected = selections[course.id];
                  return (
                    <tr key={course.id} className="border-t">
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          {course.isRetake && <span className="text-red-500">🔴</span>}
                          <span className="font-mono font-medium">{course.courseCode}</span>
                          <span className="text-muted-foreground">—</span>
                          <span>{course.courseName}</span>
                          {course.isRetake && (
                            <Badge variant="destructive" className="text-[10px]">Tekrar</Badge>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-center">{course.ects}</td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-center gap-2">
                          {(['kesin', 'muhtemel', 'dusunuyor'] as const).map(p => (
                            <button
                              key={p}
                              disabled={submitted || (course.isRetake && p !== 'kesin')}
                              onClick={() => setPriority(course.id, selected === p ? null : p)}
                              className={cn(
                                'px-3 py-1.5 rounded-full text-xs font-medium transition-all border',
                                selected === p
                                  ? p === 'kesin'
                                    ? 'bg-primary text-white border-primary'
                                    : p === 'muhtemel'
                                      ? 'bg-amber-100 text-amber-800 border-amber-300'
                                      : 'bg-slate-100 text-slate-700 border-slate-300'
                                  : 'bg-white text-muted-foreground border-border hover:bg-muted',
                                (submitted || (course.isRetake && p !== 'kesin')) && 'opacity-50 cursor-not-allowed',
                              )}
                            >
                              {p === 'kesin' ? '⚫ Kesin' : p === 'muhtemel' ? '○ Muhtemel' : '○ Düşünüyorum'}
                            </button>
                          ))}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      {/* Alt eylem */}
      <div className="flex gap-3">
        <Button onClick={() => setSubmitted(true)} disabled={submitted}>
          Gönder ✅
        </Button>
        <Button variant="outline" disabled={submitted}>
          Sonra Tamamla 💾
        </Button>
      </div>
    </div>
  );
}
