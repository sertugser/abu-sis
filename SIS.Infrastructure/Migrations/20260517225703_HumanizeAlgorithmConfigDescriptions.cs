using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <summary>
    /// 2026-05-17 — Algoritma Parametreleri sayfasının açıklamalarını insan okuyabilir
    /// Türkçe'ye çevirir. Eski açıklamalar kural kodları (H4, H5, H8, S20), kod alan adları
    /// (RequiredTheoryClassroomType), ve solver terimleriyle (UNSAT, modulo eşi, pair) doluydu.
    /// Yeni metinler son kullanıcı (dekan, akademik personel, sysadmin) tarafından okunabilir.
    /// </summary>
    public partial class HumanizeAlgorithmConfigDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            UpdateDescription(migrationBuilder,
                "Schedule.LunchBreak.IsHard",
                "Öğle arası saatlerine ders konulması yasak mı? Açık: bu saatlere hiç ders yerleştirilemez (varsayılan). Kapalı: yerleştirilebilir ama programın puanı düşer.");

            UpdateDescription(migrationBuilder,
                "Schedule.AmphiThresholdEnabled",
                "Kalabalık dersler amfiye yerleştirilmek zorunda olsun mu? Açık: belirlenen eşikten kalabalık şubelere amfi şart koşulur. Kapalı: amfi tercih edilir ama zorunlu değil (varsayılan — amfi sayınız yetersizse programın oluşmasını engellemez).");

            UpdateDescription(migrationBuilder,
                "Schedule.ClassroomTypeRequirementEnabled",
                "Ders kataloğunda tanımlı derslik tipi (laboratuvar, amfi, normal sınıf) zorunlu olsun mu? Açık: ders yalnızca tanımlanan tipte bir derslikte açılabilir. Kapalı: derslik tipi tercih edilir ama farklı tipler de kabul edilir (varsayılan).");

            UpdateDescription(migrationBuilder,
                "Schedule.AllowSameCourseSectionOverlap",
                "Aynı dersin farklı şubeleri aynı saatte birden fazla derslikte paralel açılabilsin mi? Açık: paralel açılır (aynı anda farklı hoca/derslik). Kapalı: şubeler farklı saatlere yerleşir, öğrenci bir alternatifi seçer (varsayılan).");

            UpdateDescription(migrationBuilder,
                "Schedule.MaxLabBlockHours",
                "Bir laboratuvar dersi tek seferde en fazla kaç saat sürebilir? Daha uzun lab'lar bu süreye bölünüp farklı günlere dağıtılır. Örnek: 4 saatlik bir lab, ayar 2 ise → 2 saat Salı + 2 saat Perşembe şeklinde yerleşir; ayar 4 ise tek seferde 4 saat olarak verilir. Varsayılan: 2 saat (çoğu üniversitede standart).");

            UpdateDescription(migrationBuilder,
                "Schedule.CurriculumConflictMode",
                "Aynı sınıftaki öğrencilerin alması gereken dersler arasında çakışma nasıl kontrol edilsin? Cohort (önerilen): aynı dersin birden fazla şubesi varsa, öğrenci için en az bir çakışmasız şube kombinasyonu kalır — gerçek üniversite mantığı. Curriculum: şube sayısını dikkate almaz, tüm dersleri farklı saatlere zorlar — büyük programlarda program oluşturmayı imkansız hale getirebilir. Enrollment: gerçek kayıt verisinden çakışma haritası — henüz hazır değil.");

            UpdateDescription(migrationBuilder,
                "SoftConstraint.S20.PenaltyPerPair",
                "Komşu sınıfların dersleri (örn. 2. sınıfın bir dersi + 3. sınıfın bir dersi) aynı saate konursa programa eklenecek ceza puanı. Bazı öğrenciler alttan/üstten ders alır; bu çakışmaları azaltmak için sistem bu cezayı düşürmeye çalışır. Yüksek değer = sistem bu çakışmadan daha çok kaçınır. Varsayılan: 5.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: eski teknik açıklamalara dön.
            UpdateDescription(migrationBuilder,
                "Schedule.LunchBreak.IsHard",
                "AÇIK: öğle arasına ders koyulamaz (zorunlu). KAPALI: koyulabilir ama ceza alır (tercihli).");

            UpdateDescription(migrationBuilder,
                "Schedule.AmphiThresholdEnabled",
                "AÇIK: kontenjan ≥ amfi eşiği olan derslere amfi zorunlu (H4). KAPALI: kural devre dışı (varsayılan — amfi envanteri olmayan ortamda UNSAT'i önler).");

            UpdateDescription(migrationBuilder,
                "Schedule.ClassroomTypeRequirementEnabled",
                "AÇIK: dersin RequiredTheoryClassroomType / RequiredLabClassroomType alanları zorunlu (H4). KAPALI: kural devre dışı.");

            UpdateDescription(migrationBuilder,
                "Schedule.AllowSameCourseSectionOverlap",
                "AÇIK: aynı dersin farklı şubeleri aynı slotta paralel açılabilir (H8 kapalı). KAPALI (varsayılan): şubeler farklı slotta olmalı.");

            UpdateDescription(migrationBuilder,
                "Schedule.MaxLabBlockHours",
                "Lab dersi en fazla kaç saatlik bloklara bölünsün. Bu değerden uzun lab'lar otomatik parçalara ayrılıp farklı günlere dağıtılır. Örnek: 4 saatlik lab + bu değer 2 → iki ayrı günde 2'şer saat. Default 2 (çoğu üniversitede standart).");

            UpdateDescription(migrationBuilder,
                "Schedule.CurriculumConflictMode",
                "H5 müfredat çakışma kontrolü modu. Cohort (önerilen): section'ların alternatif olduğunu bilir, gerçek üniversite gibi çakışma kontrolü yapar. Curriculum: section sayısını umursamaz, daha agresif (çoğu durumda UNSAT). Enrollment: henüz desteklenmiyor.");

            UpdateDescription(migrationBuilder,
                "SoftConstraint.S20.PenaltyPerPair",
                "S20: Komşu yıl ders çiftinde modulo eşi çakışırsa pair başına eklenen ceza puanı.");
        }

        private static void UpdateDescription(MigrationBuilder b, string key, string description)
        {
            // Description içindeki tek tırnakları SQL'de '' olarak escape et.
            var escaped = description.Replace("'", "''");
            b.Sql($@"UPDATE ""SystemConfigs"" SET ""Description"" = '{escaped}' WHERE ""Key"" = '{key}';");
        }
    }
}
