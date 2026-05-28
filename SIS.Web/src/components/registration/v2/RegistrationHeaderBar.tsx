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
    <div className="flex flex-col gap-0.5 py-1.5 border-b border-slate-200 last:border-b-0">
      <span className="text-[11px] uppercase tracking-wide text-slate-500 leading-none">
        {label}
      </span>
      <span className="text-[13px] text-slate-700 leading-snug">{value}</span>
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
    <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
      <div className="flex gap-6">
        {/* Left column */}
        <div className="flex-1 min-w-0">
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

        {/* Vertical divider */}
        <div className="w-px bg-slate-200 flex-shrink-0" />

        {/* Right column */}
        <div className="flex-1 min-w-0 flex flex-col justify-between">
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
          <div className="mt-3">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button className="inline-flex items-center gap-1.5 rounded border border-rose-300 bg-white px-3 py-1.5 text-[12px] font-medium text-rose-600 hover:bg-rose-50 focus:outline-none focus:ring-2 focus:ring-rose-300">
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
    </div>
  );
}
