import { Signature } from 'lucide-react';

export default function EbysSignatureAuthoritiesPage() {
  return (
    <div>
      <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-border rounded-lg bg-muted/20">
        <Signature className="h-12 w-12 text-muted-foreground/40 mb-4" />
        <h2 className="font-heading text-lg font-semibold text-foreground mb-2">Yakında</h2>
        <p className="text-sm text-muted-foreground max-w-md">
          Bu sayfa henüz aktif değil. EBYS imza yetkilileri yakın zamanda bu ekrandan yönetilebilecek.
        </p>
      </div>
    </div>
  );
}
