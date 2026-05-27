import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, Download, Globe, X } from 'lucide-react';
import { useStudentTranscript } from '../hooks/useStudentTranscript';
import { TranscriptReportDocument } from '../components/transcript/TranscriptReportDocument';

export default function TranscriptPage() {
  const navigate = useNavigate();
  const transcriptQuery = useStudentTranscript();

  useEffect(() => {
    const previousTitle = document.title;
    document.title = 'Transkript';
    return () => {
      document.title = previousTitle;
    };
  }, []);

  const handleClose = () => {
    if (window.history.length > 1) {
      navigate(-1);
      return;
    }

    navigate('/');
  };

  const handleDownload = () => {
    window.print();
  };

  return (
    <div className="min-h-screen bg-[#dbe3ec] print:bg-white">
      <style>{`
        @media print {
          @page {
            size: A4 portrait;
            margin: 10mm;
          }

          body {
            background: white;
          }
        }
      `}</style>

      <header className="sticky top-0 z-30 flex items-center justify-between border-b border-white/10 bg-[#1f1f1f] px-5 py-4 text-white shadow-[0_14px_38px_rgba(15,23,42,0.28)] print:hidden">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-white/10">
            <Globe className="h-5 w-5" />
          </div>
          <div>
            <h1 className="font-heading text-[1.7rem] font-semibold leading-none tracking-tight">Transkript</h1>
            <p className="mt-1 text-xs text-white/60">Öğrenci transkript raporu</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            type="button"
            size="sm"
            variant="secondary"
            onClick={handleDownload}
            disabled={transcriptQuery.isLoading || transcriptQuery.isError || !transcriptQuery.data}
            className="border border-white/15 bg-white/10 text-white hover:bg-white/20"
          >
            <Download className="h-4 w-4" />
            PDF İndir
          </Button>

          <button
            type="button"
            onClick={handleClose}
            className="flex h-10 w-10 items-center justify-center rounded-full text-white/80 transition-colors hover:bg-white/10 hover:text-white"
            aria-label="Raporu kapat"
            title="Kapat"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
      </header>

      <div className="px-4 py-5 md:px-8 md:py-7 print:p-0">
        {transcriptQuery.isLoading ? (
          <LoadingState />
        ) : transcriptQuery.isError || !transcriptQuery.data ? (
          <ErrorState />
        ) : (
          <div className="overflow-x-auto print:overflow-visible">
            <div className="mx-auto w-[860px] max-w-none rounded-[18px] border border-slate-300 bg-white shadow-[0_28px_80px_rgba(15,23,42,0.18)] print:w-auto print:rounded-none print:border-0 print:shadow-none">
              <TranscriptReportDocument transcript={transcriptQuery.data} />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function LoadingState() {
  return (
    <div className="mx-auto w-full max-w-[860px] rounded-[18px] border border-slate-300 bg-white p-6 shadow-[0_28px_80px_rgba(15,23,42,0.18)]">
      <Skeleton className="mb-5 h-20 rounded-xl" />
      <Skeleton className="mb-4 h-24 rounded-xl" />
      <Skeleton className="mb-4 h-64 rounded-xl" />
      <Skeleton className="h-64 rounded-xl" />
    </div>
  );
}

function ErrorState() {
  return (
    <div className="mx-auto w-full max-w-[860px]">
      <Alert variant="destructive" className="border border-destructive/30 bg-white shadow-md">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Transkript yüklenemedi</AlertTitle>
        <AlertDescription>
          Rapor için gerekli transkript bilgileri alınırken bir hata oluştu. Lütfen sayfayı yeniden deneyin.
        </AlertDescription>
      </Alert>
    </div>
  );
}
