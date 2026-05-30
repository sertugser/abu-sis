/**
 * "Notlar" açıklama paneli. Proliz davranışına paralel: öğrenciye "Sonuçlandırılmadı"
 * ve harf notu "—" durumlarının ne anlama geldiğini anlatan sabit info kutusu.
 *
 * Ortak component — Alınan Dersler (MyCoursesPage) ve Not Listesi (GradeListPage)
 * ikisi de öğrenciye aynı semantik soruyu yanıtlar. İkibaşlılığı önlemek için
 * metin ve yapı tek yerde tanımlıdır.
 */
export function NotlarInfoBanner() {
  return (
    <div className="grid gap-x-8 gap-y-3 text-[12px] leading-snug md:grid-cols-2">
      <div>
        <p className="font-semibold text-destructive">
          Not sütununda "—" görünüyorsa ders henüz sonuçlandırılmamış olabilir:
        </p>
        <ol className="list-decimal pl-5 text-muted-foreground">
          <li>Yarıyıl sonu sınavı notu henüz girilmedi</li>
          <li>Tüm sınav notları girildi, harf notu hesaplanmadı</li>
          <li>Not girişi tamamlanmayan bütünleme sınavı var</li>
          <li>Bütünleme notları girildi, harf notu hesaplanmadı</li>
        </ol>
      </div>
      <div>
        <p className="font-semibold text-primary">Harf notu hesaplandığı halde "—" görünüyorsa:</p>
        <p className="text-muted-foreground">
          Akademik takvime göre harf notlarının ilan tarihi bekleniyor.
          İlgili tarihleri akademik takvimden kontrol edebilirsiniz.
        </p>
      </div>
    </div>
  );
}
