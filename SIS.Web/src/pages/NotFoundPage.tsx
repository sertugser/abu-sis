import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { FileQuestion, ArrowLeft } from 'lucide-react';

export default function NotFoundPage() {
  return (
    <div className="flex items-center justify-center min-h-[60vh]">
      <div className="text-center animate-fade-in">
        <FileQuestion className="h-16 w-16 text-muted-foreground/30 mx-auto mb-6" />
        <h1 className="font-heading text-7xl font-extrabold bg-gradient-to-b from-primary to-primary-light bg-clip-text text-transparent mb-4">
          404
        </h1>
        <p className="font-heading text-xl font-semibold text-foreground mb-2">Sayfa Bulunamadı</p>
        <p className="text-muted-foreground mb-8">Aradığınız sayfa mevcut değil veya taşınmış olabilir.</p>
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