using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImproveAlgorithmConfigDescriptions : Migration
    {
        // Algoritma ayarlarının açıklamaları geliştirici jargonundan akademik admin
        // diline çevrildi. Mevcut DB'lerdeki Description alanları da güncelleniyor;
        // yeni kurulumlar için seed (SeedData.SchedulingAlgorithmConfig.cs) zaten doğru.
        private static readonly (string Key, string Description)[] NewDescriptions =
        {
            ("Classroom.AmphiThreshold", "Bir dersin kontenjanı bu sayıyı geçerse amfi zorunlu olur. (Derslik Tipi Uyumu kısıtı için kullanılır.)"),
            ("Schedule.LunchBreak.IsHard", "AÇIK: öğle arasına ders koyulamaz (zorunlu). KAPALI: koyulabilir ama ceza alır (tercihli)."),
            ("Schedule.LunchBreakStart", "Öğle arasının başlangıç saati (HH:mm). Bu pencereye değen dersler 'öğle arası ihlali' sayılır."),
            ("Schedule.LunchBreakEnd", "Öğle arasının bitiş saati (HH:mm)."),
            ("Schedule.MaxConsecutiveHours", "Bir öğretim üyesi en fazla kaç saat aralıksız ders verebilir. Bu sayıyı geçen blok zorunlu olarak bölünür."),
            ("Classroom.OccupancyTarget.Min", "Bir derslik en az yüzde kaç dolu olmalı (S1 hedefi). Daha düşük doluluk soft penalty alır."),
            ("Classroom.OccupancyTarget.Max", "Bir derslik en fazla yüzde kaç dolu olmalı (S1 hedefi). Aşırı dolu sınıflar da penalty alır."),
            ("SoftConstraint.S2.MaxDailyHours", "Bir öğretim üyesinin günde kaç saatten fazla ders almaması ideal. Aşılan her saat penalty üretir."),
            ("SoftConstraint.S3.MaxSectionsPerSlot", "Aynı saat diliminde en fazla kaç şube olabilir (prime-time yığılması). Aşılan her şube penalty alır."),
            ("SoftConstraint.S4.MinCoPairThreshold", "Geçmiş kayıtlara göre kaç öğrenciyi etkileyen ders çiftleri 'sık birlikte alınan' sayılır. Eşik altında çakışma penalty üretmez."),
            ("SoftConstraint.S7.MaxGapSlots", "Öğrencinin gün içinde kaç ardışık boş slota toleransı var. Aşan her boşluk penalty üretir."),
            ("Schedule.SA.MaxIterations", "Motor en fazla kaç iyileştirme denemesi yapsın. Artırırsan daha iyi program, ama daha yavaş çalışır. Öneri: 10000."),
            ("Schedule.SA.InitialTemperature", "Motorun başlangıçta ne kadar cesur olacağı (geniş arama). Zamanla soğur ve daha titiz olur. Öneri: 100."),
            ("Schedule.SA.TimeoutSeconds", "Motor en fazla kaç saniye çalışsın. Süre dolunca elindeki en iyi programı verir. Öneri: 90."),
            ("Schedule.SA.StagnationLimit", "Motor kaç deneme üst üste iyileşme sağlamazsa sıkışmış sayılıp durur. Öneri: 2000."),
            ("Schedule.SA.BacktrackLimit", "İlk program kurulurken (Construction Solver) kaç kez geri alıp tekrar denenebilir. Öneri: 50."),
            ("Schedule.SA.RelocateWeight", "Motor 'tek bir dersin saatini değiştir' hamlesini ne sıklıkla denesin. Üç hamle ağırlığının toplamı 100 olur (varsayılan: 50/35/15)."),
            ("Schedule.SA.SwapWeight", "Motor 'iki dersin yerini değiştir' hamlesini ne sıklıkla denesin."),
            ("Schedule.SA.ChainWeight", "Motor 'zincirleme yer değiştir' (3+ ders aynı anda kaydır) hamlesini ne sıklıkla denesin."),
            ("Schedule.LNS.MaxIterations", "Yan motor (yerel optimum kaçışı) en fazla kaç deneme yapsın. Ana motor sıkıştığında devreye girer."),
            ("Schedule.LNS.TimeoutSeconds", "Yan motor en fazla kaç saniye çalışsın."),
            ("Schedule.LNS.DestroySize", "Yan motor sıkışınca kaç dersin programını yıkıp baştan çözsün. Büyütürsen daha geniş kurtarma, daha yavaş."),
            ("Schedule.MRV.ClassroomWeight", "İlk programı kurarken: derslik bulması zor olan dersleri ne kadar öne al. Yüksek değer = uygun derslik az olan dersler önce."),
            ("Schedule.MRV.SlotWeight", "İlk programı kurarken: zaman dilimi seçeneği az olan dersleri ne kadar öne al."),
            ("Schedule.MRV.LabWeight", "İlk programı kurarken: lab gerektiren dersleri ne kadar öne al (uygun lab sınırlı olabilir)."),
            ("Schedule.MRV.CapacityWeight", "İlk programı kurarken: kontenjanı yüksek dersleri ne kadar öne al (büyük dersliğe sığması zor)."),
            ("Scheduling.MaxBacktrackingDepth", "Motor takıldığında kaç adım geri gidip tekrar denesin. Yüksek = daha titiz, daha yavaş. Öneri: 3."),
            ("Schedule.TheoryLabSplitThreshold", "Bir dersin teori + lab toplamı bu saati aşarsa motor otomatik olarak farklı günlere böler. Öneri: 4 saat."),
            ("Schedule.SplittableBlockTypes", "Bir derslik yetmediğinde hangi tip dersler aynı saatte iki ayrı dersliğe bölünebilir. Virgülle ayrılmış (örn: 'Theory,Lab'). Boş = bölünme yok."),
        };

        private static readonly (string Key, string Old)[] OldDescriptions =
        {
            ("Classroom.AmphiThreshold", "Bu sayıdan fazla kontenjan → Amfi zorunlu (H4)"),
            ("Schedule.LunchBreak.IsHard", "Öğle arası hard constraint mi (H13) soft constraint mi (S5)"),
            ("Schedule.LunchBreakStart", "Öğle arası başlangıç saati (H13/S5 evaluator penceresi)"),
            ("Schedule.LunchBreakEnd", "Öğle arası bitiş saati (H13/S5 evaluator penceresi)"),
            ("Schedule.MaxConsecutiveHours", "Hocanın aralıksız max ardışık ders saati (H15)"),
            ("Classroom.OccupancyTarget.Min", "S1: Derslik doluluk oranı alt hedefi (%)"),
            ("Classroom.OccupancyTarget.Max", "S1: Derslik doluluk oranı üst hedefi (%)"),
            ("SoftConstraint.S2.MaxDailyHours", "S2: Bir hocanın günlük max ders saati"),
            ("SoftConstraint.S3.MaxSectionsPerSlot", "S3: Bir slot'ta max section sayısı (prime-time yığılma önleme)"),
            ("SoftConstraint.S4.MinCoPairThreshold", "S4: Birlikte alınan ders çifti minimum öğrenci eşiği"),
            ("SoftConstraint.S7.MaxGapSlots", "S7: Max ardışık boş slot sayısı"),
            ("Schedule.SA.MaxIterations", "SA algoritması max iterasyon sayısı"),
            ("Schedule.SA.InitialTemperature", "SA başlangıç sıcaklığı"),
            ("Schedule.SA.TimeoutSeconds", "SA zaman aşımı (saniye)"),
            ("Schedule.SA.StagnationLimit", "SA durgunluk limiti (iyileşme olmayan iterasyon)"),
            ("Schedule.SA.BacktrackLimit", "Construction solver backtrack limiti"),
            ("Schedule.SA.RelocateWeight", "SA relocate hamlesi ağırlığı"),
            ("Schedule.SA.SwapWeight", "SA swap hamlesi ağırlığı"),
            ("Schedule.SA.ChainWeight", "SA chain-relocate hamlesi ağırlığı"),
            ("Schedule.LNS.MaxIterations", "LNS algoritması max iterasyon sayısı"),
            ("Schedule.LNS.TimeoutSeconds", "LNS zaman aşımı (saniye)"),
            ("Schedule.LNS.DestroySize", "LNS bir iterasyonda yıkılan section sayısı"),
            ("Schedule.MRV.ClassroomWeight", "MRV: derslik kısıtı ağırlığı"),
            ("Schedule.MRV.SlotWeight", "MRV: zaman dilimi kısıtı ağırlığı"),
            ("Schedule.MRV.LabWeight", "MRV: lab gereksinimi ağırlığı"),
            ("Schedule.MRV.CapacityWeight", "MRV: kapasite kısıtı ağırlığı"),
            ("Scheduling.MaxBacktrackingDepth", "Backtracking max derinlik"),
            ("Schedule.TheoryLabSplitThreshold", "Teori+lab toplamı bu değeri aşarsa ayrı günlere bölünür"),
            ("Schedule.SplittableBlockTypes", "Kapasite yetmeyince paralel oda split'i yapılabilen block tipleri (virgülle ayrılmış: Theory, Lab)"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, description) in NewDescriptions)
            {
                var safe = description.Replace("'", "''");
                migrationBuilder.Sql($"""
                    UPDATE "SchedulingAlgorithmConfigs"
                    SET "Description" = '{safe}'
                    WHERE "Key" = '{key}';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, old) in OldDescriptions)
            {
                var safe = old.Replace("'", "''");
                migrationBuilder.Sql($"""
                    UPDATE "SchedulingAlgorithmConfigs"
                    SET "Description" = '{safe}'
                    WHERE "Key" = '{key}';
                    """);
            }
        }
    }
}
