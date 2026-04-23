import { useState, useMemo } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { CalendarEventsPanel } from '@/components/academic-calendar/CalendarEventsPanel';
import { MonthCalendarView } from '@/components/academic-calendar/MonthCalendarView';
import {
  useAcademicCalendarTerms,
  useStudentCalendar,
  type StudentAcademicCalendarDto,
} from '@/hooks/useAcademicCalendar';

function fmtDate(iso: string | null | undefined) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: 'long', year: 'numeric' });
}

function DateRow({ label, start, end, open }: {
  label: string; start?: string | null; end?: string | null; open?: boolean;
}) {
  return (
    <div className="flex items-center justify-between py-3 border-b last:border-0">
      <div className="text-slate-700 font-medium">{label}</div>
      <div className="flex items-center gap-3">
        <div className="text-slate-600 text-sm">
          {fmtDate(start)} {end && `— ${fmtDate(end)}`}
        </div>
        {open !== undefined && (
          open
            ? <Badge className="bg-emerald-100 text-emerald-700">Açık</Badge>
            : <Badge className="bg-slate-100 text-slate-600">Kapalı</Badge>
        )}
      </div>
    </div>
  );
}

export default function AcademicCalendarPage() {
  const terms = useAcademicCalendarTerms();
  const [explicitTermId, setExplicitTermId] = useState<number | null>(null);

  const defaultTermId = useMemo<number | null>(() => {
    if (!terms.data || terms.data.length === 0) return null;
    const active = terms.data.find(t => t.isActive && !t.isCancelled);
    return active?.termId ?? terms.data[0].termId;
  }, [terms.data]);

  const termId = explicitTermId ?? defaultTermId;

  const calendar = useStudentCalendar(termId);
  const data: StudentAcademicCalendarDto | undefined = calendar.data;

  return (
    <div className="p-6 space-y-6 max-w-6xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Akademik Takvim</h1>
        <p className="text-slate-600 mt-1">Dönem tarihleri, fazlar ve önemli etkinlikler.</p>
      </div>

      <div className="max-w-md">
        <label htmlFor="term-select" className="sr-only">Dönem</label>
        <Select value={termId?.toString() ?? ''} onValueChange={(v) => setExplicitTermId(Number(v))}>
          <SelectTrigger id="term-select" aria-label="Dönem seçimi">
            <SelectValue placeholder="Dönem seçin..." />
          </SelectTrigger>
          <SelectContent>
            {terms.data?.map(t => (
              <SelectItem key={t.termId} value={t.termId.toString()}>
                {t.termName} {t.isCancelled && '(İptal)'}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {data && (
        <>
          {data.isCancelled && (
            <div className="rounded-lg border-2 border-red-300 bg-red-50 p-4">
              <Badge className="bg-red-600 text-white">İPTAL EDİLDİ</Badge>
              <p className="text-red-800 mt-2">Bu dönem iptal edilmiştir.</p>
            </div>
          )}

          {data.registrationStart === null && !data.isCancelled && (
            <div className="rounded-lg border-2 border-amber-300 bg-amber-50 p-4 flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
              <div>
                <h3 className="font-semibold text-amber-900">Bu dönemin akademik takvimi henüz tanımlanmamış</h3>
                <p className="text-sm text-amber-800 mt-1">
                  Kayıt, ekle/bırak ve not girişi tarihleri belirlenmemiş. Bilgi için öğrenci işleri daire başkanlığı ile iletişime geçebilirsiniz.
                </p>
              </div>
            </div>
          )}

          <Tabs defaultValue="list">
            <TabsList>
              <TabsTrigger value="list">Liste</TabsTrigger>
              <TabsTrigger value="grid">Takvim</TabsTrigger>
            </TabsList>

            <TabsContent value="list" className="space-y-6">
              <div className="rounded-xl border bg-white p-5 shadow-sm">
                <h2 className="font-semibold text-slate-800 mb-3">{data.termName} Akademik Takvimi</h2>
                <DateRow label="Dönem" start={data.termStart} end={data.termEnd} />
                <DateRow
                  label="Kayıt Dönemi"
                  start={data.registrationStart}
                  end={data.registrationEnd}
                  open={data.isRegistrationOpen}
                />
                <DateRow
                  label="Ekle/Bırak"
                  start={data.addDropStart}
                  end={data.addDropEnd}
                  open={data.isAddDropOpen}
                />
                <DateRow
                  label="Çekilme Son Tarihi"
                  start={data.withdrawalDeadline}
                  open={data.isWithdrawalOpen}
                />
                <DateRow
                  label="Not Girişi"
                  start={data.gradeEntryStart}
                  end={data.gradeEntryEnd}
                  open={data.isGradeEntryOpen}
                />
              </div>

              <div className="rounded-xl border bg-white p-5 shadow-sm space-y-3">
                <h2 className="font-semibold text-slate-800">Önemli Günler</h2>
                <CalendarEventsPanel events={data.events} />
              </div>
            </TabsContent>

            <TabsContent value="grid">
              <MonthCalendarView
                events={data.events}
                termStart={data.termStart}
                termEnd={data.termEnd}
              />
            </TabsContent>
          </Tabs>
        </>
      )}
    </div>
  );
}
