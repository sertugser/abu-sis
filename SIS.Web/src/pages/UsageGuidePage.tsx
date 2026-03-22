const PDF_URL = '/documents/ogrenci-kullanim-kilavuzu.pdf#page=1&zoom=page-fit&pagemode=thumbs';

export default function UsageGuidePage() {
  return (
    <div className="space-y-4 lg:relative lg:left-1/2 lg:right-1/2 lg:w-[calc(100vw-var(--sidebar-width)-4rem)] lg:max-w-none lg:-translate-x-1/2">
      <div className="overflow-hidden rounded-2xl border border-border bg-[#202124] shadow-[0_18px_50px_rgba(15,23,42,0.18)]">
        <iframe
          src={PDF_URL}
          title="Öğrenci Kullanım Kılavuzu"
          className="block h-[calc(100vh-8.75rem)] min-h-[720px] w-full bg-[#202124]"
        />
      </div>

      <div className="flex items-center justify-end">
        <a
          href="/documents/ogrenci-kullanim-kilavuzu.pdf"
          target="_blank"
          rel="noreferrer"
          className="text-sm font-medium text-primary hover:text-primary/80"
        >
          PDF'yi yeni sekmede aç
        </a>
      </div>
    </div>
  );
}
