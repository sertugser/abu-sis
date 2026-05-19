import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckSquare, Download, HelpCircle, Inbox, Mail, Printer, Square } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useOpenedCourses } from '../../../hooks/useCourseOpening';
import { useAdminProgramsByStructure } from '../../../hooks/useAdminAcademicStructure';
import { semesterToClassLevel, CLASS_LEVELS } from '../types';
import type { OpenedCourseSectionDto } from '../../../types/courseOpening';
import { TablePagination } from './TablePagination';

interface DepartmentOption {
  departmentId: number;
  departmentName: string;
}

interface TermOption {
  id: number;
  name: string;
}

interface TargetTermPanelProps {
  departments: DepartmentOption[];
  /** Bölüm — sol panel ile ÇİFT YÖNLÜ sync (kullanıcı kararı: ortak state). */
  departmentId: number | null;
  onDepartmentChange: (id: number) => void;
  /** Program — sol panel ile ÇİFT YÖNLÜ sync. */
  programId: number | null;
  onProgramChange: (id: number | null) => void;
  /** Hedef dönem — bağımsız (sağ panel kendi içinde tutuyor). */
  defaultTermId: number | null;
  availableTerms: TermOption[];
  /** Çift tık ile şube yönetim penceresini aç (incele/düzenle/çoğalt/erişim/sil). */
  onManage: (section: OpenedCourseSectionDto) => void;
}

export function TargetTermPanel({
  departments,
  departmentId,
  onDepartmentChange,
  programId,
  onProgramChange,
  defaultTermId,
  availableTerms,
  onManage,
}: TargetTermPanelProps) {
  // Bölüm/Program: parent kontrolünde (çift yönlü sync sol panel ile)
  // Dönem/Sınıf/Arama: bu panele özel
  const [termId, setTermId] = useState<number | null>(defaultTermId);
  const [classLevel, setClassLevel] = useState<number | null>(null);
  const [searchCode, setSearchCode] = useState('');
  const [searchName, setSearchName] = useState('');
  const [selectedSectionId, setSelectedSectionId] = useState<number | null>(null);

  // defaultTermId değişince termId'yi senkronla — adjust-state-during-render
  // (React 19+: useEffect içinde setState yasaklandı, prev-tracking ile render'da update)
  const [prevDefaultTermId, setPrevDefaultTermId] = useState(defaultTermId);
  if (defaultTermId !== prevDefaultTermId) {
    setPrevDefaultTermId(defaultTermId);
    if (defaultTermId !== null) setTermId(defaultTermId);
  }

  const { data, isLoading } = useOpenedCourses(departmentId, termId);
  const programsQuery = useAdminProgramsByStructure(departmentId ?? undefined);
  const programs = useMemo(() => (programsQuery.data ?? []).map((p) => ({ id: p.id, name: p.name })), [programsQuery.data]);

  const sections = useMemo(() => data?.sections ?? [], [data]);

  // Section-bazlı liste (her satır 1 şube)
  const filtered = useMemo(() => {
    let result = sections;
    if (classLevel != null) {
      const levelMin = (classLevel - 1) * 2 + 1;
      const levelMax = levelMin + 1;
      result = result.filter((s) => s.suggestedSemester >= levelMin && s.suggestedSemester <= levelMax);
    }
    if (searchCode.trim()) {
      const q = searchCode.trim().toLowerCase();
      result = result.filter((s) => s.courseCode.toLowerCase().includes(q));
    }
    if (searchName.trim()) {
      const q = searchName.trim().toLowerCase();
      result = result.filter((s) => s.courseName.toLowerCase().includes(q));
    }
    return result.sort((a, b) => a.courseCode.localeCompare(b.courseCode) || a.sectionNumber - b.sectionNumber);
  }, [sections, classLevel, searchCode, searchName]);

  // ── Sayfalama ────────────────────────────────────────────────────────────
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);
  const safePage = Math.min(page, totalPages);
  const pageItems = useMemo(
    () => filtered.slice((safePage - 1) * pageSize, safePage * pageSize),
    [filtered, safePage, pageSize],
  );

  // ── Toplam Krd / AKTS ve unique ders sayısı (Proliz altbar muadili) ─────────
  const totals = useMemo(() => {
    let credits = 0;
    let ects = 0;
    const courseIds = new Set<number>();
    for (const s of filtered) {
      credits += s.credits;
      ects += s.ects;
      courseIds.add(s.courseId);
    }
    return { credits, ects, courseCount: courseIds.size };
  }, [filtered]);

  // ── CSV / Mail / Yazdır ─────────────────────────────────────────────────────
  const exportCsv = useCallback(() => {
    if (filtered.length === 0) return;
    const escape = (v: string | null | undefined) =>
      `"${(v ?? '').replace(/"/g, '""')}"`;
    const header = 'Kod,Ad,Şube,Sınıf,Krd,AKTS,Kontenjan,Kayıtlı,Hoca';
    const rows = filtered.map((s) =>
      [
        s.courseCode,
        escape(s.courseName),
        s.sectionNumber,
        semesterToClassLevel(s.suggestedSemester) ?? '',
        s.credits,
        s.ects,
        s.capacity,
        s.enrolledCount,
        escape(s.instructorName),
      ].join(','),
    );
    const csv = '﻿' + header + '\n' + rows.join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'hedef-acilan-dersler.csv';
    a.click();
    URL.revokeObjectURL(url);
  }, [filtered]);

  const mailList = useCallback(() => {
    const subject = `Açılan Dersler Listesi (${filtered.length} şube)`;
    const lines = [
      `Dönem ID: ${termId ?? '—'}`,
      `Şube Sayısı: ${filtered.length}`,
      `Toplam Kredi: ${totals.credits}`,
      `Toplam AKTS: ${totals.ects}`,
      '',
      'Kod\tAd\tŞb\tKrd\tAKTS\tHoca',
      ...filtered
        .slice(0, 100)
        .map((s) =>
          `${s.courseCode}\t${s.courseName}\t${s.sectionNumber}\t${s.credits}\t${s.ects}\t${s.instructorName ?? ''}`,
        ),
      filtered.length > 100 ? `… (${filtered.length - 100} şube daha)` : '',
    ].filter(Boolean);
    window.location.href = `mailto:?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(lines.join('\n'))}`;
  }, [filtered, termId, totals]);

  const printPanel = useCallback(() => window.print(), []);

  return (
    <Card className="overflow-hidden flex flex-col h-full min-h-0">
      {/* Başlık + Filtreler */}
      <div className="border-b bg-muted/30 shrink-0">
        <div className="px-3 py-1.5 flex items-center gap-2 border-b border-border/40">
          <h3 className="text-sm font-semibold">Hedef Dönemde Açılmış Dersler</h3>
          <TooltipProvider delayDuration={200}>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  type="button"
                  className="text-muted-foreground hover:text-foreground transition-colors"
                  aria-label="Bu panel hakkında bilgi"
                >
                  <HelpCircle className="h-3.5 w-3.5" />
                </button>
              </TooltipTrigger>
              <TooltipContent side="bottom" align="start" className="max-w-sm text-xs leading-relaxed">
                <p className="font-semibold mb-1.5">Aktif dönemde açılmış olan tüm şubeler burada listelenir.</p>
                <p className="mb-1.5">Bir satıra tıklayarak şu işlemleri yapabilirsin:</p>
                <ul className="list-disc pl-4 space-y-0.5">
                  <li><strong>İncele</strong> — şube detaylarını gör.</li>
                  <li><strong>Düzenle</strong> — kontenjan, hoca ve kotaları güncelle.</li>
                  <li><strong>Çoğalt</strong> — aynı dersin yeni bir şubesini ekle.</li>
                  <li><strong>Erişim</strong> — şubenin yetkili fakülte/bölümlerini yönet.</li>
                  <li><strong>Sil</strong> — şubeyi kaldır (kayıt yoksa).</li>
                </ul>
                <p className="mt-2 text-[11px] text-muted-foreground">
                  İpucu: Üstteki filtrelerle bölüm, sınıf veya ders kodu / adına göre listeyi daraltabilirsin.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <div className="ml-auto flex items-center gap-0.5">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={printPanel}
              disabled={filtered.length === 0}
              title="Yazdır"
            >
              <Printer className="h-3.5 w-3.5" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={mailList}
              disabled={filtered.length === 0}
              title="Mail olarak gönder"
            >
              <Mail className="h-3.5 w-3.5" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 w-7 p-0"
              onClick={exportCsv}
              disabled={filtered.length === 0}
              title="CSV / Excel olarak indir"
            >
              <Download className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>

        <div className="px-3 py-2 space-y-1.5">
          <FilterRow label="Dönem">
            <Select value={String(termId ?? '')} onValueChange={(v) => setTermId(Number(v))}>
              <SelectTrigger className="h-7 text-xs">
                <SelectValue placeholder="Dönem seçin" />
              </SelectTrigger>
              <SelectContent>
                {availableTerms.map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Bölüm">
            <Select
              value={departmentId == null ? '' : String(departmentId)}
              onValueChange={(v) => onDepartmentChange(Number(v))}
              disabled={departments.length === 0}
            >
              <SelectTrigger className="h-7 text-xs">
                <SelectValue placeholder="Bölüm seçin" />
              </SelectTrigger>
              <SelectContent>
                {departments.map((d) => (
                  <SelectItem key={d.departmentId} value={String(d.departmentId)}>
                    {d.departmentName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Program">
            <Select value={programId == null ? 'all' : String(programId)} onValueChange={(v) => onProgramChange(v === 'all' ? null : Number(v))}>
              <SelectTrigger className="h-7 text-xs">
                <SelectValue placeholder="Tüm programlar" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tüm programlar</SelectItem>
                {programs.map((p) => (
                  <SelectItem key={p.id} value={String(p.id)}>
                    {p.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Sınıf">
            <Select value={classLevel == null ? 'all' : String(classLevel)} onValueChange={(v) => setClassLevel(v === 'all' ? null : Number(v))}>
              <SelectTrigger className="h-7 text-xs">
                <SelectValue placeholder="Tümü" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Tümü</SelectItem>
                {CLASS_LEVELS.map((lvl) => (
                  <SelectItem key={lvl.value} value={String(lvl.value)}>
                    {lvl.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FilterRow>

          <FilterRow label="Ders Kod">
            <Input value={searchCode} onChange={(e) => setSearchCode(e.target.value)} className="h-7 text-xs" placeholder="Örn. CENG101" />
          </FilterRow>

          <FilterRow label="Ders Ad">
            <Input value={searchName} onChange={(e) => setSearchName(e.target.value)} className="h-7 text-xs" placeholder="Ders adında ara..." />
          </FilterRow>
        </div>

      </div>

      <CardContent className="p-0 flex-1 min-h-0 flex flex-col">
        {isLoading ? (
          <div className="flex-1 p-3 space-y-2">
            <Skeleton className="h-7 w-full" />
            <Skeleton className="h-7 w-full" />
            <Skeleton className="h-7 w-full" />
          </div>
        ) : !departmentId || !termId ? (
          <div className="flex-1 flex items-center justify-center py-12 text-sm text-muted-foreground">
            Bölüm ve hedef dönem seçin.
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex-1 flex flex-col items-center justify-center py-12">
            <Inbox className="h-9 w-9 text-muted-foreground/30 mb-2" />
            <p className="text-sm text-muted-foreground">
              {sections.length === 0 ? 'Bu dönemde henüz ders açılmamış.' : 'Filtrelerle eşleşen ders yok.'}
            </p>
          </div>
        ) : (
          <div className="flex-1 min-h-0 overflow-auto">
            <table className="w-full text-xs">
              <thead className="bg-muted/50 sticky top-0 z-10">
                <tr>
                  {/* Sol panel master checkbox'ıyla görsel simetri — seçimi temizler */}
                  <th className="px-2 py-2 w-8 text-center">
                    <input
                      type="checkbox"
                      className="h-3.5 w-3.5 cursor-pointer"
                      checked={selectedSectionId !== null}
                      onChange={() => setSelectedSectionId(null)}
                      title={selectedSectionId !== null ? 'Seçimi temizle' : 'Bir satıra tıklayarak şube seçin'}
                      disabled={selectedSectionId === null}
                    />
                  </th>
                  <th className="px-2 py-2 text-left font-semibold">Kod</th>
                  <th className="px-2 py-2 text-left font-semibold">Ad</th>
                  <th className="px-1 py-2 text-center font-semibold w-9" title="Şube">Şb</th>
                  <th className="px-1 py-2 text-center font-semibold w-9" title="Sınıf">Sn</th>
                  <th className="px-1 py-2 text-center font-semibold w-10">Krd</th>
                  <th className="px-1 py-2 text-center font-semibold w-12">AKTS</th>
                  <th className="px-2 py-2 text-left font-semibold w-40">Hoca</th>
                </tr>
              </thead>
              <tbody>
                {pageItems.map((s, idx) => {
                  const isSelected = selectedSectionId === s.sectionId;
                  // Zebra striping — göz takibi için tek/çift satır farklı arka plan
                  const zebra = idx % 2 === 1 ? 'bg-muted/20' : '';
                  return (
                    <tr
                      key={s.sectionId}
                      className={`border-t cursor-pointer transition-colors ${
                        isSelected ? 'bg-blue-100 dark:bg-blue-950/40' : `${zebra} hover:bg-muted/40`
                      }`}
                      onClick={() => setSelectedSectionId(isSelected ? null : s.sectionId)}
                      onDoubleClick={(e) => {
                        e.preventDefault();
                        onManage(s);
                      }}
                      title="Çift tık: şube yönetim ekranını aç"
                    >
                      <td className="px-2 py-2 text-center w-8">
                        {isSelected ? (
                          <CheckSquare className="h-3.5 w-3.5 text-primary mx-auto" />
                        ) : (
                          <Square className="h-3.5 w-3.5 text-muted-foreground mx-auto" />
                        )}
                      </td>
                      <td className="px-2 py-2 font-mono font-medium">{s.courseCode}</td>
                      <td className="px-2 py-2 truncate max-w-[260px]" title={s.courseName}>{s.courseName}</td>
                      <td className="px-1 py-2 text-center">{s.sectionNumber}</td>
                      <td className="px-1 py-2 text-center text-muted-foreground">{semesterToClassLevel(s.suggestedSemester) || '—'}</td>
                      <td className="px-1 py-2 text-center">{s.credits}</td>
                      <td className="px-1 py-2 text-center">{s.ects}</td>
                      <td className="px-2 py-2 text-muted-foreground truncate max-w-[160px]" title={s.instructorName ?? '—'}>
                        {s.instructorName ?? '— Atanmamış —'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Tek altbar: sayfalama + ders sayısı + toplam Krd/AKTS — daima dipte sticky */}
        {filtered.length > 0 && (
          <div className="shrink-0 mt-auto">
            <TablePagination
              totalCount={filtered.length}
              page={safePage}
              pageSize={pageSize}
              onPageChange={setPage}
              onPageSizeChange={(s) => {
                setPageSize(s);
                setPage(1);
              }}
              itemLabel="şube"
              extraInfo={
                <>
                  <strong className="text-foreground">{totals.courseCount}</strong> ders ·{' '}
                  Toplam Krd:{' '}
                  <strong className="text-foreground">{totals.credits}</strong> · AKTS:{' '}
                  <strong className="text-foreground">{totals.ects}</strong>
                </>
              }
            />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function FilterRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[64px_1fr] items-center gap-2">
      <Label className="text-xs text-muted-foreground font-medium">{label}</Label>
      {children}
    </div>
  );
}
