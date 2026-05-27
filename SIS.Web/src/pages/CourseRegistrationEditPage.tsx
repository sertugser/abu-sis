import { useMemo, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Search, ChevronRight, AlertTriangle, Loader2, Users } from 'lucide-react';
import { useAdvisorStudents } from '../hooks/useAdvisor';
import type { AdvisorStudent } from '../api/advisor.api';
import AdvisorStudentDetailModal, { PLAN_STATUS_LABELS, PLAN_STATUS_VARIANTS } from '../components/advisor/AdvisorStudentDetailModal';

type TabKey = 'pending' | 'approved' | 'all';

function initials(name: string): string {
  return name.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]?.toLocaleUpperCase('tr')).join('');
}

function StudentRow({ student, onClick }: { student: AdvisorStudent; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex w-full items-center gap-3 rounded-lg border border-border bg-card px-4 py-3 text-left transition-all hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
        {initials(student.studentName)}
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate font-medium text-foreground">{student.studentName}</span>
          <span className="shrink-0 font-mono text-xs text-muted-foreground">{student.studentNumber}</span>
        </div>
        <p className="truncate text-xs text-muted-foreground">
          CGPA {student.cgpa.toFixed(2)} · Dönem {student.completedSemesters} · {student.programName}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        {student.isAtRisk && <Badge variant="destructive" className="text-[10px]">Riskli</Badge>}
        <Badge variant={PLAN_STATUS_VARIANTS[student.planStatus] ?? 'secondary'} className="text-[10px]">
          {PLAN_STATUS_LABELS[student.planStatus] ?? student.planStatus}
        </Badge>
        <ChevronRight className="h-4 w-4 text-muted-foreground" />
      </div>
    </button>
  );
}

export default function CourseRegistrationEditPage() {
  const { data: students = [], isLoading, error } = useAdvisorStudents();

  const [tab, setTab] = useState<TabKey>('pending');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<AdvisorStudent | null>(null);

  const counts = useMemo(() => ({
    pending: students.filter(s => s.planStatus === 'Submitted').length,
    approved: students.filter(s => s.planStatus === 'Approved').length,
    all: students.length,
  }), [students]);

  const filtered = useMemo(() => {
    let list = students;
    if (tab === 'pending') list = list.filter(s => s.planStatus === 'Submitted');
    else if (tab === 'approved') list = list.filter(s => s.planStatus === 'Approved');
    const q = search.trim().toLocaleLowerCase('tr');
    if (q) list = list.filter(s =>
      s.studentName.toLocaleLowerCase('tr').includes(q) || s.studentNumber.includes(q),
    );
    return list;
  }, [students, tab, search]);

  const emptyLabel = tab === 'pending'
    ? 'Onay bekleyen öğrenci yok.'
    : tab === 'approved'
      ? 'Onaylanmış plan yok.'
      : search ? 'Eşleşen öğrenci yok.' : 'Danışmanlık öğrenciniz bulunmuyor.';

  return (
    <div className="flex flex-1 flex-col min-h-0">
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border bg-card shadow-sm">
        {/* Üst şerit: sekmeler + arama */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b px-4 py-3">
          <Tabs value={tab} onValueChange={(v) => setTab(v as TabKey)}>
            <TabsList>
              <TabsTrigger value="pending" className="gap-1.5">
                Bekleyen
                {counts.pending > 0 && <Badge variant="warning" className="px-1 py-0 text-[10px]">{counts.pending}</Badge>}
              </TabsTrigger>
              <TabsTrigger value="approved">Onaylanan</TabsTrigger>
              <TabsTrigger value="all" className="gap-1.5">
                Tümü
                <Badge variant="secondary" className="px-1 py-0 text-[10px]">{counts.all}</Badge>
              </TabsTrigger>
            </TabsList>
          </Tabs>

          <div className="relative w-full sm:w-72">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-9 rounded-lg pl-9"
              placeholder="Ad veya öğrenci no ara..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>

        {/* Gövde: öğrenci listesi */}
        <div className="min-h-0 flex-1 overflow-auto bg-muted/20 p-4">
          {isLoading ? (
            <div className="flex items-center justify-center gap-2 py-20 text-muted-foreground">
              <Loader2 className="h-5 w-5 animate-spin" /> Yükleniyor...
            </div>
          ) : error ? (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Hata</AlertTitle>
              <AlertDescription>Öğrenciler yüklenirken hata oluştu. Lütfen sayfayı yenileyin.</AlertDescription>
            </Alert>
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed py-20 text-center">
              <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-muted text-muted-foreground">
                <Users className="h-6 w-6" />
              </div>
              <p className="text-sm font-medium text-foreground">{emptyLabel}</p>
            </div>
          ) : (
            <div className="space-y-2">
              {filtered.map(student => (
                <StudentRow
                  key={student.studentId}
                  student={student}
                  onClick={() => setSelected(student)}
                />
              ))}
            </div>
          )}
        </div>
      </div>

      <AdvisorStudentDetailModal
        studentId={selected?.studentId ?? null}
        open={selected != null}
        onOpenChange={(open) => { if (!open) setSelected(null); }}
      />
    </div>
  );
}
