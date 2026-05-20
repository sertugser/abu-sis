import { Lock } from 'lucide-react';

export default function UserGroupRestrictionsPage() {
  return (
    <div>
      <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-border rounded-lg bg-muted/20">
        <Lock className="h-12 w-12 text-muted-foreground/40 mb-4" />
        <h2 className="font-heading text-lg font-semibold text-foreground mb-2">Yakında</h2>
        <p className="text-sm text-muted-foreground max-w-md">
          Bu sayfa henüz aktif değil. Kullanıcı grup sınırlandırmaları yakın zamanda bu ekrandan yürütülebilecek.
        </p>
      </div>
    </div>
  );
}
