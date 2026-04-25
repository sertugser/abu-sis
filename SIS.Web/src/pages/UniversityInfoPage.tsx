import { Building2 } from 'lucide-react';

export default function UniversityInfoPage() {
  return (
    <div className="container mx-auto p-6">
      <div className="mb-6">
        <h1 className="font-heading text-3xl font-bold tracking-tight">Üniversite Bilgileri</h1>
        <p className="text-muted-foreground mt-2">
          Üniversitenin temel bilgileri (ad, logo, iletişim, kuruluş tarihi vb.) yönetimi.
        </p>
      </div>
      <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-border rounded-lg bg-muted/20">
        <Building2 className="h-12 w-12 text-muted-foreground/40 mb-4" />
        <h2 className="font-heading text-lg font-semibold text-foreground mb-2">Yakında</h2>
        <p className="text-sm text-muted-foreground max-w-md">
          Bu sayfa henüz aktif değil. Üniversite bilgileri yakın zamanda bu ekrandan yönetilebilecek.
        </p>
      </div>
    </div>
  );
}
