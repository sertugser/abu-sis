import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useActiveTerm } from '../hooks/useActiveTerm';
import { Info } from 'lucide-react';

export default function AdvisorDashboardPage() {
  const { data: term } = useActiveTerm();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-heading text-2xl font-extrabold text-foreground tracking-tight">
          Danisman Paneli
        </h1>
        <p className="text-muted-foreground mt-1">
          Bu demo kurulumunda danisman rolü icin kirik linkler kapatildi ve panel yalnizca erisilebilir akislari gosteriyor.
        </p>
      </div>

      <Alert>
        <Info className="h-4 w-4" />
        <AlertTitle>Rol kapsami</AlertTitle>
        <AlertDescription>
          Forecast ve merkezi schedule operasyonlari yonetim rollerine, ders/sinif ve not akislari ise ogretim uyelerine ayrildi.
        </AlertDescription>
      </Alert>

      <Card>
        <CardHeader>
          <CardTitle>Aktif Donem</CardTitle>
          <CardDescription>Demo baglami icin kullanilan akademik donem bilgisi.</CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          {term ? term.name : 'Aktif term bulunamadi.'}
        </CardContent>
      </Card>
    </div>
  );
}
