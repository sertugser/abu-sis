namespace SIS.Domain.Entities;

using SIS.Domain.Enums;

public class ExamRegulation
{
    public int Id { get; set; }

    // Kapsam (unique: ProgramId + DegreeLevel + TermType + Year)
    public int ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;
    public DegreeLevel DegreeLevel { get; set; } = DegreeLevel.Bachelor; // Lisans/Önlisans
    public TermType TermType { get; set; } = TermType.Fall;
    public int Year { get; set; }

    public bool IsActive { get; set; } = true;
    public string Name { get; set; } = string.Empty;

    // Sınav yapısı
    public int MidtermCount { get; set; } = 1;   // Ara Sınav Sayısı
    public int FinalCount { get; set; } = 1;     // YYS Sınav Sayısı

    // Etki oranı sınırları (hoca ağırlık girişine Faz 2'de dayatılacak)
    public decimal MinMidtermWeightPercent { get; set; } = 40m;
    public decimal MaxMidtermWeightPercent { get; set; } = 60m;
    public decimal MinFinalWeightPercent { get; set; } = 40m;
    public decimal MaxFinalWeightPercent { get; set; } = 60m;

    // Geçme / baraj (Faz 2'de Finalize'a bağlanacak; bu faz sadece saklanır)
    public decimal PassingGrade { get; set; } = 0m;       // Geçme Notu
    public decimal RawScoreFloor1 { get; set; } = 0m;     // Ham Başarı Notu Alt Limiti
    public decimal RawScoreFloor2 { get; set; } = 0m;     // Ham Başarı Notu Alt Limiti2
    public decimal FinalExamFloor { get; set; } = 0m;     // Yarıyıl Sonu Sınav Limiti
    public GradeCode BelowFloorGrade { get; set; } = GradeCode.FF; // Geçme/final barajı altında atanacak harf (Proliz "Altı Atanacak Harf")

    // Harf notu aralığı eşikleri (alt sınır — bu puan ve üzeri o harfi alır)
    public int AAMin { get; set; } = 91;
    public int BAMin { get; set; } = 81;
    public int BBMin { get; set; } = 71;
    public int CBMin { get; set; } = 61;
    public int CCMin { get; set; } = 55;
    public int DCMin { get; set; } = 50;
    public int DDMin { get; set; } = 45;
    public bool LetterRangesEditable { get; set; } = true; // "Harf Aralığı Değişebilir mi"

    // Bağıl değerlendirme (çan eğrisi) — Faz 3a çekirdek
    public GradeEvaluationType EvaluationType { get; set; } = GradeEvaluationType.Absolute;
    public int RelativeMinStudentCount { get; set; } = 10;     // Altında → mutlağa dön
    public decimal RelativeInclusionLimit { get; set; } = 0m;  // Bu ham puanın altı istatistiğe katılmaz (0 = herkes dahil)
    public decimal RelativeAbsoluteFloor { get; set; } = 0m;   // Bu ham puanın altı çan ne derse desin baraj altı (0 = kapalı)
    // Bağıl harf tablosu — T-skor alt sınırları (bu T-skor ve üzeri o harfi alır)
    public int TAAMin { get; set; } = 70;
    public int TBAMin { get; set; } = 63;
    public int TBBMin { get; set; } = 57;
    public int TCBMin { get; set; } = 50;
    public int TCCMin { get; set; } = 45;
    public int TDCMin { get; set; } = 40;
    public int TDDMin { get; set; } = 35;

    // Sınav giriş/değerlendirme uygunluk kuralları (hepsi opt-in, default kapalı)
    public bool BlockFinalIfNoMidterm { get; set; }       // hiç vizeye girmeyen finalden yararlanamaz
    public bool FailIfNoFinal { get; set; }               // finale (mazeretsiz) girmeyen kalır
    public bool BlockMakeupIfNoFinal { get; set; }        // finale girmeyen bütünlemeye giremez
    public bool ZeroIfMissedMakeup { get; set; }          // bütünlemeye girmeyene 0
    public bool MakeupReplacesFinal { get; set; }         // bütünleme finalin yerine aynı ağırlıkla
    public bool AutoTransferFailedToMakeup { get; set; }  // FF'ler otomatik bütünleme listesine
    public bool AutoTransferExcusedToMakeup { get; set; } // mazeretliler otomatik mazeret listesine

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
