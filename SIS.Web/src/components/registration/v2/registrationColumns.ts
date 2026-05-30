/**
 * Ders kayıt tablolarının ORTAK sütun genişlikleri — TEK KAYNAK.
 * Hem "Müfredat Dersleri" (CurriculumCourseGrid) hem "Seçilen Dersler"
 * (SelectedCoursesGrid) bu sabitleri kullanır; ikibaşlılık yok.
 *
 * Tablolar `table-fixed` ile render edilir → bu genişlikler tarayıcı tarafından
 * KESİN uygulanır (içerik/dönem genişlikleri değiştiremez). Z/S → Sınıf bloğunun
 * iki tabloda da aynı dikey hatta hizalanmasını bu garanti eder:
 * her iki tabloda Ad sütununa kadarki sol blok + Ad genişliği BİREBİR eşittir.
 *
 * Sol blok eşitliği (Ad'dan önce, toplam aynı olmalı):
 *   - Müfredat:  +(w-12) + Kod(w-32)                 = 44
 *   - Seçilen:   Çıkar(w-12) + Şube(w-12) + Kod(w-20) = 44
 */
export const REG_COL = {
  /** Ders adı — ortak sabit genişlik (wrap edilir, taşmaz). */
  name: 'w-80',
  /** Ortak sayısal/orta sütunlar — iki tabloda da aynı. */
  zs: 'w-16',
  tu: 'w-16',
  krd: 'w-16',
  akts: 'w-16',
  sinif: 'w-16',
} as const;
