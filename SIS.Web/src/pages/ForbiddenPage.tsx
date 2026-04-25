import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ShieldAlert, ArrowLeft } from 'lucide-react';

export default function ForbiddenPage() {
  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <div className="text-center animate-fade-in">
        <ShieldAlert className="h-16 w-16 text-destructive/40 mx-auto mb-6" />
        <h1 className="font-heading text-7xl font-extrabold bg-gradient-to-b from-destructive to-destructive/70 bg-clip-text text-transparent mb-4">
          403
        </h1>
        <p className="font-heading text-xl font-semibold text-foreground mb-2">Erişim Yetkiniz Yok</p>
        <p className="text-muted-foreground mb-8">
          Bu sayfayı görüntülemek için gerekli yetkiniz bulunmuyor. Yetki gerekiyorsa sistem yöneticinize başvurabilirsiniz.
        </p>
        <Button asChild size="lg" className="gap-2">
          <Link to="/">
            <ArrowLeft className="h-4 w-4" />
            Ana Sayfaya Dön
          </Link>
        </Button>
      </div>
    </div>
  );
}
