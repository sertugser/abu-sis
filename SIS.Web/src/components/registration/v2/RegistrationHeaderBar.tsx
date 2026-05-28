import { ChevronDown, BookOpen, CalendarDays, History, Loader2, Printer } from 'lucide-react';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { IMPLEMENTED_ROUTES, ROUTES } from '@/lib/constants';
import type { RegistrationStudentHeader } from '@/types/registrationCatalog';

interface RegistrationHeaderBarProps {
  student: RegistrationStudentHeader;
  onDownloadPdf: () => void;
  pdfLoading: boolean;
}

interface LabelValueProps {
  label: string;
  value: React.ReactNode;
}

function LabelValue({ label, value }: LabelValueProps) {
  return (
    <div className="flex items-baseline gap-3 py-1 border-b border-slate-100 last:border-b-0">
      <span className="w-44 shrink-0 text-[12px] text-slate-500">{label}</span>
      <span className="text-[12px] text-slate-700 font-medium">{value}</span>
    </div>
  );
}

export function RegistrationHeaderBar({
  student,
  onDownloadPdf,
  pdfLoading,
}: RegistrationHeaderBarProps) {
  const isDebt = student.financialStatus !== 'Uygun';

  return (
    <section className="border-b border-slate-200 bg-white px-4 py-2">
      <div className="flex flex-col md:flex-row md:divide-x divide-slate-200 gap-0 md:gap-0">
        {/* Left column */}
        <div className="flex-1 min-w-0 md:pr-4">
          <LabelValue
            label="Öğrenci No / Adı Soyadı"
            value={`${student.studentNumber} / ${student.fullName}`}
          />
          <LabelValue
            label="Fakülte - Program / Sınıfı"
            value={`${student.programName} / ${student.classYear}. Sınıf`}
          />
          <LabelValue label="Kayıt Tarihi / Nedeni" value="—" />
          <LabelValue label="Bağlı Olunan Müfredat" value={student.curriculumName} />
          <LabelValue label="Öğrenim Süre Durum" value={student.studyDurationStatus} />
          <LabelValue label="Genel Ortalama" value={student.cgpa.toFixed(2)} />
        </div>

        {/* Right column */}
        <div className="flex-1 min-w-0 md:pl-4 flex flex-col justify-between">
          <div>
            <LabelValue label="AKTS Limiti" value={`${student.ectsLimit} AKTS`} />
            <LabelValue
              label="Borç Durumu"
              value={
                <span
                  className={cn(
                    'font-semibold',
                    isDebt ? 'text-rose-700' : 'text-emerald-700',
                  )}
                >
                  {student.financialStatus}
                </span>
              }
            />
            <LabelValue label="Önceki Dönem Bakiye" value="—" />
            <LabelValue label="Dönemlik Ücret" value="—" />
            <LabelValue label="Ödenmesi Gereken" value="—" />
            <LabelValue label="Ödenen" value="—" />
            <LabelValue label="Genel Bakiye" value="—" />
          </div>

          {/* Ek İşlemler dropdown */}
          <div className="mt-2 flex justify-end">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button className="inline-flex items-center gap-1.5 rounded border border-rose-300 bg-white px-2.5 py-1 text-[11px] font-medium text-rose-600 hover:bg-rose-50 focus:outline-none focus:ring-2 focus:ring-rose-300 h-7">
                  Ek İşlemler <ChevronDown className="h-3 w-3" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48">
                {IMPLEMENTED_ROUTES.has(ROUTES.INSTRUCTOR_SCHEDULING) && (
                  <DropdownMenuItem asChild>
                    <Link to={ROUTES.INSTRUCTOR_SCHEDULING} className="flex items-center gap-2">
                      <CalendarDays className="h-3.5 w-3.5" />
                      Ders Programı
                    </Link>
                  </DropdownMenuItem>
                )}
                {IMPLEMENTED_ROUTES.has(ROUTES.MUFREDAT_DURUM) && (
                  <DropdownMenuItem asChild>
                    <Link to={ROUTES.MUFREDAT_DURUM} className="flex items-center gap-2">
                      <BookOpen className="h-3.5 w-3.5" />
                      Müfredat Durumu
                    </Link>
                  </DropdownMenuItem>
                )}
                {IMPLEMENTED_ROUTES.has(ROUTES.ALINAN_DERSLER) && (
                  <DropdownMenuItem asChild>
                    <Link to={ROUTES.ALINAN_DERSLER} className="flex items-center gap-2">
                      <History className="h-3.5 w-3.5" />
                      Ders Kaydı Geçmişi
                    </Link>
                  </DropdownMenuItem>
                )}
                <DropdownMenuItem
                  onClick={onDownloadPdf}
                  disabled={pdfLoading}
                  className="flex items-center gap-2"
                >
                  {pdfLoading ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Printer className="h-3.5 w-3.5" />
                  )}
                  Yazdır
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </div>
    </section>
  );
}
