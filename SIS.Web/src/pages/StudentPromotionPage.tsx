import { GraduationCap } from 'lucide-react';

export default function StudentPromotionPage() {
  return (
    <div className="container mx-auto p-6">
      <div className="mb-6">
        <h1 className="font-heading text-3xl font-bold tracking-tight">Sınıf Atlatma İşlemleri</h1>
        <p className="text-muted-foreground mt-2">
          Öğrencilerin sınıf atlatma kurallarına göre toplu sınıf yükseltme işlemleri.
        </p>
      </div>

      <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-border rounded-lg bg-muted/20">
        <GraduationCap className="h-12 w-12 text-muted-foreground/40 mb-4" />
        <h2 className="font-heading text-lg font-semibold text-foreground mb-2">Yakında</h2>
        <p className="text-sm text-muted-foreground max-w-md">
          Bu sayfa henüz aktif değil. Sınıf atlatma işlemleri yakın zamanda bu ekrandan yürütülebilecek.
        </p>
      </div>
    </div>
  );
}
