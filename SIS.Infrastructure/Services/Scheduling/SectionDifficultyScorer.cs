using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services.Scheduling;

/// <summary>
/// Section başına 'zorluk' skoru hesaplar — kaç oda kapasitesini karşılıyor,
/// derslik tipi nadir mi, kapasite büyük mü. ConstructionSolver MRV tiebreaker'da
/// kullanılır: büyük zorunlu (CENG 384 88 kişi gibi) section'lar önce yerleşir,
/// AMFİ 1 gibi nadir kaynaklar onlara verilir.
///
/// Mevcut DomainStore.GetDegree (instructor/dept çakışma sayısı) tamamlayıcı —
/// bu sınıf onu silmiyor, ek bir boyut ekliyor.
/// </summary>
public sealed class SectionDifficultyScorer
{
    private static readonly HashSet<ClassroomType> _generalTeachingTypes = new()
    {
        ClassroomType.Lecture,
        ClassroomType.Seminar,
        ClassroomType.Amphitheater,
    };

    private static readonly HashSet<ClassroomType> _labTeachingTypes = new()
    {
        ClassroomType.Lab,
        ClassroomType.Computer,
    };

    /// <summary>
    /// Tüm section'lar için zorluk skoru hesaplar.
    /// Yüksek skor = "öncelikli yerleştir" (kaynak az, kapasite tight, vs.)
    /// </summary>
    public Dictionary<int, double> ScoreAll(
        IEnumerable<SectionSnapshot> sections,
        List<Classroom> classrooms,
        HashSet<int> facultyClassroomIds)
    {
        var muhClassrooms = classrooms
            .Where(c => facultyClassroomIds.Contains(c.Id))
            .ToList();

        var result = new Dictionary<int, double>();
        foreach (var s in sections)
        {
            result[s.SectionId] = ComputeDifficulty(s, muhClassrooms);
        }
        return result;
    }

    private static double ComputeDifficulty(SectionSnapshot section, List<Classroom> facultyClassrooms)
    {
        double score = 0;

        // 1) Theory tarafında kaç oda kapasiteyi karşılıyor?
        if (section.TheoryHours > 0)
        {
            var theoryType = section.TheoryClassroomType ?? ClassroomType.Lecture;
            score += ClassroomFitDifficulty(theoryType, section.Capacity, facultyClassrooms);
        }

        // 2) Lab tarafında — paralel split mantığı kabul edilirse cap=1 yeter ama
        //    pratikte tek başına yeterli oda yok ise zorluk artıyor.
        if (section.LabHours > 0)
        {
            var labType = section.LabClassroomType ?? ClassroomType.Lab;
            score += ClassroomFitDifficulty(labType, section.Capacity, facultyClassrooms);
        }

        // 3) Capacity tightness — büyük section'lar yapısal olarak daha zor
        // (daha az oda alternatifi, çakışma şansı yüksek)
        score += section.Capacity / 5.0;

        // 4) Kombine ders (theory + lab) günler arası ayrılma zorunluluğu — daha zor
        if (section.TheoryHours > 0 && section.LabHours > 0)
        {
            score += 5;
        }

        return score;
    }

    /// <summary>
    /// Belirli derslik tipi + kapasitesi için kaç oda uygun? Az = zor.
    /// </summary>
    private static double ClassroomFitDifficulty(
        ClassroomType requiredType,
        int requiredCapacity,
        List<Classroom> classrooms)
    {
        var matching = classrooms.Count(c =>
            IsCompatibleType(requiredType, c.Type) && c.Capacity >= requiredCapacity);

        // 0 oda: imkansız (skor maksimum, ama bu section'lar zaten fail eder)
        // 1 oda: çok zor (sadece o oda dolu olmamalı)
        // 2 oda: zor (çakışma riski yüksek)
        // 3-5 oda: orta
        // 6+ oda: kolay
        return matching switch
        {
            0 => 100,
            1 => 50,
            2 => 30,
            3 => 20,
            4 => 12,
            5 => 8,
            _ => 3,
        };
    }

    private static bool IsCompatibleType(ClassroomType required, ClassroomType actual)
    {
        if (_generalTeachingTypes.Contains(required))
            return _generalTeachingTypes.Contains(actual);
        if (_labTeachingTypes.Contains(required))
            return _labTeachingTypes.Contains(actual);
        return required == actual;
    }
}
