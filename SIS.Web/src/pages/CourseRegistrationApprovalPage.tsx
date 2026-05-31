import { useMemo, useState } from 'react';
import { UserCheck, Info, MessageSquare, Printer } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { useAdvisorStudents } from '../hooks/useAdvisor';
import {
  APPROVAL_CATEGORIES, countByCategory, distinctPrograms, filterStudents,
  type ApprovalCategory,
} from '../lib/advisorApprovalCategories';
import AdvisorStudentDetailModal from '../components/advisor/AdvisorStudentDetailModal';
import { printHtmlTable } from '../lib/printTable';

const PROGRAM_ALL = 'all';
const PAGE_SIZE = 15;

export default function CourseRegistrationApprovalPage() {
  const { data: students = [], isLoading } = useAdvisorStudents();

  const [category, setCategory] = useState<ApprovalCategory>('pending');
  const [program, setProgram] = useState(PROGRAM_ALL);
  const [studentNumber, setStudentNumber] = useState('');
  const [name, setName] = useState('');
  const [page, setPage] = useState(1);

  const [modalStudentId, setModalStudentId] = useState<number | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  const counts = useMemo(() => countByCategory(students), [students]);
  const programs = useMemo(() => distinctPrograms(students), [students]);

  // Filtreler anlık uygulanır (Listele butonu yok).
  const filtered = useMemo(
    () => filterStudents(students, {
      category,
      program: program === PROGRAM_ALL ? '' : program,
      studentNumber,
      name,
    }),
    [students, category, program, studentNumber, name],
  );

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const pageItems = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  function openModal(studentId: number) {
    setModalStudentId(studentId);
    setModalOpen(true);
  }

  function handlePrint() {
    const ok = printHtmlTable({
      title: 'Danışman Ders Kayıt Onay — Öğrenci Listesi',
      meta: [
        { label: 'Onay Durumu', value: APPROVAL_CATEGORIES.find(c => c.key === category)?.label ?? '' },
        { label: 'Program', value: program === PROGRAM_ALL ? 'Tümü' : program },
        { label: 'Kayıt', value: String(filtered.length) },
      ],
      columns: [
        { header: 'Öğrenci No' },
        { header: 'Adı' },
        { header: 'Soyadı' },
        { header: 'Sınıf', align: 'center' },
        { header: 'Fakülte/Bölüm' },
      ],
      rows: filtered.map(s => [s.studentNumber, s.firstName, s.lastName, s.classYear, `${s.facultyName} - ${s.programName}`]),
    });
    if (!ok) {
      // eslint-disable-next-line no-alert
      alert('Yazdırma penceresi açılamadı. Tarayıcının açılır pencere (popup) engelini kontrol edin.');
    }
  }

  return (
    <div className="space-y-4">
      {/* Filtre paneli */}
      <div className="rounded-lg border bg-card p-4 space-y-4">
        {/* Onay Durumu radio şeridi */}
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <span className="text-sm font-medium text-muted-foreground">Onay Durumu</span>
          {APPROVAL_CATEGORIES.map(c => (
            <label key={c.key} className="flex items-center gap-1.5 text-sm cursor-pointer">
              <input
                type="radio"
                name="approval-category"
                checked={category === c.key}
                onChange={() => { setCategory(c.key); setPage(1); }}
                className="h-4 w-4 accent-primary"
              />
              <span className={cn(category === c.key && 'font-semibold text-foreground')}>
                {c.label} ({counts[c.key]})
              </span>
            </label>
          ))}
        </div>

        {/* Program + arama + butonlar */}
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">Program</label>
            <Select value={program} onValueChange={(v) => { setProgram(v); setPage(1); }}>
              <SelectTrigger className="h-10 w-72 text-sm"><SelectValue placeholder="Tümü" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={PROGRAM_ALL}>Tümü</SelectItem>
                {programs.map(p => <SelectItem key={p} value={p}>{p}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">Öğrenci No</label>
            <Input className="h-10 w-40 text-sm" value={studentNumber}
              placeholder="Öğrenci no"
              onChange={e => { setStudentNumber(e.target.value); setPage(1); }} />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-medium text-muted-foreground">Adı Soyadı</label>
            <Input className="h-10 w-64 text-sm" value={name}
              placeholder="Ad veya soyad ile ara"
              onChange={e => { setName(e.target.value); setPage(1); }} />
          </div>
          <Button type="button" variant="outline" onClick={handlePrint} className="h-10">
            <Printer className="h-4 w-4" /> Yazdır
          </Button>
        </div>
      </div>

      {/* Tablo */}
      <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
        <div className="border-b bg-muted/40 px-3 py-2 text-sm font-semibold text-foreground">
          Danışmanı Olunan Öğrenciler
        </div>
        {isLoading ? (
          <div className="p-3 space-y-1.5">
            <Skeleton className="h-8 w-full" /><Skeleton className="h-8 w-full" /><Skeleton className="h-8 w-full" />
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <Table className="min-w-[760px]">
                <TableHeader>
                  <TableRow className="bg-muted/60 hover:bg-muted/60">
                    <TableHead className="w-28 px-3 text-sm font-semibold">#</TableHead>
                    <TableHead className="w-28 px-3 text-sm font-semibold">Öğrenci No</TableHead>
                    <TableHead className="px-3 text-sm font-semibold">Adı</TableHead>
                    <TableHead className="px-3 text-sm font-semibold">Soyadı</TableHead>
                    <TableHead className="w-16 px-3 text-center text-sm font-semibold">Snf</TableHead>
                    <TableHead className="px-3 text-sm font-semibold">Fakülte/Bölüm</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {pageItems.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} className="px-3 py-6 text-center text-xs text-muted-foreground italic">
                        Bu filtrelerle öğrenci bulunamadı.
                      </TableCell>
                    </TableRow>
                  ) : pageItems.map(s => (
                    <TableRow key={s.studentId} className="border-b hover:bg-muted/30">
                      <TableCell className="px-3 py-2">
                        <div className="flex items-center gap-1.5">
                          <button type="button" title="Ders Kayıt Onay" onClick={() => openModal(s.studentId)}
                            className="text-emerald-600 hover:text-emerald-700">
                            <UserCheck className="h-4 w-4" />
                          </button>
                          <button type="button" title="Öğrenci Bilgileri" onClick={() => openModal(s.studentId)}
                            className="text-sky-600 hover:text-sky-700">
                            <Info className="h-4 w-4" />
                          </button>
                          <span title="Ders Kayıt Mesajları (yakında)" className="text-muted-foreground/40 cursor-not-allowed">
                            <MessageSquare className="h-4 w-4" />
                          </span>
                        </div>
                      </TableCell>
                      <TableCell className="px-3 py-2 font-mono text-sm">{s.studentNumber}</TableCell>
                      <TableCell className="px-3 py-2 text-sm">{s.firstName}</TableCell>
                      <TableCell className="px-3 py-2 text-sm">{s.lastName}</TableCell>
                      <TableCell className="px-3 py-2 text-center text-sm tabular-nums">{s.classYear}</TableCell>
                      <TableCell className="px-3 py-2 text-sm text-muted-foreground">
                        {s.facultyName} - {s.programName}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            {/* Sayfalama */}
            <div className="flex items-center justify-between border-t px-3 py-1.5 text-xs text-muted-foreground">
              <span>{filtered.length} kayıt · Sayfa {page} / {totalPages}</span>
              <div className="flex items-center gap-1">
                <Button type="button" size="sm" variant="outline" className="h-8"
                  disabled={page <= 1} onClick={() => setPage(p => Math.max(1, p - 1))}>Önceki</Button>
                <Button type="button" size="sm" variant="outline" className="h-8"
                  disabled={page >= totalPages} onClick={() => setPage(p => Math.min(totalPages, p + 1))}>Sonraki</Button>
              </div>
            </div>
          </>
        )}
      </div>

      <AdvisorStudentDetailModal
        studentId={modalStudentId}
        open={modalOpen}
        onOpenChange={setModalOpen}
      />
    </div>
  );
}
